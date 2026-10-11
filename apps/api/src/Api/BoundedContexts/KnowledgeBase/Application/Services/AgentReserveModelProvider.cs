#pragma warning disable MA0048 // File name must match type name - interface and its only implementation
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// #4166 (ADR-095 D6): il modello di riserva della versione pubblicata del profilo agente, quello
/// che il gateway usa per le risposte fra la soglia di degrado e il tetto.
/// </summary>
internal interface IAgentReserveModelProvider
{
    /// <summary>Il modello di riserva, o <c>null</c> se il profilo non ne ha o non si legge.</summary>
    Task<string?> GetReserveModelAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Legge il profilo al più una volta al minuto: il gateway lo chiede a ogni chiamata in degrado, e
/// una pubblicazione nuova può aspettare un minuto.
/// </summary>
internal sealed class PublishedProfileReserveModelProvider : IAgentReserveModelProvider
{
    internal static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PublishedProfileReserveModelProvider> _logger;
    private readonly Lock _lock = new();
    private (string? Model, DateTimeOffset ReadAt)? _cached;

    public PublishedProfileReserveModelProvider(
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider,
        ILogger<PublishedProfileReserveModelProvider> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string?> GetReserveModelAsync(CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        lock (_lock)
        {
            if (_cached is { } cached && now - cached.ReadAt < CacheDuration)
            {
                return cached.Model;
            }
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var profile = await scope.ServiceProvider.GetRequiredService<IAgentProfileRepository>()
                .GetAsync(cancellationToken).ConfigureAwait(false);
            var model = profile?.Published.Content.FallbackModelId;
            lock (_lock)
            {
                _cached = (model, now);
            }

            return model;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // La lettura fallita vale come lettura fino al prossimo minuto: in degrado il gateway
            // chiede la riserva a ogni chiamata, e un database giù non va interrogato a ogni chiamata.
            _logger.LogWarning(ex, "The agent profile could not be read: no reserve model for the budget degrade");
            lock (_lock)
            {
                var model = _cached?.Model;
                _cached = (model, now);
                return model;
            }
        }
    }
}
