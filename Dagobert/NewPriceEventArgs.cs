using System;

namespace Dagobert;

internal sealed class NewPriceEventArgs(int newPrice, PriceIdentity identity, long generation) : EventArgs
{
    public int NewPrice { get; } = newPrice;
    public PriceIdentity Identity { get; } = identity;
    public long Generation { get; } = generation;
}
