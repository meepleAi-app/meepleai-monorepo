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

    /// <summary>
    /// Collects domain events from a domain object that implements
    /// <see cref="IDomainEventSource"/> but is not a full <see cref="IAggregateRoot"/>
    /// subclass (e.g. <c>Session</c> in SessionTracking BC — BE-3 #1590).
    /// Because <see cref="IDomainEventSource"/> extends <see cref="IAggregateRoot"/> the
    /// existing <see cref="IDomainEventCollector.CollectEventsFrom"/> overload is reused
    /// without any modification to the collector.
    /// </summary>
    protected void CollectDomainEvents(IDomainEventSource source)
    {
        if (source == null) return;

        if (source.DomainEvents.Count > 0)
        {
            EventCollector.CollectEventsFrom(source);
            source.ClearDomainEvents();
        }
    }
}
