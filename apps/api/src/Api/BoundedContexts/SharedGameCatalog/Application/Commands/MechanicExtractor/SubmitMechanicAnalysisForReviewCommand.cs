using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

/// <summary>
/// Transitions a <c>MechanicAnalysis</c> aggregate to <c>InReview</c> (ISSUE-524 / M1.2 follow-up,
/// ADR-051). Allowed source states are <c>Draft</c> (first submission) and <c>Rejected</c>
/// (resubmission after edits). Suppression is orthogonal: a suppressed analysis can still be
/// resubmitted — the status machine and the T5 takedown switch are decoupled by design.
/// </summary>
/// <param name="AnalysisId">Aggregate id to submit.</param>
/// <param name="ActorId">Admin user id sourced from the validated session (never from the body).</param>
internal record SubmitMechanicAnalysisForReviewCommand(Guid AnalysisId, Guid ActorId)
    : ICommand<MechanicAnalysisLifecycleResponseDto>;
