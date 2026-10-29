using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using TinyShop.PaymentService.Api.Services;

namespace TinyShop.PaymentService.Api.Extensions;

internal static class WebApplicationExtensions
{
    /// <summary>Maps the gRPC services and health endpoints.</summary>
    public static WebApplication UseApi(this WebApplication app)
    {
        app.MapGrpcService<PaymentGrpcService>();

        // grpc.health.v1.Health/Check and /Watch.
        app.MapGrpcHealthChecksService();

        // Reflection lets tools such as grpcurl list and call the service without the .proto file.
        // It describes the whole API to anyone who can connect, so it's on only where configured
        // (appsettings.Development.json), the usual practice for production services.
        if (app.Configuration.GetValue<bool>(ServiceCollectionExtensions.EnableReflectionKey))
        {
            app.MapGrpcReflectionService();
        }

        // The same /health/live and /health/ready as the HTTP services (contracts.md §5.4), served
        // over HTTP/2 like everything else on this port. Same rule as TinyShop.ServiceDefaults'
        // MapDefaultHealthEndpoints, written out here because this gRPC service doesn't reference
        // that HTTP library (TS-024): liveness runs only checks tagged "live" (none), readiness runs
        // every check (today just "self"). So a dependency check added later can make the service
        // not ready, but never gets it restarted.
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("live"),
        });
        app.MapHealthChecks("/health/ready");

        return app;
    }
}
