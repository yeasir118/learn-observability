using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace TinyShop.OrderService.IntegrationTests.Orders;

/// <summary>Small helpers for calling the orders endpoints the way a client would.</summary>
internal static class OrderApi
{
    public static Task<HttpResponseMessage> PostOrderAsync(this HttpClient client, long userId, long productId, int quantity) =>
        client.PostAsJsonAsync("/orders", new { userId, productId, quantity }, TestContext.Current.CancellationToken);

    public static async Task<JsonObject> GetOrderAsync(this HttpClient client, long id)
    {
        var response = await client.GetAsync($"/orders/{id}", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadObjectAsync(response);
    }

    public static async Task<JsonArray> ListOrdersAsync(this HttpClient client, long userId)
    {
        var response = await client.GetAsync($"/orders?userId={userId}", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        return (await ReadObjectAsync(response))["items"]!.AsArray();
    }

    public static async Task<JsonObject> ReadObjectAsync(HttpResponseMessage response) =>
        JsonNode.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!.AsObject();
}
