# PRD: VDScrollSwitch

**Ctrl+Scroll virtual desktop switcher for Windows**

Author: Hendriansyah Wijaya
Status: Draft v1
Target: Windows 10/11, .NET 8

---

## 1. Problem / Motivation

Windows has no built-in way to switch virtual desktops with the mouse wheel.
The only native paths are `Ctrl+Win+Left/Right` (keyboard) or Task View +
click. Tools like [GestureWheel](https://github.com/iodes/GestureWheel) solve
this with a middle-click-drag gesture; this PRD instead specs a simpler
**Ctrl+Scroll anywhere** gesture, implemented as a lightweight always-on tray
utility.

## 2. Goals

- Switch to the next/previous virtual desktop by holding **Ctrl** and
  scrolling the mouse wheel, from anywhere on the screen.
- Zero noticeable input lag; one wheel notch = exactly one desktop switch.
- Don't leak the Ctrl+Scroll gesture to the app under the cursor (no
  accidental zoom in browsers, VS Code, etc. while switching).
- Runs quietly in the system tray, near-zero idle CPU/memory footprint.
- Optional autostart with Windows.

## 3. Non-goals (out of scope for v1)

- Custom animations/transitions beyond what Windows' native
  `Ctrl+Win+Arrow` shortcut already provides.
- Touchpad gesture support (trackpad swipe) — mouse wheel only.
- Cross-monitor–specific behavior — behaves the same regardless of which
  monitor the cursor is on.
- Per-app exclusion list (documented as a fast-follow, not v1).
- Elevated/admin target window support (see Known Caveats).

## 4. User Stories

1. As a user with many virtual desktops, I want to hold Ctrl and scroll to
   jump between desktops instead of using both hands for
   `Ctrl+Win+Arrow`.
2. As a user, I want to toggle the feature off temporarily from the tray
   icon without closing the app, in case I need Ctrl+Scroll zoom in a
   specific app.
3. As a user, I want the app to start automatically when Windows boots,
   optionally.

## 5. Functional Requirements

| ID  | Requirement                                                                                                                                                           |
| --- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| FR1 | App installs a global low-level mouse hook (`WH_MOUSE_LL`) that observes wheel events system-wide.                                                                    |
| FR2 | On each wheel event, app checks whether Ctrl is currently held (`GetAsyncKeyState`).                                                                                  |
| FR3 | If Ctrl is held: app suppresses/eats the wheel event (does not propagate to the focused app) and triggers a desktop switch.                                           |
| FR4 | Scroll up → previous desktop. Scroll down → next desktop. (Direction should be a config option, not hardcoded, per FR9.)                                              |
| FR5 | Desktop switch is performed by simulating `Ctrl+Win+Left`/`Ctrl+Win+Right` via `SendInput`.                                                                           |
| FR6 | A debounce/cooldown (default 150ms) ensures one wheel notch triggers exactly one switch, even on high-polling-rate mice.                                              |
| FR7 | Tray icon with right-click context menu: **Enabled** (checkbox toggle), **Start with Windows** (checkbox toggle), **Exit**.                                           |
| FR8 | "Start with Windows" writes/removes a value under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.                                                               |
| FR9 | Config (direction reversed, cooldown ms) persisted locally (e.g. simple JSON in `%AppData%\VDScrollSwitch\config.json`) — v1.1, not required for first working build. |

## 6. Non-Functional Requirements

- **Performance**: hook callback must be fast (<1ms typical) — Windows will
  silently disable a low-level hook that blocks too long.
- **Footprint**: idle memory <30MB, idle CPU ~0%.
- **Reliability**: no crash on repeated rapid scrolling; hook must survive
  Explorer restarts and display sleep/wake cycles.
- **Privacy**: no telemetry, no network calls.
- **Permissions**: runs unelevated by default (see caveat on UIPI below).

## 7. Architecture

Plain C# / .NET 8, `WinExe`, WinForms only for `NotifyIcon` (no WPF needed
for a tray-only utility — keeps footprint small).

```
Program.cs              → entry point, message loop
TrayAppContext.cs        → NotifyIcon, context menu, debounce, wiring
MouseHook.cs              → WH_MOUSE_LL P/Invoke wrapper, Ctrl+wheel detection
VirtualDesktopSwitcher.cs → SendInput simulation of Ctrl+Win+Arrow
```

No dependency on the undocumented `IVirtualDesktopManagerInternal` COM
interface — deliberately avoided since it breaks across Windows builds.
`SendInput`-simulated native shortcut is the stable choice.

## 8. Key Technical Risks / Caveats

1. **UIPI**: `SendInput` may be blocked if the foreground window runs
   elevated and this app doesn't. Mitigation: document the limitation;
   don't request admin by default (avoids UAC prompt on every launch).
2. **Global gesture takeover**: eating Ctrl+Scroll everywhere means any app
   that binds that combo to something else (zoom, etc.) loses it while
   this tool is enabled. Mitigation: tray Enable/Disable toggle (FR7).
3. **Low-level hook timeout**: Windows disables a hook if its callback is
   too slow. Keep `HookCallback` allocation-free and branch-minimal.

## 9. Acceptance Criteria

- [ ] Holding Ctrl and scrolling anywhere switches virtual desktop with the
      native Windows animation.
- [ ] Regular Ctrl+Scroll (zoom) in a browser/editor does NOT occur while
      the app is enabled.
- [ ] Disabling via tray menu restores normal Ctrl+Scroll behavior
      immediately.
- [ ] Toggling "Start with Windows" correctly adds/removes the Run key.
- [ ] App survives at least 1 hour of normal use with no crash and no
      measurable memory growth.
- [ ] Fast scrolling (multiple notches within cooldown window) results in
      exactly one switch per debounce window, not one per notch.

## 10. Fast Follows (post-v1)

- Settings window: reverse direction, adjustable cooldown, custom tray icon.
- Per-app exclusion list (skip eating the event for specified process
  names, e.g. `Code.exe`, `chrome.exe`).
- MSIX packaging for clean install/uninstall + auto-update.
- Optional: subtle on-screen desktop-number indicator on switch.

## 11. Reference

Inspiration/prior art: [iodes/GestureWheel](https://github.com/iodes/GestureWheel)
(middle-click-drag gesture + `IVirtualDesktopManager` API). This PRD
intentionally diverges to a Ctrl+Scroll gesture and a `SendInput`-based
switch mechanism for simplicity and cross-version stability.
