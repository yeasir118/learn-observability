using Grpc.Net.Client;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using V1 = TinyShop.Payment.V1;

// One default server for the whole test assembly; tests that need different settings create
// their own factory.
[assembly: AssemblyFixture(typeof(TinyShop.PaymentService.IntegrationTests.Infrastructure.PaymentServiceFactory))]

namespace TinyShop.PaymentService.IntegrationTests.Infrastructure;

/// <summary>
/// Hosts payment-service in memory: the real Program.cs, gRPC services, interceptor and health
/// checks, with a gRPC channel that calls it without opening a network port.
/// </summary>
public class PaymentServiceFactory : WebApplicationFactory<Program>
{
    private readonly IReadOnlyDictionary<string, string?> _settings;

    public PaymentServiceFactory()
        : this(new Dictionary<string, string?>())
    {
    }

    /// <summary>
    /// A factory with extra configuration, for tests that need different settings.
    /// Internal because xUnit requires an assembly fixture to have exactly one public constructor,
    /// the one it calls itself.
    /// </summary>
    internal PaymentServiceFactory(IReadOnlyDictionary<string, string?> settings)
    {
        _settings = settings;
    }

    /// <summary>
    /// A channel whose HTTP handler is the in-memory test server. The address is only used to
    /// build request URLs; nothing goes over the network.
    /// </summary>
    public GrpcChannel CreateChannel() =>
        GrpcChannel.ForAddress(Server.BaseAddress, new GrpcChannelOptions
        {
            HttpHandler = Server.CreateHandler(),
        });

    public V1.PaymentService.PaymentServiceClient CreatePaymentClient() => new(CreateChannel());

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Not "Development": that would turn on reflection and make tests depend on the
        // developer's settings. Tests opt in to anything they need explicitly.
        builder.UseEnvironment("Testing");

        foreach (var (key, value) in _settings)
        {
            builder.UseSetting(key, value);
        }
    }
}
