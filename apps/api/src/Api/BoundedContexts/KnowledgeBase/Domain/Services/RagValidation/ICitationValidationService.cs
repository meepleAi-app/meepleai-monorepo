using Api.Models;

#pragma warning disable MA0048 // File name must match type name - Contains Service with Configuration classes
namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Domain service for validating citation accuracy and source references
/// ISSUE-971: BGAI-029 - Citation validation (verify source references)
/// </summary>
internal interface ICitationValidationService
{
    /// <summary>
    /// Validate that citations reference actual source documents
    /// </summary>
    /// <param name="snippets">Citations/snippets to validate</param>
    /// <param name="gameId">Game ID for context</param>
    /// <param name="cancellationToken"></param>
    /// <returns>Validation result with errors if any</returns>
    Task<CitationValidationResult> ValidateCitationsAsync(
        IReadOnlyList<Snippet> snippets,
        string gameId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate a single citation
    /// </summary>
    Task<bool> ValidateSingleCitationAsync(
        Snippet snippet,
        string gameId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of citation validation
/// </summary>
internal record CitationValidationResult
{
    /// <summary>
    /// Whether all citations are valid
    /// </summary>
    public required bool IsValid { get; init; }

    /// <summary>
    /// Total number of citations checked
    /// </summary>
    public required int TotalCitations { get; init; }

    /// <summary>
    /// Number of valid citations
    /// </summary>
    public required int ValidCitations { get; init; }

    /// <summary>
    /// Number of invalid citations
    /// </summary>
    public int InvalidCitations => TotalCitations - ValidCitations;

    /// <summary>
    /// Validation accuracy (0.0-1.0)
    /// </summary>
    public double ValidationAccuracy => TotalCitations > 0
        ? (double)ValidCitations / TotalCitations
        : 1.0;

    /// <summary>
    /// List of validation errors (empty if all valid)
    /// </summary>
    public required List<CitationValidationError> Errors { get; init; }

    /// <summary>
    /// Summary message
    /// </summary>
    public required string Message { get; init; }
}

/// <summary>
/// Citation validation error details
/// </summary>
internal record CitationValidationError
{
    /// <summary>
    /// Citation source that failed validation
    /// </summary>
    public required string Source { get; init; }

    /// <summary>
    /// Page number that failed validation
    /// </summary>
    public required int Page { get; init; }

    /// <summary>
    /// Error message describing the validation failure
    /// </summary>
    public required string ErrorMessage { get; init; }

    /// <summary>
    /// Error type
    /// </summary>
    public required CitationErrorType ErrorType { get; init; }
}

/// <summary>
/// Types of citation validation errors
/// </summary>
internal enum CitationErrorType
{
    /// <summary>
    /// Citation references non-existent PDF document
    /// </summary>
    DocumentNotFound,

    /// <summary>
    /// Page number out of range for document
    /// </summary>
    InvalidPageNumber,

    /// <summary>
    /// Citation text doesn't match source content
    /// </summary>
    TextMismatch,

    /// <summary>
    /// Malformed citation source format
    /// </summary>
    MalformedSource
}
