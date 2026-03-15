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

        return hasOwnership;
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
}
