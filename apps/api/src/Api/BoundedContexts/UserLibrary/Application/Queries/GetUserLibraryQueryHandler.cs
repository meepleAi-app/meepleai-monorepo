using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Infrastructure;
using Api.Services.Pdf;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Covers;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.UserLibrary.Application.Queries;

/// <summary>
/// Handler for getting paginated user library.
/// Fetches game details from SharedGameCatalog. Private games are not part of this list: the
/// repository returns catalog entries only, and the PrivateGames endpoints serve the rest (#4235).
/// </summary>
internal class GetUserLibraryQueryHandler : IQueryHandler<GetUserLibraryQuery, PaginatedLibraryResponseDto>
{
    private readonly IUserLibraryRepository _libraryRepository;
    private readonly ISharedGameRepository _sharedGameRepository;
    private readonly MeepleAiDbContext _dbContext;
    private readonly IBlobStorageService _blobStorage;

    public GetUserLibraryQueryHandler(
        IUserLibraryRepository libraryRepository,
        ISharedGameRepository sharedGameRepository,
        MeepleAiDbContext dbContext,
        IBlobStorageService blobStorage)
    {
        _libraryRepository = libraryRepository ?? throw new ArgumentNullException(nameof(libraryRepository));
        _sharedGameRepository = sharedGameRepository ?? throw new ArgumentNullException(nameof(sharedGameRepository));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _blobStorage = blobStorage ?? throw new ArgumentNullException(nameof(blobStorage));
    }

    public async Task<PaginatedLibraryResponseDto> Handle(GetUserLibraryQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // #4187: the page actually served. The response used to echo the requested size, so a
        // client asking for 500 got 100 entries and "1 page, nothing after".
        var page = UserLibraryPaging.NormalizePage(query.Page);
        var pageSize = UserLibraryPaging.NormalizePageSize(query.PageSize);

        var (entries, total) = await _libraryRepository.GetUserLibraryPaginatedAsync(
            query.UserId,
            query.Search,
            query.FavoritesOnly,
            query.StateFilter,
            query.HasKb,
            query.SortBy,
            query.Descending,
            page,
            pageSize,
            cancellationToken
        ).ConfigureAwait(false);

        // The repository returns only catalog (shared) entries: GetUserLibraryPaginatedAsync filters
        // SharedGameId != null, and private games are served by the PrivateGames endpoints. A
        // branch for private entries lived here and could not be reached; removed with #4235.
        var entryIds = entries.Select(e => e.Id).ToList();
        var sharedGameIds = entries.Select(e => e.GameId).Where(id => id != Guid.Empty).ToList();

        // Issue #4998: Batch load KB stats for the shared games in a single query.
        // ProcessingState is stored as string (HasConversion<string>()), so group in memory.
        //
        // PDF -> SharedGame migration: pdf_documents.GameId removed. PDFs now reference
        // SharedGames.Id directly via pdf_documents.SharedGameId.
        var pdfDocumentsRaw = await _dbContext.PdfDocuments
            .Where(p => p.SharedGameId.HasValue && sharedGameIds.Contains(p.SharedGameId.Value))
            .Select(p => new { p.SharedGameId, p.PrivateGameId, p.ProcessingState })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // ProcessingState is stored as the enum member name (e.g., "Ready", "Failed")
        // because PdfDocumentEntity.ProcessingState is a string property (HasConversion<string>()).
        // Which states count as completed lives in LibraryKbStates, shared with the hasKb filter
        // of the repository (#4227).
        static bool IsCompleted(string? state) => LibraryKbStates.IsCompleted(state);

        static bool IsFailed(string? state) =>
            string.Equals(state, nameof(PdfProcessingState.Failed), StringComparison.OrdinalIgnoreCase);

        // Shared game PDFs: PrivateGameId IS NULL. Group by SharedGameId so the
        // dictionary key matches entry.GameId used below.
        var kbStatsByGame = pdfDocumentsRaw
            .Where(p => !p.PrivateGameId.HasValue && p.SharedGameId.HasValue)
            .GroupBy(p => p.SharedGameId!.Value)
            .ToDictionary(
                g => g.Key,
                g => new
                {
                    KbCardCount = g.Count(),
                    KbIndexedCount = g.Count(p => IsCompleted(p.ProcessingState)),
                    KbProcessingCount = g.Count(p => !IsCompleted(p.ProcessingState) && !IsFailed(p.ProcessingState)),
                });

        // Batch load: Get all SharedGames in a single query (prevents N+1).
        var gameIds = entries.Select(e => e.GameId).Where(id => id != Guid.Empty).ToList();
        var sharedGames = await _sharedGameRepository.GetByIdsAsync(gameIds, cancellationToken).ConfigureAwait(false);

        // Ownership/RAG access: batch-load IsRagPublic flags and user role to avoid N+1.
        // Compute HasRagAccess in-memory: isAdmin || isRagPublic || ownershipDeclaredAt != null.
        var ragPublicFlags = await _dbContext.SharedGames
            .AsNoTracking()
            .Where(sg => gameIds.Contains(sg.Id) && !sg.IsDeleted)
            .Select(sg => new { sg.Id, sg.IsRagPublic })
            .ToDictionaryAsync(sg => sg.Id, sg => sg.IsRagPublic, cancellationToken)
            .ConfigureAwait(false);

        // Issue #1852 (Gap A): Batch-load SharedGameEntity objects (cover keys: PdfCoverR2Key,
        // WikidataCoverR2Key) for CoverUrlResolver. The domain aggregate from ISharedGameRepository
        // does not carry EF-only columns, so we query the EF DbSet directly.
        var sharedGameEntityMap = await _dbContext.SharedGames
            .AsNoTracking()
            // Epic #3470 Slice 2c: eager-load CoverAssignments so the per-context (Card)
            // resolver can honor an admin override below the L3 user-custom cover.
            // Values are the full entity (no Select projection), so the Include is honored.
            .Include(sg => sg.CoverAssignments)
            .Where(sg => gameIds.Contains(sg.Id) && !sg.IsDeleted)
            .ToDictionaryAsync(sg => sg.Id, cancellationToken)
            .ConfigureAwait(false);

        // Issue #1852 (Gap A): Batch-load UserLibraryEntryEntity for all entries
        // to access CustomCoverR2Key (L3 user-custom cover layer used by CoverUrlResolver).
        var entryEntityMap = await _dbContext.UserLibraryEntries
            .AsNoTracking()
            .Where(e => entryIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, cancellationToken)
            .ConfigureAwait(false);

        var userRoleStr = await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.Id == query.UserId)
            .Select(u => u.Role)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var isAdmin = string.Equals(userRoleStr, "admin", StringComparison.OrdinalIgnoreCase)
                      || string.Equals(userRoleStr, "superadmin", StringComparison.OrdinalIgnoreCase);

        // Build DTOs from batch-loaded data
        var entryDtos = new List<UserLibraryEntryDto>();
        foreach (var entry in entries)
        {
            if (sharedGames.TryGetValue(entry.GameId, out var sharedGame))
            {
                // Issue #4998: Compute KB stats for this game
                kbStatsByGame.TryGetValue(entry.GameId, out var kbStats);

                // Issue #1852 (Gap A): Resolve cover URL via L3 → L4 → L2 priority.
                // Requires the EF SharedGameEntity (cover keys) and the EF UserLibraryEntryEntity (custom cover key).
                sharedGameEntityMap.TryGetValue(entry.GameId, out var sharedGameEntity);
                entryEntityMap.TryGetValue(entry.Id, out var entryEntity);
                // Epic #3470 Slice 2c — My Library grid is the Card context; L3 user-custom
                // cover still outranks the admin per-context override (AC-4).
                var coverUrl = sharedGameEntity is not null
                    ? await CoverUrlResolver.ResolveForUserAsync(sharedGameEntity, entryEntity, CoverContext.Card, _blobStorage).ConfigureAwait(false)
                    : null;

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
                    AgentIsOwned: true, // Always true in library context: user owns all their library agents
                    MinPlayers: sharedGame.MinPlayers,
                    MaxPlayers: sharedGame.MaxPlayers,
                    PlayingTimeMinutes: sharedGame.PlayingTimeMinutes,
                    ComplexityRating: sharedGame.ComplexityRating,
                    AverageRating: sharedGame.AverageRating,
                    OwnershipDeclaredAt: entry.OwnershipDeclaredAt,
                    HasRagAccess: isAdmin
                        || (ragPublicFlags.TryGetValue(entry.GameId, out var isPublic) && isPublic)
                        || entry.OwnershipDeclaredAt != null,
                    TimesPlayed: entry.Stats.TimesPlayed,
                    LastPlayed: entry.Stats.LastPlayed,
                    CoverUrl: coverUrl
                ));
            }
        }

        var totalPages = (int)Math.Ceiling((double)total / pageSize);
        var hasNextPage = page < totalPages;
        var hasPreviousPage = page > 1;

        return new PaginatedLibraryResponseDto(
            Items: entryDtos,
            Page: page,
            PageSize: pageSize,
            TotalCount: total,
            TotalPages: totalPages,
            HasNextPage: hasNextPage,
            HasPreviousPage: hasPreviousPage
        );
    }
}
