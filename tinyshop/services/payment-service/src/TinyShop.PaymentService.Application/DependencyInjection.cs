using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using TinyShop.PaymentService.Application.Payments;
using TinyShop.ServiceDefaults.Abstractions.Faults;

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

        // The fault switches (TS-016, docs/faults.md). All off unless configured; a bad value stops
        // the service at startup. Registered as IFaultSettings so the Api logs active ones.
        services.AddOptions<PaymentFaultOptions>()
            .Bind(configuration.GetSection(PaymentFaultOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<IFaultSettings>(provider =>
            provider.GetRequiredService<IOptions<PaymentFaultOptions>>().Value);

        // The real clock and randomness, unless something (a test) registered others first.
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IFaultRandom, SharedFaultRandom>();

        services.AddScoped<IPaymentProcessor, PaymentProcessor>();

        return services;
    }
}
