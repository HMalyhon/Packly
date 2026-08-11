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
    private readonly List<Entry> _entries = [];

    public IReadOnlyList<Entry> Entries
    {
        get
        {
            lock (_entries)
            {
                return [.. _entries];
            }
        }
    }

    public ILogger CreateLogger(string categoryName) => new Sink(this);

    public void Dispose()
    {
    }

    private void Add(Entry entry)
    {
        lock (_entries)
        {
            _entries.Add(entry);
        }
    }

    /// <summary>
    /// One captured line. The level is part of it because the level is part of
    /// what the saga decided: an ignored event is reported as tolerated, not as
    /// routine.
    /// </summary>
    /// <param name="Level">The level the line was written at.</param>
    /// <param name="Message">The formatted message.</param>
    internal sealed record Entry(LogLevel Level, string Message);

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
            owner.Add(new Entry(logLevel, formatter(state, exception)));
        }
    }
}
