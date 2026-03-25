using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Queries;

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

        // Sequential fetch — all calls share the same IUserLibraryRepository (and its DbContext)
        // which is scoped per request. DbContext is not thread-safe, so Task.WhenAll would fail.
        // To parallelize, each call would need its own IServiceScope — not worth the complexity here.
        var totalGames = await _libraryRepository.GetUserLibraryCountAsync(query.UserId, cancellationToken).ConfigureAwait(false);
        var favoriteGames = await _libraryRepository.GetFavoriteCountAsync(query.UserId, cancellationToken).ConfigureAwait(false);
        var privatePdfs = await _libraryRepository.GetPrivatePdfCountAsync(query.UserId, cancellationToken).ConfigureAwait(false);
        var (oldest, newest) = await _libraryRepository.GetLibraryDateRangeAsync(query.UserId, cancellationToken).ConfigureAwait(false);
        var stateCounts = await _libraryRepository.GetStateCountsAsync(query.UserId, cancellationToken).ConfigureAwait(false);

        return new UserLibraryStatsDto(
            TotalGames: totalGames,
            FavoriteGames: favoriteGames,
            PrivatePdfs: privatePdfs,
            OldestAddedAt: oldest,
            NewestAddedAt: newest,
            NuovoCount: stateCounts.GetValueOrDefault(GameStateType.Nuovo),
            InPrestitoCount: stateCounts.GetValueOrDefault(GameStateType.InPrestito),
            WishlistCount: stateCounts.GetValueOrDefault(GameStateType.Wishlist),
            OwnedCount: stateCounts.GetValueOrDefault(GameStateType.Owned)
        );
    }
}
