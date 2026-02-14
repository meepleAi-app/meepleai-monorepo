using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.Models;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using MediatR;

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
    private readonly ILogger<GetPdfPreviewForWizardQueryHandler> _logger;

    private const int MaxBggSuggestions = 5;

    public GetPdfPreviewForWizardQueryHandler(
        IMediator mediator,
        IBggApiService bggApiService,
        ILogger<GetPdfPreviewForWizardQueryHandler> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _bggApiService = bggApiService ?? throw new ArgumentNullException(nameof(bggApiService));
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

        // Step 3: Check for duplicate games using enhanced detection (BggId + fuzzy title matching)
        var duplicateCheckQuery = new CheckDuplicateGameQuery(
            Title: metadata.Title,
            BggId: bggSuggestions?.FirstOrDefault()?.BggId); // Use top BGG suggestion if available

        var duplicateResult = await _mediator.Send(duplicateCheckQuery, cancellationToken).ConfigureAwait(false);

        // Build duplicate warning messages for UI display
        var duplicateWarnings = new List<string>();

        if (duplicateResult.HasExactDuplicate && duplicateResult.ExactDuplicateTitle != null)
        {
            duplicateWarnings.Add($"⚠️ Exact match found: '{duplicateResult.ExactDuplicateTitle}' (BGG ID: {bggSuggestions?.FirstOrDefault()?.BggId})");
        }

        foreach (var fuzzyDup in duplicateResult.FuzzyDuplicates)
        {
            duplicateWarnings.Add($"Similar: '{fuzzyDup.Title}' ({fuzzyDup.SimilarityScore}% match)");
        }

        _logger.LogInformation(
            "[{RequestId}] Duplicate detection complete: ExactMatch={HasExact}, FuzzyMatches={FuzzyCount}",
            requestId, duplicateResult.HasExactDuplicate, duplicateResult.FuzzyDuplicates.Count);

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

}
