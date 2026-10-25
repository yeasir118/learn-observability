using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TinyShop.PaymentService.Application.Payments;

namespace TinyShop.PaymentService.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the Application layer's services. Implementations stay <c>internal</c>; only their
    /// interfaces are visible to other projects.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<PaymentProcessingOptions>()
            .Bind(configuration.GetSection(PaymentProcessingOptions.SectionName))
            .ValidateDataAnnotations()
            // Fail at startup on a bad setting, rather than on the first payment.
            .ValidateOnStart();

        // The real clock, unless something (a test) registered another one first.
        services.TryAddSingleton(TimeProvider.System);

        services.AddScoped<IPaymentProcessor, PaymentProcessor>();

        return services;
    }
}
