using System.ComponentModel.DataAnnotations;
using System.Globalization;
using TinyShop.ServiceDefaults.Abstractions.Faults;

namespace TinyShop.ServiceDefaults.Data.Faults;

/// <summary>
/// Configuration section "Faults": the database-delay switch, the same in every service with a
/// database (TS-016, decision 6; docs/faults.md). Off by default. Set through an environment
/// variable, e.g. Faults__DatabaseDelay=00:00:02.
/// </summary>
public sealed class DatabaseFaultOptions : IFaultSettings
{
    public const string SectionName = "Faults";

    /// <summary>
    /// How much longer every database command of this service takes. Postgres itself waits
    /// (<see cref="DatabaseDelayInterceptor"/>), so the time is spent inside the database call.
    /// Zero = off. At most 25 s: Npgsql cancels a command after 30 s (its default command timeout),
    /// and since the wait runs after the real statement, the cut-off wait would make the request
    /// succeed after an arbitrary 30 s instead (found in TS-016's experiments).
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:00", "00:00:25")]
    public TimeSpan DatabaseDelay { get; set; }

    public IEnumerable<string> DescribeActive()
    {
        if (DatabaseDelay > TimeSpan.Zero)
        {
            yield return string.Create(CultureInfo.InvariantCulture,
                $"Faults:DatabaseDelay={DatabaseDelay}: every database command waits {DatabaseDelay.TotalMilliseconds} ms in Postgres");
        }
    }
}
