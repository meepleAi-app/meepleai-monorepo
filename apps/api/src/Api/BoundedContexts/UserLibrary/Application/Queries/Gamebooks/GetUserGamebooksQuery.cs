using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Queries.Gamebooks;

/// <summary>
/// Query for `GET /api/v1/gamebooks` (Issue #869).
///
/// Returns the list of gamebook cards for the authenticated user, sourced
/// from PrivateGame aggregates. See <see cref="GamebookCardDataDto"/> for
/// the MVP scope and deferred fields.
/// </summary>
internal sealed record GetUserGamebooksQuery(Guid UserId) : IQuery<IReadOnlyList<GamebookCardDataDto>>;
