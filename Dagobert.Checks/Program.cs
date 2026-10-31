using Dagobert;

var passed = 0;
void Check(string name, Action test)
{
    test();
    passed++;
    Console.WriteLine($"PASS {name}");
}
void Expect(bool result, bool expected)
{
    if (result != expected) throw new Exception($"Expected {expected}, got {result}");
}
PinchSchedule Schedule()
{
    var s = new PinchSchedule(); s.Configure(true, 1, 0); return s;
}
Check("Disabled by default", () => Expect(new PinchSchedule().IsDue(long.MaxValue), false));
Check("First run waits the interval", () =>
{
    var s = Schedule(); Expect(s.IsDue(59999), false); Expect(s.IsDue(60000), true);
});
Check("No overlapping scheduled runs", () =>
{
    var s = Schedule(); s.Start(); Expect(s.IsDue(999999), false);
});
Check("Interval begins at completion, not start", () =>
{
    var s = Schedule(); s.Start(); s.Complete(90000);
    Expect(s.IsDue(149999), false); Expect(s.IsDue(150000), true);
});
Check("Missed periods produce one run, without catch-up bursts", () =>
{
    var s = Schedule(); Expect(s.IsDue(3600000), true); s.Start(); s.Complete(3600001);
    Expect(s.IsDue(3600002), false); Expect(s.IsDue(3660001), true);
});
Check("Cancel pauses until resumed", () =>
{
    var s = Schedule(); s.Start(); s.Pause("Cancelled");
    Expect(s.IsDue(999999), false); Expect(s.Running, false);
    s.Resume(1000000); Expect(s.IsDue(1059999), false); Expect(s.IsDue(1060000), true);
});
Check("Run now resumes a paused schedule", () =>
{
    var s = Schedule(); s.Pause("Timeout"); s.RunNow(100); Expect(s.IsDue(100), true);
});
Check("Repeated configuration does not reset the countdown", () =>
{
    var s = Schedule(); s.Configure(true, 1, 59999); Expect(s.IsDue(60000), true);
});
Check("Changing interval restarts countdown while preserving pause", () =>
{
    var s = Schedule(); s.Pause("Error"); s.Configure(true, 120, 100);
    Expect(s.IsDue(7200100), false); Expect(s.PauseReason == "Error", true);
});
Check("Turning off/on clears pause and waits a new interval", () =>
{
    var s = Schedule(); s.Pause("Error"); s.Configure(false, 1, 100); s.Configure(true, 1, 200);
    Expect(s.IsDue(60199), false); Expect(s.IsDue(60200), true);
});
Check("Minimum interval is one minute", () =>
{
    var s = new PinchSchedule(); s.Configure(true, -1, 0);
    Expect(s.IsDue(59999), false); Expect(s.IsDue(60000), true);
});
Check("Maximum interval is one week without integer overflow", () =>
{
    var s = new PinchSchedule(); s.Configure(true, int.MaxValue, 0);
    Expect(s.IsDue(604799999), false); Expect(s.IsDue(604800000), true);
});
Check("Disable prevents due runs and Run Now does not override it", () =>
{
    var s = Schedule(); s.Configure(false, 1, 100); s.RunNow(100); Expect(s.IsDue(100), false);
});
Check("Long-running completion uses the latest configured interval", () =>
{
    var s = Schedule(); s.Start(); s.Configure(true, 60, 100); s.Complete(1000);
    Expect(s.IsDue(3600999), false); Expect(s.IsDue(3601000), true);
});
Check("Zero randomness preserves exact intervals without sampling", () =>
{
    var s = new PinchSchedule(_ => throw new Exception("Unexpected random sample"));
    s.Configure(true, 1, 0, 0); Expect(s.IsDue(60000), true);
    s.Complete(100); Expect(s.NextDue == 60100, true);
});
Check("Random delay includes zero and the configured maximum", () =>
{
    var s = new PinchSchedule(_ => 0); s.Configure(true, 60, 0, 10);
    Expect(s.NextDue == 3600000, true);
    s = new PinchSchedule(maximum => maximum); s.Configure(true, 60, 0, 10);
    Expect(s.NextDue == 4200000, true);
});
Check("Each completed cycle samples once, not each frame", () =>
{
    int samples = 0;
    var s = new PinchSchedule(_ => ++samples * 1000);
    s.Configure(true, 1, 0, 1); Expect(s.NextDue == 61000, true);
    s.Configure(true, 1, 100, 1); Expect(samples == 1, true);
    s.Start(); s.Complete(100000); Expect(samples == 2 && s.NextDue == 162000, true);
});
Check("Resume samples a fresh delay and Run Now remains immediate", () =>
{
    var s = new PinchSchedule(maximum => maximum); s.Configure(true, 1, 0, 1);
    s.Pause("User"); s.Resume(100); Expect(s.NextDue == 120100, true);
    s.RunNow(101); Expect(s.IsDue(101), true);
});
Check("Changing random maximum replans once and preserves pause", () =>
{
    int samples = 0;
    var s = new PinchSchedule(maximum => { samples++; return maximum; });
    s.Configure(true, 1, 0, 1); s.Pause("User"); s.Configure(true, 1, 10, 2);
    Expect(samples == 2 && s.NextDue == 180010, true);
    Expect(s.PauseReason == "User", true);
});
Check("Random maximum is clamped and total delay avoids overflow", () =>
{
    var s = new PinchSchedule(maximum => maximum); s.Configure(true, int.MaxValue, 0, int.MaxValue);
    Expect(s.NextDue == 691200000, true);
    s.Configure(true, 1, 0, -1); Expect(s.NextDue == 60000, true);
});
Console.WriteLine($"{passed} scheduling checks passed.");
