using Microsoft.Extensions.Logging;
using TinyShop.OrderService.Application.Abstractions.Dependencies;
using TinyShop.OrderService.Application.Models;

namespace TinyShop.OrderService.Infrastructure.Clients;

/// <summary>
/// Typed HttpClient for user-service (contracts.md §5.2). IHttpClientFactory supplies the
/// HttpClient, with its BaseAddress set from configuration (DependencyInjection).
/// </summary>
internal sealed class UserServiceClient(HttpClient httpClient, ILogger<UserServiceClient> logger) : IUserDirectory
{
    public const string DependencyName = "user-service";

    public async Task<UserSummary?> FindAsync(long userId, CancellationToken cancellationToken)
    {
        var user = await DependencyHttp.GetAsync<UserResponse>(
            httpClient,
            DependencyName,
            $"users/{userId}",
            notFoundCode: "user_not_found",
            isValid: body => body.Id == userId && !string.IsNullOrWhiteSpace(body.Name),
            logger,
            cancellationToken);

        return user is null ? null : new UserSummary(user.Id, user.Name!);
    }

    /// <summary>The User resource as user-service sends it (contracts.md §3).</summary>
    private sealed record UserResponse(long Id, string? Name);
}
