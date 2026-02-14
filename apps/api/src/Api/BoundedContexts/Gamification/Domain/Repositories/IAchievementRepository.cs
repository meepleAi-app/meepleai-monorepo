using Api.BoundedContexts.Gamification.Domain.Entities;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.Gamification.Domain.Repositories;

/// <summary>
/// Repository interface for Achievement aggregate.
/// Issue #3922: Achievement System and Badge Engine.
/// </summary>
internal interface IAchievementRepository : IRepository<Achievement, Guid>
{
    /// <summary>
    /// Gets all active achievements.
    /// </summary>
    Task<IReadOnlyList<Achievement>> GetActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an achievement by its unique code.
    /// </summary>
    Task<Achievement?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
}
