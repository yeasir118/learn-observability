using TinyShop.ServiceDefaults.Errors;

namespace TinyShop.UserService.Api.Errors;

/// <summary>
/// The error codes only user-service returns (contracts.md §2.3). The codes every service
/// shares (validation_failed, not_found, ...) are in TinyShop.ServiceDefaults' <see cref="ErrorCodes"/>.
/// </summary>
internal static class UserErrorCodes
{
    public const string UserNotFound = "user_not_found";

    /// <summary>Adds this service's codes to its error catalog (passed to AddDefaultProblemDetails).</summary>
    public static void Register(ErrorCatalog catalog) =>
        catalog.Add(UserNotFound, "User not found");
}
