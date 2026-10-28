namespace TinyShop.ServiceDefaults.Errors;

/// <summary>
/// The Problem Details <c>type</c> URI for each error code (contracts.md §2.1):
/// <c>https://tinyshop.example/problems/{code}</c>.
/// </summary>
/// <remarks>
/// Setting one of these URIs is how a controller or exception handler picks the error code:
/// <c>Problem(type: ProblemType.For(ProductErrorCodes.ProductNotFound))</c>. The Problem Details
/// customizer reads the code back from the URI and fills in the matching title and <c>code</c>.
/// </remarks>
public static class ProblemType
{
    private const string BaseUri = "https://tinyshop.example/problems/";

    public static string For(string code) => BaseUri + code;

    /// <summary>Returns the code if <paramref name="type"/> is one of our type URIs, otherwise null.</summary>
    public static string? CodeOf(string? type) =>
        type is not null && type.StartsWith(BaseUri, StringComparison.Ordinal)
            ? type[BaseUri.Length..]
            : null;
}
