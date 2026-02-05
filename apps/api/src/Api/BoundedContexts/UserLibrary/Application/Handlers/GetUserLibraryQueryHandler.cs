using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.UserLibrary.Application.Handlers;

/// <summary>
/// Handler for getting paginated user library.
/// Fetches game details from SharedGameCatalog.
/// </summary>
internal class GetUserLibraryQueryHandler : IQueryHandler<GetUserLibraryQuery, PaginatedLibraryResponseDto>
{
    private readonly IUserLibraryRepository _libraryRepository;
    private readonly ISharedGameRepository _sharedGameRepository;
    private readonly MeepleAiDbContext _dbContext;

    public GetUserLibraryQueryHandler(
        IUserLibraryRepository libraryRepository,
        ISharedGameRepository sharedGameRepository,
        MeepleAiDbContext dbContext)
    {
        _libraryRepository = libraryRepository ?? throw new ArgumentNullException(nameof(libraryRepository));
        _sharedGameRepository = sharedGameRepository ?? throw new ArgumentNullException(nameof(sharedGameRepository));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<PaginatedLibraryResponseDto> Handle(GetUserLibraryQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var (entries, total) = await _libraryRepository.GetUserLibraryPaginatedAsync(
            query.UserId,
            query.Search,
            query.FavoritesOnly,
            query.StateFilter,
            query.SortBy,
            query.Descending,
            query.Page,
            query.PageSize,
            cancellationToken
        ).ConfigureAwait(false);

        // Batch load: Get all GameIds that have PDF documents (single query)
        var gameIds = entries.Select(e => e.GameId).ToList();
        var gameIdsWithPdfs = await _dbContext.PdfDocuments
            .Where(p => gameIds.Contains(p.GameId))
            .Select(p => p.GameId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var pdfGameSet = gameIdsWithPdfs.ToHashSet();

        // Batch load: Get all SharedGames in a single query (prevents N+1)
        var sharedGames = await _sharedGameRepository.GetByIdsAsync(gameIds, cancellationToken).ConfigureAwait(false);

        // Batch load: Get all PrivateGame library entries in a single query
        var entryIds = entries.Select(e => e.Id).ToList();
        var privateGameEntries = await _dbContext.UserLibraryEntries
            .AsNoTracking()
            .Include(e => e.PrivateGame)
            .Where(e => entryIds.Contains(e.Id) && e.PrivateGameId.HasValue)
            .ToDictionaryAsync(e => e.Id, cancellationToken)
            .ConfigureAwait(false);

        // Build DTOs from batch-loaded data
        var entryDtos = new List<UserLibraryEntryDto>();
        foreach (var entry in entries)
        {
            // Try SharedGame first (most common)
            if (sharedGames.TryGetValue(entry.GameId, out var sharedGame))
            {
                entryDtos.Add(new UserLibraryEntryDto(
                    Id: entry.Id,
                    UserId: entry.UserId,
                    GameId: entry.GameId,
                    GameTitle: sharedGame.Title,
                    GamePublisher: sharedGame.Publishers.FirstOrDefault()?.Name,
                    GameYearPublished: sharedGame.YearPublished,
                    GameIconUrl: sharedGame.ThumbnailUrl,
                    GameImageUrl: sharedGame.ImageUrl,
                    AddedAt: entry.AddedAt,
                    Notes: entry.Notes?.Value,
                    IsFavorite: entry.IsFavorite,
                    CurrentState: entry.CurrentState.Value.ToString(),
                    StateChangedAt: entry.CurrentState.ChangedAt,
                    StateNotes: entry.CurrentState.StateNotes,
                    HasPdfDocuments: pdfGameSet.Contains(entry.GameId)
                ));
            }
            // Check PrivateGame entries (batch-loaded above)
            else if (privateGameEntries.TryGetValue(entry.Id, out var libraryEntity) &&
                     libraryEntity.PrivateGame != null)
            {
                var privateGame = libraryEntity.PrivateGame;
                entryDtos.Add(new UserLibraryEntryDto(
                    Id: entry.Id,
                    UserId: entry.UserId,
                    GameId: libraryEntity.PrivateGameId!.Value,
                    GameTitle: privateGame.Title,
                    GamePublisher: null, // Private games don't have publishers
                    GameYearPublished: privateGame.YearPublished,
                    GameIconUrl: privateGame.ThumbnailUrl,
                    GameImageUrl: privateGame.ImageUrl,
                    AddedAt: entry.AddedAt,
                    Notes: entry.Notes?.Value,
                    IsFavorite: entry.IsFavorite,
                    CurrentState: entry.CurrentState.Value.ToString(),
                    StateChangedAt: entry.CurrentState.ChangedAt,
                    StateNotes: entry.CurrentState.StateNotes,
                    HasPdfDocuments: pdfGameSet.Contains(libraryEntity.PrivateGameId!.Value)
                ));
            }
        }

        var totalPages = (int)Math.Ceiling((double)total / query.PageSize);
        var hasNextPage = query.Page < totalPages;
        var hasPreviousPage = query.Page > 1;

        return new PaginatedLibraryResponseDto(
            Items: entryDtos,
            Page: query.Page,
            PageSize: query.PageSize,
            TotalCount: total,
            TotalPages: totalPages,
            HasNextPage: hasNextPage,
            HasPreviousPage: hasPreviousPage
        );
    }
}
