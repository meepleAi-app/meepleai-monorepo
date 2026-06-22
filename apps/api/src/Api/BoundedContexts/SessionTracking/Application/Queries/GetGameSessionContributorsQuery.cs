using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SessionTracking.Application.Queries;

/// <summary>
/// Issue #2036 — Query the top session contributors for a game (i.e. registered
/// users with at least one finalized session), ordered by descending session
/// count. Used by the SessionContributorsStrip avatar component on the game-
/// detail page (social proof). Public read.
///
/// Distinct from <c>GameContributor</c> (Issue #2746): that one tracks
/// catalog-sharing contributions (toolkit/agent/KB publishing) and lives under
/// the SharedGameCatalog BC. The two surface different "contributor" semantics
/// on the same UI strip.
/// </summary>
/// <param name="GameId">Shared catalog game identifier.</param>
/// <param name="Limit">
/// Maximum contributors to return. Clamped to <c>[1, 50]</c> in the handler;
/// the FE default and mockup show 8.
/// </param>
internal record GetGameSessionContributorsQuery(Guid GameId, int Limit = 8)
    : IQuery<IReadOnlyList<SessionContributorDto>>;
