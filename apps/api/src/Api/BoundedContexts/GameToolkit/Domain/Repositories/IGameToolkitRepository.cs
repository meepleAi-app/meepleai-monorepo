using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.GameToolkit.Domain.Repositories;

internal interface IGameToolkitRepository : IRepository<Domain.Entities.GameToolkit, Guid>
{
    Task<IReadOnlyList<Domain.Entities.GameToolkit>> GetByGameIdAsync(
        Guid gameId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns toolkits for a private game, enforcing owner access.
    /// Pass callingUserId to restrict results to games owned by that user (returns empty if not owner).
    /// </summary>
    Task<IReadOnlyList<Domain.Entities.GameToolkit>> GetByPrivateGameIdAsync(
        Guid privateGameId, Guid callingUserId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Domain.Entities.GameToolkit>> GetPublishedAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Domain.Entities.GameToolkit>> GetApprovedTemplatesAsync(
        Domain.Enums.TemplateCategory? category, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Domain.Entities.GameToolkit>> GetPendingReviewAsync(
        CancellationToken cancellationToken = default);
}
