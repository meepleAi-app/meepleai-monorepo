using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;

namespace Api.BoundedContexts.SharedGameCatalog.Application.DTOs;

/// <summary>
/// DTO for duplicate check results when reviewing private game proposals.
/// Issue #3667: Phase 6 - Admin Review Enhancements.
/// </summary>
/// <param name="HasExactDuplicate">True if a SharedGame exists with the same BggId</param>
/// <param name="ExactDuplicateId">ID of the exact duplicate SharedGame</param>
/// <param name="ExactDuplicateTitle">Title of the exact duplicate</param>
/// <param name="HasFuzzyDuplicates">True if similar games exist based on title/metadata</param>
/// <param name="FuzzyDuplicates">List of potential duplicate games</param>
/// <param name="RecommendedAction">Recommended approval action based on duplicates</param>
public record DuplicateCheckResultDto(
    bool HasExactDuplicate,
    Guid? ExactDuplicateId,
    string? ExactDuplicateTitle,
    bool HasFuzzyDuplicates,
    List<FuzzyDuplicateDto> FuzzyDuplicates,
    ProposalApprovalAction RecommendedAction
);

/// <summary>
/// DTO for a fuzzy duplicate candidate.
/// </summary>
/// <param name="SharedGameId">ID of the candidate game</param>
/// <param name="Title">Title of the candidate</param>
/// <param name="YearPublished">Year published</param>
/// <param name="SimilarityScore">Similarity score (0-100)</param>
public record FuzzyDuplicateDto(
    Guid SharedGameId,
    string Title,
    int? YearPublished,
    int SimilarityScore
);
