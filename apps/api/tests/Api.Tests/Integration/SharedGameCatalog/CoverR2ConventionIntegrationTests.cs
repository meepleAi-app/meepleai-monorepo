using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;
using Api.Services.Pdf;
using Api.Tests.Infrastructure;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.Integration.SharedGameCatalog;

/// <summary>
/// Issue #2947 — end-to-end round-trip proving the deterministic R2 cover-key
/// convention actually resolves through a real S3-compatible store: write via
/// each dedicated upload pipeline (raw <see cref="IAmazonS3.PutObjectAsync"/>
/// against a deterministic key), then resolve via
/// <see cref="S3BlobStorageService.GetPresignedUrlForRawKeyAsync"/> exactly as
/// <c>CoverUrlResolver</c> does, and HTTP-GET the presigned URL to confirm the
/// bytes round-trip.
/// </summary>
/// <remarks>
/// Uses the same standalone MinIO Testcontainer + skip convention as
/// <see cref="Api.Tests.Integration.DocumentProcessing.S3BlobStorageIntegrationTests"/>
/// (harness copied verbatim per Issue #2947 Task 6 Step 1). MinIO-over-HTTP does
/// not support <c>DisablePayloadSigning</c>, so these tests are expected to skip
/// locally and only run where MinIO/R2-HTTPS is genuinely reachable (CI gated
/// lane / staging). See CLAUDE.md "Known Flaky Tests" — this is intentional, not
/// a regression.
/// </remarks>
[Trait("Category", "Integration")]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class CoverR2ConventionIntegrationTests : IAsyncLifetime
{
    private IContainer? _minioContainer;
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

            // Quick smoke test to verify S3 connectivity (mirrors S3BlobStorageIntegrationTests).
            var probeLogger = new Mock<Microsoft.Extensions.Logging.ILogger<S3BlobStorageService>>().Object;
            var probeService = new S3BlobStorageService(_s3Client, _options, probeLogger);
            using var probe = new MemoryStream("probe"u8.ToArray());
            var probeResult = await probeService.StoreAsync(probe, "probe.txt", BlobCategory.Pdf, "healthcheck");
            if (!probeResult.Success)
            {
                Console.WriteLine($"S3 connectivity probe failed: {probeResult.ErrorMessage}. Tests will be skipped.");
                _skipTests = true;
                return;
            }
            await probeService.DeleteAsync(probeResult.FileId!, BlobCategory.Pdf, "healthcheck");
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

    private S3BlobStorageService BuildBlobService()
        => new(_s3Client, _options, new Mock<Microsoft.Extensions.Logging.ILogger<S3BlobStorageService>>().Object);

    [Fact]
    public async Task BggCover_Uploaded_ResolvesViaRawKeyNoSuffix_And200()
    {
        SkipIfNotAvailable();

        var bggPipeline = new BggCoverUploadPipeline(_s3Client, _options, NullLogger<BggCoverUploadPipeline>.Instance);
        var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A };

        var dbKey = await bggPipeline.UploadAsync(13, bytes, ".jpg", CancellationToken.None);
        dbKey.Should().Be("bgg-covers/13/cover.jpg");

        var blob = BuildBlobService();
        var url = await blob.GetPresignedUrlForRawKeyAsync(dbKey);
        url.Should().NotBeNull();

        using var http = new HttpClient();
        using var resp = await http.GetAsync(url);
        resp.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        (await resp.Content.ReadAsByteArrayAsync()).Should().BeEquivalentTo(bytes);
    }

    [Fact]
    public async Task PdfCover_Uploaded_ResolvesViaPreviewSuffix_And200()
    {
        SkipIfNotAvailable();

        var pdfPipeline = new PdfCoverUploadPipeline(_s3Client, _options, NullLogger<PdfCoverUploadPipeline>.Instance);
        var id = Guid.NewGuid();
        var dbKey = $"covers/pdf/{id:D}/cover";
        var bytes = new byte[] { 0x52, 0x49, 0x46, 0x46 };

        var returned = await pdfPipeline.UploadAsync(dbKey, bytes, CancellationToken.None);
        returned.Should().Be(dbKey);

        var blob = BuildBlobService();
        var url = await blob.GetPresignedUrlForRawKeyAsync($"{dbKey}-preview.webp");
        url.Should().NotBeNull();

        using var http = new HttpClient();
        using var resp = await http.GetAsync(url);
        resp.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        (await resp.Content.ReadAsByteArrayAsync()).Should().BeEquivalentTo(bytes);
    }

    [Fact]
    public async Task MissingKey_ResolvesToNull_FailClosed()
    {
        SkipIfNotAvailable();

        var blob = BuildBlobService();
        var url = await blob.GetPresignedUrlForRawKeyAsync("bgg-covers/999/cover.jpg");
        url.Should().BeNull("fail-closed existence check must return null for a non-existent object");
    }
}
