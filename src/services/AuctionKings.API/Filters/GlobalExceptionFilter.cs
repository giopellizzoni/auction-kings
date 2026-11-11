using AuctionKings.Application.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AuctionKings.API.Filters;

public sealed partial class GlobalExceptionFilter(
    ILogger<GlobalExceptionFilter> logger,
    IHostEnvironment environment
) : IExceptionFilter
{
    // De-facto (nginx) status for "client closed request"; never actually reaches the client.
    private const int StatusCodeClientClosedRequest = 499;

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception [{StatusCode}]")]
    private static partial void LogUnhandledException(ILogger logger, int statusCode, Exception exception);

    public void OnException(ExceptionContext context)
    {
        // The client disconnected / aborted the request: not a server error, nothing to log at Error.
        if (context.Exception is OperationCanceledException
            && context.HttpContext.RequestAborted.IsCancellationRequested)
        {
            context.Result = new StatusCodeResult(StatusCodeClientClosedRequest);
            context.ExceptionHandled = true;
            return;
        }

        var (statusCode, problem) = context.Exception switch
        {
            ValidationException ve => (StatusCodes.Status400BadRequest, new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc7807#section-3.1",
                Title = "Validation Failed",
                Status = StatusCodes.Status400BadRequest,
                Detail = ve.Message,
                Extensions = { ["errors"] = ve.Errors },
            }),
            ConcurrencyException => (StatusCodes.Status409Conflict, new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc7807#section-3.1",
                Title = "Conflict",
                Status = StatusCodes.Status409Conflict,
                Detail = "A concurrency conflict occurred. Please retry.",
            }),
            DuplicateCommandException => (StatusCodes.Status409Conflict, new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc7807#section-3.1",
                Title = "Duplicate Request",
                Status = StatusCodes.Status409Conflict,
                Detail = environment.IsDevelopment()
                    ? context.Exception.Message
                    : "A duplicate request was detected. The operation has already been processed.",
            }),
            _ => (StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc7807#section-3.1",
                Title = "Internal Server Error",
                Status = StatusCodes.Status500InternalServerError,
                Detail = environment.IsDevelopment()
                    ? context.Exception.Message
                    : "An unexpected error occurred.",
            }),
        };

        if (context.HttpContext.Response.Headers.TryGetValue("X-Correlation-Id", out var correlationId))
        {
            problem.Extensions["traceId"] = correlationId.ToString();
        }

        LogUnhandledException(logger, statusCode, context.Exception);

        context.Result = new ObjectResult(problem)
        {
            StatusCode = statusCode,
            ContentTypes = { "application/problem+json" },
        };
        context.ExceptionHandled = true;
    }
}
