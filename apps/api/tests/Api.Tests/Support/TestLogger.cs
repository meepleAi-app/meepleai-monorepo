using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Api.Tests.Support;

/// <summary>
/// Simple test logger that captures log entries for inspection in unit tests.
/// </summary>
/// <typeparam name="T">The category type for the logger.</typeparam>
public sealed class TestLogger<T> : ILogger<T>
{
    private readonly List<LogEntry> _entries = [];

    /// <summary>
    /// Captured log entries.
    /// </summary>
    public IReadOnlyList<LogEntry> Entries => _entries;

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var message = formatter != null ? formatter(state, exception) : state?.ToString() ?? string.Empty;
        _entries.Add(new LogEntry(logLevel, eventId, state, exception, message));
    }

    /// <summary>
    /// Represents a captured log entry.
    /// </summary>
    /// <param name="LogLevel">The level of the log.</param>
    /// <param name="EventId">The associated event identifier.</param>
    /// <param name="State">The structured state payload.</param>
    /// <param name="Exception">The exception attached to the log, if any.</param>
    /// <param name="Message">The formatted message.</param>
    public readonly record struct LogEntry(
        LogLevel LogLevel,
        EventId EventId,
        object? State,
        Exception? Exception,
        string Message);

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
