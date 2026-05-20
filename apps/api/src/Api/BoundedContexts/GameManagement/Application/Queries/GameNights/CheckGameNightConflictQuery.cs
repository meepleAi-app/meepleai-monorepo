using Api.BoundedContexts.GameManagement.Application.DTOs.GameNights;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.GameNights;

/// <summary>
/// Query to check whether the current user already has a game night scheduled
/// within ±2 hours of <paramref name="ProposedAt"/>.
/// Issue #950 (W1-PR2). Spec §7b.3.
/// </summary>
/// <param name="UserId">Current authenticated user.</param>
/// <param name="ProposedAt">Proposed start time of the new game night.</param>
internal sealed record CheckGameNightConflictQuery(
    Guid UserId,
    DateTimeOffset ProposedAt) : IQuery<ConflictCheckDto>;
