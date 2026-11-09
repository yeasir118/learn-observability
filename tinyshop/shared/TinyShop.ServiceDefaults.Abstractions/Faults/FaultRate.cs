namespace TinyShop.ServiceDefaults.Abstractions.Faults;

/// <summary>Decides whether a fault with a given rate (0 to 1) happens this time.</summary>
public static class FaultRate
{
    /// <summary>
    /// True with probability <paramref name="rate"/>. A rate of 0 or 1 draws no random number at
    /// all: a switched-off fault costs nothing (TS-016, decision 10), and 1 always hits.
    /// </summary>
    public static bool Hits(double rate, IFaultRandom random) => rate switch
    {
        <= 0 => false,
        >= 1 => true,
        _ => random.NextDouble() < rate,
    };
}
