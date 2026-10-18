using Microsoft.Extensions.Logging;

namespace Api.Tests.Infrastructure.Seeders.ReferenceQuestions;

/// <summary>
/// #4470: un logger che tiene i messaggi, per asserire ciò che il seeder delle domande di riferimento
/// dice nel log. Gli errori di quel seeder non escono come eccezioni, quindi il log è il contratto.
/// </summary>
internal sealed class CapturingLogger : ILogger
{
    private readonly List<(LogLevel Level, string Message)> _entries = new();

    public IReadOnlyList<(LogLevel Level, string Message)> Entries
    {
        get
        {
            lock (_entries)
                return _entries.ToList();
        }
    }

    public IEnumerable<string> At(LogLevel level) =>
        Entries.Where(e => e.Level == level).Select(e => e.Message);

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        var message = formatter(state, exception);
        if (exception is not null)
            message = $"{message} | {exception.GetType().Name}: {exception.Message}";
        lock (_entries)
            _entries.Add((logLevel, message));
    }
}
