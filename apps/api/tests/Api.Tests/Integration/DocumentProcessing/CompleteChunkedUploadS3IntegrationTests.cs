using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Api.Services.Pdf;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.Integration.DocumentProcessing;

/// <summary>
/// #4442 con lo storage S3 (MinIO): il caso in cui la copia della pipeline rimossa finiva sempre
/// <c>Failed</c>. I test stanno in <see cref="CompleteChunkedUploadIntegrationTestsBase"/>.
/// </summary>
/// <remarks>
/// MinIO è un servizio L1: se il container non parte il test fallisce, non salta
/// (<see cref="L1Services.FailBecauseUnavailable"/>).
/// </remarks>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("Feature", "S3Storage")]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "4442")]
public sealed class CompleteChunkedUploadS3IntegrationTests(SharedTestcontainersFixture fixture)
    : CompleteChunkedUploadIntegrationTestsBase(fixture)
{
    private IContainer? _minio;
    private IAmazonS3? _s3Client;
    private string? _minioFailure;

    protected override string StorageName => "s3";

    protected override async Task StartStorageAsync()
    {
        try
        {
            var endpoint = Environment.GetEnvironmentVariable("TEST_S3_ENDPOINT");
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                _minio = new ContainerBuilder()
                    .WithImage(TestcontainersConfiguration.MinioImage)
                    .WithCreateParameterModifier(p => p.User = TestcontainersConfiguration.MinioContainerUser)
                    .WithPortBinding(TestcontainersConfiguration.MinioApiPort, true)
                    .WithEnvironment("MINIO_ROOT_USER", TestcontainersConfiguration.MinioRootUser)
                    .WithEnvironment("MINIO_ROOT_PASSWORD", TestcontainersConfiguration.MinioRootPassword)
                    .WithCommand("server", "/data")
                    .WithWaitStrategy(Wait.ForUnixContainer()
                        .UntilHttpRequestIsSucceeded(r => r
                            .ForPath("/minio/health/live")
                            .ForPort(TestcontainersConfiguration.MinioApiPort)
                            .ForStatusCode(System.Net.HttpStatusCode.OK)))
                    .WithCleanUp(true)
                    .Build();
                await _minio.StartAsync(Ct);
                endpoint = $"http://localhost:{_minio.GetMappedPublicPort(TestcontainersConfiguration.MinioApiPort)}";
            }

            var accessKey = Environment.GetEnvironmentVariable("TEST_S3_ACCESS_KEY") ?? TestcontainersConfiguration.MinioRootUser;
            var secretKey = Environment.GetEnvironmentVariable("TEST_S3_SECRET_KEY") ?? TestcontainersConfiguration.MinioRootPassword;
            _s3Client = new AmazonS3Client(new BasicAWSCredentials(accessKey, secretKey), new AmazonS3Config
            {
                ServiceURL = endpoint,
                ForcePathStyle = true,
                AuthenticationRegion = "us-east-1",
            });
            try
            {
                await _s3Client.PutBucketAsync(new PutBucketRequest { BucketName = TestcontainersConfiguration.MinioTestBucket }, Ct);
            }
            catch (AmazonS3Exception ex) when (ex.ErrorCode is "BucketAlreadyOwnedByYou" or "BucketAlreadyExists")
            {
                // Già presente.
            }

            Storage = new S3BlobStorageService(_s3Client, new S3StorageOptions
            {
                Endpoint = endpoint,
                AccessKey = accessKey,
                SecretKey = secretKey,
                BucketName = TestcontainersConfiguration.MinioTestBucket,
                Region = "us-east-1",
                PresignedUrlExpirySeconds = 3600,
                EnableEncryption = false,
                ForcePathStyle = true,
            }, NullLogger<S3BlobStorageService>.Instance);
        }
        catch (Exception ex)
        {
            _minioFailure = ex.Message;
        }
    }

    protected override async ValueTask DisposeStorageAsync()
    {
        _s3Client?.Dispose();
        if (_minio is not null)
        {
            await _minio.DisposeAsync();
        }
    }

    protected override void RequireStorage()
    {
        if (Storage is null)
        {
            L1Services.FailBecauseUnavailable("minio", $"il container MinIO non e partito oppure TEST_S3_ENDPOINT non risponde: {_minioFailure}");
        }
    }

    protected override void AssertFilePathShape(string filePath) =>
        Path.IsPathRooted(filePath).Should().BeFalse("con S3 il FilePath è la chiave dell'oggetto, non un percorso su disco");

    protected override async Task<IReadOnlyList<string>> ListStoredPdfsAsync()
    {
        var objects = await _s3Client!.ListObjectsV2Async(new ListObjectsV2Request
        {
            BucketName = TestcontainersConfiguration.MinioTestBucket,
            Prefix = $"pdfs/{TestSharedGameId}/",
        }, Ct);
        return (objects.S3Objects ?? []).Select(o => o.Key).ToList();
    }
}
