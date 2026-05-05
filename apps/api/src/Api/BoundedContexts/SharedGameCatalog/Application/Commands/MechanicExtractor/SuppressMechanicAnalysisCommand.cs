using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

/// <summary>
/// Applies the T5 takedown (kill-switch) on a <c>MechanicAnalysis</c> (ISSUE-524 / M1.2
/// follow-up, ADR-051). Suppression is orthogonal to the lifecycle state machine — allowed
/// from any status, including <c>Published</c>. The reason field (20–500 chars) is legally
/// significant and preserved in the audit trail.
/// </summary>
/// <param name="AnalysisId">Aggregate id to suppress.</param>
/// <param name="ActorId">Admin user id sourced from the validated session (never from the body).</param>
/// <param name="Reason">Free-form justification (20–500 chars). Required for the legal evidence chain.</param>
/// <param name="RequestSource">Origin of the takedown request (Email, Legal, Other).</param>
/// <param name="RequestedAt">Optional UTC timestamp of when the third-party takedown notice was received.</param>
internal record SuppressMechanicAnalysisCommand(
    Guid AnalysisId,
    Guid ActorId,
    string Reason,
    SuppressionRequestSource RequestSource,
    DateTime? RequestedAt) : ICommand<MechanicAnalysisLifecycleResponseDto>;
