using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.Enums;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SystemConfiguration;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SystemConfiguration.Infrastructure.Persistence;

/// <summary>
/// Issue #1089: EF Core repository for the singleton incident banner row.
/// </summary>
public sealed class IncidentBannerRepository : RepositoryBase, IIncidentBannerRepository
{
    public IncidentBannerRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<IncidentBannerState> GetAsync(CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.Set<IncidentBannerStateEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == IncidentBannerState.SingletonId, cancellationToken)
            .ConfigureAwait(false);

        if (entity == null)
        {
            // Seed missing singleton defensively (migration normally seeds it).
            var seeded = IncidentBannerState.Create(
                message: string.Empty,
                severity: BannerSeverity.Info,
                isActive: false,
                startsAt: null,
                endsAt: null,
                updatedBy: null);

            var seedEntity = MapToEntity(seeded);
            await DbContext.Set<IncidentBannerStateEntity>().AddAsync(seedEntity, cancellationToken).ConfigureAwait(false);
            await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return seeded;
        }

        return MapToDomain(entity);
    }

    public async Task UpdateAsync(IncidentBannerState entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var existing = await DbContext.Set<IncidentBannerStateEntity>()
            .FirstOrDefaultAsync(e => e.Id == IncidentBannerState.SingletonId, cancellationToken)
            .ConfigureAwait(false);

        if (existing == null)
        {
            await DbContext.Set<IncidentBannerStateEntity>().AddAsync(MapToEntity(entity), cancellationToken).ConfigureAwait(false);
        }
        else
        {
            existing.Message = entity.Message;
            existing.Severity = (int)entity.Severity;
            existing.IsActive = entity.IsActive;
            existing.StartsAt = entity.StartsAt;
            existing.EndsAt = entity.EndsAt;
            existing.UpdatedAt = entity.UpdatedAt;
            existing.UpdatedBy = entity.UpdatedBy;
        }

        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static IncidentBannerState MapToDomain(IncidentBannerStateEntity entity)
    {
        var state = (IncidentBannerState)System.Runtime.CompilerServices.RuntimeHelpers
            .GetUninitializedObject(typeof(IncidentBannerState));

        typeof(IncidentBannerState).GetProperty(nameof(IncidentBannerState.Id))!.SetValue(state, entity.Id);
        typeof(IncidentBannerState).GetProperty(nameof(IncidentBannerState.Message))!.SetValue(state, entity.Message);
        typeof(IncidentBannerState).GetProperty(nameof(IncidentBannerState.Severity))!.SetValue(state, (BannerSeverity)entity.Severity);
        typeof(IncidentBannerState).GetProperty(nameof(IncidentBannerState.IsActive))!.SetValue(state, entity.IsActive);
        typeof(IncidentBannerState).GetProperty(nameof(IncidentBannerState.StartsAt))!.SetValue(state, entity.StartsAt);
        typeof(IncidentBannerState).GetProperty(nameof(IncidentBannerState.EndsAt))!.SetValue(state, entity.EndsAt);
        typeof(IncidentBannerState).GetProperty(nameof(IncidentBannerState.CreatedAt))!.SetValue(state, entity.CreatedAt);
        typeof(IncidentBannerState).GetProperty(nameof(IncidentBannerState.UpdatedAt))!.SetValue(state, entity.UpdatedAt);
        typeof(IncidentBannerState).GetProperty(nameof(IncidentBannerState.UpdatedBy))!.SetValue(state, entity.UpdatedBy);

        return state;
    }

    private static IncidentBannerStateEntity MapToEntity(IncidentBannerState domain)
    {
        return new IncidentBannerStateEntity
        {
            Id = domain.Id,
            Message = domain.Message ?? string.Empty,
            Severity = (int)domain.Severity,
            IsActive = domain.IsActive,
            StartsAt = domain.StartsAt,
            EndsAt = domain.EndsAt,
            CreatedAt = domain.CreatedAt,
            UpdatedAt = domain.UpdatedAt,
            UpdatedBy = domain.UpdatedBy
        };
    }
}
