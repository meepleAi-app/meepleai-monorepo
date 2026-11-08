using Api.BoundedContexts.Administration.Domain.Entities;

namespace Api.BoundedContexts.Administration.Domain.Repositories;

internal interface IUserProfileRepository
{
    Task<UserProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserProfile>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Partial, case-insensitive match on the display name ONLY. The email is deliberately not matched:
    /// a partial email search lets any signed-in user list the addresses of every account (#4727).
    /// Wildcards in <paramref name="searchTerm"/> are literal characters, not patterns.
    /// </summary>
    Task<IReadOnlyList<UserProfile>> SearchByDisplayNameAsync(string searchTerm, int? maxResults = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Exact, case-insensitive match on the whole email address; at most one user (#4727).
    /// </summary>
    Task<UserProfile?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserProfile>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);
}
