using System.Diagnostics;
using System.Runtime.InteropServices;
using WindowsDesktop;

namespace VDScrollSwitch;

/// <summary>
/// Manages virtual desktop switching via the Slions.VirtualDesktop COM wrapper.
/// When AutoManageEnabled, maintains a buffer desktop to the right of the
/// current desktop: auto-creates one when the user reaches the rightmost
/// desktop, and trims empty auto-created desktops from the right edge
/// when the user moves left.
/// Falls back to SendInput (Ctrl+Win+Arrow) if the COM API is unavailable.
/// </summary>
internal sealed class VirtualDesktopSwitcher
{
    private readonly HashSet<Guid> _autoCreatedDesktops = new();
    private bool _comAvailable = true;
    private static readonly string _logPath = Path.Combine(
        Path.GetDirectoryName(Environment.ProcessPath) ?? ".", "vdswitch.log");

    public bool AutoManageEnabled { get; set; } = true;

    public VirtualDesktopSwitcher()
    {
        try
        {
            _ = VirtualDesktop.GetDesktops();
            Log("COM API initialized OK");
        }
        catch (Exception ex)
        {
            _comAvailable = false;
            Log($"COM API unavailable: {ex.Message}");
        }
    }

    public void Next()
    {
        // Always use SendInput for switching — preserves Windows slide animation.
        SendInputSwitch(VK_RIGHT);

        if (!_comAvailable || !AutoManageEnabled)
            return;

        // After the switch, ensure there's a buffer desktop to the right.
        // Small delay so the switch completes and VirtualDesktop.Current updates.
        Thread.Sleep(200);
        try
        {
            EnsureBufferDesktop();
        }
        catch (Exception ex)
        {
            Log($"Next() auto-manage error: {ex.Message}");
        }
    }

    public void Previous()
    {
        // Always use SendInput for switching — preserves Windows slide animation.
        SendInputSwitch(VK_LEFT);

        if (!_comAvailable || !AutoManageEnabled)
            return;

        // After the switch, trim empty auto-created desktops from the right edge.
        Thread.Sleep(200);
        try
        {
            TrimRightEdge();
        }
        catch (Exception ex)
        {
            Log($"Previous() auto-manage error: {ex.Message}");
        }
    }

    /// <summary>
    /// If the current desktop is the rightmost, create a new empty desktop
    /// to the right so the user always has room to scroll right.
    /// </summary>
    private void EnsureBufferDesktop()
    {
        try
        {
            var current = VirtualDesktop.Current;
            if (current.GetRight() is null)
            {
                var buf = VirtualDesktop.Create();
                _autoCreatedDesktops.Add(buf.Id);
                Log($"Buffer created: {buf.Id}");
            }
        }
        catch (Exception ex)
        {
            Log($"EnsureBufferDesktop error: {ex.Message}");
        }
    }

    /// <summary>
    /// After moving left, repeatedly remove the rightmost desktop if it is
    /// auto-created and empty. Stops at the first non-empty or non-auto-created
    /// desktop.
    /// </summary>
    private void TrimRightEdge()
    {
        try
        {
            var desktops = VirtualDesktop.GetDesktops();
            // Walk from the right edge inward.
            for (int i = desktops.Length - 1; i >= 1; i--)
            {
                var last = desktops[i];
                if (!_autoCreatedDesktops.Contains(last.Id))
                    break;
                if (DesktopHasWindows(last.Id))
                    break;

                // Don't delete the desktop the user is currently on.
                if (last.Id == VirtualDesktop.Current.Id)
                    break;

                last.Remove(desktops[i - 1]);
                _autoCreatedDesktops.Remove(last.Id);
                Log($"Trimmed: {last.Id}");
            }
        }
        catch (Exception ex)
        {
            Log($"TrimRightEdge error: {ex.Message}");
        }
    }

    /// <summary>
    /// Checks whether any visible application window exists on the given desktop.
    /// </summary>
    private static bool DesktopHasWindows(Guid desktopId)
    {
        bool hasWindows = false;

        EnumWindows((hwnd, _) =>
        {
            if (!IsWindowVisible(hwnd))
                return true;

            if (GetWindowTextLength(hwnd) == 0)
                return true;

            long exStyle = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
            if ((exStyle & WS_EX_TOOLWINDOW) != 0)
                return true;

            if (DwmGetWindowAttribute(hwnd, DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0 && cloaked != 0)
                return true;

            try
            {
                var desktop = VirtualDesktop.FromHwnd(hwnd);
                if (desktop is not null && desktop.Id == desktopId)
                {
                    hasWindows = true;
                    return false;
                }
            }
            catch
            {
                // Window not associated with any desktop — skip
            }

            return true;
        }, IntPtr.Zero);

        return hasWindows;
    }

    private static void Log(string msg)
    {
        try
        {
            File.AppendAllText(_logPath, $"[{DateTime.Now:HH:mm:ss.fff}] {msg}{Environment.NewLine}");
        }
        catch { }
        Debug.WriteLine($"[VDScrollSwitch] {msg}");
    }

    // ── P/Invoke: window enumeration ────────────────────────────────

    private const int GWL_EXSTYLE = -20;
    private const long WS_EX_TOOLWINDOW = 0x00000080;
    private const int DWMWA_CLOAKED = 14;

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);

    // ── P/Invoke: SendInput fallback ────────────────────────────────

    private const int VK_LWIN = 0x5B;
    private const int VK_CONTROL = 0x11;
    private const int VK_MENU = 0x12;
    private const int VK_LEFT = 0x25;
    private const int VK_RIGHT = 0x27;

    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public nint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public nint dwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    private static void SendInputSwitch(int arrowKey)
    {
        var inputs = new[]
        {
            KeyUp(VK_MENU),
            KeyDown(VK_CONTROL),
            KeyDown(VK_LWIN, extended: true),
            KeyDown(arrowKey, extended: true),
            KeyUp(arrowKey, extended: true),
            KeyUp(VK_LWIN, extended: true),
            KeyUp(VK_CONTROL),
        };

        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }

    private static INPUT KeyDown(int vk, bool extended = false) => new()
    {
        type = INPUT_KEYBOARD,
        U = new InputUnion
        {
            ki = new KEYBDINPUT { wVk = (ushort)vk, dwFlags = extended ? KEYEVENTF_EXTENDEDKEY : 0 },
        },
    };

    private static INPUT KeyUp(int vk, bool extended = false) => new()
    {
        type = INPUT_KEYBOARD,
        U = new InputUnion
        {
            ki = new KEYBDINPUT
            {
                wVk = (ushort)vk,
                dwFlags = KEYEVENTF_KEYUP | (extended ? KEYEVENTF_EXTENDEDKEY : 0),
            },
        },
    };
}
