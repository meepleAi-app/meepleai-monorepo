using Api.BoundedContexts.GameToolkit.Domain.Entities;
using Api.BoundedContexts.GameToolkit.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.GameToolkit;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.GameToolkit.Infrastructure.Persistence;

/// <summary>
/// EF Core repository for <see cref="AiToolkitSuggestionCacheEntry"/>.
/// Cache-aside pattern — ADR-069 follow-up (#2383).
///
/// Upsert logic: checked-then-insert/update (within single DbContext scope).
/// Race condition between two concurrent requests for the same game is handled
/// by the UNIQUE index on game_id raising 23505 on commit, which bubbles as
/// <c>DbUpdateException</c> that the handler catches and logs (second writer
/// loses the race but the first writer's value is preserved).
/// </summary>
internal sealed class AiToolkitSuggestionCacheRepository
    : RepositoryBase, IAiToolkitSuggestionCacheRepository
{
    private readonly ILogger<AiToolkitSuggestionCacheRepository> _logger;

    public AiToolkitSuggestionCacheRepository(
        MeepleAiDbContext dbContext,
        IDomainEventCollector eventCollector,
        ILogger<AiToolkitSuggestionCacheRepository> logger)
        : base(dbContext, eventCollector)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AiToolkitSuggestionCacheEntry?> GetByGameIdAsync(Guid gameId, CancellationToken ct = default)
    {
        var entity = await DbContext.AiToolkitSuggestionCache
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.GameId == gameId, ct)
            .ConfigureAwait(false);
        return entity is null ? null : MapToDomain(entity);
    }

    public async Task UpsertAsync(AiToolkitSuggestionCacheEntry entry, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        CollectDomainEvents(entry);

        // Look up existing by game_id (UNIQUE). Use tracked query so EF detects modification.
        var existing = await DbContext.AiToolkitSuggestionCache
            .FirstOrDefaultAsync(e => e.GameId == entry.GameId, ct)
            .ConfigureAwait(false);

        if (existing is null)
        {
            await DbContext.AiToolkitSuggestionCache.AddAsync(MapToPersistence(entry), ct).ConfigureAwait(false);
        }
        else
        {
            existing.SuggestionJson = entry.SuggestionJson;
            existing.GeneratedAt = entry.GeneratedAt;
            existing.KbVersion = entry.KbVersion;
        }
    }

    public async Task DeleteByGameIdAsync(Guid gameId, CancellationToken ct = default)
    {
        var deleted = await DbContext.AiToolkitSuggestionCache
            .Where(e => e.GameId == gameId)
            .ExecuteDeleteAsync(ct)
            .ConfigureAwait(false);
        _logger.LogInformation(
            "AiToolkit cache delete for game {GameId}: {DeletedCount} row(s)",
            gameId, deleted);
    }

    private AiToolkitSuggestionCacheEntry MapToDomain(AiToolkitSuggestionCacheEntity entity)
    {
        // Use GetUninitializedObject to bypass the factory constructor (avoids domain events
        // during reconstitution). Follows the same pattern as GameToolkitRepository.MapToDomain.
        var entry = (AiToolkitSuggestionCacheEntry)System.Runtime.CompilerServices.RuntimeHelpers
            .GetUninitializedObject(typeof(AiToolkitSuggestionCacheEntry));

        // Initialize _domainEvents backing field from AggregateRoot<Guid>
        SetPrivateField(entry, "_domainEvents",
            new List<Api.SharedKernel.Domain.Interfaces.IDomainEvent>());

        SetPrivateProperty(entry, "Id", entity.Id);
        SetPrivateProperty(entry, "GameId", entity.GameId);
        SetPrivateProperty(entry, "SuggestionJson", entity.SuggestionJson);
        SetPrivateProperty(entry, "GeneratedAt", entity.GeneratedAt);
        SetPrivateProperty(entry, "KbVersion", (object?)entity.KbVersion);

        return entry;
    }

    private static AiToolkitSuggestionCacheEntity MapToPersistence(AiToolkitSuggestionCacheEntry domain) =>
        new()
        {
            Id = domain.Id,
            GameId = domain.GameId,
            SuggestionJson = domain.SuggestionJson,
            GeneratedAt = domain.GeneratedAt,
            KbVersion = domain.KbVersion,
        };

    // Reflection helpers for DDD aggregate reconstitution from persistence layer.
    // S3011 suppressed: accessibility bypass is intentional for restoring aggregate state
    // without invoking the factory constructor (which would raise domain events).
#pragma warning disable S3011
    private static void SetPrivateProperty(object obj, string propertyName, object? value)
    {
        var type = obj.GetType();
        System.Reflection.PropertyInfo? prop = null;
        while (type != null && prop == null)
        {
            prop = type.GetProperty(propertyName,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.DeclaredOnly);
            type = type.BaseType;
        }
        (prop ?? throw new InvalidOperationException($"Property '{propertyName}' not found")).SetValue(obj, value);
    }

    private static void SetPrivateField(object obj, string fieldName, object? value)
    {
        var type = obj.GetType();
        System.Reflection.FieldInfo? field = null;
        while (type != null && field == null)
        {
            field = type.GetField(fieldName,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.DeclaredOnly);
            type = type.BaseType;
        }
        (field ?? throw new InvalidOperationException($"Field '{fieldName}' not found")).SetValue(obj, value);
    }
#pragma warning restore S3011
}
