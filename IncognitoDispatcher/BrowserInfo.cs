namespace IncognitoDispatcher;

/// <summary>
/// Browser type enumeration.
/// </summary>
public enum BrowserType
{
    Chrome,
    Edge,
    Firefox,
    Brave,
    Vivaldi,
    Opera,
    Arc,
    ChromiumGeneric, // Generic Chromium-based
    Unknown
}

/// <summary>
/// Browser information model.
/// </summary>
public class BrowserInfo
{
    /// <summary>Browser type.</summary>
    public BrowserType Type { get; set; }

    /// <summary>Display name (e.g. "Google Chrome").</summary>
    public string DisplayName { get; set; } = "";

    /// <summary>Full path to the executable.</summary>
    public string ExecutablePath { get; set; } = "";

    /// <summary>Original registry command template.</summary>
    public string OriginalCommand { get; set; } = "";

    /// <summary>ProgId (e.g. ChromeHTML, MSEdgeHTM).</summary>
    public string ProgId { get; set; } = "";

    /// <summary>Incognito/private mode command-line flag.</summary>
    public string IncognitoFlag => Type switch
    {
        BrowserType.Chrome => "--incognito",
        BrowserType.Edge => "--inprivate",
        BrowserType.Firefox => "--private-window",
        BrowserType.Brave => "--incognito",
        BrowserType.Vivaldi => "--incognito",
        BrowserType.Opera => "--private",
        BrowserType.Arc => "--incognito",
        BrowserType.ChromiumGeneric => "--incognito",
        _ => "--incognito" // Unknown browser: try --incognito as default
    };

    /// <summary>
    /// Build incognito mode command-line arguments.
    /// </summary>
    public string BuildIncognitoArgs(string url)
    {
        return Type switch
        {
            // Chromium: pass URL directly
            BrowserType.Chrome or BrowserType.Edge or BrowserType.Brave
            or BrowserType.Vivaldi or BrowserType.Opera or BrowserType.Arc
            or BrowserType.ChromiumGeneric =>
                $"--incognito \"{url}\"",

            // Firefox
            BrowserType.Firefox =>
                $"--private-window \"{url}\"",

            // Unknown: simple fallback
            _ => $"--incognito \"{url}\""
        };
    }

    /// <summary>
    /// Build normal mode command-line arguments (restore original).
    /// </summary>
    public string BuildNormalArgs(string url)
    {
        // Try to restore the original registry command format
        if (!string.IsNullOrEmpty(OriginalCommand))
        {
            return OriginalCommand
                .Replace("\"%1\"", $"\"{url}\"")
                .Replace("%1", $"\"{url}\"");
        }

        return $"\"{url}\"";
    }

    public override string ToString() => $"{DisplayName} ({Type})";
}
