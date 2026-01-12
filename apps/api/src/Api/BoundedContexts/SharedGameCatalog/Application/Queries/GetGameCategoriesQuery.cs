using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

public sealed record GameCategoryDto(Guid Id, string Name, string Slug);

internal record GetGameCategoriesQuery : IRequest<List<GameCategoryDto>>;
