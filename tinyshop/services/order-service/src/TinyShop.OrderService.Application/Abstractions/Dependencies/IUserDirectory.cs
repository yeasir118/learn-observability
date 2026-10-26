using TinyShop.OrderService.Application.Models;

namespace TinyShop.OrderService.Application.Abstractions.Dependencies;

/// <summary>
/// Looks users up in user-service. Declared here, implemented in Infrastructure (over HTTP).
/// </summary>
/// <remarks>
/// Implementations translate transport failures into application exceptions, so the use case
/// never sees an HTTP status code: <see cref="Common.Exceptions.DependencyUnavailableException"/>
/// (user-service failed or couldn't be reached) or
/// <see cref="Common.Exceptions.DependencyTimeoutException"/> (it didn't answer in time).
/// </remarks>
public interface IUserDirectory
{
    /// <summary>Returns the user, or <c>null</c> if user-service says it doesn't exist.</summary>
    Task<UserSummary?> FindAsync(long userId, CancellationToken cancellationToken);
}
