using Api.BoundedContexts.GameManagement.Application.DTOs.GameNights;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.GameNights;

/// <summary>
/// Query for the candidate-vote tally of a game night — Issue #2700.
/// <see cref="CallerUserId"/> scopes the read to participants (organizer or invited) and
/// drives the per-candidate <c>VotedByMe</c> flag.
/// </summary>
internal record GetGameNightVoteTallyQuery(Guid GameNightId, Guid CallerUserId)
    : IQuery<GameNightVoteTallyDto>;
