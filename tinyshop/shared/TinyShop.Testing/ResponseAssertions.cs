using System.Net;
using System.Text.Json.Nodes;
using Xunit;

namespace TinyShop.Testing;

/// <summary>
/// Assertions on the HTTP response as it's actually sent: raw status, headers and JSON text.
/// Tests never deserialize into the service's own C# types; that would hide wrong field names
/// or casing, because the same serializer settings would be used to read them back.
/// </summary>
public static class ResponseAssertions
{
    /// <summary>
    /// Asserts the body is exactly <paramref name="expectedJson"/>: same property names
    /// (including casing), same values, same array order. Whitespace doesn't matter.
    /// </summary>
    public static async Task AssertJsonBodyAsync(HttpResponseMessage response, string expectedJson)
    {
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        var actualJson = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var expected = JsonNode.Parse(expectedJson);
        var actual = JsonNode.Parse(actualJson);

        Assert.True(
            JsonNode.DeepEquals(expected, actual),
            $"JSON body differs.\nExpected: {expected?.ToJsonString()}\nActual:   {actual?.ToJsonString()}");
    }

    /// <summary>
    /// Asserts the response is a Problem Details error that follows contracts.md §2.1, and returns
    /// the parsed body for further checks.
    /// </summary>
    public static async Task<JsonObject> AssertProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedCode,
        string expectedInstance)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var problem = JsonNode.Parse(body)!.AsObject();

        Assert.Equal($"https://tinyshop.example/problems/{expectedCode}", (string?)problem["type"]);
        Assert.False(string.IsNullOrWhiteSpace((string?)problem["title"]));
        Assert.Equal((int)expectedStatus, (int?)problem["status"]);
        Assert.Equal(expectedCode, (string?)problem["code"]);
        Assert.Equal(expectedInstance, (string?)problem["instance"]);
        Assert.False(string.IsNullOrWhiteSpace((string?)problem["traceId"]));

        // Never leak internals to clients (contracts.md §2.1).
        Assert.DoesNotContain("Exception", body, StringComparison.Ordinal);
        Assert.DoesNotContain("   at ", body, StringComparison.Ordinal);
        Assert.DoesNotContain("localhost", body, StringComparison.OrdinalIgnoreCase);

        return problem;
    }
}
