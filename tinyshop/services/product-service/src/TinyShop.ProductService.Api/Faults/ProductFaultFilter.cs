using System.Globalization;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using TinyShop.ServiceDefaults.Abstractions.Faults;

namespace TinyShop.ProductService.Api.Faults;

/// <summary>
/// product-service's error fault (TS-016, decision 4): before a controller action runs, fails a share
/// of requests by throwing. The exception handler turns that into 500 internal_error and logs it with
/// its stack trace, exactly like a real bug would be (Failure Exercise 1).
/// An action filter rather than a middleware: it only ever sees controller actions, so the health
/// endpoints and unknown routes are never affected (decision 9), and it gets the action's bound
/// arguments (the product ID) without parsing the path. It runs after model validation, so a request
/// that is already a 400 stays a 400.
/// </summary>
internal sealed class ProductFaultFilter(IOptions<ProductFaultOptions> options, IFaultRandom random) : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        var faults = options.Value;
        if (faults.ErrorRate <= 0 || !AppliesTo(context, faults.ProductId))
        {
            return;
        }

        if (FaultRate.Hits(faults.ErrorRate, random))
        {
            var request = context.HttpContext.Request;
            throw new InjectedFaultException(string.Create(
                CultureInfo.InvariantCulture,
                $"Injected fault (Faults:ErrorRate={faults.ErrorRate}): {request.Method} {request.Path} failed on purpose."));
        }
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
    }

    /// <summary>Without a product filter, every action; with one, only the action for that ID.</summary>
    private static bool AppliesTo(ActionExecutingContext context, long? productId) =>
        productId is null
        || (context.ActionArguments.TryGetValue("id", out var id) && id is long requested && requested == productId);
}
