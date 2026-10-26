using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using TinyShop.OrderService.Application.Common.Exceptions;
using TinyShop.OrderService.Application.Models;
using TinyShop.OrderService.Infrastructure.Clients;

namespace TinyShop.OrderService.UnitTests.Infrastructure;

/// <summary>
/// The user-service and product-service rows of contracts.md §6, at the HTTP level: what each
/// response, or missing response, becomes. Both clients share DependencyHttp, so most cases run
/// against both.
/// </summary>
public class HttpDependencyClientTests
{
    public enum Dependency
    {
        User,
        Product,
    }

    [Fact]
    public async Task User200_ReturnsTheUser_FromTheRightPath()
    {
        var handler = Respond(HttpStatusCode.OK, """{ "id": 123, "name": "Alice" }""");

        var user = await new UserServiceClient(Client(handler), NullLogger<UserServiceClient>.Instance)
            .FindAsync(123, TestContext.Current.CancellationToken);

        Assert.Equal(new UserSummary(123, "Alice"), user);
        Assert.Equal("http://user-service:8080/users/123", Assert.Single(handler.RequestedUris).ToString());
    }

    [Fact]
    public async Task Product200_ReturnsTheProduct_FromTheRightPath()
    {
        var handler = Respond(HttpStatusCode.OK, """{ "id": 1, "name": "Keyboard", "priceCents": 10000 }""");

        var product = await new ProductServiceClient(Client(handler), NullLogger<ProductServiceClient>.Instance)
            .FindAsync(1, TestContext.Current.CancellationToken);

        Assert.Equal(new ProductSummary(1, "Keyboard", 10_000), product);
        Assert.Equal("http://user-service:8080/products/1", Assert.Single(handler.RequestedUris).ToString());
    }

    [Theory]
    [InlineData(Dependency.User, "user_not_found")]
    [InlineData(Dependency.Product, "product_not_found")]
    public async Task NotFoundWithTheExpectedCode_ReturnsNull(Dependency dependency, string code)
    {
        var handler = Respond(HttpStatusCode.NotFound, Problem(404, code), "application/problem+json");

        Assert.Null(await FindAsync(dependency, handler));
    }

    [Theory]
    [InlineData(Dependency.User)]
    [InlineData(Dependency.Product)]
    public async Task NotFoundForAnUnmatchedRoute_IsOurBug(Dependency dependency)
    {
        // A 404 "not_found" means the URL matched no route: order-service built the wrong URL.
        // It mustn't be mistaken for "no such user/product" (which would be a 422 for the client).
        var handler = Respond(HttpStatusCode.NotFound, Problem(404, "not_found"), "application/problem+json");

        await Assert.ThrowsAsync<InvalidOperationException>(() => FindAsync(dependency, handler));
    }

    [Theory]
    [InlineData(Dependency.User, HttpStatusCode.BadRequest)]
    [InlineData(Dependency.User, HttpStatusCode.Conflict)]
    [InlineData(Dependency.Product, HttpStatusCode.BadRequest)]
    [InlineData(Dependency.Product, HttpStatusCode.MethodNotAllowed)]
    public async Task Other4xx_IsOurBug(Dependency dependency, HttpStatusCode status)
    {
        var handler = Respond(status, Problem((int)status, "validation_failed"), "application/problem+json");

        await Assert.ThrowsAsync<InvalidOperationException>(() => FindAsync(dependency, handler));
    }

    [Theory]
    [InlineData(Dependency.User, HttpStatusCode.InternalServerError)]
    [InlineData(Dependency.User, HttpStatusCode.ServiceUnavailable)]
    [InlineData(Dependency.Product, HttpStatusCode.InternalServerError)]
    [InlineData(Dependency.Product, HttpStatusCode.BadGateway)]
    public async Task ServerError_IsDependencyUnavailable(Dependency dependency, HttpStatusCode status)
    {
        var handler = Respond(status, Problem((int)status, "internal_error"), "application/problem+json");

        var exception = await Assert.ThrowsAsync<DependencyUnavailableException>(() => FindAsync(dependency, handler));
        Assert.Equal(NameOf(dependency), exception.Dependency);
    }

    [Theory]
    [InlineData(Dependency.User, HttpRequestError.ConnectionError, SocketError.ConnectionRefused)]
    [InlineData(Dependency.User, HttpRequestError.NameResolutionError, SocketError.HostNotFound)]
    [InlineData(Dependency.Product, HttpRequestError.ConnectionError, SocketError.ConnectionRefused)]
    [InlineData(Dependency.Product, HttpRequestError.NameResolutionError, SocketError.HostNotFound)]
    public async Task CouldNotConnect_IsDependencyUnavailable(Dependency dependency, HttpRequestError error, SocketError socketError)
    {
        // What SocketsHttpHandler throws when the connection is refused or the name doesn't resolve.
        var handler = new StubHttpMessageHandler((_, _) =>
            throw new HttpRequestException(error, "Couldn't connect.", new SocketException((int)socketError)));

        var exception = await Assert.ThrowsAsync<DependencyUnavailableException>(() => FindAsync(dependency, handler));
        Assert.IsType<HttpRequestException>(exception.InnerException);
    }

    [Theory]
    [InlineData(Dependency.User)]
    [InlineData(Dependency.Product)]
    public async Task HttpClientTimeout_IsDependencyTimeout(Dependency dependency)
    {
        // The dependency never answers; HttpClient.Timeout is what ends the wait.
        var handler = new StubHttpMessageHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new UnreachableException();
        });

        var exception = await Assert.ThrowsAsync<DependencyTimeoutException>(
            () => FindAsync(dependency, handler, timeout: TimeSpan.FromMilliseconds(50)));
        Assert.IsType<TaskCanceledException>(exception.InnerException);
    }

    [Theory]
    [InlineData(Dependency.User)]
    [InlineData(Dependency.Product)]
    public async Task CallerCancelled_IsNotATimeout(Dependency dependency)
    {
        // Our own client went away: that's a cancellation, not the dependency's fault.
        using var cancellation = new CancellationTokenSource();
        var handler = new StubHttpMessageHandler(async (_, cancellationToken) =>
        {
            await cancellation.CancelAsync();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new UnreachableException();
        });

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => FindAsync(dependency, handler, cancellationToken: cancellation.Token));
        Assert.IsNotType<DependencyTimeoutException>(exception);
    }

    [Theory]
    [InlineData(Dependency.User, "not json")]
    [InlineData(Dependency.User, "null")]
    [InlineData(Dependency.User, """{ "id": 999, "name": "Someone else" }""")]
    [InlineData(Dependency.User, """{ "id": 1, "name": "" }""")]
    [InlineData(Dependency.Product, """{ "id": 1, "name": "Keyboard" }""")]
    [InlineData(Dependency.Product, """{ "id": 1, "name": "Keyboard", "priceCents": 0 }""")]
    [InlineData(Dependency.Product, """{ "id": 1, "name": null, "priceCents": 100 }""")]
    [InlineData(Dependency.Product, """{ "id": "1", "name": "Keyboard", "priceCents": 100 }""")]
    public async Task UnexpectedOkBody_IsDependencyUnavailable(Dependency dependency, string body)
    {
        var handler = Respond(HttpStatusCode.OK, body);

        await Assert.ThrowsAsync<DependencyUnavailableException>(() => FindAsync(dependency, handler));
    }

    [Fact]
    public async Task ProductNameTooLongToSnapshot_IsDependencyUnavailable()
    {
        var handler = Respond(HttpStatusCode.OK, $$"""{ "id": 1, "name": "{{new string('x', 101)}}", "priceCents": 100 }""");

        await Assert.ThrowsAsync<DependencyUnavailableException>(() => FindAsync(Dependency.Product, handler));
    }

    private static async Task<object?> FindAsync(
        Dependency dependency,
        StubHttpMessageHandler handler,
        TimeSpan? timeout = null,
        CancellationToken? cancellationToken = null)
    {
        var client = Client(handler, timeout);
        var token = cancellationToken ?? TestContext.Current.CancellationToken;

        return dependency == Dependency.User
            ? await new UserServiceClient(client, NullLogger<UserServiceClient>.Instance).FindAsync(1, token)
            : await new ProductServiceClient(client, NullLogger<ProductServiceClient>.Instance).FindAsync(1, token);
    }

    private static string NameOf(Dependency dependency) =>
        dependency == Dependency.User ? UserServiceClient.DependencyName : ProductServiceClient.DependencyName;

    private static HttpClient Client(StubHttpMessageHandler handler, TimeSpan? timeout = null) =>
        new(handler)
        {
            BaseAddress = new Uri("http://user-service:8080/"),
            Timeout = timeout ?? TimeSpan.FromSeconds(30),
        };

    private static StubHttpMessageHandler Respond(HttpStatusCode status, string body, string mediaType = "application/json") =>
        new((_, _) => Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, mediaType),
        }));

    private static string Problem(int status, string code) =>
        $$"""{ "type": "https://tinyshop.example/problems/{{code}}", "status": {{status}}, "code": "{{code}}" }""";
}
