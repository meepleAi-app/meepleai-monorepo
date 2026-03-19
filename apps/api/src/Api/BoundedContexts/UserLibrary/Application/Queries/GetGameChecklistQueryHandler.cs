using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Caching.Hybrid;

namespace Api.BoundedContexts.UserLibrary.Application.Queries;

/// <summary>
/// Handler for retrieving game setup checklist with optional wizard mode.
/// Uses HybridCache with 15-minute TTL for static checklist data.
/// </summary>
internal class GetGameChecklistQueryHandler : IQueryHandler<GetGameChecklistQuery, ChecklistDto>
{
    private readonly HybridCache _cache;
    private readonly ILogger<GetGameChecklistQueryHandler> _logger;

    private static readonly HybridCacheEntryOptions CacheOptions = new()
    {
        Expiration = TimeSpan.FromMinutes(15),
        LocalCacheExpiration = TimeSpan.FromMinutes(5)
    };

    public GetGameChecklistQueryHandler(
        HybridCache cache,
        ILogger<GetGameChecklistQueryHandler> logger)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ChecklistDto> Handle(GetGameChecklistQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var cacheKey = $"game-checklist:{query.GameId}:{query.IncludeWizard}";

        return await _cache.GetOrCreateAsync(
            cacheKey,
            cancel =>
            {
                _logger.LogDebug("Cache miss for game checklist {GameId}", query.GameId);

                // For this implementation, return generic checklist items
                // In a real scenario, this would fetch from GameManagement BC or user's custom checklist
                var items = new[]
                {
                    new ChecklistItemDto("Open game box and verify all components", 0, "Check against component list"),
                    new ChecklistItemDto("Read rulebook (or use AI assistant)", 1, "Quick rules available in app"),
                    new ChecklistItemDto("Organize components by type", 2, "Use sorting trays if available"),
                    new ChecklistItemDto("Shuffle decks if applicable", 3, null),
                    new ChecklistItemDto("Set up game board", 4, "Follow setup diagram in rulebook"),
                    new ChecklistItemDto("Deal starting resources to players", 5, null)
                };

                WizardStepDto[]? wizardSteps = null;
                if (query.IncludeWizard)
                {
                    wizardSteps = new[]
                    {
                        new WizardStepDto(1, "Setup", "Prepare game components", IsSkippable: false),
                        new WizardStepDto(2, "Players", "Configure players and teams", IsSkippable: false),
                        new WizardStepDto(3, "Options", "Select game variants", IsSkippable: true),
                        new WizardStepDto(4, "Ready", "Start game when ready", IsSkippable: false)
                    };
                }

                _logger.LogInformation("Retrieved checklist for game {GameId} (wizard: {IncludeWizard})",
                    query.GameId, query.IncludeWizard);

                var result = new ChecklistDto(
                    GameId: query.GameId,
                    Items: items,
                    WizardSteps: wizardSteps
                );

                return new ValueTask<ChecklistDto>(result);
            },
            options: CacheOptions,
            cancellationToken: cancellationToken
        ).ConfigureAwait(false);
    }
}
