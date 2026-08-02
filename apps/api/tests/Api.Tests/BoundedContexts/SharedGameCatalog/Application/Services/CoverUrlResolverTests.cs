using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Infrastructure.Entities.UserLibrary;
using Api.Observability;
using Api.Services.Pdf;
using Api.SharedKernel.Domain.Covers;
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
        _blob.Setup(b => b.GetPresignedUrlForRawKeyAsync("custom-key.webp", null))
             .ReturnsAsync("https://r2/custom.webp");

        var url = await CoverUrlResolver.ResolveForUserAsync(sg, entry, _blob.Object);

        url.Should().Be("https://r2/custom.webp");
        _blob.Verify(b => b.GetPresignedUrlForRawKeyAsync("pdf-key-preview.webp", It.IsAny<int?>()), Times.Never);
    }

    [Fact]
    public async Task ResolveForUserAsync_NoL3_FallsBackToL4()
    {
        var sg = new SharedGameEntity { PdfCoverR2Key = "pdf-key", WikidataCoverR2Key = "wiki-key" };
        var entry = new UserLibraryEntryEntity { CustomCoverR2Key = null };
        _blob.Setup(b => b.GetPresignedUrlForRawKeyAsync("pdf-key-preview.webp", null))
             .ReturnsAsync("https://r2/pdf.webp");

        var url = await CoverUrlResolver.ResolveForUserAsync(sg, entry, _blob.Object);

        url.Should().Be("https://r2/pdf.webp");
    }

    [Fact]
    public async Task ResolvePublicAsync_L4WinsOverL2()
    {
        var sg = new SharedGameEntity { PdfCoverR2Key = "pdf-key", WikidataCoverR2Key = "wiki-key" };
        _blob.Setup(b => b.GetPresignedUrlForRawKeyAsync("pdf-key-preview.webp", null))
             .ReturnsAsync("https://r2/pdf.webp");

        var url = await CoverUrlResolver.ResolvePublicAsync(sg, _blob.Object);

        url.Should().Be("https://r2/pdf.webp");
    }

    [Fact]
    public async Task ResolvePublicAsync_NoL4_FallsBackToL2()
    {
        var sg = new SharedGameEntity { PdfCoverR2Key = null, WikidataCoverR2Key = "wiki-key" };
        _blob.Setup(b => b.GetPresignedUrlForRawKeyAsync("wiki-key.webp", null))
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
        _blob.Setup(b => b.GetPresignedUrlForRawKeyAsync("pdf-key-preview.webp", It.IsAny<int?>()))
             .ReturnsAsync((string?)null);
        _blob.Setup(b => b.GetPresignedUrlForRawKeyAsync("wiki-key.webp", It.IsAny<int?>()))
             .ReturnsAsync("https://r2/wiki.webp");

        var url = await CoverUrlResolver.ResolvePublicAsync(sg, _blob.Object);

        url.Should().Be("https://r2/wiki.webp");
    }

    [Fact]
    public async Task ResolvePublicAsync_L4SlashAndDotKey_ResolvesViaRawKeyMethod()
    {
        // P1 fix regression guard: the real PdfCoverR2Key shape is
        // "covers/{sharedGameId:D}/pdf-cover-pending" (contains '/'). Before the fix,
        // GetPresignedDownloadUrlAsync validated this via PathSecurity.ValidateIdentifier
        // and always returned null. Now it must resolve via GetPresignedUrlForRawKeyAsync
        // called with the exact physical key including the "-preview.webp" suffix.
        var pdfKey = $"covers/{Guid.NewGuid():D}/pdf-cover-pending";
        var sg = new SharedGameEntity { PdfCoverR2Key = pdfKey };
        _blob.Setup(b => b.GetPresignedUrlForRawKeyAsync($"{pdfKey}-preview.webp", null))
             .ReturnsAsync("https://r2/pdf-cover.webp");

        var url = await CoverUrlResolver.ResolvePublicAsync(sg, _blob.Object);

        url.Should().Be("https://r2/pdf-cover.webp");
    }

    [Fact]
    public async Task ResolvePublicAsync_L2SlashAndDotKey_ResolvesViaRawKeyMethod()
    {
        var wikidataKey = $"covers/{Guid.NewGuid():D}/wikidata-cover";
        var sg = new SharedGameEntity { WikidataCoverR2Key = wikidataKey };
        _blob.Setup(b => b.GetPresignedUrlForRawKeyAsync($"{wikidataKey}.webp", null))
             .ReturnsAsync("https://r2/wikidata-cover.webp");

        var url = await CoverUrlResolver.ResolvePublicAsync(sg, _blob.Object);

        url.Should().Be("https://r2/wikidata-cover.webp");
    }

    [Fact]
    public async Task ResolveForUserAsync_L3SlashAndDotKey_ResolvesViaRawKeyMethod()
    {
        var customKey = $"covers/{Guid.NewGuid():D}/user-custom-cover";
        var sg = new SharedGameEntity();
        var entry = new UserLibraryEntryEntity { CustomCoverR2Key = customKey };
        _blob.Setup(b => b.GetPresignedUrlForRawKeyAsync($"{customKey}.webp", null))
             .ReturnsAsync("https://r2/user-custom-cover.webp");

        var url = await CoverUrlResolver.ResolveForUserAsync(sg, entry, _blob.Object);

        url.Should().Be("https://r2/user-custom-cover.webp");
    }

    // ----- Issue #2123 metric emission tests --------------------------------

    [Fact]
    public async Task ResolveForUserAsync_EmitsR2UserMetric_WhenL3CustomCoverWins()
    {
        using var capture = new CoverMetricsCapture();
        var sg = new SharedGameEntity();
        var entry = new UserLibraryEntryEntity { CustomCoverR2Key = "custom-key" };
        _blob.Setup(b => b.GetPresignedUrlForRawKeyAsync("custom-key.webp", null))
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
        _blob.Setup(b => b.GetPresignedUrlForRawKeyAsync("pdf-key-preview.webp", null))
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
        var sg = new SharedGameEntity { BggCoverR2Key = "bgg-covers/13/cover.jpg" };
        _blob.Setup(b => b.GetPresignedUrlForRawKeyAsync("bgg-covers/13/cover.jpg", null))
             .ReturnsAsync("https://r2/bgg.jpg");

        await CoverUrlResolver.ResolvePublicAsync(sg, _blob.Object);

        capture.LongMeasurements.Should().ContainSingle(m =>
            m.Name == "meepleai.cover.resolution.total" &&
            m.Value == 1 &&
            string.Equals(m.Tags["source"] as string, "r2_bgg", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ResolvePublicAsync_L25BggSlashKey_ResolvesViaRawKeyMethodNoSuffix()
    {
        // Issue #2947: BGG DB key is the FULL physical key (contains '/' and a
        // dot-extension) and the resolver passes it verbatim to
        // GetPresignedUrlForRawKeyAsync with NO appended suffix. The legacy
        // GetPresignedDownloadUrlAsync path (PathSecurity.ValidateIdentifier)
        // would have rejected the '/' and '.'.
        var sg = new SharedGameEntity { BggCoverR2Key = "bgg-covers/42/cover.png" };
        _blob.Setup(b => b.GetPresignedUrlForRawKeyAsync("bgg-covers/42/cover.png", null))
             .ReturnsAsync("https://r2/bgg-42.png");

        var url = await CoverUrlResolver.ResolvePublicAsync(sg, _blob.Object);

        url.Should().Be("https://r2/bgg-42.png");
        _blob.Verify(b => b.GetPresignedDownloadUrlAsync(
            It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<int?>()), Times.Never);
    }

    [Fact]
    public async Task ResolvePublicAsync_EmitsR2WikidataMetric_WhenL2WikidataCoverWins()
    {
        using var capture = new CoverMetricsCapture();
        var sg = new SharedGameEntity { WikidataCoverR2Key = "wiki-key" };
        _blob.Setup(b => b.GetPresignedUrlForRawKeyAsync("wiki-key.webp", null))
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
        _blob.Setup(b => b.GetPresignedUrlForRawKeyAsync("wiki-key.webp", null))
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

        _blob.Setup(b => b.GetPresignedUrlForRawKeyAsync("custom-key.webp", null))
             .ReturnsAsync((string?)null);
        _blob.Setup(b => b.GetPresignedUrlForRawKeyAsync("wiki-key.webp", null))
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

        _blob.Setup(b => b.GetPresignedUrlForRawKeyAsync("custom-key.webp", null))
             .ReturnsAsync((string?)null);

        await CoverUrlResolver.ResolveForUserAsync(sg, entry, _blob.Object);

        var emissions = capture.LongMeasurements
            .Where(m => m.Name == "meepleai.cover.resolution.total")
            .ToList();
        emissions.Should().HaveCount(1);
        emissions[0].Tags["source"].Should().Be("placeholder");
    }

    // ----- Epic #3470 Slice 1c: context-aware admin override -----------------

    private static GameCoverAssignmentEntity Assignment(
        CoverContext context,
        CoverAssignmentSource source,
        string? generatedR2Key = null) =>
        new()
        {
            Context = context,
            Source = source,
            GeneratedR2Key = generatedR2Key,
        };

    [Fact]
    public async Task ResolveForContextAsync_GeneratedCropResolves_WinsOverImplicitPrecedence()
    {
        // PDF would win the implicit chain, but the admin pinned a per-context
        // crop for Card — the rendered crop key wins.
        var sg = new SharedGameEntity
        {
            PdfCoverR2Key = "pdf-key",
            WikidataCoverR2Key = "wiki-key",
            CoverAssignments = new List<GameCoverAssignmentEntity>
            {
                Assignment(CoverContext.Card, CoverAssignmentSource.Wikidata, "covers/card/crop.webp"),
            },
        };
        _blob.Setup(b => b.GetPresignedUrlForRawKeyAsync("covers/card/crop.webp", null))
             .ReturnsAsync("https://r2/card-crop.webp");

        var url = await CoverUrlResolver.ResolveForContextAsync(sg, CoverContext.Card, _blob.Object);

        url.Should().Be("https://r2/card-crop.webp");
        // The implicit PDF layer must NOT have been consulted.
        _blob.Verify(b => b.GetPresignedUrlForRawKeyAsync("pdf-key-preview.webp", It.IsAny<int?>()), Times.Never);
    }

    [Fact]
    public async Task ResolveForContextAsync_NoGeneratedCrop_UsesPinnedSourceBaseKey()
    {
        // No crop rendered yet: the assignment still pins Wikidata, which must win
        // over the implicit PDF cover (PDF is higher in the implicit chain).
        var sg = new SharedGameEntity
        {
            PdfCoverR2Key = "pdf-key",
            WikidataCoverR2Key = "wiki-key",
            CoverAssignments = new List<GameCoverAssignmentEntity>
            {
                Assignment(CoverContext.Card, CoverAssignmentSource.Wikidata),
            },
        };
        _blob.Setup(b => b.GetPresignedUrlForRawKeyAsync("wiki-key.webp", null))
             .ReturnsAsync("https://r2/wiki.webp");

        var url = await CoverUrlResolver.ResolveForContextAsync(sg, CoverContext.Card, _blob.Object);

        url.Should().Be("https://r2/wiki.webp");
        _blob.Verify(b => b.GetPresignedUrlForRawKeyAsync("pdf-key-preview.webp", It.IsAny<int?>()), Times.Never);
    }

    [Fact]
    public async Task ResolveForContextAsync_ManualSource_UsesManualBaseKey()
    {
        var sg = new SharedGameEntity
        {
            ManualCoverR2Key = "covers/manual/game/cover",
            CoverAssignments = new List<GameCoverAssignmentEntity>
            {
                Assignment(CoverContext.Hero, CoverAssignmentSource.Manual),
            },
        };
        _blob.Setup(b => b.GetPresignedUrlForRawKeyAsync("covers/manual/game/cover.webp", null))
             .ReturnsAsync("https://r2/manual.webp");

        var url = await CoverUrlResolver.ResolveForContextAsync(sg, CoverContext.Hero, _blob.Object);

        url.Should().Be("https://r2/manual.webp");
    }

    [Fact]
    public async Task ResolveForContextAsync_StaleGeneratedCrop_FallsBackToPinnedSourceBaseKey()
    {
        // The rendered crop key is stale (blob returns null) but the pinned source
        // is still resolvable — the assignment still wins via its base key, it does
        // NOT drop to the implicit chain.
        var sg = new SharedGameEntity
        {
            PdfCoverR2Key = "pdf-key",
            WikidataCoverR2Key = "wiki-key",
            CoverAssignments = new List<GameCoverAssignmentEntity>
            {
                Assignment(CoverContext.Card, CoverAssignmentSource.Wikidata, "covers/card/stale.webp"),
            },
        };
        _blob.Setup(b => b.GetPresignedUrlForRawKeyAsync("covers/card/stale.webp", null))
             .ReturnsAsync((string?)null);
        _blob.Setup(b => b.GetPresignedUrlForRawKeyAsync("wiki-key.webp", null))
             .ReturnsAsync("https://r2/wiki.webp");

        var url = await CoverUrlResolver.ResolveForContextAsync(sg, CoverContext.Card, _blob.Object);

        url.Should().Be("https://r2/wiki.webp");
    }

    [Fact]
    public async Task ResolveForContextAsync_PinnedSourceKeyAbsent_FallsThroughToImplicit_NotPlaceholder()
    {
        // The assignment pins Manual but no manual cover exists on the entity —
        // resolution must fall through to the implicit precedence (PDF here), NOT
        // return a placeholder.
        var sg = new SharedGameEntity
        {
            PdfCoverR2Key = "pdf-key",
            CoverAssignments = new List<GameCoverAssignmentEntity>
            {
                Assignment(CoverContext.Card, CoverAssignmentSource.Manual),
            },
        };
        _blob.Setup(b => b.GetPresignedUrlForRawKeyAsync("pdf-key-preview.webp", null))
             .ReturnsAsync("https://r2/pdf.webp");

        var url = await CoverUrlResolver.ResolveForContextAsync(sg, CoverContext.Card, _blob.Object);

        url.Should().Be("https://r2/pdf.webp");
    }

    [Fact]
    public async Task ResolveForContextAsync_PinnedSourceBlobUnreachable_FallsThroughToImplicit()
    {
        // Assignment pins Manual and the manual key exists, but the blob is
        // unreachable — fall through to the implicit Wikidata layer.
        var sg = new SharedGameEntity
        {
            WikidataCoverR2Key = "wiki-key",
            ManualCoverR2Key = "manual-key",
            CoverAssignments = new List<GameCoverAssignmentEntity>
            {
                Assignment(CoverContext.Card, CoverAssignmentSource.Manual),
            },
        };
        _blob.Setup(b => b.GetPresignedUrlForRawKeyAsync("manual-key.webp", null))
             .ReturnsAsync((string?)null);
        _blob.Setup(b => b.GetPresignedUrlForRawKeyAsync("wiki-key.webp", null))
             .ReturnsAsync("https://r2/wiki.webp");

        var url = await CoverUrlResolver.ResolveForContextAsync(sg, CoverContext.Card, _blob.Object);

        url.Should().Be("https://r2/wiki.webp");
    }

    [Fact]
    public async Task ResolveForContextAsync_NoAssignmentForContext_UsesImplicitPrecedence()
    {
        // A Hero assignment exists but we resolve Card — the Card path is purely
        // implicit (per-context isolation).
        var sg = new SharedGameEntity
        {
            PdfCoverR2Key = "pdf-key",
            CoverAssignments = new List<GameCoverAssignmentEntity>
            {
                Assignment(CoverContext.Hero, CoverAssignmentSource.Wikidata, "covers/hero/crop.webp"),
            },
        };
        _blob.Setup(b => b.GetPresignedUrlForRawKeyAsync("pdf-key-preview.webp", null))
             .ReturnsAsync("https://r2/pdf.webp");

        var url = await CoverUrlResolver.ResolveForContextAsync(sg, CoverContext.Card, _blob.Object);

        url.Should().Be("https://r2/pdf.webp");
        _blob.Verify(b => b.GetPresignedUrlForRawKeyAsync("covers/hero/crop.webp", It.IsAny<int?>()), Times.Never);
    }

    [Fact]
    public async Task ResolveForContextAsync_PerContextIsolation_EachContextResolvesItsOwnAssignment()
    {
        var sg = new SharedGameEntity
        {
            PdfCoverR2Key = "pdf-key",
            CoverAssignments = new List<GameCoverAssignmentEntity>
            {
                Assignment(CoverContext.Card, CoverAssignmentSource.Wikidata, "covers/card/crop.webp"),
                Assignment(CoverContext.Hero, CoverAssignmentSource.Manual, "covers/hero/crop.webp"),
            },
        };
        _blob.Setup(b => b.GetPresignedUrlForRawKeyAsync("covers/card/crop.webp", null))
             .ReturnsAsync("https://r2/card.webp");
        _blob.Setup(b => b.GetPresignedUrlForRawKeyAsync("covers/hero/crop.webp", null))
             .ReturnsAsync("https://r2/hero.webp");

        (await CoverUrlResolver.ResolveForContextAsync(sg, CoverContext.Card, _blob.Object))
            .Should().Be("https://r2/card.webp");
        (await CoverUrlResolver.ResolveForContextAsync(sg, CoverContext.Hero, _blob.Object))
            .Should().Be("https://r2/hero.webp");
    }

    [Fact]
    public async Task ResolveForContextAsync_OverrideWins_EmitsSingleMetricTaggedWithPinnedSource()
    {
        using var capture = new CoverMetricsCapture();
        var sg = new SharedGameEntity
        {
            ManualCoverR2Key = "manual-key",
            CoverAssignments = new List<GameCoverAssignmentEntity>
            {
                Assignment(CoverContext.Card, CoverAssignmentSource.Manual),
            },
        };
        _blob.Setup(b => b.GetPresignedUrlForRawKeyAsync("manual-key.webp", null))
             .ReturnsAsync("https://r2/manual.webp");

        await CoverUrlResolver.ResolveForContextAsync(sg, CoverContext.Card, _blob.Object);

        capture.LongMeasurements
            .Where(m => m.Name == "meepleai.cover.resolution.total")
            .Should().ContainSingle()
            .Which.Tags["source"].Should().Be("r2_manual");
    }

    [Fact]
    public async Task ResolveForContextAsync_FallThrough_EmitsExactlyOneImplicitMetric()
    {
        // Defense against double-counting: an override that misses and drops to the
        // implicit chain must emit exactly ONE metric, tagged with the winning
        // implicit layer — never the (unresolved) pinned source.
        using var capture = new CoverMetricsCapture();
        var sg = new SharedGameEntity
        {
            PdfCoverR2Key = "pdf-key",
            CoverAssignments = new List<GameCoverAssignmentEntity>
            {
                Assignment(CoverContext.Card, CoverAssignmentSource.Manual),
            },
        };
        _blob.Setup(b => b.GetPresignedUrlForRawKeyAsync("pdf-key-preview.webp", null))
             .ReturnsAsync("https://r2/pdf.webp");

        await CoverUrlResolver.ResolveForContextAsync(sg, CoverContext.Card, _blob.Object);

        var emissions = capture.LongMeasurements
            .Where(m => m.Name == "meepleai.cover.resolution.total")
            .ToList();
        emissions.Should().HaveCount(1);
        emissions[0].Tags["source"].Should().Be("r2_pdf");
    }

    // ----- Epic #3470 Slice 1d-a: source-aware resolution --------------------

    [Fact]
    public async Task ResolvePublicWithSourceAsync_PdfWins_ReturnsPdfKind()
    {
        var sg = new SharedGameEntity { PdfCoverR2Key = "pdf-key", WikidataCoverR2Key = "wiki-key" };
        _blob.Setup(b => b.GetPresignedUrlForRawKeyAsync("pdf-key-preview.webp", null))
             .ReturnsAsync("https://r2/pdf.webp");

        var result = await CoverUrlResolver.ResolvePublicWithSourceAsync(sg, _blob.Object);

        result.Url.Should().Be("https://r2/pdf.webp");
        result.Kind.Should().Be(CoverKind.Pdf);
    }

    [Fact]
    public async Task ResolvePublicWithSourceAsync_WikidataWins_ReturnsWikidataKind()
    {
        var sg = new SharedGameEntity { WikidataCoverR2Key = "wiki-key" };
        _blob.Setup(b => b.GetPresignedUrlForRawKeyAsync("wiki-key.webp", null))
             .ReturnsAsync("https://r2/wiki.webp");

        var result = await CoverUrlResolver.ResolvePublicWithSourceAsync(sg, _blob.Object);

        result.Url.Should().Be("https://r2/wiki.webp");
        result.Kind.Should().Be(CoverKind.Wikidata);
    }

    [Fact]
    public async Task ResolvePublicWithSourceAsync_AllMiss_ReturnsNullUrlAndKind()
    {
        var result = await CoverUrlResolver.ResolvePublicWithSourceAsync(new SharedGameEntity(), _blob.Object);

        result.Url.Should().BeNull();
        result.Kind.Should().BeNull();
    }

    [Fact]
    public async Task ResolvePublicWithSourceAsync_EmitsExactlyOneMetric()
    {
        using var capture = new CoverMetricsCapture();
        var sg = new SharedGameEntity { PdfCoverR2Key = "pdf-key" };
        _blob.Setup(b => b.GetPresignedUrlForRawKeyAsync("pdf-key-preview.webp", null))
             .ReturnsAsync("https://r2/pdf.webp");

        await CoverUrlResolver.ResolvePublicWithSourceAsync(sg, _blob.Object);

        capture.LongMeasurements
            .Where(m => m.Name == "meepleai.cover.resolution.total")
            .Should().HaveCount(1);
    }

    [Fact]
    public async Task ResolvePublicAsync_DelegatesToWithSource_StillReturnsUrlAndEmitsOnce()
    {
        using var capture = new CoverMetricsCapture();
        var sg = new SharedGameEntity { WikidataCoverR2Key = "wiki-key" };
        _blob.Setup(b => b.GetPresignedUrlForRawKeyAsync("wiki-key.webp", null))
             .ReturnsAsync("https://r2/wiki.webp");

        var url = await CoverUrlResolver.ResolvePublicAsync(sg, _blob.Object);

        url.Should().Be("https://r2/wiki.webp");
        capture.LongMeasurements
            .Where(m => m.Name == "meepleai.cover.resolution.total")
            .Should().HaveCount(1);
    }
}
