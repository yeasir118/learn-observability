using System.ComponentModel.DataAnnotations;
using System.Globalization;
using TinyShop.ServiceDefaults.Abstractions.Faults;

namespace TinyShop.PaymentService.Application.Payments;

/// <summary>
/// Configuration section "Faults": payment-service's fault switches (TS-016, docs/faults.md).
/// All off by default. Set through environment variables, e.g. Faults__DeclineRate=0.2.
/// </summary>
public sealed class PaymentFaultOptions : IFaultSettings, IValidatableObject
{
    public const string SectionName = "Faults";

    /// <summary>
    /// Added to <see cref="PaymentProcessingOptions.ProcessingDelay"/>, the simulated provider's
    /// normal latency (Failure Exercise 2). Zero = off.
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:00", "00:01:00")]
    public TimeSpan Delay { get; set; }

    /// <summary>
    /// Share of payments (0 to 1) declined with failure_reason "card_declined": a normal response,
    /// so the order becomes FAILED and the caller still gets 201. 0 = off.
    /// </summary>
    [Range(0.0, 1.0)]
    public double DeclineRate { get; set; }

    /// <summary>
    /// Share of payments (0 to 1) that fail with a technical error: an exception, so the caller gets
    /// gRPC INTERNAL and order-service answers 502 payment_unavailable. 0 = off.
    /// </summary>
    [Range(0.0, 1.0)]
    public double ErrorRate { get; set; }

    /// <summary>When set, every fault above applies only to this user's payments.</summary>
    [Range(typeof(long), "1", "9223372036854775807")]
    public long? UserId { get; set; }

    public bool AppliesTo(long userId) => UserId is null || UserId == userId;

    /// <summary>
    /// One random number decides between error, decline and approval, so the two rates are shares
    /// of the same payments and can't add up to more than all of them.
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (DeclineRate + ErrorRate > 1)
        {
            yield return new ValidationResult(
                "Faults:DeclineRate + Faults:ErrorRate may not exceed 1.",
                [nameof(DeclineRate), nameof(ErrorRate)]);
        }
    }

    public IEnumerable<string> DescribeActive()
    {
        var scope = UserId is { } userId
            ? string.Create(CultureInfo.InvariantCulture, $"user {userId} only")
            : "every user";

        if (Delay > TimeSpan.Zero)
        {
            yield return string.Create(CultureInfo.InvariantCulture,
                $"Faults:Delay={Delay}: every payment takes {Delay.TotalMilliseconds} ms longer ({scope})");
        }

        if (DeclineRate > 0)
        {
            yield return string.Create(CultureInfo.InvariantCulture,
                $"Faults:DeclineRate={DeclineRate}: {DeclineRate:P0} of payments are declined ({scope})");
        }

        if (ErrorRate > 0)
        {
            yield return string.Create(CultureInfo.InvariantCulture,
                $"Faults:ErrorRate={ErrorRate}: {ErrorRate:P0} of payments fail with an error ({scope})");
        }
    }
}
