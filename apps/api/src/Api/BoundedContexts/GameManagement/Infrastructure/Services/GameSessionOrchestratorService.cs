using Api.BoundedContexts.EntityRelationships.Domain.Enums;
using Api.BoundedContexts.EntityRelationships.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Application.DTOs.GameSessionContext;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;

namespace Api.BoundedContexts.GameManagement.Infrastructure.Services;

/// <summary>
/// Cross-context orchestrator that collects data from multiple bounded contexts
/// to build a unified GameSessionContext for the game night experience.
/// Each step uses independent try-catch for fail-open resilience.
/// Issue #5579: GameSessionContext cross-context orchestrator.
/// </summary>
internal sealed class GameSessionOrchestratorService : IGameSessionOrchestratorService
{
    private readonly ILiveSessionRepository _liveSessionRepository;
    private readonly IEntityLinkRepository _entityLinkRepository;
    private readonly IRulebookAnalysisRepository _rulebookAnalysisRepository;
    private readonly IVectorDocumentRepository _vectorDocumentRepository;
    private readonly ISharedGameRepository _sharedGameRepository;
    private readonly ILogger<GameSessionOrchestratorService> _logger;

    public GameSessionOrchestratorService(
        ILiveSessionRepository liveSessionRepository,
        IEntityLinkRepository entityLinkRepository,
        IRulebookAnalysisRepository rulebookAnalysisRepository,
        IVectorDocumentRepository vectorDocumentRepository,
        ISharedGameRepository sharedGameRepository,
        ILogger<GameSessionOrchestratorService> logger)
    {
        _liveSessionRepository = liveSessionRepository ?? throw new ArgumentNullException(nameof(liveSessionRepository));
        _entityLinkRepository = entityLinkRepository ?? throw new ArgumentNullException(nameof(entityLinkRepository));
        _rulebookAnalysisRepository = rulebookAnalysisRepository ?? throw new ArgumentNullException(nameof(rulebookAnalysisRepository));
        _vectorDocumentRepository = vectorDocumentRepository ?? throw new ArgumentNullException(nameof(vectorDocumentRepository));
        _sharedGameRepository = sharedGameRepository ?? throw new ArgumentNullException(nameof(sharedGameRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<GameSessionContextDto> BuildContextAsync(Guid sessionId, CancellationToken ct = default)
    {
        // Step 1: Load live session (mandatory — if this fails, we throw)
        var session = await _liveSessionRepository.GetByIdAsync(sessionId, ct).ConfigureAwait(false)
            ?? throw new NotFoundException("LiveGameSession", sessionId.ToString());

        var primaryGameId = session.GameId;
        var currentPhase = session.GetCurrentPhaseName();

        // Step 2: Collect expansion game IDs via EntityLinks (fail-open)
        var expansionGameIds = await CollectExpansionIdsAsync(primaryGameId, ct).ConfigureAwait(false);

        // Build the all-games list
        var allGameIds = new List<Guid>();
        if (primaryGameId.HasValue)
            allGameIds.Add(primaryGameId.Value);
        allGameIds.AddRange(expansionGameIds);

        // Step 3: Collect vector documents (KB cards) for all games (fail-open)
        var kbCardGameIds = await CollectKbCardGameIdsAsync(allGameIds, ct).ConfigureAwait(false);

        // Step 4: Collect rulebook analysis summaries (fail-open)
        var (primaryRules, expansionRules) = await CollectRulebookAnalysesAsync(
            primaryGameId, expansionGameIds, currentPhase, ct).ConfigureAwait(false);

        // Step 5: Determine missing analysis and games without PDFs
        var gamesWithoutPdf = allGameIds.Where(id => !kbCardGameIds.Contains(id)).ToList();

        var missingAnalysisGameNames = await CollectMissingAnalysisGameNamesAsync(
            expansionGameIds, expansionRules, ct).ConfigureAwait(false);

        // Step 6: Calculate degradation level
        var degradationLevel = CalculateDegradationLevel(
            primaryGameId, kbCardGameIds, expansionGameIds, gamesWithoutPdf);

        return new GameSessionContextDto(
            SessionId: sessionId,
            PrimaryGameId: primaryGameId,
            ExpansionGameIds: expansionGameIds,
            AllGameIds: allGameIds,
            KbCardIds: kbCardGameIds,
            CurrentPhase: currentPhase,
            PrimaryRules: primaryRules,
            ExpansionRules: expansionRules,
            MissingAnalysisGameNames: missingAnalysisGameNames,
            GamesWithoutPdf: gamesWithoutPdf,
            DegradationLevel: degradationLevel);
    }

    /// <inheritdoc />
    public Task<GameSessionContextDto> RefreshContextAsync(Guid sessionId, CancellationToken ct = default)
    {
        // In v1, refresh is identical to build (no caching).
        return BuildContextAsync(sessionId, ct);
    }

    /// <summary>
    /// Collects expansion game IDs linked to the primary game via EntityLink (ExpansionOf).
    /// Returns empty list on failure (fail-open).
    /// </summary>
    private async Task<List<Guid>> CollectExpansionIdsAsync(Guid? primaryGameId, CancellationToken ct)
    {
        if (!primaryGameId.HasValue)
            return new List<Guid>();

        try
        {
            var links = await _entityLinkRepository.GetForEntityAsync(
                MeepleEntityType.Game,
                primaryGameId.Value,
                linkType: EntityLinkType.ExpansionOf,
                cancellationToken: ct).ConfigureAwait(false);

            // Expansion links: source is the expansion, target is the base game.
            // When querying FOR the primary game, expansions appear as:
            //   - SourceEntityId = expansion, TargetEntityId = primary  (source is expansion of target)
            // So we collect SourceEntityId where TargetEntityId matches the primary game.
            var expansionIds = new List<Guid>();
            foreach (var link in links)
            {
                if (link.TargetEntityId == primaryGameId.Value
                    && link.SourceEntityType == MeepleEntityType.Game)
                {
                    expansionIds.Add(link.SourceEntityId);
                }
                else if (link.SourceEntityId == primaryGameId.Value
                         && link.TargetEntityType == MeepleEntityType.Game)
                {
                    // Reverse direction: primary is marked as expansion of something.
                    // This shouldn't normally happen, but collect the other side.
                    expansionIds.Add(link.TargetEntityId);
                }
            }

            return expansionIds;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect expansion links for game {GameId}. Continuing with empty list.", primaryGameId);
            return new List<Guid>();
        }
    }

    /// <summary>
    /// Collects game IDs that have indexed vector documents (KB cards).
    /// Returns empty list on failure (fail-open).
    /// </summary>
    private async Task<List<Guid>> CollectKbCardGameIdsAsync(List<Guid> allGameIds, CancellationToken ct)
    {
        if (allGameIds.Count == 0)
            return new List<Guid>();

        try
        {
            var kbCardGameIds = new List<Guid>();

            foreach (var gameId in allGameIds)
            {
                var docs = await _vectorDocumentRepository.GetByGameIdAsync(gameId, ct).ConfigureAwait(false);
                if (docs.Count > 0)
                {
                    kbCardGameIds.Add(gameId);
                }
            }

            return kbCardGameIds;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect KB card game IDs. Continuing with empty list.");
            return new List<Guid>();
        }
    }

    /// <summary>
    /// Collects rulebook analysis summaries for primary and expansion games.
    /// Returns nulls/empty on failure (fail-open).
    /// </summary>
    private async Task<(RulebookAnalysisSummaryDto? Primary, List<RulebookAnalysisSummaryDto> Expansions)>
        CollectRulebookAnalysesAsync(
            Guid? primaryGameId,
            List<Guid> expansionGameIds,
            string? currentPhaseName,
            CancellationToken ct)
    {
        RulebookAnalysisSummaryDto? primaryRules = null;
        var expansionRules = new List<RulebookAnalysisSummaryDto>();

        // Collect primary game analysis
        if (primaryGameId.HasValue)
        {
            try
            {
                var analyses = await _rulebookAnalysisRepository.GetBySharedGameIdAsync(primaryGameId.Value, ct)
                    .ConfigureAwait(false);

                var activeAnalysis = analyses.FirstOrDefault(a => a.IsActive)
                                    ?? analyses.FirstOrDefault();

                if (activeAnalysis != null)
                {
                    primaryRules = MapToSummary(activeAnalysis, currentPhaseName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to collect rulebook analysis for primary game {GameId}.", primaryGameId);
            }
        }

        // Collect expansion game analyses
        foreach (var expansionId in expansionGameIds)
        {
            try
            {
                var analyses = await _rulebookAnalysisRepository.GetBySharedGameIdAsync(expansionId, ct)
                    .ConfigureAwait(false);

                var activeAnalysis = analyses.FirstOrDefault(a => a.IsActive)
                                    ?? analyses.FirstOrDefault();

                if (activeAnalysis != null)
                {
                    expansionRules.Add(MapToSummary(activeAnalysis, currentPhaseName: null));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to collect rulebook analysis for expansion game {GameId}.", expansionId);
            }
        }

        return (primaryRules, expansionRules);
    }

    /// <summary>
    /// Collects names of expansion games that do not have a rulebook analysis.
    /// Returns empty list on failure (fail-open).
    /// </summary>
    private async Task<List<string>> CollectMissingAnalysisGameNamesAsync(
        List<Guid> expansionGameIds,
        List<RulebookAnalysisSummaryDto> expansionRules,
        CancellationToken ct)
    {
        var missingNames = new List<string>();

        // Find expansion IDs that don't have analysis
        var analyzedGameIds = expansionRules.Select(r => r.GameId).ToHashSet();
        var missingIds = expansionGameIds.Where(id => !analyzedGameIds.Contains(id)).ToList();

        if (missingIds.Count == 0)
            return missingNames;

        try
        {
            var games = await _sharedGameRepository.GetByIdsAsync(missingIds, ct).ConfigureAwait(false);
            foreach (var id in missingIds)
            {
                if (games.TryGetValue(id, out var game))
                {
                    missingNames.Add(game.Title);
                }
                else
                {
                    missingNames.Add($"Unknown game ({id})");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve names for {Count} missing-analysis expansion games.", missingIds.Count);
            // Fall back to IDs as names
            missingNames.AddRange(missingIds.Select(id => $"Unknown game ({id})"));
        }

        return missingNames;
    }

    /// <summary>
    /// Calculates the session degradation level based on available vector documents.
    /// </summary>
    internal static SessionDegradationLevel CalculateDegradationLevel(
        Guid? primaryGameId,
        List<Guid> kbCardGameIds,
        List<Guid> expansionGameIds,
        List<Guid> gamesWithoutPdf)
    {
        // No vector documents at all
        if (kbCardGameIds.Count == 0)
            return SessionDegradationLevel.NoAI;

        // Primary game doesn't have a PDF
        if (primaryGameId.HasValue && !kbCardGameIds.Contains(primaryGameId.Value))
            return SessionDegradationLevel.BasicOnly;

        // Some expansions are missing PDFs
        if (gamesWithoutPdf.Count > 0)
            return SessionDegradationLevel.Partial;

        return SessionDegradationLevel.Full;
    }

    private static RulebookAnalysisSummaryDto MapToSummary(
        SharedGameCatalog.Domain.Entities.RulebookAnalysis analysis,
        string? currentPhaseName)
    {
        return new RulebookAnalysisSummaryDto(
            GameId: analysis.SharedGameId,
            GameTitle: analysis.GameTitle,
            Summary: analysis.Summary,
            KeyMechanics: analysis.KeyMechanics.ToList(),
            CurrentPhaseName: currentPhaseName,
            PhaseNames: analysis.GamePhases.Select(p => p.Name).ToList());
    }
}
