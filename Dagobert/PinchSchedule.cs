using System;

namespace Dagobert;

internal sealed class PinchSchedule
{
    public bool Enabled { get; private set; }
    public bool Running { get; private set; }
    public string? PauseReason { get; private set; }
    public long NextDue { get; private set; }
    private long interval;

    public void Configure(bool enabled, int minutes, long now)
    {
        var nextInterval = Math.Clamp(minutes, 1, 10080) * 60000L;
        if (Enabled != enabled || interval != nextInterval)
        {
            NextDue = now + nextInterval;
            if (Enabled != enabled) PauseReason = null;
        }
        Enabled = enabled;
        interval = nextInterval;
    }

    public bool IsDue(long now) => Enabled && !Running && PauseReason == null && now >= NextDue;
    public void Start() => Running = true;
    public void Complete(long now) { Running = false; NextDue = now + interval; }
    public void Pause(string reason) { Running = false; PauseReason = reason; }
    public void Resume(long now) { PauseReason = null; NextDue = now + interval; }
    public void RunNow(long now) { PauseReason = null; NextDue = now; }
}
