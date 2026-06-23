# 🥷 Incognito Dispatcher

A lightweight Windows utility that intercepts browser link activations and redirects them to incognito (private) browsing mode.

## Why?

On macOS, when Safari has an active window in Private Browsing mode, links opened from other applications automatically open in that Private Browsing session. Windows has no equivalent behavior — there is no system-level setting to force all `http`/`https` links into incognito mode. This tool fills that gap: it registers itself as the default protocol handler and transparently relaunches every link in your chosen browser with the private browsing flag enabled.

## How It Works

When a URL is opened from any application — Outlook, Telegram, a PDF reader, or any other program — Incognito Dispatcher catches the `http`/`https` protocol handler and relaunches the link in your chosen browser with the appropriate private browsing flag enabled.

## Highlights

- **Protocol-level interception** — Registers as the system `http`/`https` handler via the Windows UserChoice registry
- **Broad browser support** — Chrome, Edge, Firefox, Brave, Vivaldi, Opera, Arc, and any Chromium-based browser; auto-detects non-standard install paths (Canary, Beta, Dev)
- **System tray resident** — Persists in the notification area after the window is closed; context menu for quick toggling
- **Configurable auto-start** — Optional `HKCU\...\Run` registration for launch at login
- **Internationalized UI** — Ships with English, Chinese (Simplified/Traditional), and Japanese; follows the system locale automatically
- **Zero dependencies** — Built entirely on .NET 10 WPF; no third-party NuGet packages required

## Requirements

| Component | Version |
|-----------|---------|
| Windows | 10 (build 19041+) or 11 |
| .NET SDK | 10.0+ |

## Getting Started

### Build from source

```bash
dotnet build -c Release
```

### Publish as a self-contained single file

```bash
dotnet publish IncognitoDispatcher/IncognitoDispatcher.csproj \
  -c Release -r win-x64 --self-contained \
  -p:PublishSingleFile=true -o publish
```

### Run

```bash
.\publish\IncognitoDispatcher.exe
# or
dotnet run --project IncognitoDispatcher
```

## Usage

1. **Launch** the application. A shield icon appears in the system tray.
2. **Double-click** the tray icon to open the settings window.
3. **Select a browser** — click "Select Browser" and choose from the detected list.
4. **Register** — click "Register" to claim the `http`/`https` protocol handler. A UAC prompt will appear (required once).
5. **Done** — any link opened from any application will now launch in incognito mode.

To restore the original browser behavior, open the settings window and click "Uninstall".

## Architecture

### Dispatch flow

```
Application calls ShellExecute("https://...")
    Windows resolves http/https protocol → IncognitoDispatcher.exe
    App.OnStartup detects URL argument
        ├── Reads settings → selected browser path
        ├── Appends --incognito / --inprivate / --private-window
        └── Process.Start(browser.exe, args)
```

### Protocol registration

Windows 11 introduced UCPD.sys (UserChoice Protection Driver), a kernel-level driver that prevents applications from modifying the default browser association. To work around this, the installer:

1. Disables the UCPD scheduled tasks (`UCPDACC*`)
2. Stops the UCPD kernel driver (`sc stop UCPD`)
3. Writes the UserChoice registry keys directly
4. Restarts UCPD and re-enables the tasks

This is a one-time operation requiring administrator privileges.

### Browser discovery

The detector queries two sources in order:

1. **Registry**: `HKLM` and `HKCU\SOFTWARE\Clients\StartMenuInternet` with Capabilities metadata
2. **Filesystem**: A hardcoded list of well-known installation paths under `%ProgramFiles%` and `%LOCALAPPDATA%` (covers Chrome Canary, Beta, Dev, Opera GX, LibreWolf, Waterfox, etc.)

## Project Structure

```
IncognitoDispatcher/
├── App.xaml / App.xaml.cs          Application entry point, tray icon, URL dispatch
├── MainWindow.xaml / .cs           Settings window (Fluent-style WPF)
├── BrowserPickerWindow.xaml.cs     Browser selection dialog
├── UrlDispatcher.cs                URL interception and browser launch logic
├── BrowserDetector.cs              Registry + filesystem browser discovery
├── BrowserInfo.cs                  Browser metadata model
├── ProtocolHandler.cs              Protocol handler registration (UCPD bypass)
├── Settings.cs                     JSON-based settings persistence
├── GlobalUsings.cs                 WPF/WinForms disambiguation aliases
└── Properties/
    ├── Strings.resx                Simplified Chinese (default)
    ├── Strings.en.resx             English
    ├── Strings.zh-TW.resx          Traditional Chinese
    ├── Strings.ja.resx             Japanese
    └── Strings.Designer.cs         Strongly-typed resource accessor
```

## Known Limitations

- **UCPD driver requirement** — Registration temporarily disables a Windows kernel driver. Some enterprise environments may restrict this via Group Policy.
- **Chromium single-instance model** — When a Chromium-based browser is already running, the `--incognito` flag is passed via IPC. Behavior may differ from a cold launch.
- **UserChoice hash bypass** — Windows uses an HMAC-based hash to protect the UserChoice key. This project circumvents the protection by disabling UCPD rather than computing the hash.

## License

[MIT](LICENSE)
