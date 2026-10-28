namespace TinyShop.ServiceDefaults.Errors;

/// <summary>
/// The error codes every TinyShop HTTP service can return (contracts.md §2.3, "any service").
/// Codes only one service returns (<c>product_not_found</c>, <c>payment_timeout</c>, ...) belong to
/// that service, which adds them to its <see cref="ErrorCatalog"/>.
/// </summary>
public static class ErrorCodes
{
    public const string ValidationFailed = "validation_failed";
    public const string NotFound = "not_found";
    public const string MethodNotAllowed = "method_not_allowed";
    public const string NotAcceptable = "not_acceptable";
    public const string UnsupportedMediaType = "unsupported_media_type";
    public const string UnsupportedApiVersion = "unsupported_api_version";
    public const string ServiceUnavailable = "service_unavailable";
    public const string InternalError = "internal_error";
}
