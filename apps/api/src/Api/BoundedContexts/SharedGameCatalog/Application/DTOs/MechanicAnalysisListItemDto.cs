using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;

namespace Api.BoundedContexts.SharedGameCatalog.Application.DTOs;

/// <summary>
/// Lightweight discovery row for the admin mechanic-extractor index
/// (spec-panel gap #2 / ADR-051 M1.2).
/// Returned by <c>GET /api/v1/admin/mechanic-analyses</c> so reviewers can browse the
/// recent analyses without having to know the UUID of an aggregate up-front.
/// </summary>
/// <param name="Id">Aggregate primary key.</param>
/// <param name="SharedGameId">Foreign key into the shared game catalog.</param>
/// <param name="GameTitle">Title of the linked shared game (joined for display).</param>
/// <param name="PdfDocumentId">Source PDF document foreign key.</param>
/// <param name="PromptVersion">Prompt template version that produced the analysis.</param>
/// <param name="Status">0=Draft, 1=InReview, 2=Published, 3=Rejected, 4=PartiallyExtracted.</param>
/// <param name="ClaimsCount">Total number of claims (any status) attached to the analysis.</param>
/// <param name="TotalTokensUsed">Sum of LLM tokens consumed by the pipeline.</param>
/// <param name="EstimatedCostUsd">Total LLM cost in USD.</param>
/// <param name="CertificationStatus">0=NotEvaluated, 1=Certified, 2=NotCertified.</param>
/// <param name="IsSuppressed">True when the T5 kill-switch has been applied.</param>
/// <param name="CreatedAt">UTC timestamp of aggregate creation (used as primary sort key).</param>
public sealed record MechanicAnalysisListItemDto(
    Guid Id,
    Guid SharedGameId,
    string GameTitle,
    Guid PdfDocumentId,
    string PromptVersion,
    MechanicAnalysisStatus Status,
    int ClaimsCount,
    int TotalTokensUsed,
    decimal EstimatedCostUsd,
    int CertificationStatus,
    bool IsSuppressed,
    DateTime CreatedAt);

/// <summary>
/// Paged response wrapper for the discovery list endpoint. Plain offset pagination is
/// sufficient — the admin index is bounded (a few hundred rows at most over the lifetime
/// of the project) and the dataset evolves slowly enough that keyset pagination would be
/// overkill.
/// </summary>
/// <param name="Items">Page of analyses, ordered by <c>CreatedAt DESC</c>.</param>
/// <param name="Page">1-based page number echoed back to the caller.</param>
/// <param name="PageSize">Page size echoed back to the caller.</param>
/// <param name="TotalCount">Total number of analyses across every status (suppression filter ignored).</param>
public sealed record MechanicAnalysisListPageDto(
    IReadOnlyList<MechanicAnalysisListItemDto> Items,
    int Page,
    int PageSize,
    int TotalCount);
