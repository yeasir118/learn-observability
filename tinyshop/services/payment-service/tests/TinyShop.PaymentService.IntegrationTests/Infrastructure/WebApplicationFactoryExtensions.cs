using Grpc.Net.Client;
using Microsoft.AspNetCore.Mvc.Testing;

namespace TinyShop.PaymentService.IntegrationTests.Infrastructure;

internal static class WebApplicationFactoryExtensions
{
    /// <summary>A gRPC channel over any factory's in-memory test server (including WithWebHostBuilder copies).</summary>
    public static GrpcChannel CreateGrpcChannel(this WebApplicationFactory<Program> factory) =>
        GrpcChannel.ForAddress(factory.Server.BaseAddress, new GrpcChannelOptions
        {
            HttpHandler = factory.Server.CreateHandler(),
        });
}
