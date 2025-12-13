using Api.Infrastructure;
using Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Domain service for validating citation accuracy and source references
/// ISSUE-971: BGAI-029 - Citation validation (verify source references)
/// </summary>
/// <remarks>
/// Validates that AI-generated citations reference actual source documents:
/// - PDF documents exist in database
/// - Page numbers are within valid range
/// - Source format is correct (PDF:guid)
///
/// Prevents hallucinated citations and ensures response quality.
/// </remarks>
public class CitationValidationService : ICitationValidationService
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<CitationValidationService> _logger;

    public CitationValidationService(
        MeepleAiDbContext dbContext,
        ILogger<CitationValidationService> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<CitationValidationResult> ValidateCitationsAsync(
        IReadOnlyList<Snippet> snippets,
        string gameId,
        CancellationToken cancellationToken = default)
    {
        if (snippets == null || snippets.Count == 0)
        {
            return new CitationValidationResult
            {
                IsValid = true,
                TotalCitations = 0,
                ValidCitations = 0,
                Errors = new List<CitationValidationError>(),
                Message = "No citations to validate"
            };
        }

        var errors = new List<CitationValidationError>();
        var validCount = 0;

        // Parse game ID
        if (!Guid.TryParse(gameId, out var gameGuid))
        {
            _logger.LogError("Invalid game ID format for citation validation: {GameId}", gameId);
            return new CitationValidationResult
            {
                IsValid = false,
                TotalCitations = snippets.Count,
                ValidCitations = 0,
                Errors = new List<CitationValidationError>
                {
                    new CitationValidationError
                    {
                        Source = gameId,
                        Page = 0,
                        ErrorMessage = "Invalid game ID format",
                        ErrorType = CitationErrorType.MalformedSource
                    }
                },
                Message = "Invalid game ID format"
            };
        }

        // Get all PDF documents for this game (single query for efficiency)
        var pdfDocuments = await _dbContext.PdfDocuments
            .AsNoTracking()
            .Where(p => p.GameId == gameGuid)
            .Select(p => new { p.Id, p.PageCount })
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var pdfDict = pdfDocuments.ToDictionary(p => p.Id.ToString(), p => p.PageCount ?? 0, StringComparer.Ordinal);

        // Validate each citation
        foreach (var snippet in snippets)
        {
            var isValid = await ValidateSingleCitationInternalAsync(
                snippet,
                pdfDict,
                errors,
                cancellationToken).ConfigureAwait(false);

            if (isValid)
            {
                validCount++;
            }
        }

        var totalCount = snippets.Count;
        var isAllValid = errors.Count == 0;

        var message = isAllValid
            ? $"All {totalCount} citations valid"
            : $"{validCount}/{totalCount} citations valid ({errors.Count} errors)";

        _logger.LogInformation(
            "Citation validation: {ValidCount}/{TotalCount} valid (game: {GameId})",
            validCount, totalCount, gameId);

        return new CitationValidationResult
        {
            IsValid = isAllValid,
            TotalCitations = totalCount,
            ValidCitations = validCount,
            Errors = errors,
            Message = message
        };
    }

    /// <inheritdoc/>
    public async Task<bool> ValidateSingleCitationAsync(
        Snippet snippet,
        string gameId,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(gameId, out var gameGuid))
        {
            return false;
        }

        var pdfDocuments = await _dbContext.PdfDocuments
            .AsNoTracking()
            .Where(p => p.GameId == gameGuid)
            .Select(p => new { p.Id, p.PageCount })
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var pdfDict = pdfDocuments.ToDictionary(p => p.Id.ToString(), p => p.PageCount ?? 0, StringComparer.Ordinal);
        var errors = new List<CitationValidationError>();

        return await ValidateSingleCitationInternalAsync(snippet, pdfDict, errors, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Internal validation logic for a single citation
    /// </summary>
    private Task<bool> ValidateSingleCitationInternalAsync(
        Snippet snippet,
        Dictionary<string, int> pdfDict,
        List<CitationValidationError> errors,
        CancellationToken _)
    {
        // Parse source format (expected: "PDF:guid")
        if (string.IsNullOrWhiteSpace(snippet.source))
        {
            errors.Add(new CitationValidationError
            {
                Source = snippet.source ?? "null",
                Page = snippet.page,
                ErrorMessage = "Empty or null citation source",
                ErrorType = CitationErrorType.MalformedSource
            });
            return Task.FromResult(false);
        }

        var sourceParts = snippet.source.Split(':', 2);
        if (sourceParts.Length != 2 || !string.Equals(sourceParts[0], "PDF", StringComparison.Ordinal))
        {
            errors.Add(new CitationValidationError
            {
                Source = snippet.source,
                Page = snippet.page,
                ErrorMessage = $"Invalid source format (expected 'PDF:guid', got '{snippet.source}')",
                ErrorType = CitationErrorType.MalformedSource
            });
            return Task.FromResult(false);
        }

        var pdfId = sourceParts[1];

        // Check if PDF document exists
        if (!pdfDict.TryGetValue(pdfId, out var totalPages))
        {
            errors.Add(new CitationValidationError
            {
                Source = snippet.source,
                Page = snippet.page,
                ErrorMessage = $"PDF document not found: {pdfId}",
                ErrorType = CitationErrorType.DocumentNotFound
            });

            _logger.LogWarning(
                "Invalid citation: PDF document not found (source: {Source}, page: {Page})",
                snippet.source, snippet.page);

            return Task.FromResult(false);
        }

        // Validate page number
        if (snippet.page < 1 || snippet.page > totalPages)
        {
            errors.Add(new CitationValidationError
            {
                Source = snippet.source,
                Page = snippet.page,
                ErrorMessage = $"Invalid page number {snippet.page} (document has {totalPages} pages)",
                ErrorType = CitationErrorType.InvalidPageNumber
            });

            _logger.LogWarning(
                "Invalid citation: Page {Page} out of range for PDF {PdfId} (total pages: {TotalPages})",
                snippet.page, pdfId, totalPages);

            return Task.FromResult(false);
        }

        // Citation is valid
        _logger.LogDebug(
            "Valid citation: {Source}, page {Page}",
            snippet.source, snippet.page);

        return Task.FromResult(true);
    }
}
