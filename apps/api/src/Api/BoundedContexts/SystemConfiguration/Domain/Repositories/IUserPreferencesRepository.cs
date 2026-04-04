using Api.BoundedContexts.SystemConfiguration.Domain.Entities;

namespace Api.BoundedContexts.SystemConfiguration.Domain.Repositories;

/// <summary>
/// Read-only repository for UserPreferences projection.
/// Used to query user settings within SystemConfiguration BC
/// without loading the full User aggregate from Authentication BC.
/// </summary>
internal interface IUserPreferencesRepository
{
    Task<UserPreferences?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
