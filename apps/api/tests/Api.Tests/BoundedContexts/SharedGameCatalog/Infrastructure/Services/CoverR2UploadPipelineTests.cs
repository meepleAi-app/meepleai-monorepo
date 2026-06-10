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
public sealed class CoverR2UploadPipelineTests : IDisposable
{
    private readonly Mock<IAmazonS3> _mockS3Client;
    private readonly Mock<ILogger<CoverR2UploadPipeline>> _mockLogger;
    private readonly S3StorageOptions _options;
    private readonly CoverR2UploadPipeline _sut;

    public CoverR2UploadPipelineTests()
    {
        _mockS3Client = new Mock<IAmazonS3>(MockBehavior.Strict);
        _mockLogger = new Mock<ILogger<CoverR2UploadPipeline>>();
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

        _sut = new CoverR2UploadPipeline(
            _mockS3Client.Object,
            _options,
            _mockLogger.Object);
    }

    public void Dispose()
    {
        _mockS3Client.Reset();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Happy path — physical R2 object key has `.webp`, returned DB key does NOT
    // (matches CoverUrlResolver contract, prevents double-suffix bug).
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UploadAsync_ValidWebpBytes_PutsObjectWithCorrectKey()
    {
        // Arrange
        var gameId = Guid.Parse("12345678-1234-1234-1234-123456789abc");
        var webpBytes = new byte[] { 0x52, 0x49, 0x46, 0x46 }; // "RIFF" header — synthetic WebP-ish bytes
        PutObjectRequest? capturedRequest = null;

        _mockS3Client
            .Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PutObjectRequest, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new PutObjectResponse { HttpStatusCode = HttpStatusCode.OK, ETag = "\"abc\"" });

        // Act
        await _sut.UploadAsync(gameId, webpBytes, CancellationToken.None);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Key.Should().Be(
            $"covers/{gameId}/cover.webp",
            "physical R2 object key MUST end in .webp");
        capturedRequest.BucketName.Should().Be("test-bucket");
    }

    [Fact]
    public async Task UploadAsync_ReturnsKeyWithoutWebpSuffix_ForDbStorage()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var webpBytes = new byte[] { 0x52, 0x49, 0x46, 0x46 };

        _mockS3Client
            .Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutObjectResponse { HttpStatusCode = HttpStatusCode.OK });

        // Act
        var returnedKey = await _sut.UploadAsync(gameId, webpBytes, CancellationToken.None);

        // Assert
        returnedKey.Should().Be(
            $"covers/{gameId}/cover",
            "DB key MUST NOT include .webp suffix — CoverUrlResolver.cs:73-79 appends '.webp' when building presigned URLs. Including .webp here causes double-suffix bug (covers/.../cover.webp.webp → 404)");
        returnedKey.Should().NotEndWith(".webp");
    }

    [Fact]
    public async Task UploadAsync_SetsContentTypeImageWebp()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var webpBytes = new byte[] { 0x52, 0x49, 0x46, 0x46 };
        PutObjectRequest? capturedRequest = null;

        _mockS3Client
            .Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PutObjectRequest, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new PutObjectResponse { HttpStatusCode = HttpStatusCode.OK });

        // Act
        await _sut.UploadAsync(gameId, webpBytes, CancellationToken.None);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.ContentType.Should().Be("image/webp");
    }

    [Fact]
    public async Task UploadAsync_SetsCacheControlImmutable1Year()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var webpBytes = new byte[] { 0x52, 0x49, 0x46, 0x46 };
        PutObjectRequest? capturedRequest = null;

        _mockS3Client
            .Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PutObjectRequest, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new PutObjectResponse { HttpStatusCode = HttpStatusCode.OK });

        // Act
        await _sut.UploadAsync(gameId, webpBytes, CancellationToken.None);

        // Assert (ADR DEC-3h: Cache-Control: public, max-age=31536000, immutable)
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Headers.CacheControl.Should().Be(
            "public, max-age=31536000, immutable",
            "ADR DEC-3h mandates 1-year immutable cache for cover assets");
    }

    [Fact]
    public async Task UploadAsync_StreamContainsExactWebpBytes()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var webpBytes = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };
        PutObjectRequest? capturedRequest = null;

        _mockS3Client
            .Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PutObjectRequest, CancellationToken>((req, _) =>
            {
                capturedRequest = req;
                // Snapshot the stream contents before pipeline disposes it.
                using var ms = new MemoryStream();
                req.InputStream.CopyTo(ms);
                req.InputStream.Position = 0; // reset for SDK consumer
                req.InputStream = new MemoryStream(ms.ToArray());
            })
            .ReturnsAsync(new PutObjectResponse { HttpStatusCode = HttpStatusCode.OK });

        // Act
        await _sut.UploadAsync(gameId, webpBytes, CancellationToken.None);

        // Assert
        capturedRequest.Should().NotBeNull();
        using var receivedStream = capturedRequest!.InputStream;
        using var receivedMs = new MemoryStream();
        receivedStream.CopyTo(receivedMs);
        receivedMs.ToArray().Should().BeEquivalentTo(webpBytes);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Validation — null/empty bytes throw ArgumentException
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UploadAsync_NullBytes_ThrowsArgumentException()
    {
        // Arrange
        var gameId = Guid.NewGuid();

        // Act
        Func<Task> act = async () => await _sut.UploadAsync(gameId, null!, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*webpBytes*");

        _mockS3Client.Verify(
            x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "S3 must not be touched when input bytes are invalid");
    }

    [Fact]
    public async Task UploadAsync_EmptyBytes_ThrowsArgumentException()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var emptyBytes = Array.Empty<byte>();

        // Act
        Func<Task> act = async () => await _sut.UploadAsync(gameId, emptyBytes, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*webpBytes*");

        _mockS3Client.Verify(
            x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "S3 must not be touched when input bytes are empty");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Cancellation propagation — never swallowed
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UploadAsync_Cancellation_RethrowsOperationCanceledException()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var webpBytes = new byte[] { 0x52, 0x49, 0x46, 0x46 };
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _mockS3Client
            .Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        // Act
        Func<Task> act = async () => await _sut.UploadAsync(gameId, webpBytes, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>(
            "cancellation must propagate; never wrapped or swallowed");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // S3 error propagation — log + rethrow (caller decides retry)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UploadAsync_S3Exception_LogsAndRethrows()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var webpBytes = new byte[] { 0x52, 0x49, 0x46, 0x46 };
        var s3Exception = new AmazonS3Exception("Bucket missing")
        {
            StatusCode = HttpStatusCode.InternalServerError,
            ErrorCode = "InternalError"
        };

        _mockS3Client
            .Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(s3Exception);

        // Act
        Func<Task> act = async () => await _sut.UploadAsync(gameId, webpBytes, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<AmazonS3Exception>();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Determinism — same gameId produces same key (cache invalidation safe)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UploadAsync_DeterministicKey_SameGameIdProducesSameKey()
    {
        // Arrange
        var gameId = Guid.Parse("aabbccdd-1122-3344-5566-7788990011aa");
        var webpBytes = new byte[] { 0x52, 0x49, 0x46, 0x46 };
        var capturedKeys = new List<string>();

        _mockS3Client
            .Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PutObjectRequest, CancellationToken>((req, _) => capturedKeys.Add(req.Key))
            .ReturnsAsync(new PutObjectResponse { HttpStatusCode = HttpStatusCode.OK });

        // Act — upload twice with same gameId
        var k1 = await _sut.UploadAsync(gameId, webpBytes, CancellationToken.None);
        var k2 = await _sut.UploadAsync(gameId, webpBytes, CancellationToken.None);

        // Assert
        k1.Should().Be(k2, "same gameId MUST yield same DB key — enables overwrite-on-republish without orphan blobs");
        capturedKeys.Should().HaveCount(2);
        capturedKeys[0].Should().Be(capturedKeys[1], "same gameId MUST yield same R2 object key — Cloudflare cache stays warm");
    }

    [Fact]
    public async Task UploadAsync_DifferentGameIds_ProduceDifferentKeys()
    {
        // Arrange
        var gameIdA = Guid.NewGuid();
        var gameIdB = Guid.NewGuid();
        var webpBytes = new byte[] { 0x52, 0x49, 0x46, 0x46 };

        _mockS3Client
            .Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutObjectResponse { HttpStatusCode = HttpStatusCode.OK });

        // Act
        var keyA = await _sut.UploadAsync(gameIdA, webpBytes, CancellationToken.None);
        var keyB = await _sut.UploadAsync(gameIdB, webpBytes, CancellationToken.None);

        // Assert
        keyA.Should().NotBe(keyB, "different games MUST yield different keys");
        keyA.Should().Be($"covers/{gameIdA}/cover");
        keyB.Should().Be($"covers/{gameIdB}/cover");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Constructor guards
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Ctor_NullS3Client_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new CoverR2UploadPipeline(null!, _options, _mockLogger.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("s3Client");
    }

    [Fact]
    public void Ctor_NullOptions_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new CoverR2UploadPipeline(_mockS3Client.Object, null!, _mockLogger.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("options");
    }

    [Fact]
    public void Ctor_NullLogger_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new CoverR2UploadPipeline(_mockS3Client.Object, _options, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }
}
