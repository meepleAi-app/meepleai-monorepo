using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of GameFAQ repository.
/// Maps between domain GameFAQ entity and GameFAQEntity persistence model.
/// Issue #2028: Backend FAQ system for game-specific FAQs.
/// </summary>
public class GameFAQRepository : RepositoryBase, IGameFAQRepository
{
    public GameFAQRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<GameFAQ?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.GameFAQs
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<(IReadOnlyList<GameFAQ> faqs, int totalCount)> GetByGameIdWithCountAsync(
        Guid gameId,
        int limit = 10,
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        // Single query with count - prevents N+1 problem
        var baseQuery = DbContext.GameFAQs
            .AsNoTracking()
            .Where(f => f.GameId == gameId);

        // Execute count and fetch in parallel
        var countTask = baseQuery.CountAsync(cancellationToken);
        var entitiesTask = baseQuery
            .OrderByDescending(f => f.Upvotes)
            .ThenByDescending(f => f.CreatedAt)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);

        await Task.WhenAll(countTask, entitiesTask).ConfigureAwait(false);

        var totalCount = await countTask.ConfigureAwait(false);
        var entities = await entitiesTask.ConfigureAwait(false);

        return (entities.Select(MapToDomain).ToList(), totalCount);
    }

    public async Task AddAsync(GameFAQ faq, CancellationToken cancellationToken = default)
    {
        CollectDomainEvents(faq);

        var entity = MapToPersistence(faq);
        await DbContext.GameFAQs.AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public Task UpdateAsync(GameFAQ faq, CancellationToken cancellationToken = default)
    {
        CollectDomainEvents(faq);

        var entity = MapToPersistence(faq);
        DbContext.GameFAQs.Update(entity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(GameFAQ faq, CancellationToken cancellationToken = default)
    {
        var entity = MapToPersistence(faq);
        DbContext.GameFAQs.Remove(entity);
        return Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext.GameFAQs
            .AsNoTracking()
            .AnyAsync(f => f.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Maps persistence entity to domain entity.
    /// </summary>
    private static GameFAQ MapToDomain(GameFAQEntity entity)
    {
        var question = new FAQQuestion(entity.Question);
        var answer = new FAQAnswer(entity.Answer);

        var faq = new GameFAQ(
            id: entity.Id,
            gameId: entity.GameId,
            question: question,
            answer: answer
        );

        // Set internal state via reflection (CreatedAt, UpdatedAt, Upvotes)
        var createdAtProp = typeof(GameFAQ).GetProperty("CreatedAt");
        createdAtProp?.SetValue(faq, entity.CreatedAt);

        var updatedAtProp = typeof(GameFAQ).GetProperty("UpdatedAt");
        updatedAtProp?.SetValue(faq, entity.UpdatedAt);

        var upvotesProp = typeof(GameFAQ).GetProperty("Upvotes");
        upvotesProp?.SetValue(faq, entity.Upvotes);

        return faq;
    }

    /// <summary>
    /// Maps domain entity to persistence entity.
    /// </summary>
    private static GameFAQEntity MapToPersistence(GameFAQ domainEntity)
    {
        return new GameFAQEntity
        {
            Id = domainEntity.Id,
            GameId = domainEntity.GameId,
            Question = domainEntity.Question.Value,
            Answer = domainEntity.Answer.Value,
            Upvotes = domainEntity.Upvotes,
            CreatedAt = domainEntity.CreatedAt,
            UpdatedAt = domainEntity.UpdatedAt
        };
    }
}
