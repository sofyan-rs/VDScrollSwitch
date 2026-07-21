# VDScrollSwitch

Alt+Scroll virtual desktop switcher for Windows.

Hold **Alt** and scroll the mouse wheel anywhere on screen to switch
virtual desktops — no need for `Ctrl+Win+Left/Right`. Runs quietly in
the system tray.

## Features

- Alt+Scroll up/down → previous/next virtual desktop, from anywhere.
- Suppresses the wheel event so it doesn't leak to the focused app.
- 150ms debounce — one wheel notch = one switch, even on high-polling mice.
- Tray menu: Enable/Disable toggle, Start with Windows, Exit.
- No telemetry, no network calls, unelevated by default.

## Requirements

- Windows 10/11
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (if not using the self-contained build)

## Build

```bash
dotnet build
```

Run:

```bash
dotnet run
```

## Publish

Small exe, requires [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) on the target machine (~164KB):

```bash
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

Standalone exe, no runtime needed on the target machine (~146MB, bundles the whole .NET runtime):

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Output either way: `bin/Release/net8.0-windows/win-x64/publish/VDScrollSwitch.exe`

## Known limitations

- If the focused window is running elevated (Administrator) and this
  app isn't, Windows UIPI blocks the simulated shortcut. Don't run
  this app elevated by default — see [PRD.md](PRD.md) for details.
- Touchpad gestures aren't supported — mouse wheel only.

## Architecture

```
Program.cs                → entry point, message loop
TrayAppContext.cs         → NotifyIcon, context menu, debounce, wiring
MouseHook.cs               → WH_MOUSE_LL P/Invoke wrapper, Alt+wheel detection
VirtualDesktopSwitcher.cs  → SendInput simulation of Ctrl+Win+Arrow
```

See [PRD.md](PRD.md) for full requirements and design rationale.
