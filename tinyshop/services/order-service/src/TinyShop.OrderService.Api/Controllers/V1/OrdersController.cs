using System.ComponentModel.DataAnnotations;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using TinyShop.OrderService.Api.Contracts;
using TinyShop.OrderService.Api.Errors;
using TinyShop.OrderService.Application.Models;
using TinyShop.OrderService.Application.Services;

namespace TinyShop.OrderService.Api.Controllers.V1;

/// <summary>Placing and looking up orders (contracts.md §5.3).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("orders")]
// No class-level [Produces]: it would force "application/json" onto error responses too,
// which must be "application/problem+json" (contracts.md §1).
public sealed class OrdersController(IOrderPlacement placement, IOrderQueries queries) : ControllerBase
{
    /// <summary>Problem Details extension naming an order that exists despite the error (contracts.md §2.1).</summary>
    private const string OrderIdExtension = "orderId";

    /// <summary>
    /// Creates an order and attempts payment. Returns 201 whether the payment was approved
    /// (status PAID) or declined (status FAILED): a decline is a business outcome, not an error.
    /// </summary>
    /// <param name="request">The user, product and quantity.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType<OrderDto>(StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status415UnsupportedMediaType, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status504GatewayTimeout, "application/problem+json")]
    public async Task<ActionResult<OrderDto>> Place(PlaceOrderRequest request, CancellationToken cancellationToken)
    {
        // [ApiController] has already rejected missing or out-of-range values with a 400.
        var command = new PlaceOrderCommand(request.UserId!.Value, request.ProductId!.Value, request.Quantity!.Value);

        var result = await placement.PlaceAsync(command, cancellationToken);

        return result switch
        {
            // A relative Location (/orders/1001). CreatedAtAction would build an absolute URL from
            // the request's Host header: http://order-service:8080/..., an internal hostname that
            // must never reach clients. The gateway rewrites this path to /api/orders/1001 (contracts.md §2.4).
            PlaceOrderResult.Placed placed => Created($"/orders/{placed.Order.Id}", placed.Order),

            PlaceOrderResult.UserNotFound userNotFound => Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                type: ErrorCatalog.TypeUri(ErrorCatalog.UserNotFound),
                detail: $"User {userNotFound.UserId} does not exist."),

            PlaceOrderResult.ProductNotFound productNotFound => Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                type: ErrorCatalog.TypeUri(ErrorCatalog.ProductNotFound),
                detail: $"Product {productNotFound.ProductId} does not exist."),

            PlaceOrderResult.PaymentUnavailable unavailable => OrderProblem(
                StatusCodes.Status502BadGateway,
                ErrorCatalog.PaymentUnavailable,
                $"Order {unavailable.Order.Id} was created, but payment could not be processed. The order is FAILED.",
                unavailable.Order.Id),

            PlaceOrderResult.PaymentTimedOut timedOut => OrderProblem(
                StatusCodes.Status504GatewayTimeout,
                ErrorCatalog.PaymentTimeout,
                $"Order {timedOut.Order.Id} was created, but the payment did not complete in time. "
                    + "Its outcome is unknown and the order stays PENDING.",
                timedOut.Order.Id),

            PlaceOrderResult.StatusNotRecorded notRecorded => OrderProblem(
                StatusCodes.Status503ServiceUnavailable,
                ErrorCatalog.ServiceUnavailable,
                $"Order {notRecorded.OrderId} was created, but its payment result could not be saved. "
                    + "The order stays PENDING.",
                notRecorded.OrderId),

            _ => throw new InvalidOperationException($"Unhandled order placement result {result.GetType().Name}."),
        };
    }

    /// <summary>Gets one order. Reads only order-db; calls no other service.</summary>
    /// <param name="id">The order's ID.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    [HttpGet("{id}")]
    [ProducesResponseType<OrderDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<OrderDto>> GetById(long id, CancellationToken cancellationToken)
    {
        var order = await queries.GetByIdAsync(id, cancellationToken);

        if (order is null)
        {
            // Setting our type URI is how a controller picks the error code; the Problem Details
            // customizer fills in the matching title and "code".
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                type: ErrorCatalog.TypeUri(ErrorCatalog.OrderNotFound),
                detail: $"Order {id} does not exist.");
        }

        return Ok(order);
    }

    /// <summary>
    /// Lists a user's orders, newest first. An unknown user simply has no orders: the user isn't
    /// checked with user-service.
    /// </summary>
    /// <param name="userId">The user whose orders to list.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    [HttpGet]
    [ProducesResponseType<CollectionResponse<OrderDto>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<CollectionResponse<OrderDto>>> ListByUser(
        [FromQuery, Required(ErrorMessage = "Is required.")] long? userId,
        CancellationToken cancellationToken)
    {
        var orders = await queries.ListByUserAsync(userId!.Value, cancellationToken);
        return Ok(new CollectionResponse<OrderDto>(orders));
    }

    /// <summary>A Problem Details response for an order that exists despite the error, carrying its ID.</summary>
    private ObjectResult OrderProblem(int statusCode, string code, string detail, long orderId)
    {
        // ProblemDetailsFactory applies the same customization as Problem() (type, title, code,
        // instance, traceId); Problem() itself can't add extension fields.
        var problem = ProblemDetailsFactory.CreateProblemDetails(
            HttpContext, statusCode, type: ErrorCatalog.TypeUri(code), detail: detail);
        problem.Extensions[OrderIdExtension] = orderId;

        return new ObjectResult(problem) { StatusCode = statusCode };
    }
}
