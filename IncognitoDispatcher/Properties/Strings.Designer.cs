using System.Resources;
using System.Globalization;

namespace IncognitoDispatcher.Properties;

/// <summary>
///   Strongly-typed resource accessor class.
/// </summary>
public static class Strings
{
    private static ResourceManager? _resourceManager;

    public static ResourceManager ResourceManager
        => _resourceManager ??= new ResourceManager("IncognitoDispatcher.Properties.Strings", typeof(Strings).Assembly);

    public static CultureInfo? Culture { get; set; }

    public static string GetString(string name)
    {
        return ResourceManager.GetString(name, Culture) ?? name;
    }

    // ===== App =====
    public static string AppName => GetString("AppName");
    public static string AppSubtitle => GetString("AppSubtitle");

    // ===== Tray =====
    public static string TrayEnabled => GetString("TrayEnabled");
    public static string TrayChangeBrowser => GetString("TrayChangeBrowser");
    public static string TraySettings => GetString("TraySettings");
    public static string TrayExit => GetString("TrayExit");
    public static string TrayEnabledStatus => GetString("TrayEnabledStatus");
    public static string TrayDisabledStatus => GetString("TrayDisabledStatus");
    public static string TrayBrowserLabel => GetString("TrayBrowserLabel");
    public static string TrayNoneSelected => GetString("TrayNoneSelected");

    // ===== Browser =====
    public static string BrowserSection => GetString("BrowserSection");
    public static string BrowserNotSelected => GetString("BrowserNotSelected");
    public static string BrowserPickPrompt => GetString("BrowserPickPrompt");
    public static string BrowserChange => GetString("BrowserChange");
    public static string BrowserSelect => GetString("BrowserSelect");
    public static string BrowserNoneDetected => GetString("BrowserNoneDetected");

    // ===== Settings =====
    public static string SettingsSection => GetString("SettingsSection");
    public static string SettingsIncognito => GetString("SettingsIncognito");
    public static string SettingsAutoStart => GetString("SettingsAutoStart");

    // ===== Protocol =====
    public static string ProtocolSection => GetString("ProtocolSection");
    public static string ProtocolDetecting => GetString("ProtocolDetecting");
    public static string ProtocolInstalled => GetString("ProtocolInstalled");
    public static string ProtocolNotInstalled => GetString("ProtocolNotInstalled");
    public static string ProtocolInstall => GetString("ProtocolInstall");
    public static string ProtocolUninstall => GetString("ProtocolUninstall");
    public static string ProtocolInstallSuccess => GetString("ProtocolInstallSuccess");
    public static string ProtocolUninstallSuccess => GetString("ProtocolUninstallSuccess");
    public static string ProtocolUninstallFail => GetString("ProtocolUninstallFail");
    public static string ProtocolOpenSettingsHint => GetString("ProtocolOpenSettingsHint");

    // ===== Footer =====
    public static string FooterHint => GetString("FooterHint");

    // ===== Picker =====
    public static string PickerTitle => GetString("PickerTitle");
    public static string PickerSubtitle => GetString("PickerSubtitle");
    public static string PickerConfirm => GetString("PickerConfirm");
    public static string PickerCancel => GetString("PickerCancel");

    // ===== Handler =====
    public static string HandlerUacPrompt => GetString("HandlerUacPrompt");
    public static string HandlerUacDenied => GetString("HandlerUacDenied");
    public static string HandlerRegistryFailed => GetString("HandlerRegistryFailed");
    public static string HandlerLogLabel => GetString("HandlerLogLabel");
    public static string HandlerNoLog => GetString("HandlerNoLog");
    public static string HandlerException => GetString("HandlerException");
    public static string HandlerOpenSettingsMsg => GetString("HandlerOpenSettingsMsg");
    public static string HandlerDescription => GetString("HandlerDescription");
}
