using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Infrastructure.Entities.UserLibrary;
using Api.Services.Pdf;
using Api.SharedKernel.Domain.Covers;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Services;

/// <summary>
/// Issue #3620 — proves the resolver actually PASSES
/// <see cref="CoverUrlResolver.CoverPresignExpirySeconds"/> to
/// <see cref="IBlobStorageService.GetPresignedUrlForRawKeyAsync"/> on every call site,
/// via an explicit Moq argument verification. <c>CoverPresignCacheInvariantTests</c>
/// only proves the two numbers relate correctly to each other — without a test like
/// this one, someone could remove the argument entirely (reverting every call to the
/// implicit 1h <see cref="S3StorageOptions.PresignedUrlExpirySeconds"/> default) and
/// the invariant test would stay green while the underlying bug came back.
///
/// Code review follow-up: joins the <c>CoverResolutionMetrics</c> collection (see
/// <see cref="CoverResolutionMetricsCollection"/>) — every test here calls
/// <c>CoverUrlResolver.Resolve*</c>, which emits on the same process-wide meter that
/// <see cref="CoverUrlResolverTests"/>'s <c>CoverMetricsCapture</c> asserts against, so
/// the two classes must not run in parallel with each other.
/// </summary>
[Collection("CoverResolutionMetrics")]
public class CoverUrlResolverPresignExpiryTests
{
    private readonly Mock<IBlobStorageService> _blob = new();

    [Fact]
    public async Task ResolveForUserAsync_L3UserCover_PassesConfiguredExpiryToBlobStorage()
    {
        var sg = new SharedGameEntity();
        var entry = new UserLibraryEntryEntity { CustomCoverR2Key = "custom-key" };
        _blob.Setup(b => b.GetPresignedUrlForRawKeyAsync("custom-key.webp", CoverUrlResolver.CoverPresignExpirySeconds))
             .ReturnsAsync("https://r2/custom.webp");

        await CoverUrlResolver.ResolveForUserAsync(sg, entry, CoverContext.Card, _blob.Object);

        _blob.Verify(
            b => b.GetPresignedUrlForRawKeyAsync("custom-key.webp", CoverUrlResolver.CoverPresignExpirySeconds),
            Times.Once);
    }

    [Fact]
    public async Task ResolvePublicAsync_L4PdfCover_PassesConfiguredExpiryToBlobStorage()
    {
        var sg = new SharedGameEntity { PdfCoverR2Key = "pdf-key" };
        _blob.Setup(b => b.GetPresignedUrlForRawKeyAsync("pdf-key-preview.webp", CoverUrlResolver.CoverPresignExpirySeconds))
             .ReturnsAsync("https://r2/pdf.webp");

        await CoverUrlResolver.ResolvePublicAsync(sg, _blob.Object);

        _blob.Verify(
            b => b.GetPresignedUrlForRawKeyAsync("pdf-key-preview.webp", CoverUrlResolver.CoverPresignExpirySeconds),
            Times.Once);
    }

    [Fact]
    public async Task ResolvePublicAsync_L25BggCover_PassesConfiguredExpiryToBlobStorage()
    {
        var sg = new SharedGameEntity { BggCoverR2Key = "bgg-covers/13/cover.jpg" };
        _blob.Setup(b => b.GetPresignedUrlForRawKeyAsync("bgg-covers/13/cover.jpg", CoverUrlResolver.CoverPresignExpirySeconds))
             .ReturnsAsync("https://r2/bgg.jpg");

        await CoverUrlResolver.ResolvePublicAsync(sg, _blob.Object);

        _blob.Verify(
            b => b.GetPresignedUrlForRawKeyAsync("bgg-covers/13/cover.jpg", CoverUrlResolver.CoverPresignExpirySeconds),
            Times.Once);
    }

    [Fact]
    public async Task ResolvePublicAsync_L2WikidataCover_PassesConfiguredExpiryToBlobStorage()
    {
        var sg = new SharedGameEntity { WikidataCoverR2Key = "wiki-key" };
        _blob.Setup(b => b.GetPresignedUrlForRawKeyAsync("wiki-key.webp", CoverUrlResolver.CoverPresignExpirySeconds))
             .ReturnsAsync("https://r2/wiki.webp");

        await CoverUrlResolver.ResolvePublicAsync(sg, _blob.Object);

        _blob.Verify(
            b => b.GetPresignedUrlForRawKeyAsync("wiki-key.webp", CoverUrlResolver.CoverPresignExpirySeconds),
            Times.Once);
    }

    [Fact]
    public async Task ResolveForContextAsync_AdminAssignmentGeneratedCrop_PassesConfiguredExpiryToBlobStorage()
    {
        var sg = new SharedGameEntity
        {
            CoverAssignments = new List<GameCoverAssignmentEntity>
            {
                new()
                {
                    Context = CoverContext.Card,
                    Source = CoverAssignmentSource.Wikidata,
                    GeneratedR2Key = "covers/card/crop.webp",
                },
            },
        };
        _blob.Setup(b => b.GetPresignedUrlForRawKeyAsync("covers/card/crop.webp", CoverUrlResolver.CoverPresignExpirySeconds))
             .ReturnsAsync("https://r2/card-crop.webp");

        await CoverUrlResolver.ResolveForContextAsync(sg, CoverContext.Card, _blob.Object);

        _blob.Verify(
            b => b.GetPresignedUrlForRawKeyAsync("covers/card/crop.webp", CoverUrlResolver.CoverPresignExpirySeconds),
            Times.Once);
    }

    [Fact]
    public async Task ResolveForContextAsync_AdminAssignmentPinnedSourceBaseKey_PassesConfiguredExpiryToBlobStorage()
    {
        var sg = new SharedGameEntity
        {
            WikidataCoverR2Key = "wiki-key",
            CoverAssignments = new List<GameCoverAssignmentEntity>
            {
                new()
                {
                    Context = CoverContext.Card,
                    Source = CoverAssignmentSource.Wikidata,
                    // No GeneratedR2Key: forces resolution via the pinned source's base key.
                },
            },
        };
        _blob.Setup(b => b.GetPresignedUrlForRawKeyAsync("wiki-key.webp", CoverUrlResolver.CoverPresignExpirySeconds))
             .ReturnsAsync("https://r2/wiki.webp");

        await CoverUrlResolver.ResolveForContextAsync(sg, CoverContext.Card, _blob.Object);

        _blob.Verify(
            b => b.GetPresignedUrlForRawKeyAsync("wiki-key.webp", CoverUrlResolver.CoverPresignExpirySeconds),
            Times.Once);
    }
}
