using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace IncognitoDispatcher;

internal static class UrlDispatcher
{
    public static void Run(string url)
    {
        try
        {
            var settings = Settings.Load();
            if (!string.IsNullOrEmpty(settings.SelectedBrowserPath))
            {
                LaunchBrowser(settings.SelectedBrowserPath, url, settings);
                return;
            }
            var browsers = GetAllBrowsers();
            if (browsers.Count == 0)
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                return;
            }

            // Protocol handler mode (no window context): use first browser directly, no picker
            // User can change via tray menu "Change Browser"
            var chosen = browsers[0];
            settings.SelectedBrowserPath = chosen.Path;
            settings.SelectedBrowserName = chosen.Name;
            settings.Save();
            LaunchBrowser(chosen.Path, url, settings);
        }
        catch (Exception ex)
        {
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
