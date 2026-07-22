using Microsoft.Win32;

namespace VDScrollSwitch;

internal sealed class TrayAppContext : ApplicationContext
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "VDScrollSwitch";
    private const int CooldownMs = 150;

    private readonly NotifyIcon _trayIcon;
    private readonly MouseHook _hook;
    private readonly VirtualDesktopSwitcher _switcher;
    private readonly ToolStripMenuItem _enabledItem;
    private readonly ToolStripMenuItem _autoManageItem;
    private readonly ToolStripMenuItem _autoDetectItem;
    private readonly ToolStripMenuItem _autostartItem;

    private DateTime _lastSwitch = DateTime.MinValue;

    public TrayAppContext()
    {
        _switcher = new VirtualDesktopSwitcher();

        _hook = new MouseHook();
        _hook.AltWheel += OnAltWheel;
        _hook.Install();

        _enabledItem = new ToolStripMenuItem("Enabled", null, OnToggleEnabled) { Checked = true };
        _autoManageItem = new ToolStripMenuItem("Auto-create/delete desktops", null, OnToggleAutoManage)
        {
            Checked = true,
        };
        _autoDetectItem = new ToolStripMenuItem("    Auto-detect desktop", null, OnToggleAutoDetect)
        {
            Checked = false,
            ToolTipText = "Only add a desktop to the right once the last one is in use, "
                        + "instead of always keeping a spare.",
        };
        _autostartItem = new ToolStripMenuItem("Start with Windows", null, OnToggleAutostart)
        {
            Checked = IsAutostartEnabled(),
        };
        var exitItem = new ToolStripMenuItem("Exit", null, OnExit);

        var menu = new ContextMenuStrip();
        menu.Items.Add(_enabledItem);
        menu.Items.Add(_autoManageItem);
        menu.Items.Add(_autoDetectItem);
        menu.Items.Add(_autostartItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);

        _trayIcon = new NotifyIcon
        {
            Icon = LoadAppIcon(),
            Text = "VDScrollSwitch",
            Visible = true,
            ContextMenuStrip = menu,
        };
    }

    private static Icon LoadAppIcon()
    {
        using var stream = typeof(TrayAppContext).Assembly.GetManifestResourceStream("VDScrollSwitch.app.ico");
        return stream is not null ? new Icon(stream) : SystemIcons.Application;
    }

    private void OnAltWheel(int delta)
    {
        var now = DateTime.UtcNow;
        if ((now - _lastSwitch).TotalMilliseconds < CooldownMs)
            return;
        _lastSwitch = now;

        // COM calls cannot be made from inside a low-level mouse hook callback
        // (RPC_E_CANTCALLOUT_ININPUTSYNCCALL). Fire on a thread-pool thread
        // so the hook returns immediately.
        Task.Run(() =>
        {
            if (delta > 0)
                _switcher.Previous();
            else
                _switcher.Next();
        });
    }

    private void OnToggleEnabled(object? sender, EventArgs e)
    {
        _enabledItem.Checked = !_enabledItem.Checked;
        _hook.Enabled = _enabledItem.Checked;
    }

    private void OnToggleAutoManage(object? sender, EventArgs e)
    {
        _autoManageItem.Checked = !_autoManageItem.Checked;
        _switcher.AutoManageEnabled = _autoManageItem.Checked;
        _autoDetectItem.Enabled = _autoManageItem.Checked;
    }

    private void OnToggleAutoDetect(object? sender, EventArgs e)
    {
        _autoDetectItem.Checked = !_autoDetectItem.Checked;
        _switcher.AutoDetectEnabled = _autoDetectItem.Checked;
    }

    private void OnToggleAutostart(object? sender, EventArgs e)
    {
        bool enable = !_autostartItem.Checked;
        SetAutostart(enable);
        _autostartItem.Checked = enable;
    }

    private static bool IsAutostartEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(RunValueName) is not null;
    }

    private static void SetAutostart(bool enable)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath);

        if (enable)
            key.SetValue(RunValueName, Environment.ProcessPath ?? Application.ExecutablePath);
        else
            key.DeleteValue(RunValueName, throwOnMissingValue: false);
    }

    private void OnExit(object? sender, EventArgs e)
    {
        _trayIcon.Visible = false;
        _hook.Dispose();
        _switcher.Dispose();
        Application.Exit();
    }
}
