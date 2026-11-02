using System.Text;
using Microsoft.Win32;

namespace TopAwake;

internal sealed class TrayAppContext : ApplicationContext
{
    private static readonly int[] TimedOptions = { 30, 60, 120 };
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunKeyName = "TopAwake";

    private static readonly string[] ShellWindowClasses =
        { "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "DV2ControlHost" };

    private readonly NotifyIcon _tray = new();
    private readonly HotkeyWindow _hotkeyWindow = new();
    private readonly Dictionary<IntPtr, BorderOverlay> _overlays = new();
    private readonly AwakeManager _awake = new();
    private readonly Settings _settings;
    private readonly System.Windows.Forms.Timer _statusTimer = new() { Interval = 30_000 };

    private readonly ToolStripMenuItem _hotkeyItem = new("窗口置顶 (Win+Ctrl+T)") { CheckOnClick = true };
    private readonly ToolStripMenuItem _awakeOffItem = new("关闭");
    private readonly ToolStripMenuItem _awakeIndefiniteItem = new("一直保持唤醒");
    private readonly ToolStripMenuItem[] _awakeTimedItems;
    private readonly ToolStripMenuItem _screenOnItem = new("保持屏幕常亮") { CheckOnClick = true };
    private readonly ToolStripMenuItem _startupItem = new("开机自启动") { CheckOnClick = true };

    private Icon? _icon;

    public TrayAppContext()
    {
        _settings = Settings.Load();

        _ = _hotkeyWindow.Handle;
        _hotkeyWindow.HotkeyPressed += (_, _) => ToggleForegroundTopMost();
        _awake.Changed += (_, _) =>
        {
            if (_hotkeyWindow.IsHandleCreated && !_hotkeyWindow.IsDisposed)
            {
                try { _hotkeyWindow.BeginInvoke(RefreshAwakeUi); }
                catch (ObjectDisposedException) { }
            }
        };
        _statusTimer.Tick += (_, _) => UpdateTrayText();

        _awakeTimedItems = TimedOptions.Select(minutes =>
        {
            var item = new ToolStripMenuItem($"定时唤醒 {FormatMinutes(minutes)}");
            item.Click += (_, _) => SetAwakeMode(AwakeMode.Timed, minutes);
            return item;
        }).ToArray();

        _awakeOffItem.Click += (_, _) => SetAwakeMode(AwakeMode.Off);
        _awakeIndefiniteItem.Click += (_, _) => SetAwakeMode(AwakeMode.Indefinite);

        var awakeMenu = new ToolStripMenuItem("防休眠 (Awake)");
        awakeMenu.DropDownItems.Add(_awakeOffItem);
        awakeMenu.DropDownItems.Add(_awakeIndefiniteItem);
        awakeMenu.DropDownItems.AddRange(_awakeTimedItems);
        awakeMenu.DropDownItems.Add(new ToolStripSeparator());
        awakeMenu.DropDownItems.Add(_screenOnItem);

        var menu = new ContextMenuStrip();
        menu.Items.Add(_hotkeyItem);
        menu.Items.Add(awakeMenu);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_startupItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => Exit());

        _hotkeyItem.Click += (_, _) => SetHotkeyEnabled(_hotkeyItem.Checked);
        _screenOnItem.Click += (_, _) =>
        {
            _awake.SetKeepScreenOn(_screenOnItem.Checked);
            _settings.KeepScreenOn = _screenOnItem.Checked;
            _settings.Save();
        };
        _startupItem.Click += (_, _) =>
        {
            try { SetRunAtStartup(_startupItem.Checked); }
            catch (Exception ex) { Balloon("设置开机自启失败: " + ex.Message); }
            _startupItem.Checked = IsRunAtStartup();
        };

        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) =>
            SetAwakeMode(_awake.Mode == AwakeMode.Off ? AwakeMode.Indefinite : AwakeMode.Off);

        SetIcon(false);
        _tray.Visible = true;

        // 恢复上次设置
        _screenOnItem.Checked = _settings.KeepScreenOn;
        _awake.SetKeepScreenOn(_settings.KeepScreenOn);
        _startupItem.Checked = IsRunAtStartup();

        if (_settings.HotkeyEnabled)
        {
            if (_hotkeyWindow.SetHotkeyEnabled(true))
            {
                _hotkeyItem.Checked = true;
            }
            else
            {
                Balloon("热键 Win+Ctrl+T 注册失败，可能被其他程序（如 PowerToys）占用。");
            }
        }

        var mode = Enum.TryParse<AwakeMode>(_settings.AwakeMode, out var parsed) ? parsed : AwakeMode.Off;
        SetAwakeMode(mode, _settings.TimedMinutes);
    }

    private void ToggleForegroundTopMost()
    {
        var hwnd = Native.GetForegroundWindow();
        if (hwnd == IntPtr.Zero || !Native.IsWindow(hwnd)) return;
        if (hwnd == _hotkeyWindow.Handle) return;

        var className = new StringBuilder(256);
        Native.GetClassName(hwnd, className, className.Capacity);
        if (ShellWindowClasses.Contains(className.ToString()))
        {
            Balloon("桌面和任务栏不支持置顶。");
            return;
        }

        var exStyle = Native.GetWindowLong(hwnd, Native.GWL_EXSTYLE);
        var makeTopMost = (exStyle & Native.WS_EX_TOPMOST) == 0;

        if (!Native.SetWindowPos(
                hwnd,
                makeTopMost ? Native.HWND_TOPMOST : Native.HWND_NOTOPMOST,
                0, 0, 0, 0,
                Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE | Native.SWP_SHOWWINDOW))
        {
            Balloon("无法置顶该窗口。目标窗口权限更高时，请以管理员身份运行 TopAwake。");
            return;
        }

        if (makeTopMost)
        {
            if (!_overlays.ContainsKey(hwnd))
            {
                var overlay = new BorderOverlay(hwnd, Color.FromArgb(0, 120, 215));
                _overlays.Add(hwnd, overlay);
                overlay.TargetUnavailable += (_, _) => RemoveOverlay(hwnd, overlay);
                if (!overlay.Start()) Balloon("实时边框同步未能启用，将使用快速轮询。");
            }
        }
        else RemoveOverlay(hwnd);
    }

    private void RemoveOverlay(IntPtr hwnd, BorderOverlay? expected = null)
    {
        if (!_overlays.TryGetValue(hwnd, out var overlay) ||
            (expected != null && !ReferenceEquals(overlay, expected))) return;
        _overlays.Remove(hwnd);
        overlay.Dispose();
    }

    private void SetHotkeyEnabled(bool enable)
    {
        if (enable && !_hotkeyWindow.SetHotkeyEnabled(true))
        {
            _hotkeyItem.Checked = false;
            Balloon("热键 Win+Ctrl+T 注册失败，可能被其他程序（如 PowerToys）占用。");
            enable = false;
        }
        else if (!enable)
        {
            _hotkeyWindow.SetHotkeyEnabled(false);
        }

        _settings.HotkeyEnabled = enable;
        _settings.Save();
    }

    private void SetAwakeMode(AwakeMode mode, int timedMinutes = 30)
    {
        _awake.SetMode(mode, timedMinutes);
        _settings.AwakeMode = mode.ToString();
        if (mode == AwakeMode.Timed) _settings.TimedMinutes = timedMinutes;
        _settings.Save();
        RefreshAwakeUi();
    }

    private void RefreshAwakeUi()
    {
        _awakeOffItem.Checked = _awake.Mode == AwakeMode.Off;
        _awakeIndefiniteItem.Checked = _awake.Mode == AwakeMode.Indefinite;
        for (var i = 0; i < _awakeTimedItems.Length; i++)
        {
            _awakeTimedItems[i].Checked =
                _awake.Mode == AwakeMode.Timed && _awake.TimedMinutes == TimedOptions[i];
        }

        _statusTimer.Enabled = _awake.Mode == AwakeMode.Timed;
        SetIcon(_awake.Mode != AwakeMode.Off);
        UpdateTrayText();
    }

    private void UpdateTrayText()
    {
        var text = _awake.Mode switch
        {
            AwakeMode.Indefinite => "TopAwake - 防休眠已开启",
            AwakeMode.Timed when _awake.EndsAt is { } end =>
                $"TopAwake - 防休眠剩余 {Math.Max(1, (int)Math.Ceiling((end - DateTimeOffset.Now).TotalMinutes))} 分钟",
            _ => "TopAwake"
        };
        if (text.Length > 127) text = text[..127];
        _tray.Text = text;
    }

    private void SetIcon(bool awakeActive)
    {
        var old = _icon;
        _icon = IconFactory.Create(awakeActive);
        _tray.Icon = _icon;
        if (old != null)
        {
            var handle = old.Handle;
            old.Dispose();
            Native.DestroyIcon(handle);
        }
    }

    private void Balloon(string text) =>
        _tray.ShowBalloonTip(3000, "TopAwake", text, ToolTipIcon.Info);

    private static string FormatMinutes(int minutes) =>
        minutes % 60 == 0 ? $"{minutes / 60} 小时" : $"{minutes} 分钟";

    private static bool IsRunAtStartup()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
        return key?.GetValue(RunKeyName) is string;
    }

    private static void SetRunAtStartup(bool enable)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
        if (key == null) return;
        if (enable)
        {
            key.SetValue(RunKeyName, $"\"{Application.ExecutablePath}\"");
        }
        else
        {
            key.DeleteValue(RunKeyName, false);
        }
    }

    private void Exit()
    {
        _hotkeyWindow.SetHotkeyEnabled(false);
        _awake.SetMode(AwakeMode.Off);
        _awake.Dispose();
        _statusTimer.Stop();
        _statusTimer.Dispose();
        _tray.Visible = false;
        _tray.Dispose();
        foreach (var (hwnd, overlay) in _overlays.ToArray())
        {
            RemoveOverlay(hwnd, overlay);
        }
        _hotkeyWindow.Dispose();
        if (_icon != null)
        {
            var handle = _icon.Handle;
            _icon.Dispose();
            Native.DestroyIcon(handle);
        }
        Application.Exit();
    }
}
