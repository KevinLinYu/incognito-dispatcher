using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IncognitoDispatcher;

/// <summary>
/// Application settings (persisted to %APPDATA%/IncognitoDispatcher/settings.json).
/// </summary>
public class Settings
{
    private static readonly string AppDataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "IncognitoDispatcher");

    private static readonly string SettingsPath = Path.Combine(AppDataDir, "settings.json");

    // ===== Settings =====

    /// <summary>Whether incognito dispatch is enabled.</summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>Whether to show the main window on startup (otherwise tray-only).</summary>
    [JsonPropertyName("showWindowOnStart")]
    public bool ShowWindowOnStart { get; set; } = true;

    /// <summary>Whether to start at login.</summary>
    [JsonPropertyName("autoStart")]
    public bool AutoStart { get; set; } = false;

    /// <summary>Original browser ProgId saved during install (for uninstall restore).</summary>
    [JsonPropertyName("originalBrowserProgId")]
    public string? OriginalBrowserProgId { get; set; }

    /// <summary>Original browser name saved during install.</summary>
    [JsonPropertyName("originalBrowserName")]
    public string? OriginalBrowserName { get; set; }

    /// <summary>User-selected browser exe path.</summary>
    [JsonPropertyName("selectedBrowserPath")]
    public string? SelectedBrowserPath { get; set; }

    /// <summary>User-selected browser name.</summary>
    [JsonPropertyName("selectedBrowserName")]
    public string? SelectedBrowserName { get; set; }

    /// <summary>
    /// URL exclusion list (supports wildcards * and ?).
    /// E.g.: ["*.microsoft.com", "https://login.*", "about:*"]
    /// </summary>
    [JsonPropertyName("excludedUrls")]
    public List<string> ExcludedUrls { get; set; } = new();

    /// <summary>
    /// Whether registered as protocol handler
    /// </summary>
    [JsonPropertyName("isInstalled")]
    public bool IsInstalled { get; set; } = false;

    // ===== Exclusion list matching =====

    /// <summary>
    /// Check if a URL is in the exclusion list.
    /// </summary>
    public bool IsExcluded(string url)
    {
        if (ExcludedUrls.Count == 0)
            return false;

        var lowerUrl = url.ToLowerInvariant();

        foreach (var pattern in ExcludedUrls)
        {
            if (MatchWildcard(lowerUrl, pattern.ToLowerInvariant()))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Simple wildcard matching (supports * and ?).
    /// </summary>
    private static bool MatchWildcard(string input, string pattern)
    {
        // Convert wildcard pattern to regex
        var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";

        return System.Text.RegularExpressions.Regex.IsMatch(input, regexPattern,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    // ===== Persistence =====

    private static Settings? _cached;
    private static bool _loaded;

    /// <summary>
    /// Load settings from disk (returns defaults if file does not exist).
    /// Results are cached for the lifetime of the process.
    /// </summary>
    public static Settings Load()
    {
        if (_loaded) return _cached!;
        _loaded = true;
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                _cached = JsonSerializer.Deserialize<Settings>(json) ?? new Settings();
                return _cached;
            }
        }
        catch
        {
            // Load failed, return defaults
        }
        _cached = new Settings();
        return _cached;
    }

    /// <summary>
    /// Save settings to disk.
    /// </summary>
    public void Save()
    {
        _cached = this; // sync cache
        try
        {
            Directory.CreateDirectory(AppDataDir);
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(this, options);
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // Save failed, silently ignore
        }
    }

    // ===== Auto-start management =====

    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "IncognitoDispatcher";

    /// <summary>
    /// Set auto-start at login.
    /// </summary>
    public void SetAutoStart(bool enable)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (key == null) return;

            if (enable)
            {
                var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";
                key.SetValue(AppName, $"\"{exePath}\"");
            }
            else
            {
                try { key.DeleteValue(AppName, throwOnMissingValue: false); } catch { }
            }

            AutoStart = enable;
            Save();
        }
        catch
        {
            // Registry operation failed
        }
    }

    /// <summary>
    /// Check if auto-start is enabled.
    /// </summary>
    public bool IsAutoStartEnabled()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(AppName) != null;
        }
        catch
        {
            return false;
        }
    }
}
