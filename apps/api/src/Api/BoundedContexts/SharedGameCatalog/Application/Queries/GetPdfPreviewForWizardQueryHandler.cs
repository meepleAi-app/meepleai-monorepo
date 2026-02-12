using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.Infrastructure;
using Api.Models;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Handler for GetPdfPreviewForWizardQuery.
/// Orchestrates metadata extraction, BGG match suggestions, and duplicate detection
/// for uploaded PDF in the game creation wizard flow.
/// Issue #4139: Backend - API Endpoints PDF Wizard
/// </summary>
internal sealed class GetPdfPreviewForWizardQueryHandler : IQueryHandler<GetPdfPreviewForWizardQuery, PdfGamePreviewDto>
{
    private readonly IMediator _mediator;
    private readonly IBggApiService _bggApiService;
    private readonly MeepleAiDbContext _context;
    private readonly ILogger<GetPdfPreviewForWizardQueryHandler> _logger;

    private const int MaxBggSuggestions = 5;

    public GetPdfPreviewForWizardQueryHandler(
        IMediator mediator,
        IBggApiService bggApiService,
        MeepleAiDbContext context,
        ILogger<GetPdfPreviewForWizardQueryHandler> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _bggApiService = bggApiService ?? throw new ArgumentNullException(nameof(bggApiService));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PdfGamePreviewDto> Handle(
        GetPdfPreviewForWizardQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var requestId = Guid.NewGuid().ToString("N")[..8];

        _logger.LogInformation(
            "[{RequestId}] Generating wizard preview for PDF: FilePath={FilePath}, UserId={UserId}",
            requestId, query.FilePath, query.UserId);

        // Step 1: Extract game metadata from PDF using AI
        var extractQuery = new ExtractGameMetadataFromPdfQuery(query.FilePath, query.UserId);
        var metadata = await _mediator.Send(extractQuery, cancellationToken).ConfigureAwait(false);

        // If extraction completely failed, return empty preview
        if (string.IsNullOrWhiteSpace(metadata.Title))
        {
            _logger.LogWarning(
                "[{RequestId}] Metadata extraction failed or returned empty title",
                requestId);

            return new PdfGamePreviewDto
            {
                ExtractedMetadata = metadata,
                BggSuggestions = [],
                HasDuplicateWarning = false,
                DuplicateTitles = [],
                ExtractionConfidence = metadata.ConfidenceScore,
                MeetsQualityThreshold = false
            };
        }

        // Step 2: Fetch BGG match suggestions (top 5) based on extracted title
        List<BggSearchResultDto>? bggSuggestions = null;
        try
        {
            var allMatches = await _bggApiService.SearchGamesAsync(
                metadata.Title,
                exact: false,
                cancellationToken).ConfigureAwait(false);

            bggSuggestions = allMatches.Take(MaxBggSuggestions).ToList();

            _logger.LogInformation(
                "[{RequestId}] BGG search completed: Found {Count} suggestions for title '{Title}'",
                requestId, bggSuggestions.Count, metadata.Title);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex,
                "[{RequestId}] BGG search failed for title '{Title}'. Proceeding without suggestions.",
                requestId, metadata.Title);
            // Continue without BGG suggestions - not critical for preview
        }

        // Step 3: Check for duplicate games by title similarity
        var duplicateWarnings = await CheckDuplicatesByTitleAsync(
            metadata.Title,
            requestId,
            cancellationToken).ConfigureAwait(false);

        // Step 4: Build preview DTO with new structure from #4138
        var preview = new PdfGamePreviewDto
        {
            ExtractedMetadata = metadata,
            BggSuggestions = bggSuggestions ?? [],
            HasDuplicateWarning = duplicateWarnings.Count > 0,
            DuplicateTitles = duplicateWarnings,
            ExtractionConfidence = metadata.ConfidenceScore,
            MeetsQualityThreshold = metadata.ConfidenceScore >= 0.50
        };

        _logger.LogInformation(
            "[{RequestId}] Wizard preview generated: Title='{Title}', Quality={Quality}, BggSuggestions={BggCount}, Duplicates={DuplicateCount}",
            requestId, metadata.Title, metadata.ConfidenceScore, bggSuggestions?.Count ?? 0, duplicateWarnings.Count);

        return preview;
    }

    /// <summary>
    /// Checks for existing shared games with similar titles using case-insensitive LIKE matching.
    /// Returns list of warning messages for duplicate detection UI.
    /// </summary>
    private async Task<List<string>> CheckDuplicatesByTitleAsync(
        string extractedTitle,
        string requestId,
        CancellationToken cancellationToken)
    {
        try
        {
            // Search for games with similar titles (case-insensitive partial match)
            var similarGames = await _context.SharedGames
                .AsNoTracking()
                .Where(g => EF.Functions.Like(g.Title, $"%{extractedTitle}%"))
                .Select(g => new { g.Title, g.BggId })
                .Take(5) // Limit to top 5 matches
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (similarGames.Count == 0)
            {
                _logger.LogInformation(
                    "[{RequestId}] No duplicate games found for title '{Title}'",
                    requestId, extractedTitle);
                return [];
            }

            _logger.LogWarning(
                "[{RequestId}] Found {Count} potential duplicates for title '{Title}'",
                requestId, similarGames.Count, extractedTitle);

            return similarGames
                .Select(g => $"'{g.Title}'" + (g.BggId.HasValue ? $" (BGG ID: {g.BggId})" : string.Empty))
                .ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex,
                "[{RequestId}] Error checking duplicates for title '{Title}'. Proceeding without duplicate warnings.",
                requestId, extractedTitle);

            // Return empty list on error - duplicate check is not critical for wizard flow
            return [];
        }
    }
}
