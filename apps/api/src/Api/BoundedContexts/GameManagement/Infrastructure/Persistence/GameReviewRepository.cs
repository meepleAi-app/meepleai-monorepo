using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.GameManagement;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of IGameReviewRepository.
/// Issue #4904: Game reviews API endpoint.
/// </summary>
internal sealed class GameReviewRepository : RepositoryBase, IGameReviewRepository
{
    public GameReviewRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<(IReadOnlyList<GameReview> Items, int TotalCount)> GetBySharedGameIdAsync(
        Guid sharedGameId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = DbContext.GameReviews
            .AsNoTracking()
            .Where(r => r.SharedGameId == sharedGameId);

        var totalCount = await query
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);

        var entities = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = entities.Select(MapToDomain).ToList();

        return (items, totalCount);
    }

    public async Task<GameReview?> FindByUserAndGameAsync(
        Guid userId,
        Guid sharedGameId,
        CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.GameReviews
            .AsNoTracking()
            .FirstOrDefaultAsync(
                r => r.UserId == userId && r.SharedGameId == sharedGameId,
                cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : MapToDomain(entity);
    }

    public async Task AddAsync(GameReview review, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(review);

        CollectDomainEvents(review);

        var entity = new GameReviewEntity
        {
            Id = review.Id,
            SharedGameId = review.SharedGameId,
            UserId = review.UserId,
            AuthorName = review.AuthorName,
            Rating = review.Rating,
            Content = review.Content,
            CreatedAt = review.CreatedAt,
            UpdatedAt = review.UpdatedAt
        };

        await DbContext.GameReviews.AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    private static GameReview MapToDomain(GameReviewEntity entity)
        => GameReview.Reconstitute(
            entity.Id,
            entity.SharedGameId,
            entity.UserId,
            entity.AuthorName,
            entity.Rating,
            entity.Content,
            entity.CreatedAt,
            entity.UpdatedAt);
}
