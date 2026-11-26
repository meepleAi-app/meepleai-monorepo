using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
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
/// Not a <c>ChangeTracker.Clear()</c>: what the caller tracked before the transaction stays, with the state and the
/// property values it had. The PDF pipeline and <c>IndexPdfCommandHandler</c> track documents before and save them
/// after, and the pipeline's unsaved vector document is sent again by the retry's save.
/// </para>
/// <para>
/// Of the navigations, the references are in the picture and the collections are not. Putting a reference back makes
/// EF fix up the collection on the other side — an orphan the failed attempt took out of its parent's collection goes
/// back into it, and so does the join row of a many-to-many link. Without the references the orphan stayed out, and
/// the retry's <c>Remove</c> had nothing to remove (measured); putting the collections back by hand on top changed
/// nothing. The references are read through the model, not through <c>EntityEntry.References</c>, which detects
/// changes for every entry it builds: with a large collection on the principal side, that read grew quadratically.
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
        // A Mock.Of collector answers PeekEvents() with null (MeepleAiDbContext guards the same way).
        _collected = events?.PeekEvents()?.ToList() ?? [];
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
        // One pass. The collections are not put back by hand: EF's fix-up puts the item back when its reference or its
        // join row returns, and moves EF's own snapshot of the collection with it, so a retry that takes the item out
        // again leaves a difference to detect. Put back by hand, a collection looked to EF like an addition, and it
        // took a detection and a second pass to undo it.
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
        IReadOnlyList<(INavigation Navigation, object? Value)> References)
    {
        public object Entity => Entry.Entity;

        public static EntrySnapshot Of(EntityEntry entry) => new(
            entry,
            entry.State,
            entry.OriginalValues.Clone(),
            entry.CurrentValues.Clone(),
            entry.Properties.Where(p => p.IsModified).Select(p => p.Metadata.Name).ToList(),
            entry.Metadata.GetNavigations()
                .Where(n => !n.IsCollection)
                .Select(n => (n, n.GetGetter().GetClrValueUsingContainingEntity(entry.Entity)))
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

            foreach (var (navigation, value) in References)
            {
                if (!ReferenceEquals(navigation.GetGetter().GetClrValueUsingContainingEntity(entry.Entity), value))
                    entry.Reference(navigation.Name).CurrentValue = value;
            }

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
    }
}
