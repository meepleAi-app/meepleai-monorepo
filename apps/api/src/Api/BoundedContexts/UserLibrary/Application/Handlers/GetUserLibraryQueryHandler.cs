using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Handlers;

/// <summary>
/// Handler for getting paginated user library.
/// </summary>
internal class GetUserLibraryQueryHandler : IQueryHandler<GetUserLibraryQuery, PaginatedLibraryResponseDto>
{
    private readonly IUserLibraryRepository _libraryRepository;
    private readonly IGameRepository _gameRepository;

    public GetUserLibraryQueryHandler(
        IUserLibraryRepository libraryRepository,
        IGameRepository gameRepository)
    {
        _libraryRepository = libraryRepository ?? throw new ArgumentNullException(nameof(libraryRepository));
        _gameRepository = gameRepository ?? throw new ArgumentNullException(nameof(gameRepository));
    }

    public async Task<PaginatedLibraryResponseDto> Handle(GetUserLibraryQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var (entries, total) = await _libraryRepository.GetUserLibraryPaginatedAsync(
            query.UserId,
            query.Search,
            query.FavoritesOnly,
            query.SortBy,
            query.Descending,
            query.Page,
            query.PageSize,
            cancellationToken
        ).ConfigureAwait(false);

        // Get game details for each entry
        var entryDtos = new List<UserLibraryEntryDto>();
        foreach (var entry in entries)
        {
            var game = await _gameRepository.GetByIdAsync(entry.GameId, cancellationToken).ConfigureAwait(false);
            if (game != null)
            {
                entryDtos.Add(new UserLibraryEntryDto(
                    Id: entry.Id,
                    UserId: entry.UserId,
                    GameId: entry.GameId,
                    GameTitle: game.Title.Value,
                    GamePublisher: game.Publisher?.Name,
                    GameYearPublished: game.YearPublished?.Value,
                    GameIconUrl: game.IconUrl,
                    GameImageUrl: game.ImageUrl,
                    AddedAt: entry.AddedAt,
                    Notes: entry.Notes?.Value,
                    IsFavorite: entry.IsFavorite
                ));
            }
        }

        var totalPages = (int)Math.Ceiling((double)total / query.PageSize);

        return new PaginatedLibraryResponseDto(
            Entries: entryDtos,
            Total: total,
            Page: query.Page,
            PageSize: query.PageSize,
            TotalPages: totalPages
        );
    }
}
