using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using TinyShop.OrderService.IntegrationTests.Infrastructure;
using static TinyShop.OrderService.IntegrationTests.Support.ResponseAssertions;

namespace TinyShop.OrderService.IntegrationTests.Orders;

/// <summary>
/// POST /orders end to end (contracts.md §5.3): every row of the dependency-failure translation
/// table (§6), triggered through the fake dependencies, plus request validation.
/// </summary>
public partial class PlaceOrderTests(PostgresFixture fixture)
{
    private readonly HttpClient _client = fixture.Factory.CreateClient();

    // ---- Happy path and business outcomes --------------------------------------------------

    [Fact]
    public async Task PaymentApproved_Returns201WithLocation_AndThePaidOrderWithASnapshot()
    {
        var userId = Scenarios.NewUserId();

        var response = await _client.PostOrderAsync(userId, Scenarios.Product.Keyboard, 2);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var order = await OrderApi.ReadObjectAsync(response);
        var id = (long)order["id"]!;
        Assert.True(id >= 1001, $"Order IDs start at 1001, got {id}.");
        // Relative, so no internal host name leaks (contracts.md §2.4).
        Assert.Equal($"/orders/{id}", response.Headers.Location?.OriginalString);

        await AssertJsonBodyAsync(response, $$"""
            {
              "id": {{id}},
              "userId": {{userId}},
              "productId": 1,
              "productName": "Keyboard",
              "unitPriceCents": 10000,
              "quantity": 2,
              "totalCents": 20000,
              "status": "PAID",
              "failureReason": null,
              "transactionId": "txn_{{id}}",
              "createdAt": "{{(string)order["createdAt"]!}}",
              "updatedAt": "{{(string)order["updatedAt"]!}}"
            }
            """);
        AssertTimestampFormat(order);

        // payment-service was asked for the total, for this order and user.
        var charged = fixture.Factory.Dependencies.PaymentRequests[id];
        Assert.Equal((id, userId, 20_000L), (charged.OrderId, charged.UserId, charged.AmountCents));

        // What was stored is what was returned.
        Assert.True(JsonNode.DeepEquals(order, await _client.GetOrderAsync(id)));
    }

    [Fact]
    public async Task PaymentDeclined_Returns201WithAFailedOrderAndTheReason()
    {
        var response = await _client.PostOrderAsync(Scenarios.Payment.Declined, 2, 1);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var order = await OrderApi.ReadObjectAsync(response);
        Assert.Equal("FAILED", (string?)order["status"]);
        Assert.Equal("insufficient_funds", (string?)order["failureReason"]);
        Assert.Null(order["transactionId"]);
        Assert.Equal(Scenarios.Product.DefaultPriceCents, (long)order["totalCents"]!);
    }

    [Fact]
    public async Task PaymentDeclinedWithoutAReason_StillRecordsOne()
    {
        var response = await _client.PostOrderAsync(Scenarios.Payment.DeclineReason, 2, 1);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var order = await OrderApi.ReadObjectAsync(response);
        Assert.Equal("FAILED", (string?)order["status"]);
        Assert.Equal("declined", (string?)order["failureReason"]);
    }

    [Fact]
    public async Task UnknownRequestFields_AreIgnored()
    {
        var response = await PostRawAsync("""{ "userId": 123, "productId": 1, "quantity": 1, "coupon": "FREE" }""");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // ---- user-service rows -----------------------------------------------------------------

    [Fact]
    public async Task UserNotFound_Returns422UserNotFound_AndCreatesNoOrder()
    {
        var response = await _client.PostOrderAsync(Scenarios.User.NotFound, 1, 1);

        var problem = await AssertProblemAsync(response, (HttpStatusCode)422, "user_not_found", "/orders");
        Assert.Equal($"User {Scenarios.User.NotFound} does not exist.", (string?)problem["detail"]);
        Assert.Empty(await _client.ListOrdersAsync(Scenarios.User.NotFound));
    }

    [Theory]
    [InlineData(Scenarios.User.BadRequest, HttpStatusCode.InternalServerError, "internal_error")]
    [InlineData(Scenarios.User.ServerError, HttpStatusCode.BadGateway, "dependency_failed")]
    [InlineData(Scenarios.User.Unreachable, HttpStatusCode.BadGateway, "dependency_failed")]
    [InlineData(Scenarios.User.Timeout, HttpStatusCode.GatewayTimeout, "dependency_timeout")]
    public async Task UserServiceFailure_IsTranslated_AndCreatesNoOrder(long userId, HttpStatusCode status, string code)
    {
        var response = await _client.PostOrderAsync(userId, 1, 1);

        var problem = await AssertProblemAsync(response, status, code, "/orders");
        AssertNoInternalNames(problem);
        Assert.Null(problem["orderId"]);
        Assert.Empty(await _client.ListOrdersAsync(userId));
    }

    // ---- product-service rows --------------------------------------------------------------

    [Fact]
    public async Task ProductNotFound_Returns422ProductNotFound_AndCreatesNoOrder()
    {
        var userId = Scenarios.NewUserId();

        var response = await _client.PostOrderAsync(userId, Scenarios.Product.NotFound, 1);

        var problem = await AssertProblemAsync(response, (HttpStatusCode)422, "product_not_found", "/orders");
        Assert.Equal($"Product {Scenarios.Product.NotFound} does not exist.", (string?)problem["detail"]);
        Assert.Empty(await _client.ListOrdersAsync(userId));
    }

    [Theory]
    [InlineData(Scenarios.Product.BadRequest, HttpStatusCode.InternalServerError, "internal_error")]
    [InlineData(Scenarios.Product.ServerError, HttpStatusCode.BadGateway, "dependency_failed")]
    [InlineData(Scenarios.Product.Unreachable, HttpStatusCode.BadGateway, "dependency_failed")]
    [InlineData(Scenarios.Product.InvalidBody, HttpStatusCode.BadGateway, "dependency_failed")]
    [InlineData(Scenarios.Product.Timeout, HttpStatusCode.GatewayTimeout, "dependency_timeout")]
    public async Task ProductServiceFailure_IsTranslated_AndCreatesNoOrder(long productId, HttpStatusCode status, string code)
    {
        var userId = Scenarios.NewUserId();

        var response = await _client.PostOrderAsync(userId, productId, 1);

        var problem = await AssertProblemAsync(response, status, code, "/orders");
        AssertNoInternalNames(problem);
        Assert.Null(problem["orderId"]);
        Assert.Empty(await _client.ListOrdersAsync(userId));
    }

    // ---- payment-service rows --------------------------------------------------------------

    [Theory]
    [InlineData(Scenarios.Payment.Unavailable)]
    [InlineData(Scenarios.Payment.Internal)]
    [InlineData(Scenarios.Payment.InvalidArgument)]
    public async Task PaymentServiceFailure_Returns502PaymentUnavailableWithOrderId_AndTheOrderIsFailed(long userId)
    {
        var response = await _client.PostOrderAsync(userId, 1, 1);

        var problem = await AssertProblemAsync(response, HttpStatusCode.BadGateway, "payment_unavailable", "/orders");
        AssertNoInternalNames(problem);
        var order = await _client.GetOrderAsync((long)problem["orderId"]!);
        Assert.Equal("FAILED", (string?)order["status"]);
        Assert.Equal("payment_unavailable", (string?)order["failureReason"]);
        Assert.Null(order["transactionId"]);
    }

    [Fact]
    public async Task PaymentDeadlineExceeded_Returns504PaymentTimeoutWithOrderId_AndTheOrderStaysPending()
    {
        var response = await _client.PostOrderAsync(Scenarios.Payment.DeadlineExceeded, 1, 1);

        var problem = await AssertProblemAsync(response, HttpStatusCode.GatewayTimeout, "payment_timeout", "/orders");
        AssertNoInternalNames(problem);
        var order = await _client.GetOrderAsync((long)problem["orderId"]!);
        Assert.Equal("PENDING", (string?)order["status"]);
        Assert.Null(order["failureReason"]);
        Assert.Null(order["transactionId"]);
        Assert.Equal((string?)order["createdAt"], (string?)order["updatedAt"]);
    }

    // ---- Request validation ----------------------------------------------------------------

    [Theory]
    [InlineData("""{ "productId": 1, "quantity": 1 }""", "userId", "Is required.")]
    [InlineData("""{ "userId": 123, "quantity": 1 }""", "productId", "Is required.")]
    [InlineData("""{ "userId": 123, "productId": 1 }""", "quantity", "Is required.")]
    [InlineData("""{ "userId": 0, "productId": 1, "quantity": 1 }""", "userId", "Must be greater than 0.")]
    [InlineData("""{ "userId": 123, "productId": -1, "quantity": 1 }""", "productId", "Must be greater than 0.")]
    [InlineData("""{ "userId": 123, "productId": 1, "quantity": 0 }""", "quantity", "Must be between 1 and 100.")]
    [InlineData("""{ "userId": 123, "productId": 1, "quantity": 101 }""", "quantity", "Must be between 1 and 100.")]
    public async Task InvalidField_Returns400WithTheFieldInErrors(string body, string field, string message)
    {
        var response = await PostRawAsync(body);

        var problem = await AssertProblemAsync(response, HttpStatusCode.BadRequest, "validation_failed", "/orders");
        var errors = problem["errors"]!.AsObject();
        Assert.Equal([field], errors.Select(error => error.Key));
        Assert.Equal(message, (string?)errors[field]![0]);
    }

    [Theory]
    [InlineData("""{ "userId": "abc", "productId": 1, "quantity": 1 }""")]
    [InlineData("""{ "userId": 123, "productId": 1, "quantity": 1.5 }""")]
    [InlineData("""{ "userId": 123, "productId": 1, """)]
    [InlineData("not json")]
    [InlineData("")]
    public async Task MalformedBody_Returns400ValidationFailed_WithoutParserInternals(string body)
    {
        var response = await PostRawAsync(body);

        var problem = await AssertProblemAsync(response, HttpStatusCode.BadRequest, "validation_failed", "/orders");
        Assert.NotNull(problem["errors"]);
        Assert.DoesNotContain("System.", problem.ToJsonString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task NonJsonBody_Returns415UnsupportedMediaType()
    {
        using var content = new StringContent("userId=123&productId=1&quantity=1", Encoding.UTF8, "application/x-www-form-urlencoded");

        var response = await _client.PostAsync("/orders", content, TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.UnsupportedMediaType, "unsupported_media_type", "/orders");
    }

    private Task<HttpResponseMessage> PostRawAsync(string json) =>
        _client.PostAsync("/orders", new StringContent(json, Encoding.UTF8, "application/json"), TestContext.Current.CancellationToken);

    /// <summary>Clients may learn that a dependency failed, never which one (contracts.md §2.1, §2.3).</summary>
    private static void AssertNoInternalNames(JsonObject problem)
    {
        var json = problem.ToJsonString();
        foreach (var name in new[] { "user-service", "product-service", "payment-service", ".test" })
        {
            Assert.DoesNotContain(name, json, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>contracts.md §1.3: "2026-10-18T14:30:05.123Z".</summary>
    private static void AssertTimestampFormat(JsonObject order)
    {
        Assert.Matches(TimestampPattern(), (string?)order["createdAt"]);
        Assert.Matches(TimestampPattern(), (string?)order["updatedAt"]);
    }

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z$")]
    private static partial Regex TimestampPattern();
}
