using System.Collections.Concurrent;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests.Integration.UserLibrary;

/// <summary>
/// Ferma una richiesta subito dopo che ha letto un link di condivisione e, in quel punto, esegue per
/// intero un'altra azione — di solito un'altra richiesta HTTP (#4558).
/// </summary>
/// <remarks>
/// <para>
/// Le gare sui link stanno fra la lettura del link e la sua scrittura, dentro un solo handler: da fuori
/// non c'è modo di fermarlo lì, e due richieste lanciate insieme si incrociano solo per caso. Il
/// decoratore del repository rende l'ordine deterministico: la prima <c>GetByShareTokenAsync</c> per quel
/// token dopo la registrazione esegue l'azione, poi restituisce il link letto <b>prima</b> dell'azione.
/// </para>
/// <para>
/// L'azione è tolta dal registro prima di partire: la richiesta che esegue legge lo stesso token senza
/// fermarsi. Le chiavi sono i token perché l'host vive quanto la classe di test, e ogni test ha il suo
/// link. Dopo la richiesta, un test controlla <see cref="IsPending"/>: un'azione mai eseguita vorrebbe
/// dire una gara mai avvenuta, e un verde senza significato.
/// </para>
/// </remarks>
internal sealed class ShareLinkReadInterleaving
{
    private readonly ConcurrentDictionary<string, Func<Task>> _afterRead = new(StringComparer.Ordinal);

    public void AfterNextRead(string shareToken, Func<Task> action)
    {
        if (!_afterRead.TryAdd(shareToken, action))
        {
            throw new InvalidOperationException($"An interleaving is already registered for share token {shareToken}.");
        }
    }

    public bool IsPending(string shareToken) => _afterRead.ContainsKey(shareToken);

    internal Task RunAfterReadAsync(string shareToken) =>
        _afterRead.TryRemove(shareToken, out var action) ? action() : Task.CompletedTask;

    /// <summary>
    /// Sostituisce nell'host il repository dei link con il decoratore, che avvolge l'implementazione
    /// registrata dall'applicazione.
    /// </summary>
    public void Decorate(IServiceCollection services)
    {
        var registrations = services.Where(d => d.ServiceType == typeof(ILibraryShareLinkRepository)).ToList();
        var original = registrations.LastOrDefault()
            ?? throw new InvalidOperationException("ILibraryShareLinkRepository is not registered in the host.");
        var implementation = original.ImplementationType
            ?? throw new InvalidOperationException(
                "ILibraryShareLinkRepository is no longer registered by type: adapt the decorator to the new registration.");

        foreach (var registration in registrations)
        {
            services.Remove(registration);
        }

        services.Add(new ServiceDescriptor(
            typeof(ILibraryShareLinkRepository),
            sp => new InterleavingShareLinkRepository(
                (ILibraryShareLinkRepository)ActivatorUtilities.CreateInstance(sp, implementation),
                this),
            original.Lifetime));
    }
}

/// <summary>
/// Il repository reale, con una sola differenza: dopo la lettura per token esegue l'azione registrata
/// in <see cref="ShareLinkReadInterleaving"/>. Tutto il resto passa invariato.
/// </summary>
internal sealed class InterleavingShareLinkRepository(
    ILibraryShareLinkRepository inner,
    ShareLinkReadInterleaving interleaving) : ILibraryShareLinkRepository
{
    public async Task<LibraryShareLink?> GetByShareTokenAsync(string shareToken, CancellationToken cancellationToken = default)
    {
        var link = await inner.GetByShareTokenAsync(shareToken, cancellationToken);
        await interleaving.RunAfterReadAsync(shareToken);
        return link;
    }

    public Task<LibraryShareLink?> GetActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        inner.GetActiveByUserIdAsync(userId, cancellationToken);

    public Task<IReadOnlyList<LibraryShareLink>> GetAllByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        inner.GetAllByUserIdAsync(userId, cancellationToken);

    public Task<int> CountRecentByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        inner.CountRecentByUserIdAsync(userId, cancellationToken);

    public Task<bool> TryRecordAccessAsync(Guid id, CancellationToken cancellationToken = default) =>
        inner.TryRecordAccessAsync(id, cancellationToken);

    public Task<LibraryShareLink?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        inner.GetByIdAsync(id, cancellationToken);

    public Task<IReadOnlyList<LibraryShareLink>> GetAllAsync(CancellationToken cancellationToken = default) =>
        inner.GetAllAsync(cancellationToken);

    public Task AddAsync(LibraryShareLink entity, CancellationToken cancellationToken = default) =>
        inner.AddAsync(entity, cancellationToken);

    public Task UpdateAsync(LibraryShareLink entity, CancellationToken cancellationToken = default) =>
        inner.UpdateAsync(entity, cancellationToken);

    public Task DeleteAsync(LibraryShareLink entity, CancellationToken cancellationToken = default) =>
        inner.DeleteAsync(entity, cancellationToken);

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default) =>
        inner.ExistsAsync(id, cancellationToken);
}
