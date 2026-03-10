using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.UserNotifications.Infrastructure.Persistence;

internal class NotificationPreferencesRepository : RepositoryBase, INotificationPreferencesRepository
{
    public NotificationPreferencesRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector) { }

    public async Task<NotificationPreferences?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.NotificationPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken).ConfigureAwait(false);
        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<NotificationPreferences?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.NotificationPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken).ConfigureAwait(false);
        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<IReadOnlyList<NotificationPreferences>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.NotificationPreferences.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
        return entities.Select(MapToDomain).ToList();
    }

    public async Task AddAsync(NotificationPreferences preferences, CancellationToken cancellationToken = default)
    {
        CollectDomainEvents(preferences);
        var entity = MapToPersistence(preferences);
        await DbContext.NotificationPreferences.AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public Task UpdateAsync(NotificationPreferences preferences, CancellationToken cancellationToken = default)
    {
        CollectDomainEvents(preferences);
        var entity = MapToPersistence(preferences);
        DbContext.NotificationPreferences.Update(entity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(NotificationPreferences preferences, CancellationToken cancellationToken = default)
    {
        var entity = MapToPersistence(preferences);
        DbContext.NotificationPreferences.Remove(entity);
        return Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext.NotificationPreferences.AnyAsync(p => p.Id == id, cancellationToken).ConfigureAwait(false);
    }

    private static NotificationPreferences MapToDomain(Api.Infrastructure.Entities.UserNotifications.NotificationPreferencesEntity entity)
    {
        return Api.BoundedContexts.UserNotifications.Domain.Aggregates.NotificationPreferences.Reconstitute(
            entity.Id, entity.UserId,
            entity.EmailOnDocumentReady, entity.EmailOnDocumentFailed, entity.EmailOnRetryAvailable,
            entity.PushOnDocumentReady, entity.PushOnDocumentFailed, entity.PushOnRetryAvailable,
            entity.InAppOnDocumentReady, entity.InAppOnDocumentFailed, entity.InAppOnRetryAvailable,
            entity.PushEndpoint, entity.PushP256dhKey, entity.PushAuthKey,
            entity.InAppOnGameNightInvitation, entity.EmailOnGameNightInvitation,
            entity.PushOnGameNightInvitation, entity.EmailOnGameNightReminder,
            entity.PushOnGameNightReminder
        );
    }

    private static Api.Infrastructure.Entities.UserNotifications.NotificationPreferencesEntity MapToPersistence(NotificationPreferences domain)
    {
        return new Api.Infrastructure.Entities.UserNotifications.NotificationPreferencesEntity
        {
            Id = domain.Id,
            UserId = domain.UserId,
            EmailOnDocumentReady = domain.EmailOnDocumentReady,
            EmailOnDocumentFailed = domain.EmailOnDocumentFailed,
            EmailOnRetryAvailable = domain.EmailOnRetryAvailable,
            PushOnDocumentReady = domain.PushOnDocumentReady,
            PushOnDocumentFailed = domain.PushOnDocumentFailed,
            PushOnRetryAvailable = domain.PushOnRetryAvailable,
            InAppOnDocumentReady = domain.InAppOnDocumentReady,
            InAppOnDocumentFailed = domain.InAppOnDocumentFailed,
            InAppOnRetryAvailable = domain.InAppOnRetryAvailable,
            PushEndpoint = domain.PushEndpoint,
            PushP256dhKey = domain.PushP256dhKey,
            PushAuthKey = domain.PushAuthKey,
            InAppOnGameNightInvitation = domain.InAppOnGameNightInvitation,
            EmailOnGameNightInvitation = domain.EmailOnGameNightInvitation,
            PushOnGameNightInvitation = domain.PushOnGameNightInvitation,
            EmailOnGameNightReminder = domain.EmailOnGameNightReminder,
            PushOnGameNightReminder = domain.PushOnGameNightReminder
        };
    }
}
