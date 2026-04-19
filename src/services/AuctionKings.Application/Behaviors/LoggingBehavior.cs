using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AuctionKings.Application.Behaviors;

public sealed partial class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken
    )
    {
        var requestName = typeof(TRequest).Name;

        LogHandling(_logger, requestName);

        var stopwatch = Stopwatch.StartNew();

        var response = await next(cancellationToken);

        stopwatch.Stop();

        LogHandled(_logger, requestName, stopwatch.ElapsedMilliseconds);

        return response;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Handling {RequestName}")]
    private static partial void LogHandling(ILogger logger, string requestName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Handled {RequestName} in {ElapsedMilliseconds}ms")]
    private static partial void LogHandled(ILogger logger, string requestName, long elapsedMilliseconds);
}
