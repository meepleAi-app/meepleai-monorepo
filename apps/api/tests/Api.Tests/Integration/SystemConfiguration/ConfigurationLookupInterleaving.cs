using System.Collections.Concurrent;
using Api.Models;
using Api.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests.Integration.SystemConfiguration;

/// <summary>
/// Ferma una richiesta subito dopo che ha cercato una configurazione per chiave e, in quel punto,
/// esegue per intero un'altra azione — di solito un'altra richiesta sullo stesso flag (#4599).
/// </summary>
/// <remarks>
/// <para>
/// La gara del primo toggle sta fra la lettura «questa chiave non ha una riga» e la creazione della riga,
/// dentro <c>FeatureFlagService</c>: da fuori non c'è modo di fermarla lì, e due richieste lanciate insieme
/// si incrociano solo per caso. Il decoratore di <see cref="IConfigurationService"/> rende l'ordine
/// deterministico: la prima <c>GetConfigurationByKeyAsync</c> per quella chiave dopo la registrazione
/// esegue l'azione, poi restituisce l'esito letto <b>prima</b> dell'azione — cioè «nessuna riga».
/// </para>
/// <para>
/// Stessa tecnica di <c>ShareLinkReadInterleaving</c> (#4558). L'azione è tolta dal registro prima di
/// partire: la richiesta che esegue cerca la stessa chiave senza fermarsi. Le chiavi sono quelle di
/// configurazione perché l'host vive quanto la classe di test e ogni test ha la sua. Dopo la richiesta,
/// un test controlla <see cref="IsPending"/>: un'azione mai eseguita vorrebbe dire una gara mai avvenuta,
/// e un verde senza significato.
/// </para>
/// <para>
/// Solo <c>GetConfigurationByKeyAsync</c> è un punto di fermata. <c>GetValueAsync</c>, con cui i toggle
/// leggono lo stato corrente (<c>IsEnabledAsync</c>), passa invariato: la ricerca che precede la scrittura
/// è la seconda, quella dentro <c>Enable/Disable…Async</c>, ed è lì che si vuole fermare la richiesta.
/// </para>
/// </remarks>
internal sealed class ConfigurationLookupInterleaving
{
    private readonly ConcurrentDictionary<string, Func<Task>> _afterLookup = new(StringComparer.Ordinal);

    public void AfterNextLookup(string key, Func<Task> action)
    {
        if (!_afterLookup.TryAdd(key, action))
        {
            throw new InvalidOperationException($"An interleaving is already registered for configuration key {key}.");
        }
    }

    public bool IsPending(string key) => _afterLookup.ContainsKey(key);

    internal Task RunAfterLookupAsync(string key) =>
        _afterLookup.TryRemove(key, out var action) ? action() : Task.CompletedTask;

    /// <summary>
    /// Sostituisce nell'host il servizio di configurazione con il decoratore, che avvolge
    /// l'implementazione registrata dall'applicazione.
    /// </summary>
    public void Decorate(IServiceCollection services)
    {
        var registrations = services.Where(d => d.ServiceType == typeof(IConfigurationService)).ToList();
        var original = registrations.LastOrDefault()
            ?? throw new InvalidOperationException("IConfigurationService is not registered in the host.");
        var implementation = original.ImplementationType
            ?? throw new InvalidOperationException(
                "IConfigurationService is no longer registered by type: adapt the decorator to the new registration.");

        foreach (var registration in registrations)
        {
            services.Remove(registration);
        }

        services.Add(new ServiceDescriptor(
            typeof(IConfigurationService),
            sp => new InterleavingConfigurationService(
                (IConfigurationService)ActivatorUtilities.CreateInstance(sp, implementation),
                this),
            original.Lifetime));
    }
}

/// <summary>
/// Il servizio reale, con una sola differenza: dopo la ricerca per chiave esegue l'azione registrata in
/// <see cref="ConfigurationLookupInterleaving"/>. Tutto il resto passa invariato.
/// </summary>
internal sealed class InterleavingConfigurationService(
    IConfigurationService inner,
    ConfigurationLookupInterleaving interleaving) : IConfigurationService
{
    public async Task<SystemConfigurationDto?> GetConfigurationByKeyAsync(
        string key, string? environment = null, CancellationToken cancellationToken = default)
    {
        var found = await inner.GetConfigurationByKeyAsync(key, environment, cancellationToken);
        await interleaving.RunAfterLookupAsync(key);
        return found;
    }

    public Task<T?> GetValueAsync<T>(string key, T? defaultValue = default, string? environment = null) =>
        inner.GetValueAsync(key, defaultValue, environment);

    public Task InvalidateAsync(string key, string? environment = null, CancellationToken cancellationToken = default) =>
        inner.InvalidateAsync(key, environment, cancellationToken);
}
