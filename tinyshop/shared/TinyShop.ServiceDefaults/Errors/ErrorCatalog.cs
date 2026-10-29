namespace TinyShop.ServiceDefaults.Errors;

/// <summary>
/// The error codes one service can return: their titles, and which code a framework-generated
/// response with a given status gets. Starts with the codes every service shares
/// (<see cref="ErrorCodes"/>); each service adds its own at startup through
/// <see cref="ProblemDetailsServiceCollectionExtensions.AddDefaultProblemDetails"/>.
/// </summary>
public sealed class ErrorCatalog
{
    private readonly Dictionary<string, string> _titles = new(StringComparer.Ordinal)
    {
        [ErrorCodes.ValidationFailed] = "Validation failed",
        [ErrorCodes.NotFound] = "Resource not found",
        [ErrorCodes.MethodNotAllowed] = "Method not allowed",
        [ErrorCodes.NotAcceptable] = "Not acceptable",
        [ErrorCodes.UnsupportedMediaType] = "Unsupported media type",
        [ErrorCodes.UnsupportedApiVersion] = "Unsupported API version",
        [ErrorCodes.ServiceUnavailable] = "Service unavailable",
        [ErrorCodes.InternalError] = "Internal error",
    };

    // The code for a response the framework generated itself (no code was chosen explicitly).
    // A status not listed here gets internal_error.
    private readonly Dictionary<int, string> _codesByStatus = new()
    {
        [400] = ErrorCodes.ValidationFailed,
        [404] = ErrorCodes.NotFound,
        [405] = ErrorCodes.MethodNotAllowed,
        [406] = ErrorCodes.NotAcceptable,
        [415] = ErrorCodes.UnsupportedMediaType,
        [503] = ErrorCodes.ServiceUnavailable,
    };

    /// <summary>
    /// Adds a code only this service returns. <paramref name="forStatus"/> also makes it the code
    /// for framework-generated responses with that status (order-service: 502 is dependency_failed).
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The code or the status already has an entry. Redefining a shared code would make one
    /// service's errors differ from all the others, so it fails at startup instead.
    /// </exception>
    public ErrorCatalog Add(string code, string title, int? forStatus = null)
    {
        if (!_titles.TryAdd(code, title))
        {
            throw new InvalidOperationException($"Error code '{code}' is already in the catalog.");
        }

        if (forStatus is { } status && !_codesByStatus.TryAdd(status, code))
        {
            throw new InvalidOperationException(
                $"Status {status} already maps to error code '{_codesByStatus[status]}'.");
        }

        return this;
    }

    public string TitleFor(string code) =>
        _titles.TryGetValue(code, out var title) ? title : _titles[ErrorCodes.InternalError];

    public string CodeForStatus(int? statusCode) =>
        statusCode is { } status && _codesByStatus.TryGetValue(status, out var code)
            ? code
            : ErrorCodes.InternalError;
}
