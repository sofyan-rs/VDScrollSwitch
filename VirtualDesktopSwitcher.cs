using System.Runtime.InteropServices;

namespace VDScrollSwitch;

/// <summary>
/// Switches virtual desktop by simulating the native Ctrl+Win+Left/Right
/// shortcut via SendInput. Deliberately avoids the undocumented
/// IVirtualDesktopManagerInternal COM interface, which breaks across builds.
/// </summary>
internal static class VirtualDesktopSwitcher
{
    private const int VK_LWIN = 0x5B;
    private const int VK_CONTROL = 0x11;
    private const int VK_MENU = 0x12; // Alt
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

    // Must include MOUSEINPUT even though it's unused here: it's the largest
    // member of the real Win32 union, and SendInput validates cbSize against
    // the true native INPUT size (40 bytes on x64). Omitting it undersizes
    // the struct and SendInput rejects every event with ERROR_INVALID_PARAMETER.
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

    public static void Next() => SwitchDesktop(VK_RIGHT);

    public static void Previous() => SwitchDesktop(VK_LEFT);

    private static void SwitchDesktop(int arrowKey)
    {
        // Trigger key (Alt) may still be physically held by the caller at this
        // point; release it synthetically first so it doesn't get mixed into
        // the combo below and stop it matching the registered shortcut.
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
