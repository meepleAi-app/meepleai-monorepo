using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;

namespace Api.BoundedContexts.SharedGameCatalog.Application.DTOs;

/// <summary>
/// Lightweight projection of a <see cref="Domain.Aggregates.MechanicAnalysis"/> returned by
/// <c>GET /api/v1/admin/mechanic-analyses/{id}/status</c> (ISSUE-524 / M1.2). Admin callers use
/// this endpoint to poll a background-running analysis after receiving <c>202 Accepted</c> from
/// the generate endpoint. The payload includes per-section run telemetry (provider/model/tokens/
/// latency) so operators can diagnose mid-pipeline failures without digging through logs.
/// </summary>
public sealed record MechanicAnalysisStatusDto(
    Guid Id,
    Guid SharedGameId,
    Guid PdfDocumentId,
    string PromptVersion,
    MechanicAnalysisStatus Status,
    string? RejectionReason,
    Guid CreatedBy,
    DateTime CreatedAt,
    Guid? ReviewedBy,
    DateTime? ReviewedAt,
    string Provider,
    string ModelUsed,
    int TotalTokensUsed,
    decimal EstimatedCostUsd,
    decimal CostCapUsd,
    bool CostCapOverrideApplied,
    DateTime? CostCapOverrideAt,
    Guid? CostCapOverrideBy,
    string? CostCapOverrideReason,
    bool IsSuppressed,
    DateTime? SuppressedAt,
    Guid? SuppressedBy,
    string? SuppressionReason,
    int ClaimsCount,
    IReadOnlyList<MechanicSectionRunSummaryDto> SectionRuns);

/// <summary>
/// Per-section execution summary persisted in <c>mechanic_analysis_section_runs</c> (B6=C).
/// One row per section attempt; rerunning the pipeline creates a new analysis with its own set
/// of section runs. Exposed on the status endpoint for operator observability.
/// <para>
/// <c>Section</c> values: 0=Summary, 1=Mechanics, 2=Victory, 3=Resources, 4=Phases, 5=Faq.
/// <c>Status</c> values: 0=Succeeded, 1=Failed, 2=SkippedDueToCostCap.
/// </para>
/// </summary>
public sealed record MechanicSectionRunSummaryDto(
    int Section,
    int RunOrder,
    string Provider,
    string ModelUsed,
    int PromptTokens,
    int CompletionTokens,
    int TotalTokens,
    decimal EstimatedCostUsd,
    int LatencyMs,
    int Status,
    string? ErrorMessage,
    DateTime StartedAt,
    DateTime CompletedAt);
