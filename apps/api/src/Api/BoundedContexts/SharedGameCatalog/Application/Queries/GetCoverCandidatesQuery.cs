using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Epic #3470 (Slice 1c-3) — fetches the admin cover-picker read shape for a game:
/// the materialized source candidates + the current per-context assignments.
/// Returns <see langword="null"/> when the game does not exist (or is soft-deleted).
/// </summary>
public sealed record GetCoverCandidatesQuery(Guid GameId) : IRequest<CoverCandidatesDto?>;
