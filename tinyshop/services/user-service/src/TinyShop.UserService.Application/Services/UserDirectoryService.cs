using Microsoft.Extensions.Logging;
using TinyShop.UserService.Application.Abstractions.Persistence;
using TinyShop.UserService.Application.Mappings;
using TinyShop.UserService.Application.Models;

namespace TinyShop.UserService.Application.Services;

internal sealed class UserDirectoryService(IUserRepository repository, ILogger<UserDirectoryService> logger)
    : IUserDirectoryService
{
    public async Task<UserDto?> GetByIdAsync(long id, CancellationToken cancellationToken)
    {
        logger.LogInformation("Fetching user {UserId}", id);

        var user = await repository.GetByIdAsync(id, cancellationToken);
        if (user is null)
        {
            logger.LogInformation("User {UserId} not found", id);
        }

        return user?.ToDto();
    }
}
