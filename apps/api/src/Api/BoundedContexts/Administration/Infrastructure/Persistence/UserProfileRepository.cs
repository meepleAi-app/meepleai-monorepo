using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Infrastructure.Persistence;

internal sealed class UserProfileRepository : IUserProfileRepository
{
    private readonly MeepleAiDbContext _dbContext;

    public UserProfileRepository(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<UserProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Set<UserProfile>()
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<UserProfile>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Set<UserProfile>()
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>The escape character given to ILIKE, so <see cref="EscapeLikePattern"/> and the query agree.</summary>
    private const string LikeEscape = "\\";

    public async Task<IReadOnlyList<UserProfile>> SearchByDisplayNameAsync(string searchTerm, int? maxResults = null, CancellationToken cancellationToken = default)
    {
        var pattern = $"%{EscapeLikePattern(searchTerm)}%";
        var query = _dbContext.Set<UserProfile>()
            .AsNoTracking()
            .Where(u => u.DisplayName != null && EF.Functions.ILike(u.DisplayName, pattern, LikeEscape))
            .OrderBy(u => u.DisplayName);

        var limited = maxResults.HasValue ? query.Take(maxResults.Value) : query;

        return await limited.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<UserProfile?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        // ILIKE without a leading or trailing % is a case-insensitive equality; the escape keeps any
        // % or _ typed by the caller from turning it back into a pattern.
        var pattern = EscapeLikePattern(email);
        return await _dbContext.Set<UserProfile>()
            .AsNoTracking()
            .Where(u => EF.Functions.ILike(u.Email, pattern, LikeEscape))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Makes <c>%</c>, <c>_</c> and the escape character itself literal in an ILIKE pattern. Without it a
    /// two-character search of <c>__</c> matches every name, which defeats the two-character minimum.
    /// </summary>
    internal static string EscapeLikePattern(string term)
    {
        ArgumentNullException.ThrowIfNull(term);
        return term
            .Replace(LikeEscape, LikeEscape + LikeEscape, StringComparison.Ordinal)
            .Replace("%", LikeEscape + "%", StringComparison.Ordinal)
            .Replace("_", LikeEscape + "_", StringComparison.Ordinal);
    }

    public async Task<IReadOnlyList<UserProfile>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        var idList = ids.ToList();
        return await _dbContext.Set<UserProfile>()
            .AsNoTracking()
            .Where(u => idList.Contains(u.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
