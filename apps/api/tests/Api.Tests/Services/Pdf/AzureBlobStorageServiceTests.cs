using System.Text.RegularExpressions;
using Api.Services.Pdf;
using Api.Tests.Constants;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.Services.Pdf;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public sealed class AzureBlobStorageServiceTests
{
    private const string Container = "meepleai-uploads";
    private static readonly Uri ContainerUri = new("http://azurite:10000/devstoreaccount1/meepleai-uploads");
    private static readonly Response<BlobContentInfo> UploadOk =
        Response.FromValue(BlobsModelFactory.BlobContentInfo(new ETag("e"), DateTimeOffset.UtcNow, null, null, null, null, 1), Mock.Of<Response>());

    private readonly Mock<BlobServiceClient> _service = new();
    private readonly Mock<BlobContainerClient> _container = new();
    private readonly Mock<BlobClient> _blob = new();

    public AzureBlobStorageServiceTests()
    {
        _container.SetupGet(c => c.Name).Returns(Container);
        _container.SetupGet(c => c.Uri).Returns(ContainerUri);
        _container.Setup(c => c.GetBlobClient(It.IsAny<string>())).Returns(_blob.Object);
        _container
            .Setup(c => c.CreateIfNotExistsAsync(It.IsAny<PublicAccessType>(), It.IsAny<IDictionary<string, string>>(), It.IsAny<BlobContainerEncryptionScopeOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(BlobsModelFactory.BlobContainerInfo(new ETag("x"), DateTimeOffset.UtcNow), Mock.Of<Response>()));
        _blob.Setup(b => b.UploadAsync(It.IsAny<Stream>(), It.IsAny<BlobUploadOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(UploadOk);
    }

    private AzureBlobStorageService CreateSut(string? publicEndpoint = null, bool sharedKey = true) =>
        new(_service.Object, _container.Object, new AzureBlobStorageOptions
        {
            ConnectionString = "UseDevelopmentStorage=true",
            ContainerName = Container,
            PublicBlobEndpoint = publicEndpoint,
            SasExpirySeconds = 600,
            AccountName = sharedKey ? "devstoreaccount1" : null,
            AccountKey = sharedKey ? AzureStorageConnectionString.DevelopmentStorageKey : null,
        }, Mock.Of<ILogger<AzureBlobStorageService>>());

    private static Page<BlobItem> PageOf(params string[] names) =>
        Page<BlobItem>.FromValues(names.Select(n => BlobsModelFactory.BlobItem(name: n)).ToList(), null, Mock.Of<Response>());

    private void SetupListing(string prefix, params string[] names) =>
        _container
            .Setup(c => c.GetBlobsAsync(It.IsAny<BlobTraits>(), It.IsAny<BlobStates>(), prefix, It.IsAny<CancellationToken>()))
            .Returns(AsyncPageable<BlobItem>.FromPages([PageOf(names)]));

    private void VerifyNoListing() =>
        _container.Verify(c => c.GetBlobsAsync(It.IsAny<BlobTraits>(), It.IsAny<BlobStates>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

    [Fact]
    public async Task StoreAsync_WritesCategorizedKeyWithPdfContentType()
    {
        BlobUploadOptions? uploadOptions = null;
        _blob.Setup(b => b.UploadAsync(It.IsAny<Stream>(), It.IsAny<BlobUploadOptions>(), It.IsAny<CancellationToken>()))
            .Callback<Stream, BlobUploadOptions, CancellationToken>((_, o, _) => uploadOptions = o)
            .ReturnsAsync(UploadOk);
        var sut = CreateSut();
        using var content = new MemoryStream(new byte[] { 1, 2, 3 });

        var result = await sut.StoreAsync(content, "Rule:Book.pdf", BlobCategory.Pdf, "game-1");

        result.Success.Should().BeTrue(result.ErrorMessage);
        result.FileSizeBytes.Should().Be(3);
        result.FileId.Should().HaveLength(32);
        // ':' è in AdditionalInvalidChars di StringHelper → '_'; lo spazio NON verrebbe sostituito.
        result.FilePath.Should().MatchRegex("^pdfs/game-1/[0-9a-f]{32}_Rule_Book\\.pdf$");
        _container.Verify(c => c.GetBlobClient(It.Is<string>(k => Regex.IsMatch(k, "^pdfs/game-1/[0-9a-f]{32}_Rule_Book\\.pdf$"))), Times.Once);
        uploadOptions!.HttpHeaders!.ContentType.Should().Be("application/pdf");
    }

    [Fact]
    public async Task StoreAsync_TraversalResourceKey_FailsWithoutUpload()
    {
        var sut = CreateSut();
        using var content = new MemoryStream(new byte[] { 1 });

        var result = await sut.StoreAsync(content, "a.pdf", BlobCategory.Pdf, "../etc");

        result.Success.Should().BeFalse();
        _blob.Verify(b => b.UploadAsync(It.IsAny<Stream>(), It.IsAny<BlobUploadOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RetrieveAsync_ResolvesByPrefixAndReturnsStream()
    {
        SetupListing("pdfs/game-1/abc_", "pdfs/game-1/abc_a.pdf");
        var expected = new MemoryStream(new byte[] { 9 });
        _blob.Setup(b => b.DownloadStreamingAsync(It.IsAny<BlobDownloadOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(BlobsModelFactory.BlobDownloadStreamingResult(expected), Mock.Of<Response>()));
        var sut = CreateSut();

        await using var stream = await sut.RetrieveAsync("abc", BlobCategory.Pdf, "game-1");

        stream.Should().BeSameAs(expected);
        _container.Verify(c => c.GetBlobClient("pdfs/game-1/abc_a.pdf"), Times.Once);
    }

    [Fact]
    public async Task RetrieveAsync_NoMatch_ReturnsNull()
    {
        SetupListing("pdfs/game-1/missing_");

        (await CreateSut().RetrieveAsync("missing", BlobCategory.Pdf, "game-1")).Should().BeNull();
    }

    [Fact]
    public async Task RetrieveAsync_TraversalFileId_ReturnsNullWithoutListing()
    {
        (await CreateSut().RetrieveAsync("../x", BlobCategory.Pdf, "game-1")).Should().BeNull();
        VerifyNoListing();
    }

    [Fact]
    public async Task DeleteAsync_DeletesEveryMatchAndReturnsTrue()
    {
        SetupListing("pdfs/game-1/abc_", "pdfs/game-1/abc_a.pdf", "pdfs/game-1/abc_b.pdf");
        _container.Setup(c => c.DeleteBlobIfExistsAsync(It.IsAny<string>(), It.IsAny<DeleteSnapshotsOption>(), It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(true, Mock.Of<Response>()));

        (await CreateSut().DeleteAsync("abc", BlobCategory.Pdf, "game-1")).Should().BeTrue();

        _container.Verify(c => c.DeleteBlobIfExistsAsync("pdfs/game-1/abc_a.pdf", It.IsAny<DeleteSnapshotsOption>(), It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()), Times.Once);
        _container.Verify(c => c.DeleteBlobIfExistsAsync("pdfs/game-1/abc_b.pdf", It.IsAny<DeleteSnapshotsOption>(), It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_NoMatch_ReturnsFalse()
    {
        SetupListing("pdfs/game-1/abc_");

        (await CreateSut().DeleteAsync("abc", BlobCategory.Pdf, "game-1")).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_TraversalFileId_ReturnsFalseWithoutListing()
    {
        (await CreateSut().DeleteAsync("../x", BlobCategory.Pdf, "game-1")).Should().BeFalse();
        VerifyNoListing();
    }

    [Fact]
    public async Task ExistsAsync_TraversalFileId_ReturnsFalse()
    {
        (await CreateSut().ExistsAsync("../x", BlobCategory.Pdf, "game-1")).Should().BeFalse();
        VerifyNoListing();
    }

    [Fact]
    public void GetStoragePath_UsesSameLayoutAsS3()
    {
        CreateSut().GetStoragePath("abc", BlobCategory.GameImage, "game-1", "cover|one.png")
            .Should().Be("game-images/game-1/abc_cover_one.png");
    }

    [Fact]
    public async Task GetPresignedUrlForRawKeyAsync_MissingBlob_ReturnsNull()
    {
        _blob.Setup(b => b.ExistsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Response.FromValue(false, Mock.Of<Response>()));

        (await CreateSut().GetPresignedUrlForRawKeyAsync("covers/x.png")).Should().BeNull();
    }

    [Fact]
    public async Task GetPresignedUrlForRawKeyAsync_SharedKey_SignsWithoutParsingTheContainerUri()
    {
        _blob.Setup(b => b.ExistsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Response.FromValue(true, Mock.Of<Response>()));

        var url = await CreateSut(publicEndpoint: "http://localhost:20000").GetPresignedUrlForRawKeyAsync("covers/game 1/x.png", 120);

        // Host riscritto, path del container preservato, segmento con spazio codificato, firma presente.
        url.Should().StartWith("http://localhost:20000/devstoreaccount1/meepleai-uploads/covers/game%201/x.png?");
        url.Should().Contain("sig=").And.Contain("sp=r").And.Contain("sr=b");
    }

    [Fact]
    public async Task GetPresignedUrlForRawKeyAsync_UserDelegation_UsesServiceKey()
    {
        _blob.Setup(b => b.ExistsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Response.FromValue(true, Mock.Of<Response>()));
        _service.SetupGet(s => s.AccountName).Returns("devstoreaccount1");
        var key = BlobsModelFactory.UserDelegationKey(
            "oid", "tid", DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddHours(1), "b", "2024-11-04",
            Convert.ToBase64String(new byte[32]));
        _service.Setup(s => s.GetUserDelegationKeyAsync(It.IsAny<DateTimeOffset?>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(key, Mock.Of<Response>()));

        var url = await CreateSut(sharedKey: false).GetPresignedUrlForRawKeyAsync("covers/x.png", 120);

        url.Should().StartWith("http://azurite:10000/devstoreaccount1/meepleai-uploads/covers/x.png?");
        url.Should().Contain("skoid=oid").And.Contain("sig=");
    }

    [Fact]
    public async Task StoreRawKeyAsync_WritesVerbatimKeyWithGivenContentType()
    {
        BlobUploadOptions? uploadOptions = null;
        _blob.Setup(b => b.UploadAsync(It.IsAny<Stream>(), It.IsAny<BlobUploadOptions>(), It.IsAny<CancellationToken>()))
            .Callback<Stream, BlobUploadOptions, CancellationToken>((_, o, _) => uploadOptions = o)
            .ReturnsAsync(UploadOk);
        using var content = new MemoryStream(new byte[] { 1 });

        (await CreateSut().StoreRawKeyAsync("covers/game-1/cover.png", content, "image/png")).Should().BeTrue();

        _container.Verify(c => c.GetBlobClient("covers/game-1/cover.png"), Times.Once);
        uploadOptions!.HttpHeaders!.ContentType.Should().Be("image/png");
    }

    [Fact]
    public async Task RetrieveRawKeyAsync_NotFound_ReturnsNull()
    {
        _blob.Setup(b => b.DownloadStreamingAsync(It.IsAny<BlobDownloadOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(404, "BlobNotFound"));

        (await CreateSut().RetrieveRawKeyAsync("covers/none.png")).Should().BeNull();
    }
}
