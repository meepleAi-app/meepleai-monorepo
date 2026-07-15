using Amazon.S3;
using Amazon.S3.Model;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;
using Api.Services.Pdf;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using System.Net;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure.Services;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class BggCoverUploadPipelineTests : IDisposable
{
    private readonly Mock<IAmazonS3> _mockS3Client;
    private readonly Mock<ILogger<BggCoverUploadPipeline>> _mockLogger;
    private readonly S3StorageOptions _options;
    private readonly BggCoverUploadPipeline _sut;

    public BggCoverUploadPipelineTests()
    {
        _mockS3Client = new Mock<IAmazonS3>(MockBehavior.Strict);
        _mockLogger = new Mock<ILogger<BggCoverUploadPipeline>>();
        _options = new S3StorageOptions
        {
            Endpoint = "https://test.r2.cloudflarestorage.com",
            AccessKey = "test-access-key",
            SecretKey = "test-secret-key",
            BucketName = "test-bucket",
            Region = "auto",
            PresignedUrlExpirySeconds = 3600,
            EnableEncryption = true,
            ForcePathStyle = false
        };
        _sut = new BggCoverUploadPipeline(_mockS3Client.Object, _options, _mockLogger.Object);
    }

    public void Dispose() => _mockS3Client.Reset();

    [Fact]
    public async Task UploadAsync_ValidBytes_PutsObjectWithDeterministicKeyIncludingExtension()
    {
        var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
        PutObjectRequest? captured = null;
        _mockS3Client
            .Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PutObjectRequest, CancellationToken>((req, _) => captured = req)
            .ReturnsAsync(new PutObjectResponse { HttpStatusCode = HttpStatusCode.OK, ETag = "\"abc\"" });

        var key = await _sut.UploadAsync(13, bytes, ".jpg", CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.Key.Should().Be("bgg-covers/13/cover.jpg", "physical R2 key is deterministic + keeps the source extension");
        captured.BucketName.Should().Be("test-bucket");
        key.Should().Be("bgg-covers/13/cover.jpg", "the returned DB key is the exact physical key (resolver appends no suffix)");
    }

    [Fact]
    public async Task UploadAsync_NullOrEmptyExtension_DefaultsToJpg()
    {
        var bytes = new byte[] { 0x01 };
        var keys = new List<string>();
        _mockS3Client
            .Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PutObjectRequest, CancellationToken>((req, _) => keys.Add(req.Key))
            .ReturnsAsync(new PutObjectResponse { HttpStatusCode = HttpStatusCode.OK });

        var k1 = await _sut.UploadAsync(7, bytes, null!, CancellationToken.None);
        var k2 = await _sut.UploadAsync(8, bytes, "", CancellationToken.None);

        k1.Should().Be("bgg-covers/7/cover.jpg");
        k2.Should().Be("bgg-covers/8/cover.jpg");
        keys.Should().BeEquivalentTo(new[] { "bgg-covers/7/cover.jpg", "bgg-covers/8/cover.jpg" });
    }

    [Fact]
    public async Task UploadAsync_SetsCacheControlImmutable1Year()
    {
        var bytes = new byte[] { 0x01 };
        PutObjectRequest? captured = null;
        _mockS3Client
            .Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PutObjectRequest, CancellationToken>((req, _) => captured = req)
            .ReturnsAsync(new PutObjectResponse { HttpStatusCode = HttpStatusCode.OK });

        await _sut.UploadAsync(1, bytes, ".png", CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.Headers.CacheControl.Should().Be("public, max-age=31536000, immutable");
    }

    [Fact]
    public async Task UploadAsync_NullBytes_ThrowsArgumentException()
    {
        Func<Task> act = async () => await _sut.UploadAsync(1, null!, ".jpg", CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*imageBytes*");
        _mockS3Client.Verify(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UploadAsync_EmptyBytes_ThrowsArgumentException()
    {
        Func<Task> act = async () => await _sut.UploadAsync(1, Array.Empty<byte>(), ".jpg", CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*imageBytes*");
        _mockS3Client.Verify(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UploadAsync_Cancellation_RethrowsOperationCanceledException()
    {
        var bytes = new byte[] { 0x01 };
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        _mockS3Client
            .Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        Func<Task> act = async () => await _sut.UploadAsync(1, bytes, ".jpg", cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task UploadAsync_S3Exception_Rethrows()
    {
        var bytes = new byte[] { 0x01 };
        _mockS3Client
            .Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("boom") { StatusCode = HttpStatusCode.InternalServerError, ErrorCode = "InternalError" });

        Func<Task> act = async () => await _sut.UploadAsync(1, bytes, ".jpg", CancellationToken.None);

        await act.Should().ThrowAsync<AmazonS3Exception>();
    }

    [Fact]
    public void Ctor_NullS3Client_ThrowsArgumentNullException()
    {
        Action act = () => new BggCoverUploadPipeline(null!, _options, _mockLogger.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("s3Client");
    }

    [Fact]
    public void Ctor_NullOptions_ThrowsArgumentNullException()
    {
        Action act = () => new BggCoverUploadPipeline(_mockS3Client.Object, null!, _mockLogger.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("options");
    }

    [Fact]
    public void Ctor_NullLogger_ThrowsArgumentNullException()
    {
        Action act = () => new BggCoverUploadPipeline(_mockS3Client.Object, _options, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }
}
