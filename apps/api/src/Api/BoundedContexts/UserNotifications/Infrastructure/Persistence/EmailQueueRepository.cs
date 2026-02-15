using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities.UserNotifications;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.UserNotifications.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of EmailQueueItem repository.
/// Maps between domain EmailQueueItem aggregate and EmailQueueEntity persistence model.
/// Issue #4417: Email notification queue with retry policy.
/// </summary>
internal class EmailQueueRepository : RepositoryBase, IEmailQueueRepository
{
    public EmailQueueRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<EmailQueueItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.Set<EmailQueueEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken).ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<IReadOnlyList<EmailQueueItem>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.Set<EmailQueueEntity>()
            .AsNoTracking()
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<EmailQueueItem>> GetPendingAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var entities = await DbContext.Set<EmailQueueEntity>()
            .Where(e =>
                (e.Status == "pending") ||
                (e.Status == "failed" && e.NextRetryAt != null && e.NextRetryAt <= now))
            .OrderBy(e => e.CreatedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<EmailQueueItem>> GetByUserIdAsync(
        Guid userId, int skip, int take, CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.Set<EmailQueueEntity>()
            .AsNoTracking()
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<int> GetCountByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<EmailQueueEntity>()
            .AsNoTracking()
            .CountAsync(e => e.UserId == userId, cancellationToken).ConfigureAwait(false);
    }

    public async Task AddAsync(EmailQueueItem item, CancellationToken cancellationToken = default)
    {
        CollectDomainEvents(item);
        var entity = MapToPersistence(item);
        await DbContext.Set<EmailQueueEntity>().AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(EmailQueueItem item, CancellationToken cancellationToken = default)
    {
        CollectDomainEvents(item);
        var entity = MapToPersistence(item);
        DbContext.Set<EmailQueueEntity>().Update(entity);
        await Task.CompletedTask.ConfigureAwait(false);
    }

    public async Task DeleteAsync(EmailQueueItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        await DbContext.Set<EmailQueueEntity>()
            .Where(e => e.Id == item.Id)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> GetRecentCountByUserIdAsync(Guid userId, DateTime since, CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<EmailQueueEntity>()
            .AsNoTracking()
            .CountAsync(e => e.UserId == userId && e.CreatedAt >= since, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> ExistsSimilarRecentAsync(Guid userId, string subject, DateTime since, CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<EmailQueueEntity>()
            .AsNoTracking()
            .AnyAsync(e => e.UserId == userId && e.Subject == subject && e.CreatedAt >= since, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<EmailQueueEntity>()
            .AsNoTracking()
            .AnyAsync(e => e.Id == id, cancellationToken).ConfigureAwait(false);
    }

    private static EmailQueueEntity MapToPersistence(EmailQueueItem item)
    {
        return new EmailQueueEntity
        {
            Id = item.Id,
            UserId = item.UserId,
            To = item.To,
            Subject = item.Subject,
            HtmlBody = item.HtmlBody,
            Status = item.Status.Value,
            RetryCount = item.RetryCount,
            MaxRetries = item.MaxRetries,
            NextRetryAt = item.NextRetryAt,
            ErrorMessage = item.ErrorMessage,
            CreatedAt = item.CreatedAt,
            ProcessedAt = item.ProcessedAt,
            FailedAt = item.FailedAt
        };
    }

    private static EmailQueueItem MapToDomain(EmailQueueEntity entity)
    {
        return EmailQueueItem.Reconstitute(
            id: entity.Id,
            userId: entity.UserId,
            to: entity.To,
            subject: entity.Subject,
            htmlBody: entity.HtmlBody,
            status: EmailQueueStatus.FromString(entity.Status),
            retryCount: entity.RetryCount,
            maxRetries: entity.MaxRetries,
            nextRetryAt: entity.NextRetryAt,
            errorMessage: entity.ErrorMessage,
            createdAt: entity.CreatedAt,
            processedAt: entity.ProcessedAt,
            failedAt: entity.FailedAt);
    }
}
