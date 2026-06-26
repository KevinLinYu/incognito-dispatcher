using System;
using System.Windows;
using System.Windows.Forms;
using S = IncognitoDispatcher.Properties.Strings;

namespace IncognitoDispatcher;

public partial class App : Application
{
    private MainWindow? _window;
    private NotifyIcon? _trayIcon;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // URL dispatch mode — if launched with a URL argument, dispatch and exit
        var args = Environment.GetCommandLineArgs();
        foreach (var arg in args.Skip(1))
        {
            var trimmed = arg.Trim('"');
            if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                UrlDispatcher.Run(trimmed);
                Shutdown();
                return;
            }
        }

        // GUI tray mode
        InitTrayIcon();
        var s = Settings.Load();
        if (s.ShowWindowOnStart) ShowMainWindow();
    }

    private void InitTrayIcon()
    {
        _trayIcon = new NotifyIcon { Icon = System.Drawing.SystemIcons.Shield, Text = S.AppName, Visible = true };
        var menu = new ContextMenuStrip();
        var toggle = new ToolStripMenuItem(S.TrayEnabled) { CheckOnClick = true, Checked = Settings.Load().Enabled };
        toggle.Click += (_, _) => { var s = Settings.Load(); s.Enabled = toggle.Checked; s.Save(); UpdateTray(); };
        menu.Items.Add(toggle);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(S.TrayChangeBrowser, null, (_, _) => ShowMainWindow());
        menu.Items.Add(S.TraySettings, null, (_, _) => ShowMainWindow());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(S.TrayExit, null, (_, _) => { _trayIcon?.Dispose(); Shutdown(); });
        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.DoubleClick += (_, _) => ShowMainWindow();
    }

    public void ShowMainWindow()
    {
        if (_window == null) { _window = new MainWindow(); _window.Closed += (_, _) => _window = null; }
        _window.Show();
        _window.WindowState = WindowState.Normal;
        _window.Activate();
    }

    public void UpdateTray()
    {
        if (_trayIcon == null) return;
        var s = Settings.Load();
        _trayIcon.Text = $"{S.AppName} - {(s.Enabled ? S.TrayEnabledStatus : S.TrayDisabledStatus)}\n{S.TrayBrowserLabel} {s.SelectedBrowserName ?? S.TrayNoneSelected}";
    }

    protected override void OnExit(ExitEventArgs e) { _trayIcon?.Dispose(); base.OnExit(e); }
}
