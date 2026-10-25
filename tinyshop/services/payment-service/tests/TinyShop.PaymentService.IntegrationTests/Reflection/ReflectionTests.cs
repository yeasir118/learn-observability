using Grpc.Core;
using Grpc.Reflection.V1Alpha;
using TinyShop.PaymentService.IntegrationTests.Infrastructure;

namespace TinyShop.PaymentService.IntegrationTests.Reflection;

/// <summary>
/// gRPC server reflection is what lets grpcurl list and call services without the .proto file.
/// It describes the whole API to anyone who can connect, so it's only on where configured.
/// </summary>
public class ReflectionTests
{
    [Fact]
    public async Task WhenEnabled_ListsThePaymentAndHealthServices()
    {
        await using var factory = new PaymentServiceFactory(new Dictionary<string, string?>
        {
            ["Grpc:EnableReflection"] = "true",
        });

        var services = await ListServicesAsync(factory);

        Assert.Contains("tinyshop.payment.v1.PaymentService", services);
        Assert.Contains("grpc.health.v1.Health", services);
    }

    [Fact]
    public async Task WhenNotEnabled_IsUnimplemented()
    {
        await using var factory = new PaymentServiceFactory();

        var exception = await Assert.ThrowsAsync<RpcException>(() => ListServicesAsync(factory));

        Assert.Equal(StatusCode.Unimplemented, exception.StatusCode);
    }

    /// <summary>
    /// Reflection is a bidirectional stream: send "list services", read the answer. This is what
    /// "grpcurl list" does under the hood.
    /// </summary>
    private static async Task<List<string>> ListServicesAsync(PaymentServiceFactory factory)
    {
        var client = new ServerReflection.ServerReflectionClient(factory.CreateChannel());
        using var call = client.ServerReflectionInfo(cancellationToken: TestContext.Current.CancellationToken);

        await call.RequestStream.WriteAsync(new ServerReflectionRequest { ListServices = string.Empty });
        await call.RequestStream.CompleteAsync();

        Assert.True(await call.ResponseStream.MoveNext(TestContext.Current.CancellationToken));
        return [.. call.ResponseStream.Current.ListServicesResponse.Service.Select(service => service.Name)];
    }
}
