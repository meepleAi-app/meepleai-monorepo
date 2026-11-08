using Api.BoundedContexts.KnowledgeBase.Domain.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Services;

/// <summary>
/// #4164 (ADR-095): il listino dei modelli servito da un'istantanea in memoria del catalogo
/// <c>AiModelConfiguration</c>.
/// </summary>
/// <remarks>
/// <para>
/// Singleton, come <see cref="ILlmCostCalculator"/> che lo consuma: la lettura è sincrona e non
/// tocca il database. L'istantanea si carica all'avvio (<see cref="StartAsync"/>) e si ricarica in
/// background quando è più vecchia di <see cref="RefreshInterval"/>; intanto si continua a servire
/// quella precedente. Una modifica del catalogo fatta dall'admin, o dal seed all'avvio, diventa
/// quindi visibile ai costi entro un minuto.
/// </para>
/// <para>
/// Contiene tutti i modelli del catalogo, anche quelli disattivati: il prezzo è una proprietà del
/// modello, non della sua abilitazione, e una chiamata a un modello disattivato costa comunque.
/// </para>
/// </remarks>
internal sealed class CatalogModelPriceList : IModelPriceList, IHostedService, IDisposable
{
    internal static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Attesa prima di riprovare dopo un caricamento fallito: con il database giù, ogni chiamata
    /// LLM ne innescherebbe un altro.
    /// </summary>
    internal static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(5);

    internal const string PricingCurrency = "USD";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CatalogModelPriceList> _logger;
    private readonly SemaphoreSlim _loadGate = new(1, 1);

    private volatile IReadOnlyDictionary<string, LlmModelPricing>? _prices;
    private long _nextRefreshUtcTicks;
    private int _refreshScheduled;
    private Task _pendingRefresh = Task.CompletedTask;

    public CatalogModelPriceList(
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider,
        ILogger<CatalogModelPriceList> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Il ricaricamento in background avviato dall'ultima <see cref="Find"/>, se ce n'è uno.
    /// Serve ai test per attenderlo.
    /// </summary>
    internal Task PendingRefresh => Volatile.Read(ref _pendingRefresh);

    /// <inheritdoc/>
    public LlmModelPricing? Find(string modelId)
    {
        // #4765: read the snapshot BEFORE scheduling the refresh. The refresh runs on another thread
        // and swaps `_prices`; read afterwards, a fast refresh could be what this very call returns,
        // so «serve the old snapshot while reloading» held only if the thread was not descheduled
        // between the two lines.
        var prices = _prices;

        if (_timeProvider.GetUtcNow().UtcTicks >= Interlocked.Read(ref _nextRefreshUtcTicks))
        {
            ScheduleRefresh();
        }

        if (prices is null || string.IsNullOrEmpty(modelId))
        {
            return null;
        }

        return prices.TryGetValue(modelId, out var pricing) ? pricing : null;
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">
    /// Il catalogo non si è potuto caricare. Si lancia invece di lasciare il listino vuoto: chi
    /// chiede un prezzo leggerebbe «il modello non ha prezzo» per un guasto transitorio.
    /// </exception>
    public async Task EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (_prices is not null)
        {
            return;
        }

        await RefreshAsync(cancellationToken).ConfigureAwait(false);
        if (_prices is null)
        {
            throw new InvalidOperationException(
                "The model price list could not be loaded from the AI model catalog; see the previous error for the cause.");
        }
    }

    /// <summary>
    /// Ricarica l'istantanea dal catalogo. Non lancia: un fallimento resta nei log e lascia in uso
    /// l'istantanea precedente.
    /// </summary>
    /// <returns>Vero se l'istantanea è stata ricaricata.</returns>
    public async Task<bool> RefreshAsync(CancellationToken cancellationToken)
    {
        await _loadGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            IReadOnlyList<AiModelConfiguration> models;
            using (var scope = _scopeFactory.CreateScope())
            {
                var repository = scope.ServiceProvider.GetRequiredService<IAiModelConfigurationRepository>();
                models = await repository.GetAllAsync(cancellationToken).ConfigureAwait(false);
            }

            var prices = new Dictionary<string, LlmModelPricing>(models.Count, StringComparer.Ordinal);
            foreach (var model in models)
            {
                var pricing = model.Settings.Pricing;
                if (!string.Equals(pricing.Currency, PricingCurrency, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogError(
                        "Model {ModelId} is priced in {Currency}, but LLM costs are recorded in USD: the model is left out of the price list",
                        model.ModelId, pricing.Currency);
                    continue;
                }

                prices[model.ModelId] = new LlmModelPricing
                {
                    ModelId = model.ModelId,
                    Provider = model.Provider,
                    InputCostPer1M = pricing.InputPricePerMillion,
                    OutputCostPer1M = pricing.OutputPricePerMillion
                };
            }

            _prices = prices;
            SetNextRefresh(RefreshInterval);
            _logger.LogDebug("Model price list loaded from the AI model catalog: {Count} models", prices.Count);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            SetNextRefresh(RetryInterval);
            _logger.LogError(ex, "Failed to load the model price list from the AI model catalog; keeping the previous snapshot");
            return false;
        }
        finally
        {
            _loadGate.Release();
        }
    }

    /// <summary>
    /// Carica il listino all'avvio, così le prime chiamate trovano già i prezzi.
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken) => RefreshAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public void Dispose() => _loadGate.Dispose();

    private void ScheduleRefresh()
    {
        if (Interlocked.CompareExchange(ref _refreshScheduled, 1, 0) != 0)
        {
            return;
        }

        Volatile.Write(ref _pendingRefresh, Task.Run(async () =>
        {
            try
            {
                await RefreshAsync(CancellationToken.None).ConfigureAwait(false);
            }
            finally
            {
                Volatile.Write(ref _refreshScheduled, 0);
            }
        }));
    }

    private void SetNextRefresh(TimeSpan delay) =>
        Interlocked.Exchange(ref _nextRefreshUtcTicks, (_timeProvider.GetUtcNow() + delay).UtcTicks);
}
