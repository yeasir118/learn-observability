using TinyShop.UserService.Domain.Entities;

namespace TinyShop.UserService.Application.Abstractions.Persistence;

/// <summary>
/// Read access to stored users. Declared here, implemented in Infrastructure: Application decides
/// what it needs, and doesn't know or care that the implementation uses EF Core and PostgreSQL.
/// </summary>
/// <remarks>
/// Implementations throw <see cref="Common.Exceptions.DataStoreUnavailableException"/> when the
/// data store can't be reached.
/// </remarks>
public interface IUserRepository
{
    /// <summary>Returns the user with the given ID, or <c>null</c> if there is none.</summary>
    Task<User?> GetByIdAsync(long id, CancellationToken cancellationToken);
}
