using System.Diagnostics.Metrics;
using Api.Observability;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Verifies the <see cref="MeepleAiMetrics.KbGlobalSearchFacetTotal"/> counter (#1731 / D-14).
/// Uses System.Diagnostics.Metrics MeterListener to capture measurement events
/// — same pattern as RagPromptAssemblyServiceFallbackMetricsTests.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class GlobalKbSearchFacetMetricsTests
{
    private const string CounterName = "meepleai.kb.global_search.facet.total";

    /// <summary>
    /// AC: RecordKbGlobalSearchFacet(facet, state) emits one Counter event with both tags.
    /// </summary>
    [Fact]
    public void RecordKbGlobalSearchFacet_DocTypeApplied_EmitsSingleEventWithCorrectTags()
    {
        using var capture = new FacetCapture();

        MeepleAiMetrics.RecordKbGlobalSearchFacet(
            MeepleAiMetrics.KbGlobalSearchFacetTypes.DocType,
            MeepleAiMetrics.KbGlobalSearchFacetStates.Applied);

        capture.Events.Should().ContainSingle()
            .Which.Should().Be(("docType", "applied"));
    }

    /// <summary>
    /// AC: state=rejected dimension captured correctly when emitted.
    /// </summary>
    [Fact]
    public void RecordKbGlobalSearchFacet_GameIdRejected_EmitsCorrectTags()
    {
        using var capture = new FacetCapture();

        MeepleAiMetrics.RecordKbGlobalSearchFacet(
            MeepleAiMetrics.KbGlobalSearchFacetTypes.GameId,
            MeepleAiMetrics.KbGlobalSearchFacetStates.Rejected);

        capture.Events.Should().ContainSingle()
            .Which.Should().Be(("gameId", "rejected"));
    }

    /// <summary>
    /// Theory: every (facet, state) combination is emitted exactly once
    /// when RecordKbGlobalSearchFacet is called with that pair.
    /// Validates the cardinality contract (D-14: 3 facets × 2 states = 6 series).
    /// </summary>
    [Theory]
    [InlineData("docType", "applied")]
    [InlineData("docType", "rejected")]
    [InlineData("gameId", "applied")]
    [InlineData("gameId", "rejected")]
    [InlineData("language", "applied")]
    [InlineData("language", "rejected")]
    public void RecordKbGlobalSearchFacet_AllCombinations_EmitExactlyOneEvent(
        string facet, string state)
    {
        using var capture = new FacetCapture();

        MeepleAiMetrics.RecordKbGlobalSearchFacet(facet, state);

        capture.Events.Should().ContainSingle()
            .Which.Should().Be((facet, state));
    }

    /// <summary>
    /// Captures Counter measurements for the facet instrument and exposes a list
    /// of (facet, state) tag pairs for assertion.
    /// Mirror of FallbackCapture pattern from RagPromptAssemblyServiceFallbackMetricsTests.
    /// </summary>
    private sealed class FacetCapture : IDisposable
    {
        private readonly MeterListener _listener;
        public List<(string Facet, string State)> Events { get; } = new();

        public FacetCapture()
        {
            _listener = new MeterListener
            {
                InstrumentPublished = (instrument, l) =>
                {
                    if (instrument.Meter.Name == MeepleAiMetrics.MeterName
                        && instrument.Name == CounterName)
                    {
                        l.EnableMeasurementEvents(instrument);
                    }
                }
            };
            _listener.SetMeasurementEventCallback<long>((instrument, _, tags, _) =>
            {
                string facet = "<missing>";
                string state = "<missing>";
                foreach (var tag in tags)
                {
                    if (tag.Key == "facet" && tag.Value is string f)
                    {
                        facet = f;
                    }
                    else if (tag.Key == "state" && tag.Value is string s)
                    {
                        state = s;
                    }
                }
                Events.Add((facet, state));
            });
            _listener.Start();
        }

        public void Dispose() => _listener.Dispose();
    }
}
