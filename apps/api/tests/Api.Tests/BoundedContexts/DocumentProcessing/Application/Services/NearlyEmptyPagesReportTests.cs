using System.Diagnostics.Metrics;
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.Observability;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// Serializes the classes that read the <c>meepleai.pdf.extraction.nearly_empty</c> counter through a <c>MeterListener</c>:
/// a listener is global to the Meter, so another test that records the same counter at the same time would add to it.
/// </summary>
[CollectionDefinition("NearlyEmptyPagesMetrics", DisableParallelization = true)]
public sealed class NearlyEmptyPagesMetricsCollection
{
}

/// <summary>
/// #4931 — the signal for the pages a text-layer extraction leaves empty has to be observable without reading the log of
/// every document: a counter with three series (no PDF label) and a Warning only for the documents the rule flags.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "4931")]
[Collection("NearlyEmptyPagesMetrics")]
public sealed class NearlyEmptyPagesReportTests
{
    private const string PdfId = "11111111-2222-3333-4444-555555555555";

    private static PagedTextExtractionResult Extraction(string pattern, bool textLayerOnly = true) =>
        PagedTextExtractionResult.CreateSuccess(
            pattern.Select((c, i) => new PageTextChunk(i + 1, c == 'T' ? new string('a', 300) : "", 0, c == 'T' ? 299 : 0)).ToArray(),
            pattern.Length,
            pattern.Count(c => c == 'T') * 300,
            ocrTriggered: false) with { TextLayerOnly = textLayerOnly };

    /// <summary>Runs <see cref="NearlyEmptyPagesReport.Record"/> and returns the logs and the counter's outcomes it produced.</summary>
    private static (List<LogLevel> Levels, List<string?> Outcomes) Record(PagedTextExtractionResult extraction)
    {
        var levels = new List<LogLevel>();
        var logger = new Mock<ILogger>();
        logger.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        logger.Setup(l => l.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Callback(new InvocationAction(invocation => levels.Add((LogLevel)invocation.Arguments[0])));

        var outcomes = new List<string?>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Name == "meepleai.pdf.extraction.nearly_empty")
                {
                    l.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
        {
            foreach (var tag in tags)
            {
                if (tag.Key == "outcome")
                {
                    outcomes.AddRange(Enumerable.Repeat(tag.Value as string, (int)value));
                }
            }
        });
        listener.Start();

        NearlyEmptyPagesReport.Record(logger.Object, PdfId, extraction);

        return (levels, outcomes);
    }

    [Fact]
    public void Record_ForAScan_LogsOneWarning_AndCountsItAsFlagged()
    {
        var (levels, outcomes) = Record(Extraction("T......."));

        levels.Should().Equal(LogLevel.Warning);
        outcomes.Should().Equal(MeepleAiMetrics.NearlyEmptyFlagged);
    }

    [Fact]
    public void Record_ForACover_LogsNoWarning_AndCountsItAsPartial()
    {
        var (levels, outcomes) = Record(Extraction(".TTTTTTT"));

        levels.Should().Equal(LogLevel.Debug).And.NotContain(LogLevel.Warning).And.NotContain(LogLevel.Information);
        outcomes.Should().Equal(MeepleAiMetrics.NearlyEmptyPartial);
    }

    [Fact]
    public void Record_ForADocumentMadeOfText_LogsNothing_AndCountsItAsClean()
    {
        var (levels, outcomes) = Record(Extraction("TTTT"));

        levels.Should().BeEmpty();
        outcomes.Should().Equal(MeepleAiMetrics.NearlyEmptyClean);
    }

    [Fact]
    public void Record_WhenTheExtractorIsNotTextLayerOnly_DoesNothing_NotEvenACleanCount()
    {
        var (levels, outcomes) = Record(Extraction("....", textLayerOnly: false));

        levels.Should().BeEmpty();
        outcomes.Should().BeEmpty("counting this document as clean would say the extractor looked at its text layer");
    }

    [Fact]
    public void TheCounterHasThreeOutcomesAndNoPerPdfLabel()
    {
        // Cardinality is part of the contract (#4931): the issue forbids a metric with a series per PDF.
        var tagKeys = new List<string>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Name == "meepleai.pdf.extraction.nearly_empty")
                {
                    l.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>((_, _, tags, _) => tagKeys.AddRange(tags.ToArray().Select(t => t.Key)));
        listener.Start();

        NearlyEmptyPagesReport.Record(new Mock<ILogger>().Object, PdfId, Extraction("T......."));

        tagKeys.Should().Equal("outcome");
    }
}
