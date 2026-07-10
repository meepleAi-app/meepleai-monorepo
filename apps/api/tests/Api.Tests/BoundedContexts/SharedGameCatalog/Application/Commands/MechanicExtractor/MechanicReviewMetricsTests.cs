using System.Diagnostics.Metrics;
using Api.Observability;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

/// <summary>
/// #526 ME-M1.4 admin-review observability (AC-7): asserts
/// <see cref="MeepleAiMetrics.MechanicReviewBulkActions"/> emits the expected {action} tag shape,
/// captured via System.Diagnostics.Metrics MeterListener — same pattern as
/// <c>MechanicValidatorMetricsTests</c> (#2494 / ME-M1.3 AC-7).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class MechanicReviewMetricsTests
{
    private const string Counter = "mechanic_review_bulk_actions_total";

    [Fact]
    public void MechanicReviewBulkActions_Emits_ActionTag()
    {
        var events = new List<string>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == MeepleAiMetrics.MeterName && instrument.Name == Counter)
                    l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
        {
            foreach (var tag in tags)
                if (tag.Key == "action" && tag.Value is string a) events.Add(a);
        });
        listener.Start();

        MeepleAiMetrics.MechanicReviewBulkActions.Add(1, new System.Diagnostics.TagList { { "action", "bulk_reject" } });

        events.Should().ContainSingle().Which.Should().Be("bulk_reject");
    }
}
