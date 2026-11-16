namespace TinyShop.ServiceDefaults.Abstractions.Faults;

/// <summary>
/// The random numbers behind fault rates (TS-016, decision 7). Production uses
/// <see cref="SharedFaultRandom"/>; a test replaces it with a fixed sequence, so "20 % of requests
/// fail" is checked exactly instead of statistically.
/// </summary>
public interface IFaultRandom
{
    /// <summary>A number in [0, 1), like <see cref="Random.NextDouble"/>.</summary>
    double NextDouble();
}

/// <summary><see cref="IFaultRandom"/> backed by <see cref="Random.Shared"/>, which is thread-safe.</summary>
public sealed class SharedFaultRandom : IFaultRandom
{
    public double NextDouble() => Random.Shared.NextDouble();
}
