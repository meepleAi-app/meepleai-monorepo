using System.Collections.Concurrent;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Api.Tests.Integration;

/// <summary>
/// Un punto della pipeline in cui il test può fermare il codice sotto prova «dentro la chiamata».
/// </summary>
/// <remarks>
/// <para>Il doppio che lo usa chiama <see cref="EnterAsync"/> all'ingresso. La chiamata numero
/// <c>holdOnCall</c> segnala <see cref="Entered"/> e resta ferma finché il test non chiama
/// <see cref="Release"/> o finché il token del chiamante non viene cancellato. Le altre passano
/// senza fermarsi, e <see cref="Calls"/> le conta tutte: è il contatore con cui un test dimostra di
/// essere arrivato alla fase che nomina, invece di restare verde senza averla vista.</para>
/// <para>Non c'è un ritardo fisso. Il test cancella quando il segnale dice che il codice è dentro, e
/// non dopo «un po'» (#4522: il ritardo di 100 ms misurava una fase diversa a seconda del freddo
/// della prima query).</para>
/// </remarks>
internal sealed class HoldPoint
{
    private readonly int _holdOnCall;
    private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _calls;

    public HoldPoint(int holdOnCall = 1)
    {
        _holdOnCall = holdOnCall;
    }

    /// <summary>Un punto che non ferma mai nessuno e conta soltanto le chiamate.</summary>
    public static HoldPoint Never() => new(holdOnCall: 0);

    /// <summary>Quante volte il codice sotto prova è entrato qui, fermato o no.</summary>
    public int Calls => Volatile.Read(ref _calls);

    /// <summary>Si completa quando la chiamata fermata è dentro.</summary>
    public Task Entered => _entered.Task;

    /// <summary>Lascia andare la chiamata fermata, come farebbe un servizio che risponde.</summary>
    public void Release() => _release.TrySetResult();

    /// <exception cref="OperationCanceledException">Il token del chiamante è stato cancellato mentre la chiamata era ferma.</exception>
    public Task EnterAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Increment(ref _calls) != _holdOnCall)
        {
            return Task.CompletedTask;
        }

        _entered.TrySetResult();
        return _release.Task.WaitAsync(cancellationToken);
    }
}

/// <summary>
/// <see cref="IBackgroundTaskService"/> che fa ciò che fa quello di produzione per la lambda
/// dell'upload: la esegue con un token proprio, annullabile per id, e non propaga le eccezioni.
/// In più lascia al test di aspettarne la fine.
/// </summary>
/// <remarks>
/// Le classi di test che usano <c>new Mock&lt;IBackgroundTaskService&gt;()</c> senza setup non
/// eseguono mai la lambda, e con essa nemmeno <c>ProcessPdfAsync</c>: estrazione, embedding e
/// indicizzazione restano fuori dal test (#4531). <see cref="CancelTask"/> ha la semantica di
/// <c>BackgroundTaskService.CancelTask</c>, che è ciò che chiama <c>CancelPdfProcessingCommandHandler</c>.
/// </remarks>
internal sealed class CancellableBackgroundTaskService : IBackgroundTaskService
{
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _active = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<Task> _running = new();
    private readonly ConcurrentQueue<Exception> _errors = new();
    private int _started;

    /// <summary>
    /// Vero dentro la lambda avviata da <see cref="ExecuteWithCancellation"/> e in ciò che essa attende, falso
    /// sul thread della richiesta. Lascia a un doppio condiviso con la richiesta (la cache dell'AI, invalidata
    /// sia dall'upload sia dalla chiusura dell'elaborazione) di fermare soltanto il task in background, senza
    /// contare le chiamate nell'ordine in cui arrivano.
    /// </summary>
    public static readonly AsyncLocal<bool> InBackgroundTask = new();

    /// <summary>Quante lambda sono state avviate.</summary>
    public int Started => Volatile.Read(ref _started);

    /// <summary>
    /// Le eccezioni uscite dalle lambda. Quello di produzione le registra e le scarta: qui restano
    /// leggibili, così un fallimento può dire che cosa è andato storto.
    /// </summary>
    public IReadOnlyCollection<Exception> Errors => _errors;

    public void Execute(Func<Task> task)
    {
        ArgumentNullException.ThrowIfNull(task);
        _running.Enqueue(Task.Run(async () =>
        {
            try
            {
                await task().ConfigureAwait(false);
            }
#pragma warning disable CA1031 // Come BackgroundTaskService: il confine del task in background non lascia uscire eccezioni
            catch (Exception ex)
#pragma warning restore CA1031
            {
                _errors.Enqueue(ex);
            }
        }, CancellationToken.None));
    }

    public void ExecuteWithCancellation(string taskId, Func<CancellationToken, Task> taskFactory)
    {
        ArgumentNullException.ThrowIfNull(taskFactory);

        var cts = new CancellationTokenSource();
        if (!_active.TryAdd(taskId, cts))
        {
            cts.Dispose();
            return;
        }

        Interlocked.Increment(ref _started);
        _running.Enqueue(Task.Run(async () =>
        {
            InBackgroundTask.Value = true;
            try
            {
                await taskFactory(cts.Token).ConfigureAwait(false);
            }
#pragma warning disable CA1031 // Come BackgroundTaskService: il confine del task in background non lascia uscire eccezioni
            catch (Exception ex)
#pragma warning restore CA1031
            {
                _errors.Enqueue(ex);
            }
            finally
            {
                _active.TryRemove(taskId, out _);
                cts.Dispose();
            }
        }, CancellationToken.None));
    }

    public bool CancelTask(string taskId)
    {
        if (!_active.TryRemove(taskId, out var cts))
        {
            return false;
        }

        // Il finally della lambda in corso lo smaltisce.
        cts.Cancel();
        return true;
    }

    public Task WaitForAllAsync() => Task.WhenAll(_running.ToArray());
}

/// <summary>
/// <see cref="IPdfIndexingPipeline"/> che si ferma <b>all'ingresso</b> di <c>BeginIndexingAsync</c> (la prima
/// scrittura della fase di indicizzazione) e poi passa la chiamata a quello vero. <c>IndexAsync</c>, l'ultima,
/// passa senza fermarsi (#4609: prima era lei la prima scrittura).
/// </summary>
internal sealed class HoldingPdfIndexingPipeline : IPdfIndexingPipeline
{
    private readonly IPdfIndexingPipeline _inner;
    private readonly HoldPoint _hold;

    public HoldingPdfIndexingPipeline(IPdfIndexingPipeline inner, HoldPoint hold)
    {
        _inner = inner;
        _hold = hold;
    }

    public async Task BeginIndexingAsync(
        Guid pdfDocumentId,
        Guid? gameId,
        Guid? sharedGameId,
        int chunkCount,
        int totalCharacters,
        string language,
        CancellationToken cancellationToken = default)
    {
        await _hold.EnterAsync(cancellationToken).ConfigureAwait(false);
        await _inner.BeginIndexingAsync(pdfDocumentId, gameId, sharedGameId, chunkCount, totalCharacters, language, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task IndexAsync(
        Guid pdfDocumentId,
        Guid? gameId,
        Guid? sharedGameId,
        int chunkCount,
        int totalCharacters,
        string language,
        CancellationToken cancellationToken = default)
    {
        await _inner.IndexAsync(pdfDocumentId, gameId, sharedGameId, chunkCount, totalCharacters, language, cancellationToken)
            .ConfigureAwait(false);
    }
}

/// <summary>
/// Raccoglie i messaggi di errore dei log, con l'eccezione, per poterli citare in un fallimento.
/// </summary>
/// <remarks>
/// Il gestore chiude un'elaborazione fallita con un messaggio generico («Invalid operation») e mette la
/// causa solo nel log. Senza questo, un test rosso dice che la pipeline è finita <c>Failed</c> e non perché.
/// </remarks>
internal sealed class ErrorLogCapture : ILoggerProvider
{
    private readonly ConcurrentQueue<string> _entries = new();

    public IReadOnlyCollection<string> Entries => _entries;

    public ILogger CreateLogger(string categoryName) => new CaptureLogger(categoryName, _entries);

    public void Dispose()
    {
    }

    private sealed class CaptureLogger : ILogger
    {
        private readonly string _category;
        private readonly ConcurrentQueue<string> _entries;

        public CaptureLogger(string category, ConcurrentQueue<string> entries)
        {
            _category = category;
            _entries = entries;
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Error;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            _entries.Enqueue($"{_category}: {formatter(state, exception)}{(exception is null ? string.Empty : " | " + exception)}");
        }
    }
}

/// <summary>
/// Fa fallire con <paramref name="failure"/> il primo <c>SaveChangesAsync</c> che soddisfa la condizione, un
/// istante prima che parta il comando: le modifiche restano nel change tracker e il database non ha visto
/// niente, come con un vincolo violato o una connessione caduta.
/// </summary>
internal sealed class FailingSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly Func<DbContext, bool> _shouldFail;
    private readonly Exception _failure;

    private FailingSaveChangesInterceptor(Func<DbContext, bool> shouldFail, Exception failure)
    {
        _shouldFail = shouldFail;
        _failure = failure;
    }

    /// <summary>
    /// Ogni salvataggio che contiene righe di <c>pgvector_embeddings</c> nuove, finché restano nel change
    /// tracker (chi gestisce l'errore lo svuota prima di scrivere lo stato <c>Failed</c>).
    /// </summary>
    public static FailingSaveChangesInterceptor ForVectorRows(Exception failure) =>
        new(context => context.ChangeTracker.Entries<PgVectorEmbeddingEntity>().Any(e => e.State == EntityState.Added), failure);

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context && _shouldFail(context))
        {
            throw _failure;
        }

        return ValueTask.FromResult(result);
    }
}

/// <summary>
/// Ferma un <c>SaveChangesAsync</c> un istante prima che parta il comando, con le modifiche già nel change
/// tracker e gli eventi di dominio già accodati nel collector: il punto in cui una scrittura è «a metà».
/// </summary>
/// <remarks>
/// Se il token del chiamante viene cancellato mentre è fermo, esce con <see cref="OperationCanceledException"/>
/// dal <c>SaveChangesAsync</c>, come farebbe EF con un comando interrotto: le entità restano nel tracker, gli
/// eventi restano nel collector e il database non ha visto niente. È la condizione in cui chi gestisce la
/// cancellazione non deve rifare un salvataggio senza prima ripulire entrambi.
/// </remarks>
internal sealed class HoldingSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly Func<DbContext, bool> _shouldHold;
    private readonly HoldPoint _hold;

    private HoldingSaveChangesInterceptor(Func<DbContext, bool> shouldHold, HoldPoint hold)
    {
        _shouldHold = shouldHold;
        _hold = hold;
    }

    /// <summary>Il primo salvataggio che contiene righe di <c>pgvector_embeddings</c> nuove.</summary>
    public static HoldingSaveChangesInterceptor ForVectorRows(HoldPoint hold) =>
        new(context => context.ChangeTracker.Entries<PgVectorEmbeddingEntity>().Any(e => e.State == EntityState.Added), hold);

    /// <summary>Il salvataggio che porta un PDF già esistente allo stato <paramref name="state"/> (<c>PdfDocumentEntity.ProcessingState</c>).</summary>
    public static HoldingSaveChangesInterceptor ForPdfStateTransition(string state, HoldPoint hold) =>
        new(
            context => context.ChangeTracker.Entries<PdfDocumentEntity>()
                .Any(e => e.State == EntityState.Modified && string.Equals(e.Entity.ProcessingState, state, StringComparison.Ordinal)),
            hold);

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context && _shouldHold(context))
        {
            await _hold.EnterAsync(cancellationToken).ConfigureAwait(false);
        }

        return result;
    }
}
