using MediatR;

namespace Api.BoundedContexts.Administration.Domain.Services;

/// <summary>
/// Service for real-time dashboard updates using Server-Sent Events (SSE).
/// Manages global event subscriptions and broadcasting for dashboard widgets.
/// Unlike session-scoped ISessionSyncService, this is a global pub/sub for all dashboard updates.
/// </summary>
public interface IDashboardStreamService
{
    /// <summary>
    /// Subscribes to real-time dashboard events.
    /// Returns an async stream of events for SSE broadcasting to authenticated users.
    /// </summary>
    /// <param name="userId">User ID for filtering user-specific events.</param>
    /// <param name="ct">Cancellation token for cleanup on client disconnect.</param>
    /// <returns>Async enumerable stream of dashboard events.</returns>
    IAsyncEnumerable<INotification> SubscribeToDashboardEvents(
        Guid userId,
        CancellationToken ct);

    /// <summary>
    /// Publishes an event to all dashboard subscribers.
    /// Events are broadcasted in real-time to connected SSE clients.
    /// </summary>
    /// <param name="evt">Event to broadcast.</param>
    /// <param name="ct">Cancellation token.</param>
    Task PublishEventAsync(INotification evt, CancellationToken ct);

    /// <summary>
    /// Publishes an event to a specific user's dashboard stream.
    /// </summary>
    /// <param name="userId">Target user ID.</param>
    /// <param name="evt">Event to broadcast.</param>
    /// <param name="ct">Cancellation token.</param>
    Task PublishEventToUserAsync(Guid userId, INotification evt, CancellationToken ct);

    /// <summary>
    /// Quanti subscriber si sono registrati per <paramref name="userId"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 🔴 Esiste perché la registrazione di un subscriber <b>non è osservabile dall'esterno</b> e
    /// avviene in un istante che il chiamante non controlla. <see cref="SubscribeToDashboardEvents"/>
    /// è un <c>async IAsyncEnumerable</c>: il corpo — e quindi l'inserimento nei pool — non gira
    /// all'invocazione, ma alla prima <c>MoveNextAsync</c> del consumer, cioè quando il thread pool
    /// decide di far partire quel task.
    /// </para>
    /// <para>
    /// Senza questo, un test che pubblica dopo aver sottoscritto deve <i>indovinare</i> quel momento
    /// con un'attesa fissa (<c>await Task.Delay(50)</c>), e sotto carico 50 ms non bastano: il
    /// publish parte prima della registrazione, il subscriber non riceve niente, e il test fallisce
    /// per un difetto che non esiste. È la forma di #3711, dove la stessa domanda su
    /// <c>ISessionBroadcastService</c> è stata risolta con <c>GetConnectionCount</c> — non
    /// allargando il delay, che è la sconfitta e non il fix.
    /// </para>
    /// <para>
    /// Nota sul significato: conta le <b>registrazioni</b>, non le connessioni vive. Il pool non
    /// rimuove il writer alla disconnessione (il <c>finally</c> lo completa soltanto), quindi un
    /// subscriber disconnesso resta contato. Per sincronizzare un test è esattamente ciò che serve;
    /// non usarlo come metrica di connessioni attive.
    /// </para>
    /// </remarks>
    /// <param name="userId">Utente di cui contare i subscriber.</param>
    int GetSubscriberCount(Guid userId);
}
