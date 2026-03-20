using Api.BoundedContexts.Administration.Domain.Entities;

namespace Api.BoundedContexts.Administration.Domain.Repositories;

internal interface IUserProfileRepository
{
    Task<UserProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserProfile>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserProfile>> SearchAsync(string searchTerm, int? maxResults = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserProfile>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);
}
