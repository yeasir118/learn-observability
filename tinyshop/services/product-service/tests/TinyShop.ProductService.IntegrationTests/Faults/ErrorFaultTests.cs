using System.Net;
using Microsoft.Extensions.Options;
using TinyShop.ProductService.IntegrationTests.Infrastructure;
using static TinyShop.Testing.ResponseAssertions;

namespace TinyShop.ProductService.IntegrationTests.Faults;

/// <summary>
/// The error fault through the whole service (TS-016, AC5-AC7): rates of 0 and 1 only, so every
/// result is certain. Rates in between are tested exactly in the unit tests, with a fixed random.
/// </summary>
public class ErrorFaultTests(PostgresFixture fixture)
{
    private ProductServiceFactory WithFaults(params (string Key, string? Value)[] settings) =>
        new(fixture.AppConnectionString, settings.ToDictionary(s => s.Key, s => s.Value));

    [Theory]
    [InlineData("/products")]
    [InlineData("/products/2")]
    public async Task ErrorRateOne_FailsEveryProductRequest_With500InternalError(string path)
    {
        await using var factory = WithFaults(("Faults:ErrorRate", "1"));

        var response = await factory.CreateClient().GetAsync(path, TestContext.Current.CancellationToken);

        // The service's generic 500: no message from the exception reaches the caller.
        await AssertProblemAsync(response, HttpStatusCode.InternalServerError, "internal_error", path);
    }

    [Theory]
    [InlineData("/products")]
    [InlineData("/products/2")]
    public async Task ErrorRateZero_ChangesNothing(string path)
    {
        // The setting written out explicitly, as Compose passes it ("0"), and an empty product ID,
        // which is what "${PRODUCT_SERVICE_FAULT_PRODUCT_ID:-}" becomes when the variable isn't set.
        await using var factory = WithFaults(("Faults:ErrorRate", "0"), ("Faults:ProductId", ""));

        var response = await factory.CreateClient().GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/products/1", HttpStatusCode.InternalServerError)]
    [InlineData("/products/2", HttpStatusCode.OK)]
    [InlineData("/products", HttpStatusCode.OK)]
    public async Task WithProductId_OnlyThatProductFails(string path, HttpStatusCode expected)
    {
        await using var factory = WithFaults(("Faults:ErrorRate", "1"), ("Faults:ProductId", "1"));

        var response = await factory.CreateClient().GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData("/products/abc", HttpStatusCode.BadRequest)]   // model validation runs first
    [InlineData("/nope", HttpStatusCode.NotFound)]             // no controller action: no fault
    public async Task ErrorRateOne_LeavesRequestsThatNeverReachAnAction_Unchanged(string path, HttpStatusCode expected)
    {
        await using var factory = WithFaults(("Faults:ErrorRate", "1"));

        var response = await factory.CreateClient().GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task ErrorRateOne_NeverAffectsTheHealthEndpoints(string path)
    {
        await using var factory = WithFaults(("Faults:ErrorRate", "1"));

        var response = await factory.CreateClient().GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("Faults:ErrorRate", "2", "ErrorRate")]
    [InlineData("Faults:ErrorRate", "-0.1", "ErrorRate")]
    [InlineData("Faults:ProductId", "0", "ProductId")]
    public async Task InvalidSetting_StopsTheServiceAtStartup_NamingTheSetting(string key, string value, string setting)
    {
        await using var factory = WithFaults((key, value));

        var exception = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());

        Assert.Contains(setting, exception.Message);
    }

    [Fact]
    public async Task UnparsableSetting_StopsTheServiceAtStartup_NamingTheSetting()
    {
        await using var factory = WithFaults(("Faults:ErrorRate", "abc"));

        var exception = Assert.ThrowsAny<InvalidOperationException>(() => factory.CreateClient());

        Assert.Contains("ErrorRate", exception.Message);
    }
}
