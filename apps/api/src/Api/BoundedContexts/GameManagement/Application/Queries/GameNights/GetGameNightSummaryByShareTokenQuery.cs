using Api.BoundedContexts.GameManagement.Application.DTOs.GameNights;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.GameNights;

/// <summary>
/// Public (anonymous) query for a shared game-night summary by its token — Issue #2702.
/// Possession of the token is the authorisation; no user identity is consulted.
/// </summary>
internal record GetGameNightSummaryByShareTokenQuery(string ShareToken)
    : IQuery<GameNightSummaryDto>;
