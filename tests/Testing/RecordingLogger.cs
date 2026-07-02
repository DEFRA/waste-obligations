using Microsoft.Extensions.Logging;

namespace Defra.WasteObligations.Testing;

public sealed class RecordingLogger<T> : ILogger<T>
{
    public List<LogRecord> Entries { get; } = [];

    public List<string> Messages { get; } = [];

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
        var message = formatter(state, exception);
        Entries.Add(new LogRecord(logLevel, message, exception));
        Messages.Add(message);
    }

    public sealed record LogRecord(LogLevel Level, string Message, Exception? Exception);
}
