using System;

namespace Dagobert;

internal sealed class PinchSchedule
{
    public bool Enabled { get; private set; }
    public bool Running { get; private set; }
    public string? PauseReason { get; private set; }
    public long NextDue { get; private set; }
    private long interval;
    private long randomExtra;
    private readonly Func<long, long> sampleDelay;

    public PinchSchedule(Func<long, long>? sampleDelay = null)
    {
        this.sampleDelay = sampleDelay ?? (maximum => Random.Shared.NextInt64(maximum + 1));
    }

    private long NextDelay() => interval + (randomExtra == 0 ? 0 : Math.Clamp(sampleDelay(randomExtra), 0, randomExtra));

    public void Configure(bool enabled, int minutes, long now, int randomExtraMinutes = 0)
    {
        var nextInterval = Math.Clamp(minutes, 1, 10080) * 60000L;
        var nextRandomExtra = Math.Clamp(randomExtraMinutes, 0, 1440) * 60000L;
        var changed = Enabled != enabled || interval != nextInterval || randomExtra != nextRandomExtra;
        interval = nextInterval;
        randomExtra = nextRandomExtra;
        if (changed)
        {
            NextDue = now + NextDelay();
            if (Enabled != enabled) PauseReason = null;
        }
        Enabled = enabled;
    }

    public bool IsDue(long now) => Enabled && !Running && PauseReason == null && now >= NextDue;
    public void Start() => Running = true;
    public void Complete(long now) { Running = false; NextDue = now + NextDelay(); }
    public void Pause(string reason) { Running = false; PauseReason = reason; }
    public void Resume(long now) { PauseReason = null; NextDue = now + NextDelay(); }
    public void RunNow(long now) { PauseReason = null; NextDue = now; }
}
