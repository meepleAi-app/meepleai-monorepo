using Amazon.S3;
using Amazon.S3.Model;
using Api.Services.Pdf;
using Microsoft.Extensions.Logging;
using Moq;
using System.Net;
using Xunit;
using FluentAssertions;

namespace Api.Tests.Unit.Services;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "DocumentProcessing")]
public sealed class S3BlobStorageServiceTests : IDisposable
{
    private readonly Mock<IAmazonS3> _mockS3Client;
    private readonly Mock<ILogger<S3BlobStorageService>> _mockLogger;
    private readonly S3StorageOptions _options;
    private readonly S3BlobStorageService _sut;

    public S3BlobStorageServiceTests()
    {
        _mockS3Client = new Mock<IAmazonS3>();
        _mockLogger = new Mock<ILogger<S3BlobStorageService>>();
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

        // Issue #1399 (Phase 4 cleanup): StorageLayoutOptions no longer drives
        // write/read branching — the service always operates on the categorized
        // "v2" layout (BlobCategory.Pdf → "pdfs/" prefix). The ctor lost the
        // IOptions<StorageLayoutOptions> parameter as part of that simplification.
        _sut = new S3BlobStorageService(
            _mockS3Client.Object,
            _options,
            _mockLogger.Object);
    }

    public void Dispose()
    {
        _mockS3Client.Reset();
    }

    [Fact]
    public async Task StoreAsync_Success_ReturnsSuccessResult()
    {
        // Arrange
        var fileName = "test.pdf";
        var gameId = "game123";
        var content = "PDF content"u8.ToArray();
        using var stream = new MemoryStream(content);

        var putResponse = new PutObjectResponse
        {
            HttpStatusCode = HttpStatusCode.OK,
            ETag = "\"test-etag\""
        };

        _mockS3Client.Setup(x => x.PutObjectAsync(
            It.IsAny<PutObjectRequest>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(putResponse);

        // Act
        var result = await _sut.StoreAsync(stream, fileName, BlobCategory.Pdf, gameId);

        // Assert
        result.Success.Should().BeTrue();
        result.FileId.Should().NotBeNull();
        result.FilePath.Should().Contain("game123");
        result.FileSizeBytes.Should().Be(content.Length);

        _mockS3Client.Verify(x => x.PutObjectAsync(
            It.Is<PutObjectRequest>(r =>
                r.BucketName == "test-bucket" &&
                r.Key.Contains("pdfs/game123/") &&
                r.ContentType == "application/pdf" &&
                r.ServerSideEncryptionMethod == ServerSideEncryptionMethod.AES256),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StoreAsync_S3Exception_ReturnsFailureResult()
    {
        // Arrange
        var fileName = "test.pdf";
        var gameId = "game123";
        using var stream = new MemoryStream("PDF content"u8.ToArray());

        _mockS3Client.Setup(x => x.PutObjectAsync(
            It.IsAny<PutObjectRequest>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("Access denied"));

        // Act
        var result = await _sut.StoreAsync(stream, fileName, BlobCategory.Pdf, gameId);

        // Assert
        result.Success.Should().BeFalse();
        result.FileId.Should().BeNull();
        result.ErrorMessage.Should().Contain("S3 error");
    }

    [Fact]
    public async Task RetrieveAsync_FileExists_ReturnsStream()
    {
        // Arrange
        var fileId = "file123";
        var gameId = "game123";

        var listResponse = new ListObjectsV2Response
        {
            HttpStatusCode = HttpStatusCode.OK,
            S3Objects = new List<S3Object>
            {
                new() { Key = $"pdfs/{gameId}/{fileId}_test.pdf" }
            }
        };

        var getResponse = new GetObjectResponse
        {
            HttpStatusCode = HttpStatusCode.OK,
            ResponseStream = new MemoryStream("PDF content"u8.ToArray())
        };

        _mockS3Client.Setup(x => x.ListObjectsV2Async(
            It.IsAny<ListObjectsV2Request>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(listResponse);

        _mockS3Client.Setup(x => x.GetObjectAsync(
            It.IsAny<GetObjectRequest>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(getResponse);

        // Act
        var result = await _sut.RetrieveAsync(fileId, BlobCategory.Pdf, gameId);

        // Assert
        result.Should().NotBeNull();
        result.CanRead.Should().BeTrue();

        _mockS3Client.Verify(x => x.ListObjectsV2Async(
            It.Is<ListObjectsV2Request>(r =>
                r.BucketName == "test-bucket" &&
                r.Prefix == $"pdfs/{gameId}/{fileId}_" &&
                r.MaxKeys == 1),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RetrieveAsync_FileNotFound_ReturnsNull()
    {
        // Arrange
        var fileId = "nonexistent";
        var gameId = "game123";

        var listResponse = new ListObjectsV2Response
        {
            HttpStatusCode = HttpStatusCode.OK,
            S3Objects = new List<S3Object>() // Empty list
        };

        _mockS3Client.Setup(x => x.ListObjectsV2Async(
            It.IsAny<ListObjectsV2Request>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(listResponse);

        // Act
        var result = await _sut.RetrieveAsync(fileId, BlobCategory.Pdf, gameId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task RetrieveAsync_S3NotFoundException_ReturnsNull()
    {
        // Arrange
        var fileId = "file123";
        var gameId = "game123";

        var listResponse = new ListObjectsV2Response
        {
            HttpStatusCode = HttpStatusCode.OK,
            S3Objects = new List<S3Object>
            {
                new() { Key = $"pdfs/{gameId}/{fileId}_test.pdf" }
            }
        };

        _mockS3Client.Setup(x => x.ListObjectsV2Async(
            It.IsAny<ListObjectsV2Request>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(listResponse);

        _mockS3Client.Setup(x => x.GetObjectAsync(
            It.IsAny<GetObjectRequest>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("Not found") { StatusCode = HttpStatusCode.NotFound });

        // Act
        var result = await _sut.RetrieveAsync(fileId, BlobCategory.Pdf, gameId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_FileExists_ReturnsTrue()
    {
        // Arrange
        var fileId = "file123";
        var gameId = "game123";

        var listResponse = new ListObjectsV2Response
        {
            HttpStatusCode = HttpStatusCode.OK,
            S3Objects = new List<S3Object>
            {
                new() { Key = $"pdfs/{gameId}/{fileId}_test.pdf" }
            }
        };

        var deleteResponse = new DeleteObjectResponse
        {
            HttpStatusCode = HttpStatusCode.NoContent
        };

        _mockS3Client.Setup(x => x.ListObjectsV2Async(
            It.IsAny<ListObjectsV2Request>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(listResponse);

        _mockS3Client.Setup(x => x.DeleteObjectAsync(
            It.IsAny<DeleteObjectRequest>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(deleteResponse);

        // Act
        var result = await _sut.DeleteAsync(fileId, BlobCategory.Pdf, gameId);

        // Assert
        result.Should().BeTrue();

        _mockS3Client.Verify(x => x.DeleteObjectAsync(
            It.Is<DeleteObjectRequest>(r =>
                r.BucketName == "test-bucket" &&
                r.Key == $"pdfs/{gameId}/{fileId}_test.pdf"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_FileNotFound_ReturnsFalse()
    {
        // Arrange
        var fileId = "nonexistent";
        var gameId = "game123";

        var listResponse = new ListObjectsV2Response
        {
            HttpStatusCode = HttpStatusCode.OK,
            S3Objects = new List<S3Object>() // Empty list
        };

        _mockS3Client.Setup(x => x.ListObjectsV2Async(
            It.IsAny<ListObjectsV2Request>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(listResponse);

        // Act
        var result = await _sut.DeleteAsync(fileId, BlobCategory.Pdf, gameId);

        // Assert
        result.Should().BeFalse();

        _mockS3Client.Verify(x => x.DeleteObjectAsync(
            It.IsAny<DeleteObjectRequest>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void GetStoragePath_ValidInputs_ReturnsS3Key()
    {
        // Arrange
        var fileId = "file123";
        var gameId = "game123";
        var fileName = "test.pdf";

        // Act
        var result = _sut.GetStoragePath(fileId, BlobCategory.Pdf, gameId, fileName);

        // Assert
        result.Should().Be($"pdfs/{gameId}/{fileId}_test.pdf");
    }

    [Fact]
    public void GetStoragePath_InvalidGameId_ThrowsArgumentException()
    {
        // Arrange
        var fileId = "file123";
        var invalidGameId = "../../../etc/passwd";
        var fileName = "test.pdf";

        // Act & Assert
        var act = () =>
            _sut.GetStoragePath(fileId, BlobCategory.Pdf, invalidGameId, fileName);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task ExistsAsync_FileExists_ReturnsTrue()
    {
        // Arrange
        var fileId = "file123";
        var gameId = "game123";

        var listResponse = new ListObjectsV2Response
        {
            HttpStatusCode = HttpStatusCode.OK,
            S3Objects = new List<S3Object>
            {
                new() { Key = $"pdfs/{gameId}/{fileId}_test.pdf" }
            }
        };

        _mockS3Client.Setup(x => x.ListObjectsV2Async(
            It.IsAny<ListObjectsV2Request>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(listResponse);

        // Act
        var result = await _sut.ExistsAsync(fileId, BlobCategory.Pdf, gameId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_FileNotFound_ReturnsFalse()
    {
        // Arrange
        var fileId = "nonexistent";
        var gameId = "game123";

        var listResponse = new ListObjectsV2Response
        {
            HttpStatusCode = HttpStatusCode.OK,
            S3Objects = new List<S3Object>() // Empty list
        };

        _mockS3Client.Setup(x => x.ListObjectsV2Async(
            It.IsAny<ListObjectsV2Request>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(listResponse);

        // Act
        var result = await _sut.ExistsAsync(fileId, BlobCategory.Pdf, gameId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsAsync_InvalidGameId_ReturnsFalse()
    {
        // Arrange
        var fileId = "file123";
        var invalidGameId = "../../../etc/passwd";

        // Act
        var result = await _sut.ExistsAsync(fileId, BlobCategory.Pdf, invalidGameId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetPresignedDownloadUrlAsync_FileExists_ReturnsUrl()
    {
        // Arrange
        var fileId = "file123";
        var gameId = "game123";
        var expectedUrl = "https://test-bucket.s3.amazonaws.com/pdfs/game123/file123_test.pdf?presigned=true";

        var listResponse = new ListObjectsV2Response
        {
            HttpStatusCode = HttpStatusCode.OK,
            S3Objects = new List<S3Object>
            {
                new() { Key = $"pdfs/{gameId}/{fileId}_test.pdf" }
            }
        };

        _mockS3Client.Setup(x => x.ListObjectsV2Async(
            It.IsAny<ListObjectsV2Request>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(listResponse);

        _mockS3Client.Setup(x => x.GetPreSignedURLAsync(
            It.IsAny<GetPreSignedUrlRequest>()))
            .ReturnsAsync(expectedUrl);

        // Act
        var result = await _sut.GetPresignedDownloadUrlAsync(fileId, BlobCategory.Pdf, gameId);

        // Assert
        result.Should().NotBeNull();
        result.Should().Be(expectedUrl);

        _mockS3Client.Verify(x => x.GetPreSignedURLAsync(
            It.Is<GetPreSignedUrlRequest>(r =>
                r.BucketName == "test-bucket" &&
                r.Key == $"pdfs/{gameId}/{fileId}_test.pdf" &&
                r.Verb == HttpVerb.GET)),
            Times.Once);
    }

    [Fact]
    public async Task GetPresignedDownloadUrlAsync_FileNotFound_ReturnsNull()
    {
        // Arrange
        var fileId = "nonexistent";
        var gameId = "game123";

        var listResponse = new ListObjectsV2Response
        {
            HttpStatusCode = HttpStatusCode.OK,
            S3Objects = new List<S3Object>() // Empty
        };

        _mockS3Client.Setup(x => x.ListObjectsV2Async(
            It.IsAny<ListObjectsV2Request>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(listResponse);

        // Act
        var result = await _sut.GetPresignedDownloadUrlAsync(fileId, BlobCategory.Pdf, gameId);

        // Assert
        result.Should().BeNull();

        _mockS3Client.Verify(x => x.GetPreSignedURLAsync(
            It.IsAny<GetPreSignedUrlRequest>()),
            Times.Never);
    }

    [Fact]
    public async Task GetPresignedDownloadUrlAsync_CustomExpiry_UsesProvidedValue()
    {
        // Arrange
        var fileId = "file123";
        var gameId = "game123";
        var customExpiry = 7200; // 2 hours
        var expectedUrl = "https://test-bucket.s3.amazonaws.com/presigned";

        var listResponse = new ListObjectsV2Response
        {
            HttpStatusCode = HttpStatusCode.OK,
            S3Objects = new List<S3Object>
            {
                new() { Key = $"pdfs/{gameId}/{fileId}_test.pdf" }
            }
        };

        _mockS3Client.Setup(x => x.ListObjectsV2Async(
            It.IsAny<ListObjectsV2Request>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(listResponse);

        _mockS3Client.Setup(x => x.GetPreSignedURLAsync(
            It.IsAny<GetPreSignedUrlRequest>()))
            .ReturnsAsync(expectedUrl);

        // Act
        var result = await _sut.GetPresignedDownloadUrlAsync(fileId, BlobCategory.Pdf, gameId, customExpiry);

        // Assert
        result.Should().NotBeNull();
        result.Should().Be(expectedUrl);
    }

    /// <summary>
    /// I1 GATE (final whole-branch review of feature/game-cover-configuration): reproduces
    /// the exact call shape used by <c>CoverUrlResolver.ResolvePublicAsync</c>'s L4 branch —
    /// <c>GetPresignedDownloadUrlAsync($"{PdfCoverR2Key}-preview.webp", BlobCategory.GameImage,
    /// PdfCoverR2Key)</c> — where <c>PdfCoverR2Key</c> is the flat, slash-containing key
    /// written by <c>ProposeCoverChangeCommandHandler</c>
    /// (<c>covers/{sharedGameId:D}/pdf-cover-pending</c>).
    ///
    /// Both the <c>fileId</c> and <c>resourceKey</c> arguments passed here contain <c>/</c>.
    /// <see cref="S3BlobStorageService.GetPresignedDownloadUrlAsync"/> validates BOTH via
    /// <c>PathSecurity.ValidateIdentifier</c> (regex <c>^[a-zA-Z0-9_-]+$</c>, which rejects
    /// <c>/</c>) BEFORE ever touching <see cref="IAmazonS3"/>. This test asserts that
    /// outcome deterministically (no S3/HTTP call should even be attempted) — settling
    /// whether the flat-slash L4 key convention can resolve at all, independent of any
    /// Testcontainers/MinIO transport environment noise.
    /// </summary>
    [Fact]
    public async Task GetPresignedDownloadUrlAsync_FlatSlashPdfCoverKeyShape_ReturnsNullWithoutCallingS3()
    {
        // Arrange: exact shape CoverUrlResolver.ResolvePublicAsync passes for L4.
        var pdfCoverR2Key = $"covers/{Guid.NewGuid():D}/pdf-cover-pending";
        var fileId = $"{pdfCoverR2Key}-preview.webp";

        // Act
        var result = await _sut.GetPresignedDownloadUrlAsync(fileId, BlobCategory.GameImage, pdfCoverR2Key);

        // Assert: PathSecurity.ValidateIdentifier throws ArgumentException on the slash
        // before any S3 call — the catch-all handler converts it to a null return, which
        // is indistinguishable from "not found" at the CoverUrlResolver call site.
        result.Should().BeNull(
            "PathSecurity.ValidateIdentifier rejects '/' in both fileId and resourceKey, " +
            "so the flat-slash L4 key can never reach S3 — this is the I1 gate verdict");

        _mockS3Client.Verify(x => x.ListObjectsV2Async(
            It.IsAny<ListObjectsV2Request>(),
            It.IsAny<CancellationToken>()),
            Times.Never,
            "validation must fail before any S3 round-trip is attempted");

        _mockS3Client.Verify(x => x.GetPreSignedURLAsync(
            It.IsAny<GetPreSignedUrlRequest>()),
            Times.Never);
    }

    /// <summary>
    /// Companion to the I1 gate above: confirms the ALREADY-SHIPPED L2 Wikidata convention
    /// (<c>GetPresignedDownloadUrlAsync($"{WikidataCoverR2Key}.webp", GameImage,
    /// WikidataCoverR2Key)</c>, same flat-slash shape) hits the identical validation
    /// failure — proving this is a pre-existing, shared defect in the flat-slash key
    /// convention itself, not something unique to the new PDF-cover (L4) feature.
    /// </summary>
    [Fact]
    public async Task GetPresignedDownloadUrlAsync_FlatSlashWikidataKeyShape_ReturnsNullWithoutCallingS3()
    {
        // Arrange: exact shape CoverUrlResolver.ResolvePublicAsync passes for L2.
        var wikidataCoverR2Key = $"covers/{Guid.NewGuid():D}/wikidata-cover";
        var fileId = $"{wikidataCoverR2Key}.webp";

        // Act
        var result = await _sut.GetPresignedDownloadUrlAsync(fileId, BlobCategory.GameImage, wikidataCoverR2Key);

        // Assert
        result.Should().BeNull(
            "the L2 Wikidata convention uses the identical flat-slash shape as L4 and " +
            "hits the same PathSecurity.ValidateIdentifier rejection");

        _mockS3Client.Verify(x => x.ListObjectsV2Async(
            It.IsAny<ListObjectsV2Request>(),
            It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── Issue #2271: non-seekable stream handling ────────────────────────────
    // R2 (and other S3-compatible providers) reject PutObject when the SDK's
    // checksum calculator cannot read a stream's Length. This happens when the
    // caller hands us a non-seekable Stream (e.g. HTTP request body forwarded
    // raw from ASP.NET). The service must transparently buffer the payload so
    // PutObjectRequest gets a seekable stream with an explicit ContentLength.

    [Fact]
    public async Task StoreAsync_WithNonSeekableStream_BuffersAndSetsContentLength()
    {
        // Arrange
        var fileName = "wingspan-rules.pdf";
        var gameId = "game-2271";
        var content = "PDF binary content"u8.ToArray();
        using var nonSeekable = new NonSeekableReadStream(content);

        // Snapshot the request shape DURING the call — the buffered MemoryStream
        // is disposed in StoreAsync's `finally` after the SDK returns, so reading
        // CanSeek after the await would always observe False.
        bool capturedCanSeek = false;
        long capturedContentLength = -1;
        long capturedHeaderContentLength = -1;
        bool capturedDisableChecksumValidation = false;
        bool capturedDisablePayloadSigning = false;
        string? capturedKey = null;

        _mockS3Client
            .Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PutObjectRequest, CancellationToken>((req, _) =>
            {
                capturedCanSeek = req.InputStream.CanSeek;
                capturedContentLength = req.InputStream.Length;
                capturedHeaderContentLength = req.Headers.ContentLength;
                capturedDisableChecksumValidation = req.DisableDefaultChecksumValidation ?? false;
                capturedDisablePayloadSigning = req.DisablePayloadSigning ?? false;
                capturedKey = req.Key;
            })
            .ReturnsAsync(new PutObjectResponse { HttpStatusCode = HttpStatusCode.OK, ETag = "\"buffered-etag\"" });

        // Act
        var result = await _sut.StoreAsync(nonSeekable, fileName, BlobCategory.Pdf, gameId);

        // Assert — outcome
        result.Success.Should().BeTrue(because: "non-seekable streams must be transparently buffered");
        result.FileSizeBytes.Should().Be(content.Length);

        // Assert — request shape (snapshot from the live request inside PutObjectAsync)
        capturedCanSeek.Should().BeTrue(
            because: "the request stream must be seekable after buffering so the SDK can compute Length");
        capturedContentLength.Should().Be(content.Length);
        capturedHeaderContentLength.Should().Be(content.Length,
            because: "ContentLength must be explicit to satisfy R2's strict header validation");
        capturedDisableChecksumValidation.Should().BeTrue(
            because: "default checksum validation would re-read the stream; we have the buffered length already");
        capturedDisablePayloadSigning.Should().BeTrue(
            because: "regression guard — pre-existing R2/MinIO compatibility flag");
        capturedKey.Should().Contain("game-2271");
    }

    [Fact]
    public async Task StoreAsync_WithSeekableStream_DoesNotAllocateBuffer()
    {
        // Arrange — verify the seekable path stays zero-copy (no perf regression)
        var fileName = "small.pdf";
        var gameId = "game-2271-seek";
        var content = "PDF"u8.ToArray();
        using var seekable = new MemoryStream(content);

        bool capturedInputIsSameInstance = false;
        long capturedHeaderContentLength = -1;
        _mockS3Client
            .Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PutObjectRequest, CancellationToken>((req, _) =>
            {
                capturedInputIsSameInstance = ReferenceEquals(req.InputStream, seekable);
                capturedHeaderContentLength = req.Headers.ContentLength;
            })
            .ReturnsAsync(new PutObjectResponse { HttpStatusCode = HttpStatusCode.OK, ETag = "\"seek-etag\"" });

        // Act
        var result = await _sut.StoreAsync(seekable, fileName, BlobCategory.Pdf, gameId);

        // Assert
        result.Success.Should().BeTrue();
        capturedInputIsSameInstance.Should().BeTrue(
            because: "seekable streams must be forwarded as-is, no allocation");
        capturedHeaderContentLength.Should().Be(content.Length);
    }

    /// <summary>
    /// Simulates an HTTP request body or a forward-only stream. Throws on Length
    /// and Position to surface the AWS SDK's "Could not determine content length"
    /// failure mode.
    /// </summary>
    private sealed class NonSeekableReadStream : Stream
    {
        private readonly MemoryStream _inner;
        public NonSeekableReadStream(byte[] data) { _inner = new MemoryStream(data); }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException("non-seekable");
        public override long Position
        {
            get => throw new NotSupportedException("non-seekable");
            set => throw new NotSupportedException("non-seekable");
        }
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException("non-seekable");
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() { }
        protected override void Dispose(bool disposing)
        {
            if (disposing) _inner.Dispose();
            base.Dispose(disposing);
        }
    }
}
