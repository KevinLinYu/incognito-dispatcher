using System.Diagnostics;
using S = IncognitoDispatcher.Properties.Strings;
using System.Windows;

namespace IncognitoDispatcher;

public partial class MainWindow : Window
{
    private Settings _settings;
    private bool _suppress;

    public MainWindow()
    {
        InitializeComponent();
        _settings = Settings.Load();

        // Set window icon (taskbar + title bar)
        var icon = System.Drawing.SystemIcons.Shield;
        var bmp = icon.ToBitmap();
        var ms = new System.IO.MemoryStream();
        bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        ms.Position = 0;
        var bitmapImage = new System.Windows.Media.Imaging.BitmapImage();
        bitmapImage.BeginInit();
        bitmapImage.StreamSource = ms;
        bitmapImage.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
        bitmapImage.EndInit();
        bitmapImage.Freeze();
        Icon = bitmapImage;
        // Do not using/dispose - WPF needs continuous reference

        _suppress = true;
        EnabledCheck.IsChecked = _settings.Enabled;
        AutoStartCheck.IsChecked = _settings.IsAutoStartEnabled();
        _suppress = false;
        UpdateUI();

        // Refresh UI when window becomes visible (browser selection may change via tray/dispatch)
        IsVisibleChanged += (_, _) => { if (IsVisible) { _settings = Settings.Load(); UpdateUI(); } };
    }

    private void UpdateUI()
    {
        if (!string.IsNullOrEmpty(_settings.SelectedBrowserPath))
        {
            BrowserText.Text = $"{_settings.SelectedBrowserName}\n{_settings.SelectedBrowserPath}";
            ChangeBrowserBtn.Content = S.BrowserChange;
        }
        else
        {
            BrowserText.Text = S.BrowserPickPrompt;
            ChangeBrowserBtn.Content = S.BrowserSelect;
        }
        var installed = ProtocolHandler.IsInstalled();
        InstallStatusText.Text = installed ? S.ProtocolInstalled : S.ProtocolNotInstalled;
        InstallBtn.IsEnabled = !installed;
        UninstallBtn.IsEnabled = installed;
    }

    private void OnChangeBrowser(object sender, RoutedEventArgs e)
    {
        var browsers = UrlDispatcher.GetAllBrowsers();
        if (browsers.Count == 0)
        {
            System.Windows.MessageBox.Show(S.BrowserNoneDetected, S.AppName);
            return;
        }
        var picker = new BrowserPickerWindow(browsers);
        picker.Owner = this;
        if (picker.ShowDialog() == true && picker.SelectedBrowser != null)
        {
            _settings.SelectedBrowserPath = picker.SelectedBrowser.Value.Path;
            _settings.SelectedBrowserName = picker.SelectedBrowser.Value.Name;
            _settings.Save();
        }
        UpdateUI();
        (System.Windows.Application.Current as App)?.UpdateTray();
    }

    private void OnEnabledClick(object sender, RoutedEventArgs e)
    {
        if (_suppress) return;
        _settings.Enabled = EnabledCheck.IsChecked == true;
        _settings.Save();
        (System.Windows.Application.Current as App)?.UpdateTray();
    }

    private void OnAutoStartClick(object sender, RoutedEventArgs e)
    {
        if (_suppress) return;
        _settings.SetAutoStart(AutoStartCheck.IsChecked == true);
    }

    private void OnInstall(object sender, RoutedEventArgs e)
    {
        var exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
        ProtocolHandler.RegisterProgId(exePath);
        _settings.OriginalBrowserProgId = ProtocolHandler.GetRegisteredProgId() ?? "MSEdgeHTM";
        _settings.Save();
        var (ok, err) = ProtocolHandler.RunWithAdmin(ProtocolHandler.ProgId);
        if (!ok)
        {
            try { Process.Start(new ProcessStartInfo("ms-settings:defaultapps") { UseShellExecute = true }); } catch { }
            ResultText.Text = S.ProtocolOpenSettingsHint;
            ResultText.Foreground = (System.Windows.Media.Brush)FindResource("WarningBrush");
        }
        else
        {
            ResultText.Text = S.ProtocolInstallSuccess;
            ResultText.Foreground = (System.Windows.Media.Brush)FindResource("SuccessBrush");
        }
        ResultText.Visibility = Visibility.Visible;
        UpdateUI();
    }

    private void OnUninstall(object sender, RoutedEventArgs e)
    {
        var (ok, _) = ProtocolHandler.RunWithAdmin(_settings.OriginalBrowserProgId ?? "MSEdgeHTM");
        ProtocolHandler.CleanupProgId();
        ResultText.Text = ok ? S.ProtocolUninstallSuccess : S.ProtocolUninstallFail;
        ResultText.Foreground = (System.Windows.Media.Brush)FindResource(ok ? "SuccessBrush" : "ErrorBrush");
        ResultText.Visibility = Visibility.Visible;
        UpdateUI();
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }
}
