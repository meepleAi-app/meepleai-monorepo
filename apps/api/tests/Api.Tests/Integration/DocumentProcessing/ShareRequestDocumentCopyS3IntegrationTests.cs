using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Api.Services.Pdf;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.Integration.DocumentProcessing;

/// <summary>
/// #5082 with the S3 storage (MinIO), where the old copy could not find the source. The tests are in
/// <see cref="ShareRequestDocumentCopyIntegrationTestsBase"/>.
/// </summary>
/// <remarks>
/// MinIO is an L1 service: if the container does not start the test fails, it does not skip
/// (<see cref="L1Services.FailBecauseUnavailable"/>).
/// </remarks>
[Trait("Category", TestCategories.Integration)]
[Trait("Feature", "S3Storage")]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "5082")]
public sealed class ShareRequestDocumentCopyS3IntegrationTests : ShareRequestDocumentCopyIntegrationTestsBase
{
    private IContainer? _minio;
    private IAmazonS3? _s3Client;
    private string? _minioFailure;

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
                // Already there.
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
            L1Services.FailBecauseUnavailable("minio", _minioFailure ?? "the storage did not start");
        }
    }
}
