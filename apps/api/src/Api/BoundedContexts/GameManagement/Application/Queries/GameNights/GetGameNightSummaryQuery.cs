using Api.BoundedContexts.GameManagement.Application.DTOs.GameNights;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.GameNights;

/// <summary>
/// Query for a game night's post-finalize summary — Issue #2702. Participant-scoped
/// (organizer or invited); <see cref="CallerUserId"/> also drives organiser-only fields.
/// </summary>
internal record GetGameNightSummaryQuery(Guid GameNightId, Guid CallerUserId)
    : IQuery<GameNightSummaryDto>;
