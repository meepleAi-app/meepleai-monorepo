using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Query the current status of a KB reindex job. Issue #941 / ADR-057.
/// </summary>
/// <param name="GameId">The game the job belongs to (authorization scope).</param>
/// <param name="JobId">The job to look up.</param>
/// <param name="RequestingUserId">User performing the poll (authorization).</param>
internal record GetKbReindexJobStatusQuery(Guid GameId, Guid JobId, Guid RequestingUserId)
    : IQuery<KbReindexJobStatusDto?>;

/// <summary>
/// Polling-friendly snapshot of a KB reindex job.
/// </summary>
public record KbReindexJobStatusDto(
    Guid JobId,
    Guid GameId,
    string Status,
    int TotalPdfs,
    int ProcessedPdfs,
    DateTime CreatedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    string? FailureReason);
