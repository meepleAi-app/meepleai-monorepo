using Api.BoundedContexts.GameManagement.Application.DTOs.GameNights;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.GameNights;

/// <summary>
/// #2633 Slice A: the night-live read model — the night header + its session progression.
/// Restricted to participants (organizer or an invited player) — unlike the lean
/// <c>GetGameNightByIdQuery</c>, this exposes per-session <c>WinnerId</c> (user GUIDs) + progression,
/// so it must not be readable by arbitrary authenticated users (code-review finding).
/// </summary>
internal record GetGameNightLiveQuery(Guid GameNightId, Guid CallerUserId) : IQuery<GameNightLiveDto>;
