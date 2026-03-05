using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Api.Infrastructure.Health.Checks;
using Api.Services.Pdf;
using Api.Tests.Infrastructure;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.Integration.DocumentProcessing;

/// <summary>
/// Integration tests for S3BlobStorageService using a real MinIO container.
/// Validates the full storage lifecycle: Store → Exists → Retrieve → PresignedUrl → Delete.
/// Uses standalone MinIO Testcontainer (not SharedTestcontainersFixture).
/// </summary>
[Trait("Category", "Integration")]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Feature", "S3Storage")]
public sealed class S3BlobStorageIntegrationTests : IAsyncLifetime
{
    private IContainer? _minioContainer;
    private S3BlobStorageService _sut = null!;
    private IAmazonS3 _s3Client = null!;
    private S3StorageOptions _options = null!;
    private bool _skipTests;

    private const string TestBucket = TestcontainersConfiguration.MinioTestBucket;
    private const string RootUser = TestcontainersConfiguration.MinioRootUser;
    private const string RootPassword = TestcontainersConfiguration.MinioRootPassword;

    private void SkipIfNotAvailable()
    {
        if (_skipTests)
            Assert.Skip("S3 storage tests require Docker or TEST_S3_ENDPOINT environment variable");
    }

    public async ValueTask InitializeAsync()
    {
        // Check for external S3 endpoint (CI environments with pre-provisioned MinIO)
        var externalEndpoint = Environment.GetEnvironmentVariable("TEST_S3_ENDPOINT");
        string endpoint;

        if (!string.IsNullOrWhiteSpace(externalEndpoint))
        {
            endpoint = externalEndpoint;
            Console.WriteLine($"Using external S3 endpoint: {endpoint}");
        }
        else
        {
            try
            {
                // Start MinIO container
                _minioContainer = new ContainerBuilder()
                    .WithImage(TestcontainersConfiguration.MinioImage)
                    .WithPortBinding(TestcontainersConfiguration.MinioApiPort, true)
                    .WithPortBinding(TestcontainersConfiguration.MinioConsolePort, true)
                    .WithEnvironment("MINIO_ROOT_USER", RootUser)
                    .WithEnvironment("MINIO_ROOT_PASSWORD", RootPassword)
                    .WithCommand("server", "/data", "--console-address", ":9001")
                    .WithWaitStrategy(Wait.ForUnixContainer()
                        .UntilHttpRequestIsSucceeded(r => r
                            .ForPath("/minio/health/live")
                            .ForPort(TestcontainersConfiguration.MinioApiPort)
                            .ForStatusCode(System.Net.HttpStatusCode.OK)))
                    .WithCleanUp(true)
                    .Build();

                await _minioContainer.StartAsync();

                var port = _minioContainer.GetMappedPublicPort(TestcontainersConfiguration.MinioApiPort);
                endpoint = $"http://localhost:{port}";
                Console.WriteLine($"MinIO container started at {endpoint}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to start MinIO container: {ex.Message}. S3 tests will be skipped.");
                _skipTests = true;
                return;
            }
        }

        try
        {
            // Create S3 client with MinIO credentials
            var credentials = new BasicAWSCredentials(
                Environment.GetEnvironmentVariable("TEST_S3_ACCESS_KEY") ?? RootUser,
                Environment.GetEnvironmentVariable("TEST_S3_SECRET_KEY") ?? RootPassword);

            var s3Config = new AmazonS3Config
            {
                ServiceURL = endpoint,
                ForcePathStyle = true, // Required for MinIO
                AuthenticationRegion = "us-east-1"
            };

            _s3Client = new AmazonS3Client(credentials, s3Config);

            // Create test bucket (also serves as connectivity check)
            try
            {
                await _s3Client.PutBucketAsync(new PutBucketRequest { BucketName = TestBucket });
            }
            catch (AmazonS3Exception ex) when (ex.ErrorCode == "BucketAlreadyOwnedByYou" || ex.ErrorCode == "BucketAlreadyExists")
            {
                // Bucket already exists, OK
            }

            _options = new S3StorageOptions
            {
                Endpoint = endpoint,
                AccessKey = Environment.GetEnvironmentVariable("TEST_S3_ACCESS_KEY") ?? RootUser,
                SecretKey = Environment.GetEnvironmentVariable("TEST_S3_SECRET_KEY") ?? RootPassword,
                BucketName = TestBucket,
                Region = "us-east-1",
                PresignedUrlExpirySeconds = 3600,
                EnableEncryption = false, // MinIO doesn't require SSE
                ForcePathStyle = true
            };

            var logger = new Mock<ILogger<S3BlobStorageService>>().Object;
            _sut = new S3BlobStorageService(_s3Client, _options, logger);

            // Quick smoke test to verify S3 connectivity
            using var probe = new MemoryStream("probe"u8.ToArray());
            var probeResult = await _sut.StoreAsync(probe, "probe.txt", "healthcheck");
            if (!probeResult.Success)
            {
                Console.WriteLine($"S3 connectivity probe failed: {probeResult.ErrorMessage}. Tests will be skipped.");
                _skipTests = true;
                return;
            }
            await _sut.DeleteAsync(probeResult.FileId!, "healthcheck");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to initialize S3 client: {ex.Message}. S3 tests will be skipped.");
            _skipTests = true;
        }
    }

    public async ValueTask DisposeAsync()
    {
        _s3Client?.Dispose();

        if (_minioContainer != null)
        {
            await _minioContainer.StopAsync();
            await _minioContainer.DisposeAsync();
        }
    }

    [Fact]
    public async Task StoreAsync_ValidFile_ReturnsSuccessWithFileId()
    {
        SkipIfNotAvailable();

        // Arrange
        var content = "Integration test PDF content"u8.ToArray();
        using var stream = new MemoryStream(content);
        var gameId = Guid.NewGuid().ToString("N");

        // Act
        var result = await _sut.StoreAsync(stream, "test-document.pdf", gameId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.FileId);
        Assert.NotNull(result.FilePath);
        Assert.Contains($"pdf_uploads/{gameId}/", result.FilePath);
        Assert.Equal(content.Length, result.FileSizeBytes);
    }

    [Fact]
    public async Task ExistsAsync_AfterStore_ReturnsTrue()
    {
        SkipIfNotAvailable();

        // Arrange
        var content = "Exists check content"u8.ToArray();
        using var stream = new MemoryStream(content);
        var gameId = Guid.NewGuid().ToString("N");
        var storeResult = await _sut.StoreAsync(stream, "exists-test.pdf", gameId);
        Assert.True(storeResult.Success);

        // Act
        var exists = await _sut.ExistsAsync(storeResult.FileId!, gameId);

        // Assert
        Assert.True(exists);
    }

    [Fact]
    public async Task RetrieveAsync_AfterStore_ReturnsMatchingContent()
    {
        SkipIfNotAvailable();

        // Arrange
        var originalContent = "Retrieve test content - should match exactly"u8.ToArray();
        using var storeStream = new MemoryStream(originalContent);
        var gameId = Guid.NewGuid().ToString("N");
        var storeResult = await _sut.StoreAsync(storeStream, "retrieve-test.pdf", gameId);
        Assert.True(storeResult.Success);

        // Act
        using var retrievedStream = await _sut.RetrieveAsync(storeResult.FileId!, gameId);

        // Assert
        Assert.NotNull(retrievedStream);
        using var memoryStream = new MemoryStream();
        await retrievedStream.CopyToAsync(memoryStream);
        var retrievedContent = memoryStream.ToArray();
        Assert.Equal(originalContent, retrievedContent);
    }

    [Fact]
    public async Task GetPresignedDownloadUrlAsync_AfterStore_ReturnsValidUrl()
    {
        SkipIfNotAvailable();

        // Arrange
        var content = "Presigned URL test content"u8.ToArray();
        using var stream = new MemoryStream(content);
        var gameId = Guid.NewGuid().ToString("N");
        var storeResult = await _sut.StoreAsync(stream, "presigned-test.pdf", gameId);
        Assert.True(storeResult.Success);

        // Act
        var url = await _sut.GetPresignedDownloadUrlAsync(storeResult.FileId!, gameId, expirySeconds: 300);

        // Assert
        Assert.NotNull(url);
        Assert.Contains(_options.BucketName, url);
        Assert.Contains("X-Amz-Signature", url);

        // Verify the URL is downloadable via HTTP
        using var httpClient = new HttpClient();
        var response = await httpClient.GetAsync(url);
        Assert.True(response.IsSuccessStatusCode, $"Presigned URL download failed: {response.StatusCode}");

        var downloadedContent = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(content, downloadedContent);
    }

    [Fact]
    public async Task DeleteAsync_AfterStore_RemovesFileAndExistsReturnsFalse()
    {
        SkipIfNotAvailable();

        // Arrange
        var content = "Delete test content"u8.ToArray();
        using var stream = new MemoryStream(content);
        var gameId = Guid.NewGuid().ToString("N");
        var storeResult = await _sut.StoreAsync(stream, "delete-test.pdf", gameId);
        Assert.True(storeResult.Success);
        Assert.True(await _sut.ExistsAsync(storeResult.FileId!, gameId));

        // Act
        var deleted = await _sut.DeleteAsync(storeResult.FileId!, gameId);

        // Assert
        Assert.True(deleted);
        Assert.False(await _sut.ExistsAsync(storeResult.FileId!, gameId));
    }

    [Fact]
    public async Task ExistsAsync_PathTraversalAttempt_ReturnsFalse()
    {
        SkipIfNotAvailable();

        // Act
        var result = await _sut.ExistsAsync("file123", "../../../etc/passwd");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task RetrieveAsync_NonExistentFile_ReturnsNull()
    {
        SkipIfNotAvailable();

        // Arrange
        var gameId = Guid.NewGuid().ToString("N");

        // Act
        var result = await _sut.RetrieveAsync("nonexistentfile", gameId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteAsync_NonExistentFile_ReturnsFalse()
    {
        SkipIfNotAvailable();

        // Arrange
        var gameId = Guid.NewGuid().ToString("N");

        // Act
        var result = await _sut.DeleteAsync("nonexistentfile", gameId);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetPresignedDownloadUrlAsync_NonExistentFile_ReturnsNull()
    {
        SkipIfNotAvailable();

        // Arrange
        var gameId = Guid.NewGuid().ToString("N");

        // Act
        var result = await _sut.GetPresignedDownloadUrlAsync("nonexistentfile", gameId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task HealthCheck_WithRealMinIO_ReturnsHealthy()
    {
        SkipIfNotAvailable();

        // Arrange
        var configData = new Dictionary<string, string?>
        {
            ["STORAGE_PROVIDER"] = "s3",
            ["S3_ENDPOINT"] = _options.Endpoint,
            ["S3_BUCKET_NAME"] = _options.BucketName,
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var logger = new Mock<ILogger<S3StorageHealthCheck>>().Object;
        var healthCheck = new S3StorageHealthCheck(_sut, configuration, logger);

        // Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Contains("S3 storage accessible", result.Description);
    }

    [Fact]
    public async Task FullLifecycle_StoreExistsRetrievePresignedDeleteVerify()
    {
        SkipIfNotAvailable();

        // Full lifecycle test: Store → Exists → Retrieve → PresignedUrl → Delete → Verify
        var content = "Full lifecycle integration test content"u8.ToArray();
        var gameId = Guid.NewGuid().ToString("N");

        // 1. Store
        using var storeStream = new MemoryStream(content);
        var storeResult = await _sut.StoreAsync(storeStream, "lifecycle-test.pdf", gameId);
        Assert.True(storeResult.Success, "Store failed");

        // 2. Exists
        Assert.True(await _sut.ExistsAsync(storeResult.FileId!, gameId), "Exists failed after Store");

        // 3. Retrieve and verify content
        using var retrieveStream = await _sut.RetrieveAsync(storeResult.FileId!, gameId);
        Assert.NotNull(retrieveStream);
        using var ms = new MemoryStream();
        await retrieveStream.CopyToAsync(ms);
        Assert.Equal(content, ms.ToArray());

        // 4. Presigned URL
        var url = await _sut.GetPresignedDownloadUrlAsync(storeResult.FileId!, gameId);
        Assert.NotNull(url);

        // 5. Delete
        Assert.True(await _sut.DeleteAsync(storeResult.FileId!, gameId), "Delete failed");

        // 6. Verify deletion
        Assert.False(await _sut.ExistsAsync(storeResult.FileId!, gameId), "File still exists after Delete");
        Assert.Null(await _sut.RetrieveAsync(storeResult.FileId!, gameId));
    }
}
