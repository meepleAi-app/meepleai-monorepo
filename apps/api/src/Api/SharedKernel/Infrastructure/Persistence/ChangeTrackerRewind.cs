using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Api.SharedKernel.Infrastructure.Persistence;

/// <summary>
/// The change tracker and the collected domain events as the caller handed them to a transaction, so that every
/// attempt of the retrying execution strategy starts from there (#5145).
/// </summary>
/// <remarks>
/// <para>
/// <c>EnableRetryOnFailure</c> is on outside <c>Testing</c>, and the strategy runs the delegate again after a
/// transient failure. Without this, the retry found what the failed attempt had left in the tracker: after a
/// failed save, its rows still <c>Added</c>, written again next to the ones the retry adds; after a failed commit,
/// its rows and changes already accepted as <c>Unchanged</c>, so a change the retry repeated was not written, and
/// a row it had updated carried, as original <c>xmin</c>, the one of the rolled-back update.
/// </para>
/// <para>
/// Not a <c>ChangeTracker.Clear()</c>: what the caller tracked before the transaction stays, with the state and the
/// values it had. The PDF pipeline and <c>IndexPdfCommandHandler</c> track documents before and save them after.
/// Only the tracker is put back: a domain aggregate the caller built outside the delegate and the delegate
/// mutates is not an EF entity, and stays mutated.
/// </para>
/// </remarks>
internal sealed class ChangeTrackerRewind
{
    private readonly DbContext _context;
    private readonly IDomainEventCollector? _events;
    private readonly IReadOnlyList<EntrySnapshot> _entries;
    private readonly HashSet<object> _tracked;
    private readonly IReadOnlyList<IDomainEvent> _collected;

    private ChangeTrackerRewind(DbContext context, IDomainEventCollector? events)
    {
        _context = context;
        _events = events;
        _entries = context.ChangeTracker.Entries().Select(EntrySnapshot.Of).ToList();
        _tracked = new HashSet<object>(_entries.Select(e => e.Entity), ReferenceEqualityComparer.Instance);
        _collected = events?.PeekEvents().ToList() ?? [];
    }

    /// <summary>Takes the picture, before the first attempt.</summary>
    public static ChangeTrackerRewind Capture(DbContext context, IDomainEventCollector? events = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new ChangeTrackerRewind(context, events);
    }

    /// <summary>Puts the tracker and the events back as they were in the picture, before every attempt after the first.</summary>
    public void Restore()
    {
        // What the failed attempt brought in goes. Entries() detects changes first, so an entity the attempt only
        // reached through a navigation of a tracked one is found here too.
        foreach (var entry in _context.ChangeTracker.Entries().ToList())
        {
            if (!_tracked.Contains(entry.Entity))
                entry.State = EntityState.Detached;
        }

        foreach (var snapshot in _entries)
            snapshot.Restore(_context);

        if (_events is not null)
        {
            _events.Clear();
            foreach (var domainEvent in _collected)
                _events.Collect(domainEvent);
        }
    }

    private sealed record EntrySnapshot(
        object Entity,
        EntityState State,
        PropertyValues Original,
        PropertyValues Current,
        IReadOnlyList<string> Modified,
        IReadOnlyList<(string Name, object? Value)> References,
        IReadOnlyList<(string Name, IReadOnlyList<object>? Items)> Collections)
    {
        public static EntrySnapshot Of(EntityEntry entry) => new(
            entry.Entity,
            entry.State,
            entry.OriginalValues.Clone(),
            entry.CurrentValues.Clone(),
            entry.Properties.Where(p => p.IsModified).Select(p => p.Metadata.Name).ToList(),
            entry.References.Select(r => (r.Metadata.Name, r.CurrentValue)).ToList(),
            entry.Collections
                .Select(c => (c.Metadata.Name, c.CurrentValue is null ? null : (IReadOnlyList<object>)c.CurrentValue.Cast<object>().ToList()))
                .ToList());

        public void Restore(DbContext context)
        {
            var entry = context.Entry(Entity);

            // Attached and unchanged first: from there the values decide what is modified, as they did for the caller.
            entry.State = EntityState.Unchanged;
            entry.OriginalValues.SetValues(Original);
            entry.CurrentValues.SetValues(Current);

            foreach (var (name, value) in References)
            {
                var reference = entry.Reference(name);
                if (!ReferenceEquals(reference.CurrentValue, value))
                    reference.CurrentValue = value;
            }

            foreach (var (name, items) in Collections)
                RestoreCollection(entry.Collection(name), items);

            switch (State)
            {
                case EntityState.Added:
                case EntityState.Deleted:
                    entry.State = State;
                    break;
                case EntityState.Modified:
                    entry.DetectChanges();
                    foreach (var property in Modified)
                        entry.Property(property).IsModified = true;
                    break;
            }
        }

        private void RestoreCollection(CollectionEntry collection, IReadOnlyList<object>? items)
        {
            if (items is null || collection.CurrentValue is null)
                return;

            var accessor = collection.Metadata.GetCollectionAccessor();
            if (accessor is null)
                return;

            var wanted = new HashSet<object>(items, ReferenceEqualityComparer.Instance);
            foreach (var item in collection.CurrentValue.Cast<object>().ToList())
            {
                if (!wanted.Contains(item))
                    accessor.Remove(Entity, item);
            }

            foreach (var item in items)
            {
                if (!accessor.Contains(Entity, item))
                    accessor.Add(Entity, item, forMaterialization: false);
            }
        }
    }
}
