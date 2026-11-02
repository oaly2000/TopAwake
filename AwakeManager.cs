namespace TopAwake;

internal enum AwakeMode
{
    Off,
    Indefinite,
    Timed
}

internal sealed class AwakeManager : IDisposable
{
    private System.Threading.Timer? _timer;

    public AwakeMode Mode { get; private set; } = AwakeMode.Off;
    public bool KeepScreenOn { get; private set; } = true;
    public int TimedMinutes { get; private set; } = 30;
    public DateTimeOffset? EndsAt { get; private set; }

    public event EventHandler? Changed;

    public void SetMode(AwakeMode mode, int timedMinutes = 30)
    {
        Mode = mode;
        _timer?.Dispose();
        _timer = null;
        EndsAt = null;

        switch (mode)
        {
            case AwakeMode.Off:
                Native.SetThreadExecutionState(Native.ES_CONTINUOUS);
                break;
            case AwakeMode.Indefinite:
                ApplyExecutionState();
                break;
            case AwakeMode.Timed:
                TimedMinutes = timedMinutes;
                EndsAt = DateTimeOffset.Now.AddMinutes(timedMinutes);
                ApplyExecutionState();
                _timer = new System.Threading.Timer(
                    _ => SetMode(AwakeMode.Off),
                    null,
                    TimeSpan.FromMinutes(timedMinutes),
                    Timeout.InfiniteTimeSpan);
                break;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SetKeepScreenOn(bool value)
    {
        KeepScreenOn = value;
        if (Mode != AwakeMode.Off) ApplyExecutionState();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyExecutionState()
    {
        var flags = Native.ES_CONTINUOUS | Native.ES_SYSTEM_REQUIRED;
        if (KeepScreenOn) flags |= Native.ES_DISPLAY_REQUIRED;
        Native.SetThreadExecutionState(flags);
    }

    public void Dispose() => _timer?.Dispose();
}
