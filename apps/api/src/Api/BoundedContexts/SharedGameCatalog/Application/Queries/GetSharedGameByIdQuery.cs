using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Query to get detailed information about a shared game by ID.
/// </summary>
internal record GetSharedGameByIdQuery(Guid GameId) : IRequest<SharedGameDetailDto?>;
