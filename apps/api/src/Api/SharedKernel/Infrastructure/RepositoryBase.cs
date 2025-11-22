using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.Interfaces;

namespace Api.SharedKernel.Infrastructure;

/// <summary>
/// Base repository class providing domain event collection for all repositories.
/// Ensures domain events are collected from aggregates before persistence.
/// </summary>
public abstract class RepositoryBase
{
    protected readonly MeepleAiDbContext DbContext;
    protected readonly IDomainEventCollector EventCollector;

    protected RepositoryBase(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
    {
        DbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        EventCollector = eventCollector ?? throw new ArgumentNullException(nameof(eventCollector));
    }

    /// <summary>
    /// Collects domain events from an aggregate before persistence.
    /// Call this method in AddAsync, UpdateAsync before saving persistence entity.
    /// </summary>
    protected void CollectDomainEvents(IAggregateRoot aggregate)
    {
        if (aggregate == null) return;

        if (aggregate.DomainEvents.Any())
        {
            EventCollector.CollectEventsFrom(aggregate);
            aggregate.ClearDomainEvents();
        }
    }
}
