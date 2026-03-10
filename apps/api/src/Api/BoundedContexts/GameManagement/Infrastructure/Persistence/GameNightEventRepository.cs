using System.Text.Json;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.Infrastructure;
using Api.Infrastructure.Entities.GameManagement;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of GameNightEvent repository.
/// Maps between domain GameNightEvent aggregate and GameNightEventEntity persistence model.
/// Issue #42: GameNightEvent + GameNightRsvp domain entities.
/// </summary>
internal class GameNightEventRepository : RepositoryBase, IGameNightEventRepository
{
    public GameNightEventRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<GameNightEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.GameNightEvents
            .AsNoTracking()
            .Include(e => e.Rsvps)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken).ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<IReadOnlyList<GameNightEvent>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.GameNightEvents
            .AsNoTracking()
            .Include(e => e.Rsvps)
            .OrderByDescending(e => e.ScheduledAt)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<GameNightEvent>> GetUpcomingAsync(CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.GameNightEvents
            .AsNoTracking()
            .Include(e => e.Rsvps)
            .Where(e => e.Status == nameof(GameNightStatus.Published) && e.ScheduledAt > DateTimeOffset.UtcNow)
            .OrderBy(e => e.ScheduledAt)
            .Take(50)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<GameNightEvent>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.GameNightEvents
            .AsNoTracking()
            .Include(e => e.Rsvps)
            .Where(e => e.OrganizerId == userId || e.Rsvps.Any(r => r.UserId == userId))
            .OrderByDescending(e => e.ScheduledAt)
            .Take(50)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<GameNightEvent>> GetEventsNeedingReminderAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
    {
        // No AsNoTracking — reminder job must persist SentAt flag changes via SaveChangesAsync.
        var entities = await DbContext.GameNightEvents
            .Include(e => e.Rsvps)
            .Where(e => e.Status == nameof(GameNightStatus.Published) && e.ScheduledAt >= from && e.ScheduledAt <= to)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task AddAsync(GameNightEvent gameNight, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(gameNight);
        CollectDomainEvents(gameNight);

        var entity = MapToPersistence(gameNight);
        await DbContext.GameNightEvents.AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public Task UpdateAsync(GameNightEvent gameNight, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(gameNight);
        CollectDomainEvents(gameNight);

        var entity = MapToPersistence(gameNight);
        DbContext.GameNightEvents.Update(entity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(GameNightEvent gameNight, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(gameNight);
        var entity = MapToPersistence(gameNight);
        DbContext.GameNightEvents.Remove(entity);
        return Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext.GameNightEvents
            .AsNoTracking()
            .AnyAsync(e => e.Id == id, cancellationToken).ConfigureAwait(false);
    }

    private static GameNightEventEntity MapToPersistence(GameNightEvent domain)
    {
        ArgumentNullException.ThrowIfNull(domain);

        var entity = new GameNightEventEntity
        {
            Id = domain.Id,
            OrganizerId = domain.OrganizerId,
            Title = domain.Title,
            Description = domain.Description,
            ScheduledAt = domain.ScheduledAt,
            Location = domain.Location,
            MaxPlayers = domain.MaxPlayers,
            GameIdsJson = JsonSerializer.Serialize(domain.GameIds),
            Status = domain.Status.ToString(),
            Reminder24hSentAt = domain.Reminder24hSentAt,
            Reminder1hSentAt = domain.Reminder1hSentAt,
            CreatedAt = domain.CreatedAt,
            UpdatedAt = domain.UpdatedAt
        };

        foreach (var rsvp in domain.Rsvps)
        {
            entity.Rsvps.Add(new GameNightRsvpEntity
            {
                Id = rsvp.Id,
                EventId = rsvp.EventId,
                UserId = rsvp.UserId,
                Status = rsvp.Status.ToString(),
                RespondedAt = rsvp.RespondedAt,
                CreatedAt = rsvp.CreatedAt
            });
        }

        return entity;
    }

    private static GameNightEvent MapToDomain(GameNightEventEntity entity)
    {
        var gameIds = string.IsNullOrEmpty(entity.GameIdsJson)
            ? new List<Guid>()
            : JsonSerializer.Deserialize<List<Guid>>(entity.GameIdsJson) ?? [];

        var status = Enum.Parse<GameNightStatus>(entity.Status);

        // Use the internal constructor to reconstitute from persistence
        var evt = new GameNightEvent(
            id: entity.Id,
            organizerId: entity.OrganizerId,
            title: entity.Title,
            scheduledAt: entity.ScheduledAt,
            description: entity.Description,
            location: entity.Location,
            maxPlayers: entity.MaxPlayers,
            gameIds: gameIds);

        // Restore state from persistence via reflection (same pattern as GameNightPlaylistRepository)
        var statusProp = typeof(GameNightEvent).GetProperty(nameof(GameNightEvent.Status));
        statusProp?.SetValue(evt, status);

        var createdAtProp = typeof(GameNightEvent).GetProperty(nameof(GameNightEvent.CreatedAt));
        createdAtProp?.SetValue(evt, entity.CreatedAt);

        var updatedAtProp = typeof(GameNightEvent).GetProperty(nameof(GameNightEvent.UpdatedAt));
        updatedAtProp?.SetValue(evt, entity.UpdatedAt);

        var reminder24hProp = typeof(GameNightEvent).GetProperty(nameof(GameNightEvent.Reminder24hSentAt));
        reminder24hProp?.SetValue(evt, entity.Reminder24hSentAt);

        var reminder1hProp = typeof(GameNightEvent).GetProperty(nameof(GameNightEvent.Reminder1hSentAt));
        reminder1hProp?.SetValue(evt, entity.Reminder1hSentAt);

        // Restore RSVPs
        var rsvps = entity.Rsvps.Select(r => GameNightRsvp.Reconstitute(
            id: r.Id,
            eventId: r.EventId,
            userId: r.UserId,
            status: Enum.Parse<RsvpStatus>(r.Status),
            respondedAt: r.RespondedAt,
            createdAt: r.CreatedAt)).ToList();

        evt.RestoreRsvps(rsvps);

        // Clear domain events from reconstruction
        evt.ClearDomainEvents();

        return evt;
    }
}
