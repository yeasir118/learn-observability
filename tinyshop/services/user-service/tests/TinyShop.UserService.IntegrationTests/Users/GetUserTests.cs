using System.Net;
using TinyShop.UserService.IntegrationTests.Infrastructure;
using static TinyShop.UserService.IntegrationTests.Support.ResponseAssertions;

namespace TinyShop.UserService.IntegrationTests.Users;

/// <summary>GET /users/{id} (contracts.md §5.2).</summary>
public class GetUserTests(PostgresFixture fixture)
{
    private readonly HttpClient _client = fixture.Factory.CreateClient();

    [Theory]
    [InlineData(123, "Alice")]
    [InlineData(124, "Bob")]
    [InlineData(127, "Erin")]
    public async Task GetById_WhenUserExists_ReturnsIt(long id, string name)
    {
        var response = await _client.GetAsync($"/users/{id}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await AssertJsonBodyAsync(response, $$"""{ "id": {{id}}, "name": "{{name}}" }""");
    }

    [Theory]
    [InlineData("/users/999")]
    [InlineData("/users/0")]
    [InlineData("/users/-1")]
    [InlineData("/users/122")] // just below the seeded range
    public async Task GetById_WhenUserDoesNotExist_Returns404UserNotFound(string path)
    {
        var response = await _client.GetAsync(path, TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "user_not_found", path);
    }

    [Theory]
    [InlineData("/users/abc")]
    [InlineData("/users/1.5")]
    [InlineData("/users/99999999999999999999")] // doesn't fit in a 64-bit integer
    public async Task GetById_WhenIdIsNotAValidInteger_Returns400ValidationFailed(string path)
    {
        var response = await _client.GetAsync(path, TestContext.Current.CancellationToken);

        var problem = await AssertProblemAsync(response, HttpStatusCode.BadRequest, "validation_failed", path);

        // Validation errors name the offending field (contracts.md §2.1).
        Assert.NotNull(problem["errors"]?["id"]);
    }

    [Fact]
    public async Task ListingUsers_IsNotPartOfTheContract()
    {
        // contracts.md §5.2 defines only GET /users/{id}. There's deliberately no way to list
        // every user (it would expose all names), so GET /users is an unknown route.
        var response = await _client.GetAsync("/users", TestContext.Current.CancellationToken);

        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed });
    }
}
