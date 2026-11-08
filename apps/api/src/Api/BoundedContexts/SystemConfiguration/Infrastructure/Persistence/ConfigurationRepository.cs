using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;
using SystemConfigurationAggregate = Api.BoundedContexts.SystemConfiguration.Domain.Entities.SystemConfiguration;

namespace Api.BoundedContexts.SystemConfiguration.Infrastructure.Persistence;

internal class ConfigurationRepository : RepositoryBase, IConfigurationRepository
{
    /// <summary>
    /// Le colonne di <c>system_configurations</c> che l'aggregato non porta (#4612). <see cref="UpdateAsync"/> non
    /// le scrive mai: le possiede <c>RegisterCommandHandler</c>, sull'entità.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>BootstrapAdminCreated</c> e <c>BootstrapAdminCreatedAt</c> sono il flag single-use del claim del primo
    /// admin. <c>DbSet.Update</c> su un'entità detached segna come modificata ogni colonna, e una colonna che
    /// <c>MapToPersistence</c> non copia arrivava al database al valore di default della classe (<c>false</c> e
    /// <c>NULL</c>): ogni update, toggle, rollback o import della riga <c>Authentication:BootstrapAdmin</c> riapriva
    /// il claim. Stesso schema di <c>SharedGameRepository.ColumnsNotOwnedByTheAggregate</c> (#4585) e
    /// <c>PdfDocumentRepository.ColumnsNotOwnedByTheAggregate</c> (#4347).
    /// </para>
    /// <para>
    /// Il test <c>ConfigurationRepositoryTests</c> prende le colonne dal modello EF: una colonna aggiunta domani
    /// all'entità, senza mapping e senza essere qui, lo fa fallire con il suo nome.
    /// </para>
    /// </remarks>
    internal static readonly IReadOnlyList<string> ColumnsNotOwnedByTheAggregate =
    [
        nameof(Api.Infrastructure.Entities.SystemConfigurationEntity.BootstrapAdminCreated),
        nameof(Api.Infrastructure.Entities.SystemConfigurationEntity.BootstrapAdminCreatedAt),
    ];

    public ConfigurationRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<SystemConfigurationAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.Set<Api.Infrastructure.Entities.SystemConfigurationEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken).ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<IReadOnlyList<SystemConfigurationAggregate>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.Set<Api.Infrastructure.Entities.SystemConfigurationEntity>()
            .AsNoTracking()
            .OrderBy(c => c.Key)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    /// <summary>
    /// Retrieves a configuration by key with environment-specific fallback.
    /// </summary>
    /// <param name="key">The configuration key to search for.</param>
    /// <param name="environment">
    /// Optional environment filter. If null or empty, returns configs from ANY environment (no filtering).
    /// If specified, returns configs matching the exact environment OR environment="All",
    /// with exact environment matches prioritized over "All".
    /// </param>
    /// <param name="activeOnly">If true, only returns active configurations. If false, returns all configs.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The configuration if found, otherwise null.</returns>
    public async Task<SystemConfigurationAggregate?> GetByKeyAsync(string key, string? environment = null, bool activeOnly = true, CancellationToken cancellationToken = default)
    {
        var query = DbContext.Set<Api.Infrastructure.Entities.SystemConfigurationEntity>()
            .AsNoTracking()
            .Where(c => c.Key == key);

        // Filter by active status if requested
        if (activeOnly)
        {
            query = query.Where(c => c.IsActive);
        }

        // Filter and order by environment if specified
        if (!string.IsNullOrEmpty(environment))
        {
            // Filter: Match exact environment OR "All"
            query = query.Where(c => c.Environment == environment || c.Environment == "All");

            // Order: Prioritize environment-specific (0) over "All" (1)
            // Add tiebreakers for consistent ordering when multiple configs match
            query = query
                .OrderBy(c => c.Environment == environment ? 0 : 1)
                .ThenByDescending(c => c.UpdatedAt)
                .ThenByDescending(c => c.Version);
        }

        var entity = await query.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<IReadOnlyList<SystemConfigurationAggregate>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        var idList = ids.ToList();
        var entities = await DbContext.Set<Api.Infrastructure.Entities.SystemConfigurationEntity>()
            .AsNoTracking()
            .Where(c => idList.Contains(c.Id))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<SystemConfigurationAggregate>> GetByKeysAsync(IEnumerable<string> keys, string? environment = null, bool activeOnly = false, CancellationToken cancellationToken = default)
    {
        var keyList = keys.ToList();
        var query = DbContext.Set<Api.Infrastructure.Entities.SystemConfigurationEntity>()
            .AsNoTracking()
            .Where(c => keyList.Contains(c.Key));

        if (activeOnly)
            query = query.Where(c => c.IsActive);

        if (!string.IsNullOrEmpty(environment))
            query = query.Where(c => c.Environment == environment || c.Environment == "All");

        var entities = await query.ToListAsync(cancellationToken).ConfigureAwait(false);
        return entities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<SystemConfigurationAggregate>> GetByCategoryAsync(string category, CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.Set<Api.Infrastructure.Entities.SystemConfigurationEntity>()
            .AsNoTracking()
            .Where(c => c.Category == category)
            .OrderBy(c => c.Key)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<SystemConfigurationAggregate>> GetActiveConfigurationsAsync(CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.Set<Api.Infrastructure.Entities.SystemConfigurationEntity>()
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Key)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task AddAsync(SystemConfigurationAggregate config, CancellationToken cancellationToken = default)
    {
        CollectDomainEvents(config);
        var entity = MapToPersistence(config);
        await DbContext.Set<Api.Infrastructure.Entities.SystemConfigurationEntity>().AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public Task UpdateAsync(SystemConfigurationAggregate config, CancellationToken cancellationToken = default)
    {
        CollectDomainEvents(config);
        var entity = MapToPersistence(config);
        var entry = DbContext.Set<Api.Infrastructure.Entities.SystemConfigurationEntity>().Update(entity);

        // #4612: Update() segna come modificata anche ogni colonna che il mapper lascia al default. Quelle
        // dell'elenco restano come sono nella riga. Nell'entità che resta nel contesto valgono invece il default:
        // chi la rilegge tracciata nello stesso contesto le sovrascriva, non le usi. L'UPDATE parte comunque,
        // perché le altre colonne restano modificate.
        foreach (var column in ColumnsNotOwnedByTheAggregate)
        {
            entry.Property(column).IsModified = false;
        }

        return Task.CompletedTask;
    }

    public Task DeleteAsync(SystemConfigurationAggregate config, CancellationToken cancellationToken = default)
    {
        // Il ConfigurationDeletedEvent di MarkAsDeleted() vive sull'aggregato: senza questa raccolta non arriva
        // mai all'outbox, e la cancellazione non lascia traccia (#4608).
        CollectDomainEvents(config);
        var entity = MapToPersistence(config);
        DbContext.Set<Api.Infrastructure.Entities.SystemConfigurationEntity>().Remove(entity);
        return Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<Api.Infrastructure.Entities.SystemConfigurationEntity>()
            .AnyAsync(c => c.Id == id, cancellationToken).ConfigureAwait(false);
    }

    private static SystemConfigurationAggregate MapToDomain(Api.Infrastructure.Entities.SystemConfigurationEntity entity)
    {
        // Reconstitute, non il costruttore pubblico (#4608): quello è di una configurazione nuova, e solleva
        // ConfigurationCreatedEvent a ogni lettura.
        return SystemConfigurationAggregate.Reconstitute(
            id: entity.Id,
            key: new ConfigKey(entity.Key),
            value: entity.Value,
            valueType: entity.ValueType,
            description: entity.Description,
            category: entity.Category,
            isActive: entity.IsActive,
            requiresRestart: entity.RequiresRestart,
            environment: entity.Environment,
            version: entity.Version,
            previousValue: entity.PreviousValue,
            createdAt: entity.CreatedAt,
            updatedAt: entity.UpdatedAt,
            createdByUserId: entity.CreatedByUserId,
            updatedByUserId: entity.UpdatedByUserId,
            lastToggledAt: entity.LastToggledAt);
    }

    private static Api.Infrastructure.Entities.SystemConfigurationEntity MapToPersistence(SystemConfigurationAggregate domain)
    {
        return new Api.Infrastructure.Entities.SystemConfigurationEntity
        {
            Id = domain.Id,
            Key = domain.Key.Value,
            Value = domain.Value,
            ValueType = domain.ValueType,
            Description = domain.Description,
            Category = domain.Category,
            IsActive = domain.IsActive,
            RequiresRestart = domain.RequiresRestart,
            Environment = domain.Environment,
            Version = domain.Version,
            PreviousValue = domain.PreviousValue,
            CreatedAt = domain.CreatedAt,
            UpdatedAt = domain.UpdatedAt,
            CreatedByUserId = domain.CreatedByUserId,
            UpdatedByUserId = domain.UpdatedByUserId,
            LastToggledAt = domain.LastToggledAt
        };
    }
}
