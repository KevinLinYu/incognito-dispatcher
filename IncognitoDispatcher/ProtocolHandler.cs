using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using S = IncognitoDispatcher.Properties.Strings;

namespace IncognitoDispatcher;

/// <summary>
/// Manages http/https protocol handler registration and uninstallation.
/// </summary>
public static class ProtocolHandler
{
    public const string ProgId = "IncognitoDispatcherURL";

    public static bool IsInstalled()
    {
        try
        {
            foreach (var p in new[] { "https", "http" })
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    $@"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\{p}\UserChoice");
                if (key?.GetValue("ProgId") as string != ProgId)
                    return false;
            }
            return true;
        }
        catch { return false; }
    }

    public static string? GetRegisteredProgId()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\https\UserChoice");
            return key?.GetValue("ProgId") as string;
        }
        catch { return null; }
    }

    public static (bool Success, string Error) Install()
    {
        try
        {
            var exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
            RegisterProgId(exePath);

            // Try admin script first
            var result = RunWithAdmin(ProgId);
            if (result.Success) return result;

            // If failed, open Windows Settings for manual selection
            // Save current value to detect changes
            var before = GetRegisteredProgId();
            OpenDefaultBrowserSettings();

            // Wait for user to change (max 60 seconds)
            for (int i = 0; i < 120; i++)
            {
                Thread.Sleep(500);
                var after = GetRegisteredProgId();
                if (after != before && after != null)
                {
                    // User has changed the setting
                    if (after == ProgId)
                        return (true, "");
                    else
                        return (false, $"Selected: {after}, but IncognitoDispatcherURL required. Please retry.");
                }
            }

            return (false, S.HandlerOpenSettingsMsg);
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    public static (bool Success, string Error) Uninstall()
    {
        try
        {
            var s = Settings.Load();
            var orig = s.OriginalBrowserProgId;
            if (string.IsNullOrEmpty(orig))
            {
                orig = BrowserDetector.DetectDefaultBrowser()?.ProgId ?? "MSEdgeHTM";
            }
            var result = RunWithAdmin(orig);
            CleanupProgId();
            return result;
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    /// <summary>
    /// Stop UCPD via admin, write registry directly, restore UCPD.
    /// </summary>
    public static (bool Success, string Error) RunWithAdmin(string progId)
    {
        try
        {
            var logFile = Path.Combine(Path.GetTempPath(), "id_result.txt");
            var script = Path.Combine(Path.GetTempPath(), $"id_run_{Guid.NewGuid():N}.ps1");
            var scriptContent = $@"
$ErrorActionPreference = 'SilentlyContinue'
$log = @()

# 1. Stop UCPD
Get-ScheduledTask -TaskPath '\Microsoft\Windows\AppxDeploymentClient\' -TaskName 'UCPDACC*' | Disable-ScheduledTask | Out-Null
sc.exe stop UCPD 2>&1 | Out-Null
Start-Sleep 2
$log += 'UCPD stopped'

# 2. Write registry directly
foreach ($proto in @('https','http')) {{
    $key = ""HKCU:\Software\Microsoft\Windows\Shell\Associations\UrlAssociations\$proto\UserChoice""
    try {{
        Set-ItemProperty -Path $key -Name 'ProgId' -Value '{progId}' -Force -ErrorAction Stop
        Set-ItemProperty -Path $key -Name 'Hash' -Value '' -Force -ErrorAction Stop
        $v = (Get-ItemProperty $key -EA SilentlyContinue).ProgId
        $log += ""$proto => $v""
    }} catch {{
        $log += ""$proto write failed: $_""
    }}
}}

# 3. Restore UCPD
sc.exe start UCPD 2>&1 | Out-Null
Get-ScheduledTask -TaskPath '\Microsoft\Windows\AppxDeploymentClient\' -TaskName 'UCPDACC*' | Enable-ScheduledTask | Out-Null
$log += 'UCPD restored'

$log -join [Environment]::NewLine | Set-Content '{logFile.Replace("'", "''")}' -Encoding UTF8
";
            File.WriteAllText(script, scriptContent);

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-ExecutionPolicy Bypass -File \"{script}\"",
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using var proc = Process.Start(psi);
            if (proc == null)
                return (false, S.HandlerUacPrompt);
            proc.WaitForExit(30000);

            var log = File.Exists(logFile) ? File.ReadAllText(logFile).Trim() : S.HandlerNoLog;

            // Verify
            bool ok = true;
            foreach (var p in new[] { "https", "http" })
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    $@"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\{p}\UserChoice");
                if (key?.GetValue("ProgId") as string != progId) ok = false;
            }

            try { File.Delete(script); } catch { }
            try { File.Delete(logFile); } catch { }

            return ok ? (true, "") : (false, $"{S.HandlerRegistryFailed} {S.HandlerLogLabel} {log}");
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return (false, S.HandlerUacDenied);
        }
        catch (Exception ex)
        {
            return (false, $"{S.HandlerException} {ex.Message}");
        }
    }

    public static void RegisterProgId(string exePath)
    {
        // 1. Basic ProgId registration
        using var key = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ProgId}");
        key.SetValue("", S.AppName);
        key.SetValue("URL Protocol", "");
        using var icon = key.CreateSubKey("DefaultIcon");
        icon.SetValue("", $"{exePath},0");
        using var cmd = key.CreateSubKey(@"shell\open\command");
        cmd.SetValue("", $"\"{exePath}\" \"%1\"");

        // 2. Capabilities registration (so Windows Settings recognizes it as a browser)
        using var cap = key.CreateSubKey("Capabilities");
        cap.SetValue("ApplicationDescription", S.HandlerDescription);
        cap.SetValue("ApplicationName", S.AppName);
        using var urlAssoc = cap.CreateSubKey("UrlAssociations");
        urlAssoc.SetValue("http", ProgId);
        urlAssoc.SetValue("https", ProgId);

        // 3. Register to RegisteredApplications (HKCU)
        using var regApps = Registry.CurrentUser.CreateSubKey(@"Software\RegisteredApplications");
        regApps.SetValue($"IncognitoDispatcher", $@"Software\Classes\{ProgId}\Capabilities");

        // 4. Notify the system that associations have changed
        try
        {
            SHChangeNotify(0x08000000 /* SHCNE_ASSOCCHANGED */, 0, IntPtr.Zero, IntPtr.Zero);
        }
        catch { }
    }

    [System.Runtime.InteropServices.DllImport("shell32.dll")]
    private static extern void SHChangeNotify(int wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

    public static void CleanupProgId()
    {
        try { Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\{ProgId}", false); } catch { }
    }

    /// <summary>Open the Windows Default Apps settings page.</summary>
    public static void OpenDefaultBrowserSettings()
    {
        try
        {
            // Windows 10/11 Default Apps settings page
            Process.Start(new ProcessStartInfo("ms-settings:defaultapps")
            {
                UseShellExecute = true
            });
        }
        catch
        {
            // Fallback: open Control Panel default programs page
            try
            {
                Process.Start(new ProcessStartInfo("control.exe")
                {
                    Arguments = "/name Microsoft.DefaultPrograms",
                    UseShellExecute = true
                });
            }
            catch { }
        }
    }
}
