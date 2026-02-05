using Amazon.S3;
using Amazon.S3.Model;
using Api.Services.Pdf;
using Microsoft.Extensions.Logging;
using Moq;
using System.Net;
using Xunit;

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
        Assert.True(result.Success);
        Assert.NotNull(result.FileId);
        Assert.Contains("game123", result.FilePath);
        Assert.Equal(content.Length, result.FileSizeBytes);

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
        Assert.False(result.Success);
        Assert.Null(result.FileId);
        Assert.Contains("S3 error", result.ErrorMessage);
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
        Assert.NotNull(result);
        Assert.True(result.CanRead);

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
        Assert.Null(result);
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
        Assert.Null(result);
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
        Assert.True(result);

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
        Assert.False(result);

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
        Assert.Equal($"pdf_uploads/{gameId}/{fileId}_test.pdf", result);
    }

    [Fact]
    public void GetStoragePath_InvalidGameId_ThrowsArgumentException()
    {
        // Arrange
        var fileId = "file123";
        var invalidGameId = "../../../etc/passwd";
        var fileName = "test.pdf";

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            _sut.GetStoragePath(fileId, invalidGameId, fileName));
    }

    [Fact]
    public void Exists_FileExists_ReturnsTrue()
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
        var result = _sut.Exists(fileId, gameId);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Exists_FileNotFound_ReturnsFalse()
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
        var result = _sut.Exists(fileId, gameId);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Exists_InvalidGameId_ReturnsFalse()
    {
        // Arrange
        var fileId = "file123";
        var invalidGameId = "../../../etc/passwd";

        // Act
        var result = _sut.Exists(fileId, invalidGameId);

        // Assert
        Assert.False(result);
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
        Assert.NotNull(result);
        Assert.Equal(expectedUrl, result);

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
        Assert.Null(result);

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
        Assert.NotNull(result);
        Assert.Equal(expectedUrl, result);
    }
}
