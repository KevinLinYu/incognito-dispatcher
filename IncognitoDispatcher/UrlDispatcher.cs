using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.Win32;

namespace IncognitoDispatcher;

internal static class UrlDispatcher
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "IncognitoDispatcher", "dispatch.log");

    private static void Log(string msg)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {msg}\n", Encoding.UTF8);
        }
        catch { }
    }

    public static void Run(string url)
    {
        Log($"Dispatch start: {url}");
        try
        {
            var settings = Settings.Load();
            Log($"Settings loaded, Enabled={settings.Enabled}, BrowserPath={settings.SelectedBrowserPath ?? "(null)"}");

            if (!string.IsNullOrEmpty(settings.SelectedBrowserPath))
            {
                if (File.Exists(settings.SelectedBrowserPath))
                {
                    LaunchBrowser(settings.SelectedBrowserPath, url, settings);
                    return;
                }
                Log($"SelectedBrowserPath not found: {settings.SelectedBrowserPath}, clearing");
                settings.SelectedBrowserPath = null;
                settings.SelectedBrowserName = null;
                settings.Save();
            }

            // Fast path: detect system default browser
            var detected = BrowserDetector.DetectDefaultBrowser();
            Log($"DetectDefaultBrowser: {detected?.DisplayName ?? "(null)"} -> {detected?.ExecutablePath ?? "(null)"}");

            if (detected != null && File.Exists(detected.ExecutablePath))
            {
                settings.SelectedBrowserPath = detected.ExecutablePath;
                settings.SelectedBrowserName = detected.DisplayName;
                settings.Save();
                LaunchBrowser(detected.ExecutablePath, url, settings);
                return;
            }

            // Fallback: scan all installed browsers
            var browsers = GetAllBrowsers();
            Log($"GetAllBrowsers found {browsers.Count} browsers");

            if (browsers.Count == 0)
            {
                Log("No browsers found, falling back to ShellExecute");
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                return;
            }

            var chosen = browsers[0];
            Log($"Chosen: {chosen.Name} -> {chosen.Path}");
            settings.SelectedBrowserPath = chosen.Path;
            settings.SelectedBrowserName = chosen.Name;
            settings.Save();
            LaunchBrowser(chosen.Path, url, settings);
        }
        catch (Exception ex)
        {
            Log($"Exception: {ex}");
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
        }
    }

    private static void LaunchBrowser(string exePath, string url, Settings settings)
    {
        var flag = settings.Enabled ? GetIncognitoFlag(exePath) : null;
        var args = flag != null ? $"{flag} \"{url}\"" : $"\"{url}\"";
        Process.Start(new ProcessStartInfo { FileName = exePath, Arguments = args, UseShellExecute = false });
    }

    private static string? GetIncognitoFlag(string exePath)
    {
        return Path.GetFileName(exePath).ToLowerInvariant() switch
        {
            "chrome.exe" or "chrome_sx_s.exe" => "--incognito",
            "msedge.exe" => "--inprivate",
            "firefox.exe" => "--private-window",
            "brave.exe" => "--incognito",
            "vivaldi.exe" => "--incognito",
            "opera.exe" => "--private",
            _ => "--incognito"
        };
    }

    public static List<(string Name, string Path)> GetAllBrowsers()
    {
        var list = new List<(string Name, string Path)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rootKey in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            try
            {
                using var root = rootKey.OpenSubKey(@"SOFTWARE\Clients\StartMenuInternet");
                if (root != null) foreach (var name in root.GetSubKeyNames())
                {
                    using var cmdKey = root.OpenSubKey($@"{name}\shell\open\command");
                    var cmd = cmdKey?.GetValue(null) as string;
                    if (string.IsNullOrEmpty(cmd)) continue;
                    var exe = ExtractPath(cmd);
                    if (exe == null || !File.Exists(exe) || seen.Contains(exe)) continue;
                    seen.Add(exe);
                    using var capKey = root.OpenSubKey($@"{name}\Capabilities");
                    list.Add((capKey?.GetValue("ApplicationDescription") as string ?? name, exe));
                }
            }
            catch { }
        }
        var la = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData);
        foreach (var (n, p) in new (string, string)[] {
            ("Google Chrome", @"C:\Program Files\Google\Chrome\Application\chrome.exe"),
            ("Google Chrome", @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe"),
            ("Google Chrome", Path.Combine(la, @"Google\Chrome\Application\chrome.exe")),
            ("Chrome Canary", Path.Combine(la, @"Google\Chrome SxS\Application\chrome.exe")),
            ("Chrome Beta", Path.Combine(la, @"Google\Chrome Beta\Application\chrome.exe")),
            ("Chrome Dev", Path.Combine(la, @"Google\Chrome Dev\Application\chrome.exe")),
            ("Microsoft Edge", @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"),
            ("Microsoft Edge", @"C:\Program Files\Microsoft\Edge\Application\msedge.exe"),
            ("Mozilla Firefox", @"C:\Program Files\Mozilla Firefox\firefox.exe"),
            ("Brave Browser", @"C:\Program Files\BraveSoftware\Brave-Browser\Application\brave.exe"),
            ("Brave Browser", Path.Combine(la, @"BraveSoftware\Brave-Browser\Application\brave.exe")),
            ("Vivaldi", Path.Combine(la, @"Vivaldi\Application\vivaldi.exe")),
            ("Opera", Path.Combine(la, @"Programs\Opera\opera.exe")),
            ("Opera GX", Path.Combine(la, @"Programs\Opera GX\opera.exe")),
            ("Arc", Path.Combine(la, @"Arc\Application\Arc.exe")),
            ("Thorium", @"C:\Program Files\Thorium\Application\thorium.exe"),
            ("LibreWolf", @"C:\Program Files\LibreWolf\librewolf.exe"),
            ("Waterfox", @"C:\Program Files\Waterfox\waterfox.exe"),
        }) { if (!seen.Contains(p) && File.Exists(p)) { seen.Add(p); list.Add((n, p)); } }
        return list;
    }

    private static string? ExtractPath(string cmd)
    {
        var t = cmd.Trim();
        if (t.StartsWith('"')) { var i = t.IndexOf('"', 1); return i > 1 ? t[1..i] : null; }
        var s = t.IndexOf(' '); return s > 0 ? t[..s] : t;
    }
}
