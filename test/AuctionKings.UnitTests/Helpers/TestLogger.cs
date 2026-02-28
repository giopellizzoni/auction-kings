using Microsoft.Extensions.Logging;

namespace AuctionKings.UnitTests.Helpers;

public sealed class TestLogger<T> : ILogger<T>
{
    private readonly List<(LogLevel Level, string Message)> _logs = [];

    public IReadOnlyList<(LogLevel Level, string Message)> Logs => _logs;

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter
    )
    {
        _logs.Add((logLevel, formatter(state, exception)));
    }

    public bool HasLog(LogLevel level, string containsText) =>
        _logs.Any(l => l.Level == level && l.Message.Contains(containsText));
}
