using Microsoft.EntityFrameworkCore;
using TinyShop.UserService.Application.Abstractions.Persistence;
using TinyShop.UserService.Domain.Entities;

namespace TinyShop.UserService.Infrastructure.Persistence.Repositories;

internal sealed class UserRepository(UserDbContext dbContext) : IUserRepository
{
    public Task<User?> GetByIdAsync(long id, CancellationToken cancellationToken) =>
        DatabaseExceptionTranslator.ExecuteAsync(() =>
            dbContext.Users
                // Read-only query: skip change tracking.
                .AsNoTracking()
                .SingleOrDefaultAsync(user => user.Id == id, cancellationToken));
}
