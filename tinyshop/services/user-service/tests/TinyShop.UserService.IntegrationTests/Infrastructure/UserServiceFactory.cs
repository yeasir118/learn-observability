using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace TinyShop.UserService.IntegrationTests.Infrastructure;

/// <summary>
/// Hosts user-service in memory: the real Program.cs, middleware, controllers and EF Core,
/// with an HTTP client that calls it without opening a network port.
/// </summary>
public sealed class UserServiceFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Not "Development": that would load the developer's user secrets (which point at the
        // real database) and enable Swagger. Tests must not depend on whoever runs them.
        builder.UseEnvironment("Testing");

        // UseSetting, not ConfigureAppConfiguration: AddInfrastructure reads the connection string
        // while Program.cs runs. UseSetting values are already in builder.Configuration at that
        // point; ConfigureAppConfiguration callbacks only run later, inside builder.Build().
        builder.UseSetting("ConnectionStrings:UserDb", connectionString);
    }
}
