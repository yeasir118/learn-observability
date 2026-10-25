using Microsoft.EntityFrameworkCore;

namespace TinyShop.UserService.Infrastructure.Persistence;

public static class UserDatabaseOptions
{
    /// <summary>
    /// How UserDbContext talks to PostgreSQL. The one place this is defined, used by the app
    /// (DependencyInjection), by design-time tools (the Api project's UserDbContextDesignTimeFactory) and by the
    /// integration tests, so all three always map the model the same way.
    /// </summary>
    public static DbContextOptionsBuilder UseUserDatabase(
        this DbContextOptionsBuilder options,
        string connectionString) =>
        options
            .UseNpgsql(connectionString)
            // Table and column names in snake_case (users, id, name).
            .UseSnakeCaseNamingConvention();
}
