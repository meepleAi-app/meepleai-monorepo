using Serilog.Core;
using Serilog.Events;

namespace Api.Tests.Logging;

/// <summary>
/// Simple in-memory Serilog sink for integration tests.
/// Allows capturing all log events emitted during a test run.
/// </summary>
internal sealed class TestLogSink : ILogEventSink
{
    private static readonly object _lock = new();
    private static readonly List<LogEvent> _events = new();

    public void Emit(LogEvent logEvent)
    {
        lock (_lock)
        {
            _events.Add(logEvent);
        }
    }

    public static void Clear()
    {
        lock (_lock)
        {
            _events.Clear();
        }
    }

    public static IReadOnlyList<LogEvent> GetEvents()
    {
        lock (_lock)
        {
            return _events.ToList();
        }
    }
}

