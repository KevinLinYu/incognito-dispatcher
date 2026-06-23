using System;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace IncognitoDispatcher;

/// <summary>
/// Detect the default browser from the Windows registry.
/// </summary>
public static class BrowserDetector
{
    /// <summary>
    /// Detect the current system default browser.
    /// </summary>
    public static BrowserInfo? DetectDefaultBrowser()
    {
        // Step 1: Read ProgId from UserChoice
        var progId = GetDefaultBrowserProgId();
        if (string.IsNullOrEmpty(progId))
            return null;

        // Step 2: Read command template from ProgId
        var command = GetBrowserCommand(progId);
        if (string.IsNullOrEmpty(command))
            return null;

        // Step 3: Extract executable path
        var exePath = ExtractExecutablePath(command);
        if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
            return null;

        // Step 4: Identify browser type
        var browserType = IdentifyBrowserType(exePath);
        var displayName = GetBrowserDisplayName(progId, exePath);

        return new BrowserInfo
        {
            Type = browserType,
            DisplayName = displayName,
            ExecutablePath = exePath,
            OriginalCommand = command,
            ProgId = progId
        };
    }

    /// <summary>
    /// Read the default ProgId for http/https from UserChoice.
    /// </summary>
    private static string? GetDefaultBrowserProgId()
    {
        // Prefer https, then http
        foreach (var protocol in new[] { "https", "http" })
        {
            var keyPath = $@"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\{protocol}\UserChoice";
            using var key = Registry.CurrentUser.OpenSubKey(keyPath);
            if (key != null)
            {
                var progId = key.GetValue("ProgId") as string;
                if (!string.IsNullOrEmpty(progId))
                    return progId;
            }
        }

        // fallback: read HKCR\http\shell\open\command directly
        using var httpKey = Registry.ClassesRoot.OpenSubKey(@"http\shell\open\command");
        return httpKey?.GetValue(null) as string != null ? "http" : null;
    }

    /// <summary>
    /// Read the shell\open\command template from a ProgId.
    /// </summary>
    private static string? GetBrowserCommand(string progId)
    {
        // Strategy 1: HKCU\Software\Classes\<ProgId> (preferred)
        var command = ReadCommandFromRegistry(Registry.CurrentUser, $@"Software\Classes\{progId}\shell\open\command");
        if (!string.IsNullOrEmpty(command)) return command;

        // Strategy 2: HKCR\<ProgId> (HKLM + HKCU merged view)
        command = ReadCommandFromRegistry(Registry.ClassesRoot, $@"{progId}\shell\open\command");
        if (!string.IsNullOrEmpty(command)) return command;

        // Strategy 3: If ProgId is "http", read protocol handler directly
        if (progId == "http" || progId == "https")
        {
            command = ReadCommandFromRegistry(Registry.ClassesRoot, $@"{progId}\shell\open\command");
            if (!string.IsNullOrEmpty(command)) return command;
        }

        // Strategy 4: Search HKLM\SOFTWARE\Clients\StartMenuInternet
        command = SearchStartMenuInternet(progId);
        if (!string.IsNullOrEmpty(command)) return command;

        return null;
    }

    private static string? ReadCommandFromRegistry(RegistryKey root, string subKeyPath)
    {
        try
        {
            using var key = root.OpenSubKey(subKeyPath);
            return key?.GetValue(null) as string;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Search StartMenuInternet registry for browser commands.
    /// </summary>
    private static string? SearchStartMenuInternet(string progId)
    {
        try
        {
            using var root = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Clients\StartMenuInternet");
            if (root == null) return null;

            foreach (var subKeyName in root.GetSubKeyNames())
            {
                using var subKey = root.OpenSubKey($@"{subKeyName}\shell\open\command");
                var cmd = subKey?.GetValue(null) as string;
                if (!string.IsNullOrEmpty(cmd))
                    return cmd;
            }
        }
        catch { }
        return null;
    }

    /// <summary>
    /// Extract the executable path from a command template.
    /// </summary>
    private static string? ExtractExecutablePath(string command)
    {
        // Command format examples:
        //   "C:\Path\chrome.exe" --single-argument %1
        //   "C:\Path\firefox.exe" -osint -url "%1"
        //   C:\Path\browser.exe %1

        var trimmed = command.Trim();

        if (trimmed.StartsWith('"'))
        {
            // Quoted path: take content before second quote
            var endQuote = trimmed.IndexOf('"', 1);
            if (endQuote > 1)
                return trimmed[1..endQuote];
        }
        else
        {
            // Unquoted: take content before first space
            var spaceIdx = trimmed.IndexOf(' ');
            if (spaceIdx > 0)
                return trimmed[..spaceIdx];

            // May be a bare path (no arguments)
            return trimmed;
        }

        return null;
    }

    /// <summary>
    /// Identify the browser type from the exe filename.
    /// </summary>
    private static BrowserType IdentifyBrowserType(string exePath)
    {
        var fileName = Path.GetFileName(exePath).ToLowerInvariant();

        return fileName switch
        {
            "chrome.exe" => BrowserType.Chrome,
            "msedge.exe" => BrowserType.Edge,
            "firefox.exe" => BrowserType.Firefox,
            "brave.exe" => BrowserType.Brave,
            "vivaldi.exe" => BrowserType.Vivaldi,
            "opera.exe" => BrowserType.Opera,
            "arc.exe" => BrowserType.Arc,
            _ when fileName.Contains("chrom") => BrowserType.ChromiumGeneric,
            _ => BrowserType.Unknown
        };
    }

    /// <summary>
    /// Get the display name of the browser.
    /// </summary>
    private static string GetBrowserDisplayName(string progId, string exePath)
    {
        // Try reading application description from registry
        try
        {
            // Read from Capabilities
            using var clientsKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Clients\StartMenuInternet");
            if (clientsKey != null)
            {
                foreach (var name in clientsKey.GetSubKeyNames())
                {
                    using var capKey = clientsKey.OpenSubKey($@"{name}\Capabilities");
                    var desc = capKey?.GetValue("ApplicationDescription") as string;
                    if (!string.IsNullOrEmpty(desc))
                    {
                        // Verify this entry corresponds to the current browser
                        using var cmdKey = clientsKey.OpenSubKey($@"{name}\shell\open\command");
                        var cmd = cmdKey?.GetValue(null) as string;
                        if (cmd != null && cmd.Contains(Path.GetFileName(exePath), StringComparison.OrdinalIgnoreCase))
                            return desc;
                    }
                }
            }
        }
        catch { }

        // Fallback: return name based on type
        return IdentifyBrowserType(exePath) switch
        {
            BrowserType.Chrome => "Google Chrome",
            BrowserType.Edge => "Microsoft Edge",
            BrowserType.Firefox => "Mozilla Firefox",
            BrowserType.Brave => "Brave Browser",
            BrowserType.Vivaldi => "Vivaldi",
            BrowserType.Opera => "Opera",
            BrowserType.Arc => "Arc Browser",
            _ => Path.GetFileNameWithoutExtension(exePath)
        };
    }

    /// <summary>
    /// Get all installed browsers (for settings UI).
    /// </summary>
    public static List<BrowserInfo> GetAllInstalledBrowsers()
    {
        var browsers = new List<BrowserInfo>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var root = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Clients\StartMenuInternet");
            if (root != null)
            {
                foreach (var subKeyName in root.GetSubKeyNames())
                {
                    using var cmdKey = root.OpenSubKey($@"{subKeyName}\shell\open\command");
                    var cmd = cmdKey?.GetValue(null) as string;
                    if (string.IsNullOrEmpty(cmd)) continue;

                    var exePath = ExtractExecutablePath(cmd);
                    if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath)) continue;
                    if (seen.Contains(exePath)) continue;
                    seen.Add(exePath);

                    var type = IdentifyBrowserType(exePath);
                    using var capKey = root.OpenSubKey($@"{subKeyName}\Capabilities");
                    var displayName = capKey?.GetValue("ApplicationDescription") as string
                                      ?? Path.GetFileNameWithoutExtension(exePath);

                    browsers.Add(new BrowserInfo
                    {
                        Type = type,
                        DisplayName = displayName,
                        ExecutablePath = exePath,
                        OriginalCommand = cmd,
                        ProgId = subKeyName
                    });
                }
            }
        }
        catch { }

        return browsers;
    }
}
