using AuctionKings.Shared.Common;
using Microsoft.AspNetCore.Mvc;

namespace AuctionKings.API.Controllers;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected ObjectResult MapError(Error error) =>
        error.Type switch
        {
            ErrorType.NotFound => NotFound(new { error.Code, error.Message }),
            ErrorType.Conflict => Conflict(new { error.Code, error.Message }),
            ErrorType.Unexpected => StatusCode(
                StatusCodes.Status500InternalServerError,
                new { error.Code, error.Message }
            ),
            _ => BadRequest(new { error.Code, error.Message }),
        };
}
