namespace TopAwake;

internal sealed class HotkeyWindow : Form
{
    public const int HotkeyId = 0x5450;

    public event EventHandler? HotkeyPressed;

    public HotkeyWindow()
    {
        ShowInTaskbar = false;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Location = new Point(-32000, -32000);
        Size = new Size(1, 1);
        Opacity = 0;
    }

    public bool SetHotkeyEnabled(bool enabled)
    {
        if (enabled)
        {
            return Native.RegisterHotKey(
                Handle, HotkeyId,
                Native.MOD_WIN | Native.MOD_CONTROL | Native.MOD_NOREPEAT,
                Native.VK_T);
        }

        Native.UnregisterHotKey(Handle, HotkeyId);
        return true;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == Native.WM_HOTKEY && m.WParam.ToInt32() == HotkeyId)
        {
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
        }
        base.WndProc(ref m);
    }
}
