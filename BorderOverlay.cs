namespace TopAwake;

// 置顶切换时在目标窗口四周闪烁的中空边框，不抢焦点、不可点击
internal sealed class BorderOverlay : Form
{
    private const int Thickness = 6;
    private const int ExTransparent = 0x00000020;
    private const int ExToolWindow = 0x00000080;
    private const int ExNoActivate = 0x08000000;

    private readonly System.Windows.Forms.Timer _timer = new();

    public BorderOverlay()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        _timer.Tick += (_, _) => { _timer.Stop(); Hide(); };
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

    public void Flash(Native.RECT rect, Color color, int milliseconds = 500)
    {
        BackColor = color;
        Bounds = new Rectangle(
            rect.Left, rect.Top,
            Math.Max(1, rect.Right - rect.Left),
            Math.Max(1, rect.Bottom - rect.Top));

        Region? old = Region;
        if (Width > Thickness * 2 + 2 && Height > Thickness * 2 + 2)
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

        if (!Visible) Show();
        _timer.Stop();
        _timer.Interval = milliseconds;
        _timer.Start();
    }
}
