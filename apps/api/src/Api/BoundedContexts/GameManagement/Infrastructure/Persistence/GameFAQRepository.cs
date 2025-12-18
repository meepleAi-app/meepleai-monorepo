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
internal class GameFAQRepository : RepositoryBase, IGameFAQRepository
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

        // Execute count first, then fetch (sequential to avoid DbContext concurrency issues)
        // Issue #2186: Fixed concurrent query execution - DbContext is not thread-safe
        var totalCount = await baseQuery.CountAsync(cancellationToken).ConfigureAwait(false);

        var entities = await baseQuery
            .OrderByDescending(f => f.Upvotes)
            .ThenByDescending(f => f.CreatedAt)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

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

        // Issue #2186: Handle EF Core tracking to prevent conflicts in same-scope multiple updates
        var tracked = DbContext.GameFAQs.Local.FirstOrDefault(e => e.Id == entity.Id);
        if (tracked != null)
        {
            // Update the already-tracked entity
            DbContext.Entry(tracked).CurrentValues.SetValues(entity);
        }
        else
        {
            DbContext.GameFAQs.Update(entity);
        }

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

    public async Task<GameFAQ> IncrementUpvoteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Atomic SQL update to prevent race conditions
        // Issue #2186: Fixed concurrent upvote handling with atomic increment
        try
        {
            var rowsAffected = await DbContext.Database
                .ExecuteSqlInterpolatedAsync(
                    $@"UPDATE ""GameFAQs""
                       SET ""Upvotes"" = ""Upvotes"" + 1,
                           ""UpdatedAt"" = {DateTime.UtcNow}
                       WHERE ""Id"" = {id}",
                    cancellationToken)
                .ConfigureAwait(false);

            if (rowsAffected == 0)
                throw new InvalidOperationException($"FAQ with ID {id} not found");

            // Retrieve updated entity
            var entity = await DbContext.GameFAQs
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.Id == id, cancellationToken)
                .ConfigureAwait(false);

            return entity != null ? MapToDomain(entity) : throw new InvalidOperationException($"FAQ with ID {id} not found after update");
        }
        catch (Npgsql.PostgresException ex) when (string.Equals(ex.SqlState, "22003", StringComparison.Ordinal)) // integer out of range
        {
            throw new InvalidOperationException("Maximum upvotes reached for this FAQ", ex);
        }
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