using Api.BoundedContexts.Administration.Domain.Entities;

namespace Api.BoundedContexts.Administration.Domain.Repositories;

/// <summary>
/// Repository interface for UserAiConsent (Issue #5512)
/// </summary>
public interface IUserAiConsentRepository
{
    Task<UserAiConsent?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(UserAiConsent consent, CancellationToken cancellationToken = default);
    Task UpdateAsync(UserAiConsent consent, CancellationToken cancellationToken = default);
    Task DeleteByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
}
