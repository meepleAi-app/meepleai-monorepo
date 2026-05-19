using Api.BoundedContexts.GameManagement.Application.DTOs.GameNights;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.GameNights;

/// <summary>
/// Query to retrieve the current user's "regulars" — registered users they
/// have invited to past game nights ranked by event count.
/// Issue #950 (W1-PR2). Spec §7b.2.
/// </summary>
/// <param name="UserId">Organizer identifier (current authenticated user).</param>
/// <param name="Limit">Maximum entries to return. Clamped to [1, 30] by the validator at the endpoint layer.</param>
internal sealed record GetRegularsQuery(
    Guid UserId,
    int Limit = 10) : IQuery<IReadOnlyList<RegularDto>>;
