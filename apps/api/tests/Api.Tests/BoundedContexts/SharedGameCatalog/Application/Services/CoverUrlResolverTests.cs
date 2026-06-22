using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Infrastructure.Entities.UserLibrary;
using Api.Observability;
using Api.Services.Pdf;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Services;

public class CoverUrlResolverTests
{
    private readonly Mock<IBlobStorageService> _blob = new();

    /// <summary>
    /// Issue #2123 — captures every long measurement emitted on
    /// <c>meepleai.cover.resolution.</c> instruments via <see cref="MeterListener"/>.
    /// Pattern copied from <c>GetSharedGameByIdQueryHandlerMetricsTests</c>.
    /// </summary>
    private sealed class CoverMetricsCapture : IDisposable
    {
        private readonly MeterListener _listener;
        public ConcurrentBag<(string Name, long Value, IReadOnlyDictionary<string, object?> Tags)> LongMeasurements { get; } = new();

        public CoverMetricsCapture()
        {
            _listener = new MeterListener
            {
                InstrumentPublished = (instrument, l) =>
                {
                    if (instrument.Meter.Name == MeepleAiMetrics.MeterName &&
                        instrument.Name.StartsWith("meepleai.cover.", StringComparison.Ordinal))
                    {
                        l.EnableMeasurementEvents(instrument);
                    }
                }
            };
            _listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
            {
                LongMeasurements.Add((instrument.Name, measurement, ToDict(tags)));
            });
            _listener.Start();
        }

        private static IReadOnlyDictionary<string, object?> ToDict(ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            var d = new Dictionary<string, object?>(tags.Length, StringComparer.Ordinal);
            foreach (var t in tags)
            {
                d[t.Key] = t.Value;
            }
            return d;
        }

        public void Dispose() => _listener.Dispose();
    }

    [Fact]
    public async Task ResolveForUserAsync_L3CustomCover_HasHighestPriority()
    {
        var sg = new SharedGameEntity { PdfCoverR2Key = "pdf-key", WikidataCoverR2Key = "wiki-key" };
        var entry = new UserLibraryEntryEntity { CustomCoverR2Key = "custom-key" };
        _blob.Setup(b => b.GetPresignedDownloadUrlAsync("custom-key.webp", BlobCategory.GameImage, "custom-key", null))
             .ReturnsAsync("https://r2/custom.webp");

        var url = await CoverUrlResolver.ResolveForUserAsync(sg, entry, _blob.Object);

        url.Should().Be("https://r2/custom.webp");
        _blob.Verify(b => b.GetPresignedDownloadUrlAsync("pdf-key-preview.webp", It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<int?>()), Times.Never);
    }

    [Fact]
    public async Task ResolveForUserAsync_NoL3_FallsBackToL4()
    {
        var sg = new SharedGameEntity { PdfCoverR2Key = "pdf-key", WikidataCoverR2Key = "wiki-key" };
        var entry = new UserLibraryEntryEntity { CustomCoverR2Key = null };
        _blob.Setup(b => b.GetPresignedDownloadUrlAsync("pdf-key-preview.webp", BlobCategory.GameImage, "pdf-key", null))
             .ReturnsAsync("https://r2/pdf.webp");

        var url = await CoverUrlResolver.ResolveForUserAsync(sg, entry, _blob.Object);

        url.Should().Be("https://r2/pdf.webp");
    }

    [Fact]
    public async Task ResolvePublicAsync_L4WinsOverL2()
    {
        var sg = new SharedGameEntity { PdfCoverR2Key = "pdf-key", WikidataCoverR2Key = "wiki-key" };
        _blob.Setup(b => b.GetPresignedDownloadUrlAsync("pdf-key-preview.webp", BlobCategory.GameImage, "pdf-key", null))
             .ReturnsAsync("https://r2/pdf.webp");

        var url = await CoverUrlResolver.ResolvePublicAsync(sg, _blob.Object);

        url.Should().Be("https://r2/pdf.webp");
    }

    [Fact]
    public async Task ResolvePublicAsync_NoL4_FallsBackToL2()
    {
        var sg = new SharedGameEntity { PdfCoverR2Key = null, WikidataCoverR2Key = "wiki-key" };
        _blob.Setup(b => b.GetPresignedDownloadUrlAsync("wiki-key.webp", BlobCategory.GameImage, "wiki-key", null))
             .ReturnsAsync("https://r2/wiki.webp");

        var url = await CoverUrlResolver.ResolvePublicAsync(sg, _blob.Object);

        url.Should().Be("https://r2/wiki.webp");
    }

    [Fact]
    public async Task ResolvePublicAsync_AllNull_ReturnsNull()
    {
        var sg = new SharedGameEntity { PdfCoverR2Key = null, WikidataCoverR2Key = null };

        var url = await CoverUrlResolver.ResolvePublicAsync(sg, _blob.Object);

        url.Should().BeNull();
    }

    [Fact]
    public async Task ResolvePublicAsync_PresignedReturnsNull_FallsThroughToNextLayer()
    {
        var sg = new SharedGameEntity { PdfCoverR2Key = "pdf-key", WikidataCoverR2Key = "wiki-key" };
        _blob.Setup(b => b.GetPresignedDownloadUrlAsync("pdf-key-preview.webp", It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<int?>()))
             .ReturnsAsync((string?)null);
        _blob.Setup(b => b.GetPresignedDownloadUrlAsync("wiki-key.webp", It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<int?>()))
             .ReturnsAsync("https://r2/wiki.webp");

        var url = await CoverUrlResolver.ResolvePublicAsync(sg, _blob.Object);

        url.Should().Be("https://r2/wiki.webp");
    }

    // ----- Issue #2123 metric emission tests --------------------------------

    [Fact]
    public async Task ResolveForUserAsync_EmitsR2UserMetric_WhenL3CustomCoverWins()
    {
        using var capture = new CoverMetricsCapture();
        var sg = new SharedGameEntity();
        var entry = new UserLibraryEntryEntity { CustomCoverR2Key = "custom-key" };
        _blob.Setup(b => b.GetPresignedDownloadUrlAsync("custom-key.webp", BlobCategory.GameImage, "custom-key", null))
             .ReturnsAsync("https://r2/custom.webp");

        await CoverUrlResolver.ResolveForUserAsync(sg, entry, _blob.Object);

        capture.LongMeasurements.Should().ContainSingle(m =>
            m.Name == "meepleai.cover.resolution.total" &&
            m.Value == 1 &&
            string.Equals(m.Tags["source"] as string, "r2_user", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ResolvePublicAsync_EmitsR2PdfMetric_WhenL4PdfCoverWins()
    {
        using var capture = new CoverMetricsCapture();
        var sg = new SharedGameEntity { PdfCoverR2Key = "pdf-key" };
        _blob.Setup(b => b.GetPresignedDownloadUrlAsync("pdf-key-preview.webp", BlobCategory.GameImage, "pdf-key", null))
             .ReturnsAsync("https://r2/pdf.webp");

        await CoverUrlResolver.ResolvePublicAsync(sg, _blob.Object);

        capture.LongMeasurements.Should().ContainSingle(m =>
            m.Name == "meepleai.cover.resolution.total" &&
            m.Value == 1 &&
            string.Equals(m.Tags["source"] as string, "r2_pdf", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ResolvePublicAsync_EmitsR2BggMetric_WhenL25BggCoverWins()
    {
        using var capture = new CoverMetricsCapture();
        var sg = new SharedGameEntity { BggCoverR2Key = "bgg-key" };
        _blob.Setup(b => b.GetPresignedDownloadUrlAsync("bgg-key", BlobCategory.GameImage, "bgg-key", null))
             .ReturnsAsync("https://r2/bgg.jpg");

        await CoverUrlResolver.ResolvePublicAsync(sg, _blob.Object);

        capture.LongMeasurements.Should().ContainSingle(m =>
            m.Name == "meepleai.cover.resolution.total" &&
            m.Value == 1 &&
            string.Equals(m.Tags["source"] as string, "r2_bgg", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ResolvePublicAsync_EmitsR2WikidataMetric_WhenL2WikidataCoverWins()
    {
        using var capture = new CoverMetricsCapture();
        var sg = new SharedGameEntity { WikidataCoverR2Key = "wiki-key" };
        _blob.Setup(b => b.GetPresignedDownloadUrlAsync("wiki-key.webp", BlobCategory.GameImage, "wiki-key", null))
             .ReturnsAsync("https://r2/wiki.webp");

        await CoverUrlResolver.ResolvePublicAsync(sg, _blob.Object);

        capture.LongMeasurements.Should().ContainSingle(m =>
            m.Name == "meepleai.cover.resolution.total" &&
            m.Value == 1 &&
            string.Equals(m.Tags["source"] as string, "r2_wikidata", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ResolvePublicAsync_EmitsPlaceholderMetric_WhenAllLayersNull()
    {
        using var capture = new CoverMetricsCapture();
        var sg = new SharedGameEntity();

        await CoverUrlResolver.ResolvePublicAsync(sg, _blob.Object);

        capture.LongMeasurements.Should().ContainSingle(m =>
            m.Name == "meepleai.cover.resolution.total" &&
            m.Value == 1 &&
            string.Equals(m.Tags["source"] as string, "placeholder", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ResolveForUserAsync_FallsBackToPublic_EmitsExactlyOneMetric()
    {
        // Defense against double-counting: a single user-facing resolution call
        // MUST emit exactly one CoverResolution event, even when L3 misses and
        // the resolver falls through to L4/L2.5/L2.
        using var capture = new CoverMetricsCapture();
        var sg = new SharedGameEntity { WikidataCoverR2Key = "wiki-key" };
        var entry = new UserLibraryEntryEntity { CustomCoverR2Key = null };
        _blob.Setup(b => b.GetPresignedDownloadUrlAsync("wiki-key.webp", BlobCategory.GameImage, "wiki-key", null))
             .ReturnsAsync("https://r2/wiki.webp");

        await CoverUrlResolver.ResolveForUserAsync(sg, entry, _blob.Object);

        capture.LongMeasurements
            .Where(m => m.Name == "meepleai.cover.resolution.total")
            .Should().HaveCount(1, "a single resolution call must emit exactly one metric event");
    }

    [Fact]
    public async Task ResolveForUserAsync_L3KeyPresentButBlobReturnsNull_EmitsExactlyOneFallbackMetric()
    {
        // Code-review HIGH fix (PR #2125): when userEntry.CustomCoverR2Key is
        // present but GetPresignedDownloadUrlAsync returns null (R2 outage,
        // expired blob), the resolver MUST fall through to ResolvePublicAsync
        // WITHOUT emitting an `r2_user` event. The downstream public call
        // emits exactly one event for the winning layer; the L3 attempt is
        // observable via the blob service's own logs, not duplicated here.
        using var capture = new CoverMetricsCapture();
        var sg = new SharedGameEntity { WikidataCoverR2Key = "wiki-key" };
        var entry = new UserLibraryEntryEntity { CustomCoverR2Key = "custom-key" };

        _blob.Setup(b => b.GetPresignedDownloadUrlAsync("custom-key.webp", BlobCategory.GameImage, "custom-key", null))
             .ReturnsAsync((string?)null);
        _blob.Setup(b => b.GetPresignedDownloadUrlAsync("wiki-key.webp", BlobCategory.GameImage, "wiki-key", null))
             .ReturnsAsync("https://r2/wiki.webp");

        await CoverUrlResolver.ResolveForUserAsync(sg, entry, _blob.Object);

        var emissions = capture.LongMeasurements
            .Where(m => m.Name == "meepleai.cover.resolution.total")
            .ToList();

        emissions.Should().HaveCount(1, "L3 miss must NOT double-count with the public fallback emission");
        emissions[0].Tags["source"].Should().Be("r2_wikidata", "the fallback layer should win the single emission");
    }

    [Fact]
    public async Task ResolveForUserAsync_L3MissAndAllPublicMiss_EmitsSinglePlaceholderEvent()
    {
        using var capture = new CoverMetricsCapture();
        var sg = new SharedGameEntity(); // no R2 keys at all
        var entry = new UserLibraryEntryEntity { CustomCoverR2Key = "custom-key" };

        _blob.Setup(b => b.GetPresignedDownloadUrlAsync("custom-key.webp", BlobCategory.GameImage, "custom-key", null))
             .ReturnsAsync((string?)null);

        await CoverUrlResolver.ResolveForUserAsync(sg, entry, _blob.Object);

        var emissions = capture.LongMeasurements
            .Where(m => m.Name == "meepleai.cover.resolution.total")
            .ToList();
        emissions.Should().HaveCount(1);
        emissions[0].Tags["source"].Should().Be("placeholder");
    }
}
