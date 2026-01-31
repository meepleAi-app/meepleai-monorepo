using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

public sealed record GameMechanicDto(Guid Id, string Name, string Slug);

internal record GetGameMechanicsQuery : IRequest<List<GameMechanicDto>>;
