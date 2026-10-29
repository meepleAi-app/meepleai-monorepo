using Microsoft.Extensions.Logging;

namespace Api.Tests.TestHelpers;

/// <summary>
/// Un <see cref="ILogger{TCategoryName}"/> che registra ogni voce come testo (livello, messaggio formattato e
/// l'eccezione intera, con il suo messaggio): dice se un dettaglio è finito nel log.
/// </summary>
/// <remarks>
/// #4660: il testo che un PDF fallito porta in colonna è fisso, e il dettaglio dell'eccezione sta solo nel log. Un
/// test che controlla la colonna e non il log lascia cancellare in silenzio la riga che lo scrive.
/// </remarks>
internal sealed class RecordingLogger<T> : ILogger<T>
{
    private readonly List<string> _entries = [];

    public IReadOnlyList<string> Entries => _entries;

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        _entries.Add($"{logLevel}: {formatter(state, exception)}{(exception is null ? string.Empty : " | " + exception)}");
}
