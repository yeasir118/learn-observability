namespace TinyShop.ServiceDefaults.Abstractions.Faults;

/// <summary>
/// A service's fault switches, as registered in DI. At startup the service logs a Warning for every
/// active fault (TS-016, decision 8), so one left on by accident doesn't go unnoticed.
/// </summary>
public interface IFaultSettings
{
    /// <summary>One line per fault that is on, naming its settings; nothing when all are off.</summary>
    IEnumerable<string> DescribeActive();
}
