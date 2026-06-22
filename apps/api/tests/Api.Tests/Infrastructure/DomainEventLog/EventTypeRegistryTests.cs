using System.Reflection;
using Api.BoundedContexts.Administration.Domain.Events;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
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

    /// <summary>
    /// Issue #1840 SP5 F4-C7 — AlertFiredEvent must be aliased so the
    /// AlertActivityFeed (SSE) and durable log row are emitted for both
    /// real threshold breaches and admin-triggered TestAlert dry-runs.
    /// </summary>
    [Fact]
    [Trait("Issue", "1840")]
    public void Registry_resolves_alert_fired_alias()
    {
        var ev = new AlertFiredEvent(
            RuleId: Guid.NewGuid(),
            RuleName: "high_error_rate",
            AlertType: "HighErrorRate",
            Metric: "meepleai_api_error_rate",
            Value: 0.07,
            Threshold: 0.05,
            ThresholdUnit: "%",
            Severity: AlertSeverityKind.Critical,
            Channels: new[] { "slack", "email" },
            IsDryRun: true,
            IsTest: true,
            TriggeredBy: "admin@meepleai.dev");

        EventTypeRegistry.TryResolve(ev).Should().Be("alert.fired");
    }

    /// <summary>
    /// Issue #1840 SP5 F4-C7 — AlertResolvedEvent companion alias so the
    /// activity feed can render the "after 12m 34s" delta cards.
    /// </summary>
    [Fact]
    [Trait("Issue", "1840")]
    public void Registry_resolves_alert_resolved_alias()
    {
        var ev = new AlertResolvedEvent(
            RuleId: Guid.NewGuid(),
            RuleName: "high_error_rate",
            FiredEventId: Guid.NewGuid(),
            Duration: TimeSpan.FromMinutes(12),
            IsTest: false);

        EventTypeRegistry.TryResolve(ev).Should().Be("alert.resolved");
    }

    /// <summary>
    /// Issue #2245 (epic #2242 Sub #3) — PdfStateChangedEvent must resolve so the admin
    /// LiveEventLog SSE stream renders real-time PDF pipeline transitions and the FE
    /// can drop polling. Revisits B3 (#1590 spec panel) — see EventTypeRegistry rationale.
    /// </summary>
    [Fact]
    [Trait("Issue", "2245")]
    public void Registry_resolves_pdf_state_changed_alias()
    {
        var ev = new PdfStateChangedEvent(
            pdfDocumentId: Guid.NewGuid(),
            previousState: PdfProcessingState.Indexing,
            newState: PdfProcessingState.Ready,
            uploadedByUserId: Guid.NewGuid());

        EventTypeRegistry.TryResolve(ev).Should().Be("pdf.state.changed");
    }

    /// <summary>
    /// Issue #2245 — the mapper must populate <c>AggregateId</c> and <c>UserId</c> for the
    /// SSE event. <see cref="PdfStateChangedEvent"/> exposes computed
    /// <c>AggregateId</c>/<c>UserId</c> properties (mirrors of <c>PdfDocumentId</c>/<c>UploadedByUserId</c>)
    /// so <see cref="DomainEventLogMapper.Map"/> reflection picks them up. If a future refactor drops
    /// those mirror properties, this test will fail.
    /// </summary>
    [Fact]
    [Trait("Issue", "2245")]
    public void Mapper_PdfStateChangedEvent_populates_aggregate_id_and_user_id()
    {
        var pdfId = Guid.NewGuid();
        var uploaderId = Guid.NewGuid();
        var ev = new PdfStateChangedEvent(
            pdfDocumentId: pdfId,
            previousState: PdfProcessingState.Indexing,
            newState: PdfProcessingState.Ready,
            uploadedByUserId: uploaderId);

        var row = DomainEventLogMapper.Map(ev);

        row.Should().NotBeNull("the event is registered for log persistence");
        row!.EventType.Should().Be("pdf.state.changed");
        row.AggregateId.Should().Be(pdfId, "AggregateId mirrors PdfDocumentId so the mapper can populate domain_event_logs.aggregate_id");
        row.UserId.Should().Be(uploaderId, "UserId mirrors UploadedByUserId so the mapper can populate domain_event_logs.user_id");
        row.AggregateType.Should().Be("PdfStateChanged", "class name minus 'Event' suffix → aggregate type");
    }
}
