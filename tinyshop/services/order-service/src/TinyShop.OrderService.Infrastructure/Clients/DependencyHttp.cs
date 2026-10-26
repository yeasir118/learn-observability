using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using TinyShop.OrderService.Application.Common.Exceptions;

namespace TinyShop.OrderService.Infrastructure.Clients;

/// <summary>
/// One GET to user-service or product-service, with every outcome translated per contracts.md §6.
/// This is where HTTP stops: callers get a value, <c>null</c>, or an Application exception.
/// </summary>
/// <remarks>
/// <code>
/// 200 with a valid body               -> the value
/// 404 with the expected "code"        -> null (the caller answers 422)
/// 5xx, or 200 with an invalid body    -> DependencyUnavailableException (502)
/// couldn't connect (refused, DNS)     -> DependencyUnavailableException (502)
/// HttpClient.Timeout elapsed          -> DependencyTimeoutException (504)
/// any other status (400, other 404s)  -> InvalidOperationException (500: order-service built a bad request)
/// our own caller went away            -> OperationCanceledException, untouched
/// </code>
/// </remarks>
internal static class DependencyHttp
{
    // camelCase names, as the contract uses (contracts.md §1). Strict numbers: the web defaults
    // would also accept "1" for an ID, which no TinyShop service sends.
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.Strict,
    };

    public static async Task<T?> GetAsync<T>(
        HttpClient httpClient,
        string dependency,
        string relativeUri,
        string notFoundCode,
        Func<T, bool> isValid,
        ILogger logger,
        CancellationToken cancellationToken)
        where T : class
    {
        using var response = await SendAsync(httpClient, dependency, relativeUri, logger, cancellationToken);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var value = await ReadJsonAsync<T>(response.Content, cancellationToken);
            if (value is null || !isValid(value))
            {
                logger.LogWarning("{Dependency} returned an unexpected 200 body for {Uri}", dependency, relativeUri);
                throw new DependencyUnavailableException(dependency, $"{dependency} returned an unexpected response.");
            }

            return value;
        }

        if (response.StatusCode == HttpStatusCode.NotFound
            && await ReadProblemCodeAsync(response.Content, cancellationToken) == notFoundCode)
        {
            return null;
        }

        var status = (int)response.StatusCode;
        if (status >= 500)
        {
            logger.LogWarning("{Dependency} answered {StatusCode} for {Uri}", dependency, status, relativeUri);
            throw new DependencyUnavailableException(dependency, $"{dependency} answered {status}.");
        }

        // Anything else (400, a 404 that isn't "not found" for the resource, such as an unmatched
        // route) means order-service sent a request the dependency couldn't understand: our bug.
        throw new InvalidOperationException(
            $"{dependency} answered {status} to GET {relativeUri}; order-service built an invalid request.");
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient httpClient,
        string dependency,
        string relativeUri,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            // The default completion option buffers the whole body, so a dependency that stalls
            // mid-body is still covered by HttpClient.Timeout.
            return await httpClient.GetAsync(relativeUri, cancellationToken);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Our token wasn't cancelled, so HttpClient's own Timeout fired. (It throws
            // TaskCanceledException with a TimeoutException inside.)
            logger.LogWarning(exception, "{Dependency} timed out for {Uri}", dependency, relativeUri);
            throw new DependencyTimeoutException(dependency, $"{dependency} did not answer in time.", exception);
        }
        catch (HttpRequestException exception)
        {
            // No HTTP response at all: connection refused, DNS failure, connection reset.
            // exception.HttpRequestError says which (ConnectionError, NameResolutionError, ...).
            logger.LogWarning(exception, "Couldn't reach {Dependency} for {Uri} ({Error})",
                dependency, relativeUri, exception.HttpRequestError);
            throw new DependencyUnavailableException(dependency, $"Couldn't reach {dependency}.", exception);
        }
    }

    private static async Task<T?> ReadJsonAsync<T>(HttpContent content, CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            return await content.ReadFromJsonAsync<T>(WebJson, cancellationToken);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The Problem Details "code" of an error response, or <c>null</c> if there isn't one.</summary>
    private static async Task<string?> ReadProblemCodeAsync(HttpContent content, CancellationToken cancellationToken)
    {
        var problem = await ReadJsonAsync<ProblemBody>(content, cancellationToken);
        return problem?.Code;
    }

    private sealed record ProblemBody(string? Code);
}
