using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;

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
/// Not a <c>ChangeTracker.Clear()</c>: what the caller tracked before the transaction stays, with the state, the
/// property values and the navigations it had. The PDF pipeline and <c>IndexPdfCommandHandler</c> track documents
/// before and save them after, and the pipeline's unsaved vector document is sent again by the retry's save.
/// </para>
/// <para>
/// What it does not put back, and that no caller needs today:
/// <list type="bullet">
/// <item>a domain aggregate the caller built outside the delegate and the delegate mutates — it is not an EF entity
/// (#5158);</item>
/// <item>a mutable value mutated in place (a <c>List&lt;string&gt;</c> column): the picture holds the reference,
/// not a copy;</item>
/// <item>an owned value the failed attempt replaced on an entity tracked before: EF loses the relationship snapshot
/// of the owner, and the retry's replacement is refused;</item>
/// <item>events an aggregate raised outside the delegate but that the repository collects inside it: the retry
/// collects them again only if the delegate raises them again.</item>
/// </list>
/// </para>
/// <para>
/// The picture is taken only when the strategy retries (<see cref="IExecutionStrategy.RetriesOnFailure"/>), and its
/// cost grows with the tracker: a scope that keeps thousands of rows tracked across calls pays for them every time.
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

    /// <summary>
    /// Takes the picture, before the first attempt — or nothing, when <paramref name="strategy"/> never runs the
    /// delegate twice and there will be no attempt to rewind.
    /// </summary>
    public static ChangeTrackerRewind? CaptureIfRetrying(
        IExecutionStrategy strategy, DbContext context, IDomainEventCollector? events = null)
    {
        ArgumentNullException.ThrowIfNull(strategy);
        ArgumentNullException.ThrowIfNull(context);
        return strategy.RetriesOnFailure ? new ChangeTrackerRewind(context, events) : null;
    }

    /// <summary>Puts the tracker and the events back as they were in the picture, before every attempt after the first.</summary>
    public void Restore()
    {
        // Two passes. The first puts the values, the states and the navigations back. EF keeps its own snapshot of
        // each navigation, though, and the failed attempt's accepted save moved it: a collection put back looks, to
        // EF, like an addition, and a delegate that removes the item again before the next detection leaves no
        // difference to see. The detection in between brings EF's snapshots in line with what was put back — finding
        // the join rows the first pass attached rather than making new ones — and the second pass undoes what that
        // detection changed.
        ForgetWhatTheAttemptBroughtIn();
        PutThePictureBack();
        _context.ChangeTracker.DetectChanges();
        ForgetWhatTheAttemptBroughtIn();
        PutThePictureBack();

        if (_events is not null)
        {
            _events.Clear();
            foreach (var domainEvent in _collected)
                _events.Collect(domainEvent);
        }
    }

    // What the failed attempt brought in goes. Entries() detects changes first, so an entity the attempt only reached
    // through a navigation of a tracked one is found here too.
    private void ForgetWhatTheAttemptBroughtIn()
    {
        foreach (var entry in _context.ChangeTracker.Entries().ToList())
        {
            if (!_tracked.Contains(entry.Entity))
                entry.State = EntityState.Detached;
        }
    }

    // An entity of the picture that the failed attempt detached (a deletion accepted before the commit failed) is
    // attached again through the entry the picture kept, which knows its entity type: a shared-type entity — a
    // many-to-many join row — cannot be found again from its CLR object alone.
    private void PutThePictureBack()
    {
        var tracked = _context.ChangeTracker.Entries()
            .ToDictionary(e => e.Entity, e => e, ReferenceEqualityComparer.Instance);
        foreach (var snapshot in _entries)
            snapshot.Restore(tracked.TryGetValue(snapshot.Entity, out var entry) ? entry : snapshot.Entry);
    }

    private sealed record EntrySnapshot(
        EntityEntry Entry,
        EntityState State,
        PropertyValues Original,
        PropertyValues Current,
        IReadOnlyList<string> Modified,
        IReadOnlyList<(string Name, object? Value)> References,
        IReadOnlyList<(string Name, IReadOnlyList<object>? Items)> Collections)
    {
        public object Entity => Entry.Entity;

        public static EntrySnapshot Of(EntityEntry entry) => new(
            entry,
            entry.State,
            entry.OriginalValues.Clone(),
            entry.CurrentValues.Clone(),
            entry.Properties.Where(p => p.IsModified).Select(p => p.Metadata.Name).ToList(),
            entry.References.Select(r => (r.Metadata.Name, r.CurrentValue)).ToList(),
            entry.Collections
                .Select(c => (c.Metadata.Name, c.CurrentValue is null ? null : (IReadOnlyList<object>)c.CurrentValue.Cast<object>().ToList()))
                .ToList());

        public void Restore(EntityEntry entry)
        {
            // Attached and unchanged, with the original values of the picture. Setting an original value can flag the
            // property as modified, so the current values go to the originals and the state back to unchanged; then
            // the current values of the picture flag exactly the properties that differ, as they did for the caller.
            entry.State = EntityState.Unchanged;
            entry.OriginalValues.SetValues(Original);
            entry.CurrentValues.SetValues(Original);
            entry.State = EntityState.Unchanged;
            entry.CurrentValues.SetValues(Current);

            foreach (var (name, value) in References)
            {
                var reference = entry.Reference(name);
                if (!ReferenceEquals(reference.CurrentValue, value))
                    reference.CurrentValue = value;
            }

            foreach (var (name, items) in Collections)
                RestoreCollection(entry, entry.Collection(name), items);

            switch (State)
            {
                case EntityState.Added:
                case EntityState.Deleted:
                    entry.State = State;
                    break;
                case EntityState.Modified:
                    // A property the caller marked modified without changing its value (Update() marks them all).
                    foreach (var property in Modified)
                        entry.Property(property).IsModified = true;
                    break;
            }
        }

        private static void RestoreCollection(EntityEntry owner, CollectionEntry collection, IReadOnlyList<object>? items)
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
                    accessor.Remove(owner.Entity, item);
            }

            foreach (var item in items)
            {
                if (!accessor.Contains(owner.Entity, item))
                    accessor.Add(owner.Entity, item, forMaterialization: false);
            }
        }
    }
}
