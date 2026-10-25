using Grpc.AspNetCore.HealthChecks;
using TinyShop.PaymentService.Api.Services;

namespace TinyShop.PaymentService.Api.Extensions;

internal static class ServiceCollectionExtensions
{
    /// <summary>Configuration key: expose the gRPC reflection service (see WebApplicationExtensions).</summary>
    public const string EnableReflectionKey = "Grpc:EnableReflection";

    /// <summary>Registers everything the gRPC layer needs.</summary>
    public static IServiceCollection AddApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddGrpc(options =>
        {
            options.Interceptors.Add<ExceptionInterceptor>();
            // Never send exception messages or stack traces to callers (the default, stated explicitly).
            options.EnableDetailedErrors = false;
        });

        // The standard gRPC health protocol (grpc.health.v1), backed by ASP.NET Core health checks.
        // gRPC clients and probes (grpc_health_probe, Kubernetes gRPC probes) understand it.
        // payment-service has no dependencies, so "serving" simply means "the process answers":
        // one "self" check that is always healthy. Without any registered check, the gRPC health
        // service has nothing to aggregate and reports UNKNOWN instead of SERVING.
        services.AddGrpcHealthChecks()
            .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy());
        // By default the gRPC health service answers from a cache filled by a background publisher,
        // whose first run is several seconds after startup; until then every check says UNKNOWN,
        // so a probe right after startup would wrongly see "not serving". Run the checks on each
        // request instead: they're cheap here.
        services.Configure<GrpcHealthChecksOptions>(
            options => options.UseHealthChecksCache = false);

        if (configuration.GetValue<bool>(EnableReflectionKey))
        {
            services.AddGrpcReflection();
        }

        return services;
    }
}
