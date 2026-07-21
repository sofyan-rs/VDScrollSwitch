using System.Runtime.InteropServices;

namespace VDScrollSwitch;

/// <summary>
/// Low-level global mouse hook. Fires <see cref="AltWheel"/> when the wheel
/// scrolls while Alt is held, and eats the event (returns non-zero) so it
/// never reaches the focused app.
/// </summary>
internal sealed class MouseHook : IDisposable
{
    private const int WH_MOUSE_LL = 14;
    private const int WM_MOUSEWHEEL = 0x020A;
    private const int VK_MENU = 0x12; // Alt

    private delegate nint LowLevelMouseProc(int nCode, nint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public nint dwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, nint hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(nint hhk);

    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    private static extern nint GetModuleHandle(string? lpModuleName);

    // Field, not local: prevents the delegate from being GC'd while the
    // unmanaged hook still holds a reference to it.
    private readonly LowLevelMouseProc _proc;
    private nint _hookHandle;

    /// <summary>Raised on Alt+wheel. Argument is wheel delta (positive = up/away from user).</summary>
    public event Action<int>? AltWheel;

    /// <summary>When false, events pass through untouched (feature disabled from tray).</summary>
    public bool Enabled { get; set; } = true;

    public MouseHook()
    {
        _proc = HookCallback;
    }

    public void Install()
    {
        using var curProcess = System.Diagnostics.Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule!;
        _hookHandle = SetWindowsHookEx(WH_MOUSE_LL, _proc, GetModuleHandle(curModule.ModuleName), 0);
        if (_hookHandle == 0)
            throw new InvalidOperationException($"Failed to install mouse hook, error {Marshal.GetLastWin32Error()}.");
    }

    private nint HookCallback(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0 && Enabled && wParam == WM_MOUSEWHEEL)
        {
            bool altDown = (GetAsyncKeyState(VK_MENU) & 0x8000) != 0;
            if (altDown)
            {
                var data = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                int delta = (short)(data.mouseData >> 16);
                AltWheel?.Invoke(delta);
                return 1; // eat the event
            }
        }

        return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_hookHandle != 0)
        {
            UnhookWindowsHookEx(_hookHandle);
            _hookHandle = 0;
        }
    }
}
