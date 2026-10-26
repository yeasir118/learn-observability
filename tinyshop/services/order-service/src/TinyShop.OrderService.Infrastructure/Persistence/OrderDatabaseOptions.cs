using Microsoft.EntityFrameworkCore;

namespace TinyShop.OrderService.Infrastructure.Persistence;

public static class OrderDatabaseOptions
{
    /// <summary>
    /// How OrderDbContext talks to PostgreSQL. The one place this is defined, used by the app
    /// (DependencyInjection), by design-time tools (the Api project's OrderDbContextDesignTimeFactory) and by the
    /// integration tests, so all three always map the model the same way.
    /// </summary>
    public static DbContextOptionsBuilder UseOrderDatabase(
        this DbContextOptionsBuilder options,
        string connectionString) =>
        options
            .UseNpgsql(connectionString)
            // Table and column names in snake_case (orders, user_id, unit_price_cents).
            .UseSnakeCaseNamingConvention();
}
