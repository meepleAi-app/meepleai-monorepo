using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Application.Queries.PrivateGames;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Queries.PrivateGames;

/// <summary>
/// Handler for listing private games with pagination, search, and sorting.
/// Uses existing repository methods (GetByOwnerIdAsync / SearchByTitleAsync) and
/// applies in-memory sorting + pagination — suitable for typical user library sizes.
/// </summary>
internal sealed class GetPrivateGamesListQueryHandler
    : IQueryHandler<GetPrivateGamesListQuery, PaginatedPrivateGamesResponseDto>
{
    private readonly IPrivateGameRepository _repository;

    public GetPrivateGamesListQueryHandler(IPrivateGameRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<PaginatedPrivateGamesResponseDto> Handle(
        GetPrivateGamesListQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Fetch matching games
        var games = string.IsNullOrWhiteSpace(query.Search)
            ? await _repository.GetByOwnerIdAsync(query.UserId, cancellationToken).ConfigureAwait(false)
            : await _repository.SearchByTitleAsync(query.UserId, query.Search, cancellationToken).ConfigureAwait(false);

        // Sort
        var sorted = (query.SortBy.ToLowerInvariant(), query.SortDirection.ToLowerInvariant()) switch
        {
            ("title", "asc") => games.OrderBy(g => g.Title, StringComparer.OrdinalIgnoreCase),
            ("title", _) => games.OrderByDescending(g => g.Title, StringComparer.OrdinalIgnoreCase),
            ("updatedat", "asc") => games.OrderBy(g => g.UpdatedAt ?? g.CreatedAt),
            ("updatedat", _) => games.OrderByDescending(g => g.UpdatedAt ?? g.CreatedAt),
            (_, "asc") => games.OrderBy(g => g.CreatedAt),     // createdAt asc
            _ => games.OrderByDescending(g => g.CreatedAt), // createdAt desc (default)
        };

        // Paginate
        var totalCount = games.Count;
        var pageSize = query.PageSize;
        var page = query.Page;
        var totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling((double)totalCount / pageSize);

        var items = sorted
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(MapToDto)
            .ToList();

        return new PaginatedPrivateGamesResponseDto(
            Items: items,
            Page: page,
            PageSize: pageSize,
            TotalCount: totalCount,
            TotalPages: totalPages,
            HasNextPage: page < totalPages,
            HasPreviousPage: page > 1
        );
    }

    private static PrivateGameDto MapToDto(Domain.Entities.PrivateGame game) =>
        new(
            Id: game.Id,
            OwnerId: game.OwnerId,
            Source: game.Source.ToString(),
            BggId: game.BggId,
            Title: game.Title,
            YearPublished: game.YearPublished,
            Description: game.Description,
            MinPlayers: game.MinPlayers,
            MaxPlayers: game.MaxPlayers,
            PlayingTimeMinutes: game.PlayingTimeMinutes,
            MinAge: game.MinAge,
            ComplexityRating: game.ComplexityRating,
            ImageUrl: game.ImageUrl,
            ThumbnailUrl: game.ThumbnailUrl,
            CreatedAt: game.CreatedAt,
            UpdatedAt: game.UpdatedAt,
            BggSyncedAt: game.BggSyncedAt,
            CanProposeToCatalog: game.BggId.HasValue,
            AgentDefinitionId: game.AgentDefinitionId
        );
}
