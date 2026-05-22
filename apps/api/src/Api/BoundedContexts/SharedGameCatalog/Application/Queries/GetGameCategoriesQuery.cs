using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Public DTO for game categories.
/// Issue #1440: extended with Emoji + Color for admin/filter UI rendering.
/// GameCount is null on the public surface and populated on admin queries only.
/// </summary>
public sealed record GameCategoryDto(
    Guid Id,
    string Name,
    string Slug,
    string? Emoji = null,
    string? Color = null,
    int? GameCount = null);

internal record GetGameCategoriesQuery : IRequest<List<GameCategoryDto>>;
