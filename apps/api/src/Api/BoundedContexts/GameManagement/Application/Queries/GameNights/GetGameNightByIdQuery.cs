using Api.BoundedContexts.GameManagement.Application.DTOs.GameNights;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.GameNights;

/// <summary>
/// Query to get a game night by its ID.
/// Issue #46: GameNight API endpoints.
/// #2698: <see cref="CallerUserId"/> scopes the read to participants (organizer or invited),
/// closing the cross-tenant IDOR on the detail read.
/// </summary>
internal record GetGameNightByIdQuery(Guid GameNightId, Guid CallerUserId) : IQuery<GameNightDto>;
