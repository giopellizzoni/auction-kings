using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AuctionKings.Application.Behaviors;

public sealed partial class PerformanceBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private const int WarningThresholdMs = 500;

    private readonly ILogger<PerformanceBehavior<TRequest, TResponse>> _logger;

    public PerformanceBehavior(ILogger<PerformanceBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken
    )
    {
        var stopwatch = Stopwatch.StartNew();

        var response = await next(cancellationToken);

        stopwatch.Stop();

        if (stopwatch.ElapsedMilliseconds > WarningThresholdMs)
        {
            LogLongRunning(_logger, typeof(TRequest).Name, stopwatch.ElapsedMilliseconds, WarningThresholdMs);
        }

        return response;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Long running request detected: {RequestName} took {ElapsedMilliseconds}ms (threshold: {ThresholdMs}ms)")]
    private static partial void LogLongRunning(ILogger logger, string requestName, long elapsedMilliseconds, int thresholdMs);
}
