using System.ComponentModel.DataAnnotations;
using System.Globalization;
using TinyShop.ServiceDefaults.Abstractions.Faults;

namespace TinyShop.ProductService.Api.Faults;

/// <summary>
/// Configuration section "Faults": product-service's fault switches (TS-016, docs/faults.md).
/// All off by default. Set through environment variables, e.g. Faults__ErrorRate=0.2.
/// </summary>
public sealed class ProductFaultOptions : IFaultSettings
{
    public const string SectionName = "Faults";

    /// <summary>
    /// Share of product requests (0 to 1) that fail with 500 internal_error, through an
    /// <see cref="InjectedFaultException"/>. 0 = off.
    /// </summary>
    [Range(0.0, 1.0)]
    public double ErrorRate { get; set; }

    /// <summary>
    /// When set, only GET /products/{this ID} can fail; the list and every other product are
    /// unaffected. When not set, every product request can fail.
    /// </summary>
    [Range(typeof(long), "1", "9223372036854775807")]
    public long? ProductId { get; set; }

    public IEnumerable<string> DescribeActive()
    {
        if (ErrorRate > 0)
        {
            var scope = ProductId is { } productId
                ? string.Create(CultureInfo.InvariantCulture, $"GET /products/{productId} only")
                : "every product request";
            yield return string.Create(
                CultureInfo.InvariantCulture,
                $"Faults:ErrorRate={ErrorRate}: {ErrorRate:P0} of product requests fail with 500 ({scope})");
        }
    }
}
