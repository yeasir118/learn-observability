using System.ComponentModel.DataAnnotations;

namespace TinyShop.PaymentService.Application.Payments;

/// <summary>Configuration section "Payment".</summary>
public sealed class PaymentProcessingOptions
{
    public const string SectionName = "Payment";

    /// <summary>
    /// How long a simulated payment takes, standing in for a real payment provider's response time.
    /// Default 50 ms (TS-012). Failure Exercise 2 raises it to make payments slow.
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:00", "00:01:00")]
    public TimeSpan ProcessingDelay { get; set; } = TimeSpan.FromMilliseconds(50);
}
