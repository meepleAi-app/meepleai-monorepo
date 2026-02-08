using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Middleware.Exceptions;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.CheckPrivateGameDuplicates;

/// <summary>
/// Handler for checking private game duplicates in the shared catalog.
/// Uses exact matching (BggId) and fuzzy matching (title similarity).
/// Issue #3667: Phase 6 - Admin Review Enhancements.
/// </summary>
internal sealed class CheckPrivateGameDuplicatesQueryHandler : IRequestHandler<CheckPrivateGameDuplicatesQuery, DuplicateCheckResultDto>
{
    private readonly IPrivateGameRepository _privateGameRepository;
    private readonly ISharedGameRepository _sharedGameRepository;
    private readonly ILogger<CheckPrivateGameDuplicatesQueryHandler> _logger;

    public CheckPrivateGameDuplicatesQueryHandler(
        IPrivateGameRepository privateGameRepository,
        ISharedGameRepository sharedGameRepository,
        ILogger<CheckPrivateGameDuplicatesQueryHandler> logger)
    {
        _privateGameRepository = privateGameRepository ?? throw new ArgumentNullException(nameof(privateGameRepository));
        _sharedGameRepository = sharedGameRepository ?? throw new ArgumentNullException(nameof(sharedGameRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<DuplicateCheckResultDto> Handle(
        CheckPrivateGameDuplicatesQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        _logger.LogInformation(
            "Checking duplicates for PrivateGame {PrivateGameId}",
            query.PrivateGameId);

        // 1. Get the private game
        var privateGame = await _privateGameRepository.GetByIdAsync(
            query.PrivateGameId,
            cancellationToken).ConfigureAwait(false);

        if (privateGame == null)
        {
            throw new NotFoundException("PrivateGame", query.PrivateGameId.ToString());
        }

        // 2. Check for exact duplicate by BggId
        Guid? exactDuplicateId = null;
        string? exactDuplicateTitle = null;
        bool hasExactDuplicate = false;

        if (privateGame.BggId.HasValue)
        {
            var exactMatch = await _sharedGameRepository.GetByBggIdAsync(
                privateGame.BggId.Value,
                cancellationToken).ConfigureAwait(false);

            if (exactMatch != null)
            {
                hasExactDuplicate = true;
                exactDuplicateId = exactMatch.Id;
                exactDuplicateTitle = exactMatch.Title;

                _logger.LogInformation(
                    "Found exact BggId match: PrivateGame {PrivateGameId} (BggId: {BggId}) matches SharedGame {SharedGameId}",
                    privateGame.Id, privateGame.BggId, exactMatch.Id);
            }
        }

        // 3. Check for fuzzy duplicates by title similarity
        // Note: Fuzzy search implementation is placeholder for Phase 6.1
        // Full implementation would require DbContext access to query SharedGames by title
        var fuzzyDuplicates = new List<FuzzyDuplicateDto>();

        // 4. Determine recommended action
        var recommendedAction = DetermineRecommendedAction(
            hasExactDuplicate,
            fuzzyDuplicates.Count > 0);

        _logger.LogInformation(
            "Duplicate check complete for PrivateGame {PrivateGameId}: ExactMatch={HasExact}, FuzzyMatches={FuzzyCount}, Recommended={Action}",
            privateGame.Id, hasExactDuplicate, fuzzyDuplicates.Count, recommendedAction);

        return new DuplicateCheckResultDto(
            HasExactDuplicate: hasExactDuplicate,
            ExactDuplicateId: exactDuplicateId,
            ExactDuplicateTitle: exactDuplicateTitle,
            HasFuzzyDuplicates: fuzzyDuplicates.Count > 0,
            FuzzyDuplicates: fuzzyDuplicates,
            RecommendedAction: recommendedAction);
    }

    /// <summary>
    /// Determines the recommended approval action based on duplicate detection results.
    /// </summary>
    private static ProposalApprovalAction DetermineRecommendedAction(
        bool hasExactDuplicate,
        bool hasFuzzyDuplicates)
    {
        if (hasExactDuplicate)
        {
            // Exact match found - recommend merging knowledge base
            return ProposalApprovalAction.MergeKnowledgeBase;
        }

        if (hasFuzzyDuplicates)
        {
            // Similar games found - recommend creating as variant
            return ProposalApprovalAction.ApproveAsVariant;
        }

        // No duplicates - approve as new game
        return ProposalApprovalAction.ApproveAsNew;
    }
}
