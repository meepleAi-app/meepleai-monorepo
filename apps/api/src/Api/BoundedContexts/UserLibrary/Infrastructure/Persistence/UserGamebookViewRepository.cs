using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.UserLibrary.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IUserGamebookViewRepository"/> (Issue #1288).
///
/// Composes UserLibraryEntries with cross-aggregate signals (SharedGames title/cover,
/// PrivateGames title/cover, GamebookCampaignSessions presence) into a flat projection
/// for the `/api/v1/gamebooks` index.
///
/// Cross-schema JOIN rationale: PostgreSQL single DB + performance budget P95 &lt; 200ms
/// makes the JOIN preferable to two round-trip composition. Pragmatic DDD — the domain
/// interface (<see cref="IUserGamebookViewRepository"/>) is BC-pure, the SQL JOIN is
/// an implementation detail.
/// </summary>
internal sealed class UserGamebookViewRepository : IUserGamebookViewRepository
{
    private readonly MeepleAiDbContext _dbContext;

    public UserGamebookViewRepository(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<IReadOnlyList<UserGamebookViewItem>> GetGamebookEntriesAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        // Materialize three joined queries via a single round-trip:
        //   1. UserLibraryEntries for the user with SharedGameId NOT NULL
        //   2. Title/Year/Cover from SharedGames (LEFT JOIN)
        //   3. HasActiveCampaign via EXISTS on GamebookCampaignSessions
        //   4. UNION with PrivateGameId-based entries via PrivateGames JOIN
        //
        // Filter: entry must have either an active campaign OR a private rulebook.
        var sharedSide = from entry in _dbContext.UserLibraryEntries.AsNoTracking()
                         where entry.UserId == userId && entry.SharedGameId != null
                         join shared in _dbContext.SharedGames.AsNoTracking()
                             on entry.SharedGameId equals shared.Id
                         let hasActiveCampaign = _dbContext.GamebookCampaignSessions
                             .Any(c => c.OwnerUserId == userId
                                 && c.GameId == entry.SharedGameId!.Value
                                 && !c.IsDeleted)
                         where hasActiveCampaign || entry.PrivateGameId != null
                         let latestCampaignUpdate = _dbContext.GamebookCampaignSessions
                             .Where(c => c.OwnerUserId == userId
                                 && c.GameId == entry.SharedGameId!.Value
                                 && !c.IsDeleted)
                             .Max(c => (DateTime?)c.UpdatedAt.UtcDateTime)
                         let sessionsCount = _dbContext.GamebookCampaignSessions
                             .Count(c => c.OwnerUserId == userId
                                 && c.GameId == entry.SharedGameId!.Value
                                 && !c.IsDeleted)
                         let chunkCount = _dbContext.TextChunks
                             .Count(t => t.SharedGameId == entry.SharedGameId!.Value)
                         let readyPdfCount = _dbContext.PdfDocuments
                             .Count(p => p.SharedGameId == entry.SharedGameId!.Value
                                 && p.ProcessingState == "Ready")
                         let indexingPdfCount = _dbContext.PdfDocuments
                             .Count(p => p.SharedGameId == entry.SharedGameId!.Value
                                 && (p.ProcessingState == "Pending"
                                     || p.ProcessingState == "Extracting"
                                     || p.ProcessingState == "Chunking"
                                     || p.ProcessingState == "Embedding"
                                     || p.ProcessingState == "Indexing"))
                         let failedPdfCount = _dbContext.PdfDocuments
                             .Count(p => p.SharedGameId == entry.SharedGameId!.Value
                                 && p.ProcessingState == "Failed")
                         select new UserGamebookViewItem(
                             entry.Id,
                             shared.Id,
                             shared.Title,
                             shared.YearPublished == 0 ? null : shared.YearPublished,
                             string.IsNullOrEmpty(shared.ImageUrl) ? null : shared.ImageUrl,
                             hasActiveCampaign,
                             entry.PrivateGameId != null,
                             chunkCount,
                             sessionsCount,
                             readyPdfCount,
                             indexingPdfCount,
                             failedPdfCount,
                             latestCampaignUpdate ?? entry.AddedAt);

        var privateSide = from entry in _dbContext.UserLibraryEntries.AsNoTracking()
                          where entry.UserId == userId
                              && entry.PrivateGameId != null
                              && entry.SharedGameId == null
                          join priv in _dbContext.PrivateGames.AsNoTracking()
                              on entry.PrivateGameId equals priv.Id
                          let chunkCount = _dbContext.TextChunks
                              .Count(t => t.GameId == priv.Id)
                          let readyPdfCount = _dbContext.PdfDocuments
                              .Count(p => p.PrivateGameId == priv.Id
                                  && p.ProcessingState == "Ready")
                          let indexingPdfCount = _dbContext.PdfDocuments
                              .Count(p => p.PrivateGameId == priv.Id
                                  && (p.ProcessingState == "Pending"
                                      || p.ProcessingState == "Extracting"
                                      || p.ProcessingState == "Chunking"
                                      || p.ProcessingState == "Embedding"
                                      || p.ProcessingState == "Indexing"))
                          let failedPdfCount = _dbContext.PdfDocuments
                              .Count(p => p.PrivateGameId == priv.Id
                                  && p.ProcessingState == "Failed")
                          select new UserGamebookViewItem(
                              entry.Id,
                              priv.Id,
                              priv.Title,
                              priv.YearPublished,
                              priv.ImageUrl,
                              false,
                              true,
                              chunkCount,
                              0,
                              readyPdfCount,
                              indexingPdfCount,
                              failedPdfCount,
                              entry.AddedAt);

        var combined = await sharedSide.Concat(privateSide).ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return combined;
    }
}
