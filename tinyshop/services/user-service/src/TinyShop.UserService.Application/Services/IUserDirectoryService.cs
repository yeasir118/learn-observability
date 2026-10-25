using TinyShop.UserService.Application.Models;

namespace TinyShop.UserService.Application.Services;

/// <summary>User lookups, as called by the Api layer.</summary>
/// <remarks>
/// Not named "UserService": inside the TinyShop.UserService namespace, that name would resolve to
/// the namespace instead of the class (error CS0118), the same reason product-service's service is
/// called ProductCatalogService.
/// </remarks>
public interface IUserDirectoryService
{
    /// <summary>Returns the user with the given ID, or <c>null</c> if there is none.</summary>
    Task<UserDto?> GetByIdAsync(long id, CancellationToken cancellationToken);
}
