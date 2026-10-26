namespace TinyShop.OrderService.Api.Errors;

/// <summary>
/// The error codes this service can return (contracts.md §2.3) and the Problem Details
/// <c>type</c> and <c>title</c> that go with each one.
/// </summary>
public static class ErrorCatalog
{
    public const string ValidationFailed = "validation_failed";
    public const string UserNotFound = "user_not_found";
    public const string ProductNotFound = "product_not_found";
    public const string OrderNotFound = "order_not_found";
    public const string DependencyFailed = "dependency_failed";
    public const string DependencyTimeout = "dependency_timeout";
    public const string PaymentUnavailable = "payment_unavailable";
    public const string PaymentTimeout = "payment_timeout";
    public const string NotFound = "not_found";
    public const string MethodNotAllowed = "method_not_allowed";
    public const string NotAcceptable = "not_acceptable";
    public const string UnsupportedMediaType = "unsupported_media_type";
    public const string UnsupportedApiVersion = "unsupported_api_version";
    public const string ServiceUnavailable = "service_unavailable";
    public const string InternalError = "internal_error";

    private const string TypeBaseUri = "https://tinyshop.example/problems/";

    private static readonly Dictionary<string, string> Titles = new()
    {
        [ValidationFailed] = "Validation failed",
        [UserNotFound] = "User not found",
        [ProductNotFound] = "Product not found",
        [OrderNotFound] = "Order not found",
        [DependencyFailed] = "Dependency failed",
        [DependencyTimeout] = "Dependency timeout",
        [PaymentUnavailable] = "Payment unavailable",
        [PaymentTimeout] = "Payment timeout",
        [NotFound] = "Resource not found",
        [MethodNotAllowed] = "Method not allowed",
        [NotAcceptable] = "Not acceptable",
        [UnsupportedMediaType] = "Unsupported media type",
        [UnsupportedApiVersion] = "Unsupported API version",
        [ServiceUnavailable] = "Service unavailable",
        [InternalError] = "Internal error",
    };

    /// <summary>
    /// The code for a response the framework generated itself (no code was chosen explicitly).
    /// </summary>
    public static string CodeForStatus(int? statusCode) => statusCode switch
    {
        400 => ValidationFailed,
        404 => NotFound,
        405 => MethodNotAllowed,
        406 => NotAcceptable,
        415 => UnsupportedMediaType,
        502 => DependencyFailed,
        503 => ServiceUnavailable,
        504 => DependencyTimeout,
        _ => InternalError,
    };

    public static string TypeUri(string code) => TypeBaseUri + code;

    /// <summary>Returns the code if <paramref name="type"/> is one of this catalog's type URIs.</summary>
    public static string? CodeFromTypeUri(string? type) =>
        type is not null && type.StartsWith(TypeBaseUri, StringComparison.Ordinal)
            ? type[TypeBaseUri.Length..]
            : null;

    public static string TitleFor(string code) =>
        Titles.TryGetValue(code, out var title) ? title : Titles[InternalError];
}
