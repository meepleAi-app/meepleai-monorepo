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
    private readonly MeepleAiDbContext _dbContext;
    private readonly IDomainEventCollector _eventCollector;

    protected MeepleAiDbContext DbContext => _dbContext;
    protected IDomainEventCollector EventCollector => _eventCollector;

    protected RepositoryBase(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
        ArgumentNullException.ThrowIfNull(eventCollector);
        _eventCollector = eventCollector;
    }

    /// <summary>
    /// Collects domain events from an aggregate before persistence.
    /// Call this method in AddAsync, UpdateAsync before saving persistence entity.
    /// </summary>
    protected void CollectDomainEvents(IAggregateRoot aggregate)
    {
        if (aggregate == null) return;

        if (aggregate.DomainEvents.Count > 0)
        {
            EventCollector.CollectEventsFrom(aggregate);
            aggregate.ClearDomainEvents();
        }
    }
}
