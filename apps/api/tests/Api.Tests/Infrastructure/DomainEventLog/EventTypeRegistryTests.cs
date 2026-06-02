using System.Reflection;
using Api.BoundedContexts.DocumentProcessing.Domain.Events;
using Api.Infrastructure.DomainEventLog;
using Api.SharedKernel.Domain.Interfaces;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Infrastructure.DomainEventLog;

/// <summary>
/// Tests for <see cref="EventTypeRegistry"/> — issue #661.
///
/// AC-10 (revised post-panel for opt-in): the registry MAY be empty, but every
/// entry that exists MUST resolve to a real <see cref="IDomainEvent"/>
/// implementation in the loaded assemblies. A stale alias (class renamed or
/// deleted without updating the registry) is a build failure.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Issue", "661")]
public sealed class EventTypeRegistryTests
{
    /// <summary>
    /// Every CLR type in <see cref="EventTypeRegistry.AliasByType"/> must
    /// (a) exist in the Api assembly and (b) implement <see cref="IDomainEvent"/>.
    /// If a class is renamed or moved without updating the registry, this test
    /// fails and forces the author to make a deliberate choice.
    /// </summary>
    [Fact]
    public void AliasByType_AllEntriesResolveToLoadedIDomainEvent()
    {
        var apiAssembly = typeof(EventTypeRegistry).Assembly;

        foreach (var entry in EventTypeRegistry.AliasByType)
        {
            var (clrType, alias) = (entry.Key, entry.Value);

            // The type must be assignable to IDomainEvent.
            typeof(IDomainEvent).IsAssignableFrom(clrType)
                .Should().BeTrue(
                    "registry alias '{0}' maps to {1} which must implement IDomainEvent",
                    alias, clrType.FullName);

            // The type must be loadable from the API assembly (catches stale
            // entries pointing to deleted types).
            apiAssembly.GetType(clrType.FullName ?? string.Empty)
                .Should().NotBeNull(
                    "registry alias '{0}' points to {1} which is no longer present in {2}",
                    alias, clrType.FullName, apiAssembly.GetName().Name);
        }
    }

    /// <summary>
    /// Aliases must be unique. Two events sharing the same alias would conflate
    /// log rows under a single tag at query time — silent data corruption.
    /// </summary>
    [Fact]
    public void AliasByType_AllAliasesAreUnique()
    {
        var duplicates = EventTypeRegistry.AliasByType
            .GroupBy(kvp => kvp.Value, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        duplicates.Should().BeEmpty(
            "every registered alias must map to exactly one event type (found duplicates: {0})",
            string.Join(", ", duplicates));
    }

    /// <summary>
    /// TryResolve returns null for unregistered events — that's the opt-in
    /// contract. Verified with a stub event that's never going to be registered.
    /// </summary>
    [Fact]
    public void TryResolve_UnregisteredEvent_ReturnsNull()
    {
        var unregistered = new UnregisteredStubEvent();
        EventTypeRegistry.TryResolve(unregistered).Should().BeNull();
    }

    /// <summary>
    /// TryResolve throws on null input — guard against accidental nulls in
    /// the DbContext call site.
    /// </summary>
    [Fact]
    public void TryResolve_Null_Throws()
    {
        Action act = () => EventTypeRegistry.TryResolve(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    private sealed record UnregisteredStubEvent : IDomainEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
        public DateTime OccurredAt { get; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Issue #1687 Task 4 — the metadata-change event must be registered for
    /// durable persistence. Without this entry the audit-log handler (D-11)
    /// never sees the event because the mapper returns null.
    /// </summary>
    [Fact]
    [Trait("Issue", "1687")]
    public void Registry_resolves_pdf_metadata_changed_alias()
    {
        var ev = new PdfMetadataChangedEvent(
            AggregateId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            EditorRole: "Owner",
            Changes: Array.Empty<MetadataChange>(),
            GameId: null);

        EventTypeRegistry.TryResolve(ev).Should().Be("pdf.metadata.changed");
    }
}
