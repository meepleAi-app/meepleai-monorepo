using Api.BoundedContexts.GameManagement.Application.DTOs.GameNights;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.GameNights;

/// <summary>
/// #2633 Slice A: the night-live read model — the night header + its session progression.
/// Any authenticated user can read it (mirrors <c>GetGameNightByIdQuery</c>).
/// </summary>
internal record GetGameNightLiveQuery(Guid GameNightId) : IQuery<GameNightLiveDto>;
