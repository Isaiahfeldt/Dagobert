namespace Dagobert;

internal readonly record struct PriceIdentity(uint ItemId, bool IsHq);

internal sealed class PriceRequestGuard
{
    public long Generation { get; private set; }
    private PriceIdentity? expected;
    private bool accepted;

    public long Begin(PriceIdentity identity)
    {
        Cancel();
        expected = identity;
        return Generation;
    }

    public bool Accept(long generation, PriceIdentity identity)
    {
        if (generation != Generation || expected != identity || identity.ItemId == 0) return false;
        accepted = true;
        return true;
    }

    public bool CanApply(PriceIdentity current) => accepted && current.ItemId != 0 && expected == current;
    public void Cancel() { Generation++; expected = null; accepted = false; }
}
