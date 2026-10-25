using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using TinyShop.UserService.Infrastructure.Persistence;

namespace TinyShop.UserService.Api.DesignTime;

/// <summary>
/// Used only by design-time tools ("dotnet ef migrations add", "dotnet ef database update",
/// "dotnet ef migrations bundle"), never at run time.
/// </summary>
/// <remarks>
/// Without this, dotnet ef starts the app's host to find the DbContext, which runs
/// AddInfrastructure and its "connection string is required" check, so every dotnet ef command
/// needed the app's configuration, even when "--connection" was passed.
///
/// Where the connection string comes from, highest priority first:
/// <list type="number">
///   <item>"--connection" on the dotnet ef command line (applied by dotnet ef after this returns)</item>
///   <item>the ConnectionStrings__UserDb environment variable</item>
///   <item>the ConnectionStrings:UserDb user secret of this project (local development)</item>
///   <item>a placeholder, enough for commands that never connect (migrations add, migrations bundle)</item>
/// </list>
/// It lives in the Api project because the user secrets belong to it (its UserSecretsId).
/// EF Core finds design-time factories in the startup project as well as the DbContext's project.
/// </remarks>
internal sealed class UserDbContextDesignTimeFactory : IDesignTimeDbContextFactory<UserDbContext>
{
    public UserDbContext CreateDbContext(string[] args)
    {
        // The same sources, in the same order of precedence, that the app's host uses for these values.
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<Program>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("UserDb")
            ?? "Host=localhost;Database=tinyshop_users";

        var options = new DbContextOptionsBuilder<UserDbContext>();
        options.UseUserDatabase(connectionString);

        return new UserDbContext(options.Options);
    }
}
