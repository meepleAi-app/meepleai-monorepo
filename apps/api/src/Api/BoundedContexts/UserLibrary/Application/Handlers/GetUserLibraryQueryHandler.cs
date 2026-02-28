using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
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
/// Fetches game details from SharedGameCatalog and PrivateGames.
/// Issue #3663: Updated to support PrivateGame references.
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

        // Issue #4998: Load PrivateGame entries FIRST — needed to resolve correct PrivateGameIds before the PDF query.
        // For private game library entries, the domain UserLibraryEntry.GameId = Guid.Empty
        // (computed from UserLibraryEntryEntity.SharedGameId ?? Guid.Empty), so we cannot use
        // entry.GameId to find their PDFs. Private game PDFs are stored with GameId = PrivateGameId.
        var entryIds = entries.Select(e => e.Id).ToList();
        var privateGameEntries = await _dbContext.UserLibraryEntries
            .AsNoTracking()
            .Include(e => e.PrivateGame)
            .Where(e => entryIds.Contains(e.Id) && e.PrivateGameId.HasValue)
            .ToDictionaryAsync(e => e.Id, cancellationToken)
            .ConfigureAwait(false);

        // Build ID lists for the PDF batch query.
        // Shared game IDs: exclude Guid.Empty (private game placeholder in the domain model).
        // Private game IDs: resolved from the EF entities loaded above.
        var sharedGameIds = entries.Select(e => e.GameId).Where(id => id != Guid.Empty).ToList();
        var privateGameIds = privateGameEntries.Values
            .Where(e => e.PrivateGameId.HasValue)
            .Select(e => e.PrivateGameId!.Value)
            .Distinct()
            .ToList();

        // Issue #4998: Batch load KB stats for both shared and private games in a single query.
        // Private game PDFs are stored with GameId = PrivateGameId AND PrivateGameId = PrivateGameId,
        // so we match them via the PrivateGameId column. We project PrivateGameId to split results
        // into two dictionaries without a second round-trip.
        // ProcessingState is stored as string (HasConversion<string>()), so group in memory.
        //
        // IMPORTANT: pdf_documents.GameId references games.Id (the versioned game record),
        // NOT shared_games.id (SharedGameId used by library entries). We must resolve
        // SharedGameId → games.Id before querying, then remap back for the stats dictionary.
        var sharedToGameRecord = await _dbContext.Games
            .AsNoTracking()
            .Where(g => g.SharedGameId.HasValue && sharedGameIds.Contains(g.SharedGameId!.Value))
            .Select(g => new { GameRecordId = g.Id, SharedGameId = g.SharedGameId!.Value })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var gameRecordIds = sharedToGameRecord.Select(x => x.GameRecordId).ToList();
        var gameRecordToSharedMap = sharedToGameRecord
            .ToDictionary(x => x.GameRecordId, x => x.SharedGameId);

        var pdfDocumentsRaw = await _dbContext.PdfDocuments
            .Where(p => (p.GameId.HasValue && gameRecordIds.Contains(p.GameId.Value)) ||
                        (p.PrivateGameId.HasValue && privateGameIds.Contains(p.PrivateGameId.Value)))
            .Select(p => new { p.GameId, p.PrivateGameId, p.ProcessingState })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // ProcessingState is stored as the enum member name (e.g., "Ready", "Failed")
        // because PdfDocumentEntity.ProcessingState is a string property (HasConversion<string>()).
        // Shared game PDFs: PrivateGameId IS NULL. Group by SharedGameId (not games.Id) so the
        // dictionary key matches entry.GameId used below.
        var kbStatsByGame = pdfDocumentsRaw
            .Where(p => !p.PrivateGameId.HasValue && p.GameId.HasValue && gameRecordToSharedMap.ContainsKey(p.GameId.Value))
            .GroupBy(p => gameRecordToSharedMap[p.GameId!.Value])
            .ToDictionary(
                g => g.Key,
                g => new
                {
                    KbCardCount = g.Count(),
                    KbIndexedCount = g.Count(p => string.Equals(p.ProcessingState, nameof(PdfProcessingState.Ready), StringComparison.Ordinal)),
                    KbProcessingCount = g.Count(p =>
                        !string.Equals(p.ProcessingState, nameof(PdfProcessingState.Ready), StringComparison.Ordinal) &&
                        !string.Equals(p.ProcessingState, nameof(PdfProcessingState.Failed), StringComparison.Ordinal) &&
                        !string.Equals(p.ProcessingState, nameof(PdfProcessingState.Pending), StringComparison.Ordinal)),
                });

        // Private game PDFs: PrivateGameId IS NOT NULL. Keyed by PrivateGameId.
        var kbStatsByPrivateGame = pdfDocumentsRaw
            .Where(p => p.PrivateGameId.HasValue)
            .GroupBy(p => p.PrivateGameId!.Value)
            .ToDictionary(
                g => g.Key,
                g => new
                {
                    KbCardCount = g.Count(),
                    KbIndexedCount = g.Count(p => string.Equals(p.ProcessingState, nameof(PdfProcessingState.Ready), StringComparison.Ordinal)),
                    KbProcessingCount = g.Count(p =>
                        !string.Equals(p.ProcessingState, nameof(PdfProcessingState.Ready), StringComparison.Ordinal) &&
                        !string.Equals(p.ProcessingState, nameof(PdfProcessingState.Failed), StringComparison.Ordinal) &&
                        !string.Equals(p.ProcessingState, nameof(PdfProcessingState.Pending), StringComparison.Ordinal)),
                });

        // Batch load: Get all SharedGames in a single query (prevents N+1).
        // Exclude Guid.Empty (private game placeholder) — consistent with sharedGameIds above.
        var gameIds = entries.Select(e => e.GameId).Where(id => id != Guid.Empty).ToList();
        var sharedGames = await _sharedGameRepository.GetByIdsAsync(gameIds, cancellationToken).ConfigureAwait(false);

        // Build DTOs from batch-loaded data
        var entryDtos = new List<UserLibraryEntryDto>();
        foreach (var entry in entries)
        {
            // Try SharedGame first (most common)
            if (sharedGames.TryGetValue(entry.GameId, out var sharedGame))
            {
                // Issue #4998: Compute KB stats for this game
                kbStatsByGame.TryGetValue(entry.GameId, out var kbStats);
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
                    HasKb: kbStats?.KbIndexedCount > 0,
                    KbCardCount: kbStats?.KbCardCount ?? 0,
                    KbIndexedCount: kbStats?.KbIndexedCount ?? 0,
                    KbProcessingCount: kbStats?.KbProcessingCount ?? 0,
                    AgentIsOwned: true // Always true in library context: user owns all their library agents
                ));
            }
            // Check PrivateGame entries (batch-loaded above)
            else if (privateGameEntries.TryGetValue(entry.Id, out var libraryEntity) &&
                     libraryEntity.PrivateGame != null)
            {
                var privateGame = libraryEntity.PrivateGame;
                // Issue #4998: Use kbStatsByPrivateGame (keyed by PrivateGameId) for private game KB stats.
                // kbStatsByGame only covers shared game PDFs (PrivateGameId IS NULL in DB).
                kbStatsByPrivateGame.TryGetValue(libraryEntity.PrivateGameId!.Value, out var privateKbStats);
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
                    HasKb: privateKbStats?.KbIndexedCount > 0,
                    KbCardCount: privateKbStats?.KbCardCount ?? 0,
                    KbIndexedCount: privateKbStats?.KbIndexedCount ?? 0,
                    KbProcessingCount: privateKbStats?.KbProcessingCount ?? 0,
                    AgentIsOwned: true // Always true in library context
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
