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
Check("Bell detection accepts title-case display names with lowercase Excel nouns", () =>
{
    Expect(BellSelection.Matches("Summoning Bell", "summoning bell", 0), true);
    Expect(BellSelection.Matches("summoning bell", "Summoning Bell", 0), true);
});
Check("Bell matching works with localized names and harmless whitespace", () =>
{
    Expect(BellSelection.Matches(" SONNETTE ", "sonnette", 0), true);
    Expect(BellSelection.Matches("リテイナーベル", "other", 0), true);
});
Check("Standard bell data ID works without a matching display name", () =>
{
    Expect(BellSelection.Matches("Different display text", "summoning bell", 2000401), true);
});
Check("Non-bell names and blank names are rejected", () =>
{
    Expect(BellSelection.Matches("Market Board", "summoning bell", 123), false);
    Expect(BellSelection.Matches("", "", 0), false);
});
Check("Inn range includes bells beyond the normal-world threshold", () =>
{
    Expect(BellSelection.WithinRange(4.7f, true, false, true), true);
    Expect(BellSelection.WithinRange(4.7f, true, false, false), false);
});
Check("Housing and world bells use their correct interaction boundaries", () =>
{
    Expect(BellSelection.WithinRange(6.5f, true, true, false), true);
    Expect(BellSelection.WithinRange(6.51f, true, true, false), false);
    Expect(BellSelection.WithinRange(4.6f, true, false, false), true);
});
Check("Untargetable bells and invalid distances cannot be selected", () =>
{
    Expect(BellSelection.WithinRange(1, false, false, false), false);
    Expect(BellSelection.WithinRange(float.NaN, true, false, false), false);
    Expect(BellSelection.WithinRange(-1, true, false, false), false);
});
Check("Run Now survives a settings change immediately before the request", () =>
{
    var s = new PinchSchedule(maximum => maximum);
    s.Configure(true, 60, 0, 10);
    s.Configure(true, 30, 100, 5);
    s.RunNow(100);
    s.Configure(true, 30, 101, 5);
    Expect(s.IsDue(101), true);
});
Check("Automatic retainer customization preserves manual selection", () =>
{
    var manual = new HashSet<string>();
    var automatic = RetainerSelection.WithEnabled(manual, ["A", "B"], "A", false);
    Expect(RetainerSelection.IsEnabled(automatic, "A"), false);
    Expect(RetainerSelection.IsEnabled(automatic, "B"), true);
    Expect(RetainerSelection.IsEnabled(manual, "A"), true);
});
Check("Automatic selection can enable a manually excluded retainer", () =>
{
    var manual = new HashSet<string> { "A" };
    var automatic = RetainerSelection.WithEnabled(manual, ["A", "B"], "B", true);
    Expect(RetainerSelection.IsEnabled(automatic, "B"), true);
    Expect(RetainerSelection.IsEnabled(manual, "B"), false);
});
Check("Disabling the final automatic retainer means none, not all", () =>
{
    var automatic = RetainerSelection.WithEnabled([], ["A"], "A", false);
    Expect(RetainerSelection.IsEnabled(automatic, "A"), false);
    Expect(RetainerSelection.IsEnabled(automatic, "New retainer"), false);
});
Check("A retainer can be enabled again after disabling all", () =>
{
    var automatic = RetainerSelection.WithEnabled([RetainerSelection.None], ["A", "B"], "A", true);
    Expect(RetainerSelection.IsEnabled(automatic, "A"), true);
    Expect(RetainerSelection.IsEnabled(automatic, "B"), false);
});
Check("Explicit automatic selections preserve names not currently displayed", () =>
{
    var automatic = RetainerSelection.WithEnabled(["A", "Other character"], ["A", "B"], "A", false);
    Expect(RetainerSelection.IsEnabled(automatic, "Other character"), true);
    Expect(RetainerSelection.IsEnabled(automatic, "A"), false);
});
Check("Price cannot be applied before its response is accepted", () =>
{
    var g = new PriceRequestGuard(); var beam = new PriceIdentity(100, false);
    g.Begin(beam); Expect(g.CanApply(beam), false);
});
Check("An altar price cannot be accepted or applied to a beam", () =>
{
    var g = new PriceRequestGuard(); var beam = new PriceIdentity(100, false); var altar = new PriceIdentity(200, false);
    var token = g.Begin(beam);
    Expect(g.Accept(token, altar), false); Expect(g.CanApply(beam), false);
    Expect(g.Accept(token, beam), true); Expect(g.CanApply(altar), false); Expect(g.CanApply(beam), true);
});
Check("HQ and NQ identities are separate", () =>
{
    var g = new PriceRequestGuard(); var nq = new PriceIdentity(100, false); var hq = new PriceIdentity(100, true);
    var token = g.Begin(hq); Expect(g.Accept(token, nq), false);
    Expect(g.Accept(token, hq), true); Expect(g.CanApply(nq), false);
});
Check("Late responses from the previous item cannot replace the current result", () =>
{
    var g = new PriceRequestGuard(); var a = new PriceIdentity(100, false); var b = new PriceIdentity(200, false);
    var old = g.Begin(a); var current = g.Begin(b);
    Expect(g.Accept(old, a), false); Expect(g.CanApply(b), false);
    Expect(g.Accept(current, b), true); Expect(g.CanApply(b), true);
});
Check("Cancelled runs reject late responses even for the same item", () =>
{
    var g = new PriceRequestGuard(); var item = new PriceIdentity(100, false);
    var old = g.Begin(item); g.Cancel(); var current = g.Begin(item);
    Expect(g.Accept(old, item), false); Expect(g.Accept(current, item), true);
    g.Cancel(); Expect(g.CanApply(item), false);
});
Check("Unidentified items cannot authorize price changes", () =>
{
    var g = new PriceRequestGuard(); var item = new PriceIdentity(0, false);
    var token = g.Begin(item); Expect(g.Accept(token, item), false); Expect(g.CanApply(item), false);
});
Console.WriteLine($"{passed} checks passed, including price identity validation.");
