using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Infrastructure.Entities.UserLibrary;
using Api.Services.Pdf;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Services;

public class CoverUrlResolverTests
{
    private readonly Mock<IBlobStorageService> _blob = new();

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
}
