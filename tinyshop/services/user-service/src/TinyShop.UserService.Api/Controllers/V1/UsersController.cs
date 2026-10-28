using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using TinyShop.ServiceDefaults.Errors;
using TinyShop.UserService.Api.Errors;
using TinyShop.UserService.Application.Models;
using TinyShop.UserService.Application.Services;

namespace TinyShop.UserService.Api.Controllers.V1;

/// <summary>User lookup (contracts.md §5.2).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("users")]
// No class-level [Produces]: it would force "application/json" onto error responses too,
// which must be "application/problem+json" (contracts.md §1).
public sealed class UsersController(IUserDirectoryService directory) : ControllerBase
{
    /// <summary>Gets one user.</summary>
    /// <param name="id">The user's ID.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    [HttpGet("{id}")]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<UserDto>> GetById(long id, CancellationToken cancellationToken)
    {
        var user = await directory.GetByIdAsync(id, cancellationToken);

        if (user is null)
        {
            // Setting our type URI is how a controller picks the error code; the Problem Details
            // customizer fills in the matching title and "code".
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                type: ProblemType.For(UserErrorCodes.UserNotFound),
                detail: $"User {id} does not exist.");
        }

        return Ok(user);
    }
}
