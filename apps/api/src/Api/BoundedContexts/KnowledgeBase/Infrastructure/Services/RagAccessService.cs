using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Services;

/// <summary>
/// Implementation of IRagAccessService using cross-BC read-side queries.
/// Uses direct DbContext reads (acceptable for read-only access checks).
/// </summary>
internal sealed class RagAccessService : IRagAccessService
{
    private readonly MeepleAiDbContext _dbContext;

    public RagAccessService(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <inheritdoc />
    public async Task<bool> CanAccessRagAsync(Guid userId, Guid gameId, UserRole role, CancellationToken cancellationToken = default)
    {
        // Rule 1: Admin or SuperAdmin → always allowed
        if (role is UserRole.Admin or UserRole.SuperAdmin)
            return true;

        // Rule 2: SharedGame.IsRagPublic == true → allowed for everyone
        var isRagPublic = await _dbContext.SharedGames
            .AsNoTracking()
            .Where(sg => sg.Id == gameId && !sg.IsDeleted)
            .Select(sg => sg.IsRagPublic)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (isRagPublic)
            return true;

        // Rule 3: User has declared ownership via UserLibraryEntry
        var hasOwnership = await _dbContext.UserLibraryEntries
            .AsNoTracking()
            .AnyAsync(
                ule => ule.UserId == userId
                       && ule.SharedGameId == gameId
                       && ule.OwnershipDeclaredAt != null,
                cancellationToken)
            .ConfigureAwait(false);

        if (hasOwnership)
            return true;

        // Rule 4 (Issue #4137): the caller owns the PrivateGame with this id.
        //
        // Rules 2 and 3 only ever look at SharedGames and UserLibraryEntry.SharedGameId,
        // so before this branch existed an id belonging to a PrivateGame fell through
        // all of them and the method returned false — including for the owner. Yet
        // IndexPdfCommandHandler scopes a private PDF's vectors under PrivateGameId
        // ("effectiveGameId = pdf.PrivateGameId ?? pdf.SharedGameId"), so the corpus
        // existed and was simply unreachable: upload, indexing, agent link and
        // kb-status all succeeded, and the question answered 403.
        //
        // Soft-deleted private games are excluded by the global query filter on
        // PrivateGameEntity (!IsDeleted) — not repeated here, so that the filter stays
        // the single place that decides it. A test asserts the soft-deleted case.
        return await _dbContext.PrivateGames
            .AsNoTracking()
            .AnyAsync(pg => pg.Id == gameId && pg.OwnerId == userId, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<List<Guid>> GetAccessibleKbCardsAsync(Guid userId, Guid gameId, UserRole role, CancellationToken cancellationToken = default)
    {
        var canAccess = await CanAccessRagAsync(userId, gameId, role, cancellationToken).ConfigureAwait(false);
        if (!canAccess)
            return [];

        // Return VectorDocument IDs where the game matches and indexing is completed.
        // VectorDocuments can reference games via GameId (user PDF) or SharedGameId (admin KB card).
        return await _dbContext.VectorDocuments
            .AsNoTracking()
            .Where(vd => (vd.SharedGameId == gameId || vd.GameId == gameId)
                         && vd.IndexingStatus == "completed")
            .Select(vd => vd.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<List<Guid>> GetAccessibleKbCardsFilteredAsync(
        Guid userId, Guid gameId, UserRole role,
        List<Guid>? selectedIds,
        CancellationToken cancellationToken = default)
    {
        var allAccessible = await GetAccessibleKbCardsAsync(userId, gameId, role, cancellationToken)
            .ConfigureAwait(false);

        if (selectedIds is not { Count: > 0 })
            return allAccessible;

        // Intersect: only return IDs that are both accessible AND selected
        var selectedSet = new HashSet<Guid>(selectedIds);
        return allAccessible.Where(id => selectedSet.Contains(id)).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Guid>> GetAccessibleGameIdsAsync(
        Guid userId,
        UserRole role,
        CancellationToken cancellationToken = default)
    {
        // Rule 1: Admin / SuperAdmin → all non-deleted SharedGame IDs.
        //
        // Issue #4137: deliberately NOT extended to every user's PrivateGames, even
        // though CanAccessRagAsync rule 1 lets an admin query a specific private game.
        // The two are different operations: that one is targeted support, this one is
        // the corpus of every ordinary question an admin asks. Folding every user's
        // private PDFs into it would both amplify the privacy surface and degrade
        // retrieval. An admin who needs a private game asks with that game's id.
        if (role is UserRole.Admin or UserRole.SuperAdmin)
        {
            return await _dbContext.SharedGames
                .AsNoTracking()
                .Where(sg => !sg.IsDeleted)
                .Select(sg => sg.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        // Rule 2 (non-admin): public games ∪ library games with declared ownership.
        // Both branches apply !IsDeleted to prevent deleted games leaking into the result.
        var publicGames = _dbContext.SharedGames
            .AsNoTracking()
            .Where(sg => !sg.IsDeleted && sg.IsRagPublic)
            .Select(sg => sg.Id);

        // Join UserLibraryEntries with SharedGames to enforce IsDeleted exclusion (EC-8 + deleted-game rule).
        var ownedGames = _dbContext.UserLibraryEntries
            .AsNoTracking()
            .Where(e => e.UserId == userId
                     && e.OwnershipDeclaredAt != null
                     && e.SharedGameId != null)
            .Join(
                _dbContext.SharedGames.AsNoTracking().Where(sg => !sg.IsDeleted),
                e => e.SharedGameId!.Value,
                sg => sg.Id,
                (e, sg) => sg.Id);

        // Rule 3 (Issue #4137): the caller's own PrivateGames. Without this a private
        // game never appeared in a cross-game ask, for the same reason it was denied
        // by CanAccessRagAsync: both methods only knew about SharedGames.
        // Soft-deleted rows are excluded by the global query filter on PrivateGameEntity.
        var ownPrivateGames = _dbContext.PrivateGames
            .AsNoTracking()
            .Where(pg => pg.OwnerId == userId)
            .Select(pg => pg.Id);

        // Union deduplicates at the database level; Distinct is a safety net.
        return await publicGames
            .Union(ownedGames)
            .Union(ownPrivateGames)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
