using Microsoft.Extensions.Logging;

namespace Packly.Orchestrator.Tests;

/// <summary>
/// Collects what the saga logged, so a test can assert on a decision that leaves
/// no other trace.
/// </summary>
/// <remarks>
/// Needed for exactly one case: an event a state has no handler for is ignored,
/// and an ignored event is recorded nowhere the harness exposes - not as consumed,
/// not as faulted, not as a state change. The log line is the only evidence the
/// decision was reached rather than the message being lost on the way.
/// </remarks>
internal sealed class CapturedLogs : ILoggerProvider
{
    private readonly List<string> messages = [];

    public IReadOnlyList<string> Messages
    {
        get
        {
            lock (this.messages)
            {
                return [.. this.messages];
            }
        }
    }

    public ILogger CreateLogger(string categoryName) => new Sink(this);

    public void Dispose()
    {
    }

    private void Add(string message)
    {
        lock (this.messages)
        {
            this.messages.Add(message);
        }
    }

    private sealed class Sink(CapturedLogs owner) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);
            owner.Add(formatter(state, exception));
        }
    }
}
