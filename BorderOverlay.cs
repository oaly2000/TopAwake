namespace TopAwake;

internal sealed class BorderOverlay : Form
{
    private const int Thickness = 2;
    private const int ExTransparent = 0x00000020;
    private const int ExToolWindow = 0x00000080;
    private const int ExNoActivate = 0x08000000;

    private readonly IntPtr _target;
    private readonly Native.WinEventProc _locationChangedCallback;
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 250 };
    private IntPtr _winEventHook;
    private bool _regionInitialized;

    public event EventHandler? TargetUnavailable;

    public BorderOverlay(IntPtr target, Color color)
    {
        _target = target;
        BackColor = color;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        _locationChangedCallback = OnLocationChanged;
        _timer.Tick += (_, _) => UpdateTarget();
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= ExTransparent | ExToolWindow | ExNoActivate;
            return cp;
        }
    }

    public bool Start()
    {
        Native.GetWindowThreadProcessId(_target, out var processId);
        _winEventHook = Native.SetWinEventHook(
            Native.EVENT_OBJECT_LOCATIONCHANGE,
            Native.EVENT_OBJECT_LOCATIONCHANGE,
            IntPtr.Zero,
            _locationChangedCallback,
            processId,
            0,
            Native.WINEVENT_OUTOFCONTEXT);

        if (_winEventHook == IntPtr.Zero) _timer.Interval = 16;
        _timer.Start();
        UpdateTarget();
        return _winEventHook != IntPtr.Zero;
    }

    private void OnLocationChanged(
        IntPtr hook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint eventThread, uint eventTime)
    {
        if (hwnd == _target && idObject == 0 && idChild == 0) UpdateTarget();
    }

    private void UpdateTarget()
    {
        if (!Native.IsWindow(_target) ||
            (Native.GetWindowLong(_target, Native.GWL_EXSTYLE) & Native.WS_EX_TOPMOST) == 0)
        {
            _timer.Stop();
            Hide();
            TargetUnavailable?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (Native.IsIconic(_target) || !Native.IsWindowVisible(_target) ||
            !Native.GetWindowRect(_target, out var rect))
        {
            Hide();
            return;
        }

        var bounds = Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
        var sizeChanged = !_regionInitialized || Size != bounds.Size;
        if (Bounds != bounds) Bounds = bounds;
        if (sizeChanged)
        {
            UpdateBorderRegion();
            _regionInitialized = true;
        }
        if (!Visible) Show();
    }

    private void UpdateBorderRegion()
    {
        Region? old = Region;
        if (Width > Thickness * 2 && Height > Thickness * 2)
        {
            var outer = new Rectangle(0, 0, Width, Height);
            var region = new Region(outer);
            region.Exclude(Rectangle.Inflate(outer, -Thickness, -Thickness));
            Region = region;
        }
        else
        {
            Region = null;
        }
        old?.Dispose();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
            if (_winEventHook != IntPtr.Zero)
            {
                Native.UnhookWinEvent(_winEventHook);
                _winEventHook = IntPtr.Zero;
            }
        }
        base.Dispose(disposing);
    }
}
