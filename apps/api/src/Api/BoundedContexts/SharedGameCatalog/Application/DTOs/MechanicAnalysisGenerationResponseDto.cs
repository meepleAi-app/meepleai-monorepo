using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;

namespace Api.BoundedContexts.SharedGameCatalog.Application.DTOs;

/// <summary>
/// Response returned by <c>POST /api/v1/admin/mechanic-analyses</c> (ISSUE-524 / M1.2).
/// The generation pipeline is asynchronous (B5=B): the endpoint returns 202 Accepted with
/// this body immediately after the Draft aggregate is persisted and the background job is
/// enqueued. Clients should poll <see cref="StatusUrl"/> (or subscribe to SignalR) to learn
/// when the analysis transitions out of <see cref="MechanicAnalysisStatus.Draft"/>.
/// </summary>
/// <param name="Id">The newly-created (or pre-existing, on idempotent retry) analysis id.</param>
/// <param name="Status">Aggregate status at the moment the endpoint responded. Always <c>Draft</c>
///   for a brand-new run; on idempotent short-circuit this reflects the persisted state of the
///   existing analysis (<c>Draft</c>, <c>InReview</c>, or <c>Published</c>).</param>
/// <param name="PromptVersion">Prompt version used for generation (e.g. <c>"v1.0.0"</c>).</param>
/// <param name="CostCapUsd">Effective cost cap in USD enforced for this run. If a
///   <c>CostCapOverride</c> was supplied, this equals the override amount; otherwise the original
///   admin-submitted cap.</param>
/// <param name="EstimatedCostUsd">Conservative upper-bound projection computed by the cost
///   estimator before the run starts. Real cost (captured on per-section runs) is typically lower.</param>
/// <param name="ProjectedTotalTokens">Projected total tokens (prompt + completion) for all six
///   sections. Informational only — not a hard limit.</param>
/// <param name="CostCapOverrideApplied">True when the admin explicitly overrode the default cap
///   at planning time, carrying the admin's justification into the aggregate's audit trail.</param>
/// <param name="StatusUrl">Relative URL the client should poll to observe pipeline progress,
///   e.g. <c>/api/v1/admin/mechanic-analyses/{id}/status</c>.</param>
/// <param name="IsExistingAnalysis">True when T7 idempotency short-circuited a duplicate request;
///   callers should treat the response as "already running / already completed" rather than a new
///   run.</param>
public sealed record MechanicAnalysisGenerationResponseDto(
    Guid Id,
    MechanicAnalysisStatus Status,
    string PromptVersion,
    decimal CostCapUsd,
    decimal EstimatedCostUsd,
    int ProjectedTotalTokens,
    bool CostCapOverrideApplied,
    string StatusUrl,
    bool IsExistingAnalysis);
