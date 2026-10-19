using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using TinyShop.ProductService.Api.Contracts;
using TinyShop.ProductService.Api.Errors;
using TinyShop.ProductService.Application.Models;
using TinyShop.ProductService.Application.Services;

namespace TinyShop.ProductService.Api.Controllers.V1;

/// <summary>Product catalogue (contracts.md §5.1).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("products")]
// No class-level [Produces]: it would force "application/json" onto error responses too,
// which must be "application/problem+json" (contracts.md §1).
public sealed class ProductsController(IProductCatalogService catalog) : ControllerBase
{
    /// <summary>Lists all products, ordered by ID.</summary>
    [HttpGet]
    [ProducesResponseType<CollectionResponse<ProductDto>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<CollectionResponse<ProductDto>>> GetAll(CancellationToken cancellationToken)
    {
        var products = await catalog.GetAllAsync(cancellationToken);
        return Ok(new CollectionResponse<ProductDto>(products));
    }

    /// <summary>Gets one product.</summary>
    /// <param name="id">The product's ID.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    [HttpGet("{id}")]
    [ProducesResponseType<ProductDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<ProductDto>> GetById(long id, CancellationToken cancellationToken)
    {
        var product = await catalog.GetByIdAsync(id, cancellationToken);

        if (product is null)
        {
            // Setting our type URI is how a controller picks the error code; the Problem Details
            // customizer fills in the matching title and "code".
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                type: ErrorCatalog.TypeUri(ErrorCatalog.ProductNotFound),
                detail: $"Product {id} does not exist.");
        }

        return Ok(product);
    }
}
