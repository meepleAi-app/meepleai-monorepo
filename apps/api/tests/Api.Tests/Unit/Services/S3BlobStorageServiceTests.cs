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

        _sut = new S3BlobStorageService(_mockS3Client.Object, _options, _mockLogger.Object);
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
        var result = await _sut.StoreAsync(stream, fileName, gameId);

        // Assert
        result.Success.Should().BeTrue();
        result.FileId.Should().NotBeNull();
        result.FilePath.Should().Contain("game123");
        result.FileSizeBytes.Should().Be(content.Length);

        _mockS3Client.Verify(x => x.PutObjectAsync(
            It.Is<PutObjectRequest>(r =>
                r.BucketName == "test-bucket" &&
                r.Key.Contains("pdf_uploads/game123/") &&
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
        var result = await _sut.StoreAsync(stream, fileName, gameId);

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
                new() { Key = $"pdf_uploads/{gameId}/{fileId}_test.pdf" }
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
        var result = await _sut.RetrieveAsync(fileId, gameId);

        // Assert
        result.Should().NotBeNull();
        result.CanRead.Should().BeTrue();

        _mockS3Client.Verify(x => x.ListObjectsV2Async(
            It.Is<ListObjectsV2Request>(r =>
                r.BucketName == "test-bucket" &&
                r.Prefix == $"pdf_uploads/{gameId}/{fileId}_" &&
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
        var result = await _sut.RetrieveAsync(fileId, gameId);

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
                new() { Key = $"pdf_uploads/{gameId}/{fileId}_test.pdf" }
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
        var result = await _sut.RetrieveAsync(fileId, gameId);

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
                new() { Key = $"pdf_uploads/{gameId}/{fileId}_test.pdf" }
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
        var result = await _sut.DeleteAsync(fileId, gameId);

        // Assert
        result.Should().BeTrue();

        _mockS3Client.Verify(x => x.DeleteObjectAsync(
            It.Is<DeleteObjectRequest>(r =>
                r.BucketName == "test-bucket" &&
                r.Key == $"pdf_uploads/{gameId}/{fileId}_test.pdf"),
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
        var result = await _sut.DeleteAsync(fileId, gameId);

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
        var result = _sut.GetStoragePath(fileId, gameId, fileName);

        // Assert
        result.Should().Be($"pdf_uploads/{gameId}/{fileId}_test.pdf");
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
            _sut.GetStoragePath(fileId, invalidGameId, fileName);
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
                new() { Key = $"pdf_uploads/{gameId}/{fileId}_test.pdf" }
            }
        };

        _mockS3Client.Setup(x => x.ListObjectsV2Async(
            It.IsAny<ListObjectsV2Request>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(listResponse);

        // Act
        var result = await _sut.ExistsAsync(fileId, gameId);

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
        var result = await _sut.ExistsAsync(fileId, gameId);

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
        var result = await _sut.ExistsAsync(fileId, invalidGameId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetPresignedDownloadUrlAsync_FileExists_ReturnsUrl()
    {
        // Arrange
        var fileId = "file123";
        var gameId = "game123";
        var expectedUrl = "https://test-bucket.s3.amazonaws.com/pdf_uploads/game123/file123_test.pdf?presigned=true";

        var listResponse = new ListObjectsV2Response
        {
            HttpStatusCode = HttpStatusCode.OK,
            S3Objects = new List<S3Object>
            {
                new() { Key = $"pdf_uploads/{gameId}/{fileId}_test.pdf" }
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
        var result = await _sut.GetPresignedDownloadUrlAsync(fileId, gameId);

        // Assert
        result.Should().NotBeNull();
        result.Should().Be(expectedUrl);

        _mockS3Client.Verify(x => x.GetPreSignedURLAsync(
            It.Is<GetPreSignedUrlRequest>(r =>
                r.BucketName == "test-bucket" &&
                r.Key == $"pdf_uploads/{gameId}/{fileId}_test.pdf" &&
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
        var result = await _sut.GetPresignedDownloadUrlAsync(fileId, gameId);

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
                new() { Key = $"pdf_uploads/{gameId}/{fileId}_test.pdf" }
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
        var result = await _sut.GetPresignedDownloadUrlAsync(fileId, gameId, customExpiry);

        // Assert
        result.Should().NotBeNull();
        result.Should().Be(expectedUrl);
    }
}
