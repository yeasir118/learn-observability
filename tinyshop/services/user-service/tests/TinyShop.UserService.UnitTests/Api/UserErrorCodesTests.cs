using TinyShop.ServiceDefaults.Errors;
using TinyShop.UserService.Api.Errors;

namespace TinyShop.UserService.UnitTests.Api;

/// <summary>
/// Only what user-service adds to the shared error catalog. The customizer itself is tested
/// once, in TinyShop.ServiceDefaults.UnitTests.
/// </summary>
public class UserErrorCodesTests
{
    [Fact]
    public void Register_AddsUserNotFoundWithItsTitle()
    {
        var catalog = new ErrorCatalog();

        UserErrorCodes.Register(catalog);

        Assert.Equal("User not found", catalog.TitleFor(UserErrorCodes.UserNotFound));
    }
}
