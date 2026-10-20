using System.Collections.Concurrent;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests.Integration.KnowledgeBase;

/// <summary>
/// Ferma una richiesta in un punto preciso della sua vita e, in quel punto, esegue per intero un'altra
/// azione — di solito un'altra richiesta HTTP (#4573).
/// </summary>
/// <remarks>
/// <para>
/// Le gare sul link stanno fra il momento in cui una richiesta guarda il link e quello in cui lo scrive,
/// dentro un solo handler: da fuori non c'è modo di fermarlo lì, e due richieste lanciate insieme si
/// incrociano solo per caso. Il decoratore del repository rende l'ordine deterministico. Stessa tecnica di
/// <c>ShareLinkReadInterleaving</c> (#4558), per il repository dei link di chat. I punti sono due.
/// </para>
/// <list type="bullet">
///   <item><see cref="AfterNextRead"/>: subito dopo <c>GetByIdAsync</c>, che restituisce l'aggregato letto
///     <b>prima</b> dell'azione. È il punto dei comandi del proprietario (la revoca legge e poi scrive). Era
///     anche quello della visita, finché la visita leggeva l'aggregato per riscriverlo.</item>
///   <item><see cref="BeforeNextVisitCount"/>: subito prima di <c>TryRecordAccessAsync</c>, cioè dopo che la
///     visita ha validato il token (firma, scadenza, blacklist) e prima che conti sé stessa. Dopo la
///     correzione di #4573 la visita non legge più il link: questo è il solo istante in cui il link può
///     cambiare fra ciò che la visita ha visto e ciò che scrive.</item>
/// </list>
/// <para>
/// L'azione è tolta dal registro prima di partire: la richiesta che esegue passa dallo stesso punto senza
/// fermarsi. Le chiavi sono gli id dei link perché l'host vive quanto la classe di test, e ogni test ha il
/// suo link. Dopo la richiesta, un test controlla <see cref="IsPending"/>: un'azione mai eseguita vorrebbe
/// dire una gara mai avvenuta, e un verde senza significato.
/// </para>
/// </remarks>
internal sealed class SharedThreadLinkReadInterleaving
{
    private readonly ConcurrentDictionary<Guid, Func<Task>> _afterRead = new();
    private readonly ConcurrentDictionary<Guid, Func<Task>> _beforeVisitCount = new();

    public void AfterNextRead(Guid shareLinkId, Func<Task> action)
    {
        if (!_afterRead.TryAdd(shareLinkId, action))
        {
            throw new InvalidOperationException($"An interleaving is already registered for share link {shareLinkId}.");
        }
    }

    public void BeforeNextVisitCount(Guid shareLinkId, Func<Task> action)
    {
        if (!_beforeVisitCount.TryAdd(shareLinkId, action))
        {
            throw new InvalidOperationException($"A visit interleaving is already registered for share link {shareLinkId}.");
        }
    }

    public bool IsPending(Guid shareLinkId) =>
        _afterRead.ContainsKey(shareLinkId) || _beforeVisitCount.ContainsKey(shareLinkId);

    internal Task RunAfterReadAsync(Guid shareLinkId) =>
        _afterRead.TryRemove(shareLinkId, out var action) ? action() : Task.CompletedTask;

    internal Task RunBeforeVisitCountAsync(Guid shareLinkId) =>
        _beforeVisitCount.TryRemove(shareLinkId, out var action) ? action() : Task.CompletedTask;

    /// <summary>
    /// Sostituisce nell'host il repository dei link con il decoratore, che avvolge l'implementazione
    /// registrata dall'applicazione.
    /// </summary>
    public void Decorate(IServiceCollection services)
    {
        var registrations = services.Where(d => d.ServiceType == typeof(IShareLinkRepository)).ToList();
        var original = registrations.LastOrDefault()
            ?? throw new InvalidOperationException("IShareLinkRepository is not registered in the host.");
        var implementation = original.ImplementationType
            ?? throw new InvalidOperationException(
                "IShareLinkRepository is no longer registered by type: adapt the decorator to the new registration.");

        foreach (var registration in registrations)
        {
            services.Remove(registration);
        }

        services.Add(new ServiceDescriptor(
            typeof(IShareLinkRepository),
            sp => new InterleavingChatShareLinkRepository(
                (IShareLinkRepository)ActivatorUtilities.CreateInstance(sp, implementation),
                this),
            original.Lifetime));
    }
}

/// <summary>
/// Il repository reale, con una sola differenza: dopo la lettura per id, e prima del conteggio di una visita,
/// esegue l'azione registrata in <see cref="SharedThreadLinkReadInterleaving"/>. Tutto il resto passa invariato.
/// </summary>
internal sealed class InterleavingChatShareLinkRepository(
    IShareLinkRepository inner,
    SharedThreadLinkReadInterleaving interleaving) : IShareLinkRepository
{
    public async Task<ShareLink?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var link = await inner.GetByIdAsync(id, cancellationToken);
        await interleaving.RunAfterReadAsync(id);
        return link;
    }

    public Task<IReadOnlyList<ShareLink>> GetAllAsync(CancellationToken cancellationToken = default) =>
        inner.GetAllAsync(cancellationToken);

    public Task AddAsync(ShareLink entity, CancellationToken cancellationToken = default) =>
        inner.AddAsync(entity, cancellationToken);

    public Task UpdateAsync(ShareLink entity, CancellationToken cancellationToken = default) =>
        inner.UpdateAsync(entity, cancellationToken);

    public Task DeleteAsync(ShareLink entity, CancellationToken cancellationToken = default) =>
        inner.DeleteAsync(entity, cancellationToken);

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default) =>
        inner.ExistsAsync(id, cancellationToken);

    public Task<IReadOnlyList<ShareLink>> FindByThreadIdAsync(Guid threadId, CancellationToken cancellationToken = default) =>
        inner.FindByThreadIdAsync(threadId, cancellationToken);

    public Task<IReadOnlyList<ShareLink>> FindByCreatorIdAsync(Guid creatorId, CancellationToken cancellationToken = default) =>
        inner.FindByCreatorIdAsync(creatorId, cancellationToken);

    public Task<ShareLink?> FindByThreadIdAndCreatorIdAsync(Guid threadId, Guid creatorId, CancellationToken cancellationToken = default) =>
        inner.FindByThreadIdAndCreatorIdAsync(threadId, creatorId, cancellationToken);

    public Task<IReadOnlyList<ShareLink>> FindActiveByThreadIdAsync(Guid threadId, CancellationToken cancellationToken = default) =>
        inner.FindActiveByThreadIdAsync(threadId, cancellationToken);

    public async Task<bool> TryRecordAccessAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await interleaving.RunBeforeVisitCountAsync(id);
        return await inner.TryRecordAccessAsync(id, cancellationToken);
    }

    public Task<bool> IsActiveAsync(Guid id, CancellationToken cancellationToken = default) =>
        inner.IsActiveAsync(id, cancellationToken);
}
