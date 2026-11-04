namespace TinyShop.Gateway.IntegrationTests.Infrastructure;

/// <summary>
/// Base class for tests that need all three upstreams: a fresh gateway and three fake services
/// (product, user, order) for every test, so no test sees another test's requests.
/// </summary>
public abstract class GatewayWithUpstreams : IAsyncLifetime
{
    private GatewayFactory _gateway = null!;

    protected FakeUpstream ProductService { get; private set; } = null!;

    protected FakeUpstream UserService { get; private set; } = null!;

    protected FakeUpstream OrderService { get; private set; } = null!;

    protected HttpClient Client { get; private set; } = null!;

    /// <summary>The upstream with the given name, for tests parameterized by route.</summary>
    protected FakeUpstream Upstream(string name) => name switch
    {
        "product-service" => ProductService,
        "user-service" => UserService,
        "order-service" => OrderService,
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown upstream."),
    };

    /// <summary>Asserts that no upstream received anything: the gateway answered by itself.</summary>
    protected void AssertNoUpstreamWasCalled()
    {
        Assert.Null(ProductService.LastRequest);
        Assert.Null(UserService.LastRequest);
        Assert.Null(OrderService.LastRequest);
    }

    public async ValueTask InitializeAsync()
    {
        ProductService = await FakeUpstream.StartAsync();
        UserService = await FakeUpstream.StartAsync();
        OrderService = await FakeUpstream.StartAsync();
        _gateway = new GatewayFactory
        {
            ProductServiceUrl = ProductService.BaseUrl,
            UserServiceUrl = UserService.BaseUrl,
            OrderServiceUrl = OrderService.BaseUrl,
        };
        Client = _gateway.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _gateway.DisposeAsync();
        await ProductService.DisposeAsync();
        await UserService.DisposeAsync();
        await OrderService.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
