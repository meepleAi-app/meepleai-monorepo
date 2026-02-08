using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.CheckPrivateGameDuplicates;

/// <summary>
/// Query to check if a private game has duplicates in the shared catalog.
/// Returns both exact matches (by BggId) and fuzzy matches (by title similarity).
/// Issue #3667: Phase 6 - Admin Review Enhancements.
/// </summary>
/// <param name="PrivateGameId">ID of the private game to check for duplicates</param>
public record CheckPrivateGameDuplicatesQuery(Guid PrivateGameId) : IRequest<DuplicateCheckResultDto>;
