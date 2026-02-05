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

        // Issue #3651: Parallel fetch for all stats
        var totalGamesTask = _libraryRepository.GetUserLibraryCountAsync(query.UserId, cancellationToken);
        var favoriteGamesTask = _libraryRepository.GetFavoriteCountAsync(query.UserId, cancellationToken);
        var privatePdfsTask = _libraryRepository.GetPrivatePdfCountAsync(query.UserId, cancellationToken);
        var dateRangeTask = _libraryRepository.GetLibraryDateRangeAsync(query.UserId, cancellationToken);

        await Task.WhenAll(totalGamesTask, favoriteGamesTask, privatePdfsTask, dateRangeTask).ConfigureAwait(false);

        var (oldest, newest) = await dateRangeTask.ConfigureAwait(false);

        return new UserLibraryStatsDto(
            TotalGames: await totalGamesTask.ConfigureAwait(false),
            FavoriteGames: await favoriteGamesTask.ConfigureAwait(false),
            PrivatePdfs: await privatePdfsTask.ConfigureAwait(false),
            OldestAddedAt: oldest,
            NewestAddedAt: newest
        );
    }
}
