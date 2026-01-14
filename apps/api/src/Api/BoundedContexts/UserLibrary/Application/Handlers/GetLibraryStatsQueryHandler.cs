using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Handlers;

/// <summary>
/// Handler for getting library statistics.
/// </summary>
internal class GetLibraryStatsQueryHandler : IQueryHandler<GetLibraryStatsQuery, UserLibraryStatsDto>
{
    private readonly IUserLibraryRepository _libraryRepository;

    public GetLibraryStatsQueryHandler(IUserLibraryRepository libraryRepository)
    {
        _libraryRepository = libraryRepository ?? throw new ArgumentNullException(nameof(libraryRepository));
    }

    public async Task<UserLibraryStatsDto> Handle(GetLibraryStatsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var totalGames = await _libraryRepository.GetUserLibraryCountAsync(query.UserId, cancellationToken)
            .ConfigureAwait(false);

        var favoriteGames = await _libraryRepository.GetFavoriteCountAsync(query.UserId, cancellationToken)
            .ConfigureAwait(false);

        var (oldest, newest) = await _libraryRepository.GetLibraryDateRangeAsync(query.UserId, cancellationToken)
            .ConfigureAwait(false);

        return new UserLibraryStatsDto(
            TotalGames: totalGames,
            FavoriteGames: favoriteGames,
            OldestAddedAt: oldest,
            NewestAddedAt: newest
        );
    }
}
