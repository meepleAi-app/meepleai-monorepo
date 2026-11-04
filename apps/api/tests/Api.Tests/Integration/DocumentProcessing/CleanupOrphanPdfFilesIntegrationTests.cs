using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services.Pdf;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Api.Tests.Integration.DocumentProcessing;

/// <summary>
/// #4514 on Postgres and MinIO: a file a row references stays, an unreferenced one younger than the
/// threshold stays, a dry run deletes nothing, and the run deletes the unreferenced file once it is
/// old enough. The age is MinIO's own <c>LastModified</c>; the clock of the handler moves instead.
/// </summary>
/// <remarks>
/// The bucket is new for every test. The command sweeps all of <c>pdfs/</c>, and with
/// <c>TEST_S3_ENDPOINT</c> pointed at a shared MinIO, a shared bucket would hand it every other
/// suite's files, and the files of the local stack. MinIO is an L1 service: if it does not start,
/// the test fails (<see cref="L1Services.FailBecauseUnavailable"/>).
/// </remarks>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("Feature", "S3Storage")]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "4514")]
public sealed class CleanupOrphanPdfFilesIntegrationTests(SharedTestcontainersFixture fixture) : IAsyncLifetime
{
    private static readonly Guid TestUserId = new("A0000000-0000-0000-0000-000000004514");

    private readonly string _bucket = $"orphans-{Guid.NewGuid():N}";
    private string _databaseName = string.Empty;
    private ServiceProvider? _provider;
    private IContainer? _minio;
    private IAmazonS3? _s3Client;
    private S3BlobStorageService? _storage;
    private string? _minioFailure;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await StartMinioAsync();

        _databaseName = $"test_orphan_files_{Guid.NewGuid():N}";
        var connectionString = await fixture.CreateIsolatedDatabaseAsync(_databaseName);
        _provider = IntegrationServiceCollectionBuilder.CreateBase(connectionString).BuildServiceProvider();

        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        await TestMigrationHelper.MigrateWithRetryAsync(db, Ct);
        db.Set<UserEntity>().Add(new UserEntity
        {
            Id = TestUserId,
            Email = "orphan-files-4514@meepleai.test",
            PasswordHash = "x",
            DisplayName = "Orphan files #4514",
        });
        await db.SaveChangesAsync(Ct);
    }

    public async ValueTask DisposeAsync()
    {
        if (_provider is not null)
        {
            await _provider.DisposeAsync();
        }

        if (!string.IsNullOrEmpty(_databaseName))
        {
            try
            {
                await fixture.DropIsolatedDatabaseAsync(_databaseName);
            }
            catch
            {
                // The database is isolated anyway.
            }
        }

        await DeleteBucketAsync();
        _s3Client?.Dispose();
        if (_minio is not null)
        {
            await _minio.DisposeAsync();
        }
    }

    [Fact]
    public async Task ReferencedFileStays_YoungOrphanStays_DryRunDeletesNothing_OldOrphanIsDeleted()
    {
        RequireMinio();
        var referenced = await StoreAsync("game-4514", "rules.pdf");
        var orphan = await StoreAsync("game-4514", "rules.pdf");
        await AddPdfRowAsync(referenced);
        var uploadedAt = DateTimeOffset.UtcNow;

        // Just uploaded: the orphan is younger than the threshold, and stays.
        var young = await RunAsync(dryRun: false, now: uploadedAt);
        young.Referenced.Should().Be(1);
        young.TooRecent.Should().Be(1);
        young.Deleted.Should().Be(0);
        (await ListPdfKeysAsync()).Should().BeEquivalentTo([referenced, orphan]);

        // A day and an hour later, a dry run lists the orphan and deletes nothing.
        var later = uploadedAt + CleanupOrphanPdfFilesCommandHandler.MinimumAge + TimeSpan.FromHours(1);
        var dryRun = await RunAsync(dryRun: true, now: later);
        dryRun.OrphanKeys.Should().Equal(orphan);
        dryRun.Deleted.Should().Be(0);
        dryRun.Errors.Should().BeEmpty();
        (await ListPdfKeysAsync()).Should().BeEquivalentTo([referenced, orphan]);

        // The run deletes the orphan only.
        var run = await RunAsync(dryRun: false, now: later);
        run.Deleted.Should().Be(1);
        run.Errors.Should().BeEmpty();
        (await ListPdfKeysAsync()).Should().Equal(referenced);
    }

    private async Task<CleanupOrphanPdfFilesResult> RunAsync(bool dryRun, DateTimeOffset now)
    {
        using var scope = _provider!.CreateScope();
        var handler = new CleanupOrphanPdfFilesCommandHandler(
            scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>(),
            _storage!,
            new FakeTimeProvider(now),
            NullLogger<CleanupOrphanPdfFilesCommandHandler>.Instance);
        return await handler.Handle(new CleanupOrphanPdfFilesCommand(dryRun), Ct);
    }

    private async Task<string> StoreAsync(string resourceKey, string fileName)
    {
        using var content = new MemoryStream("%PDF-1.4 #4514"u8.ToArray());
        var result = await _storage!.StoreAsync(content, fileName, BlobCategory.Pdf, resourceKey, Ct);
        result.Success.Should().BeTrue(result.ErrorMessage);
        return result.FilePath!;
    }

    private async Task AddPdfRowAsync(string filePath)
    {
        using var scope = _provider!.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        db.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            FileName = "rules.pdf",
            FilePath = filePath,
            FileSizeBytes = 1,
            UploadedByUserId = TestUserId,
        });
        await db.SaveChangesAsync(Ct);
    }

    private async Task<IReadOnlyList<string>> ListPdfKeysAsync()
    {
        var page = await _s3Client!.ListObjectsV2Async(new ListObjectsV2Request { BucketName = _bucket, Prefix = "pdfs/" }, Ct);
        return (page.S3Objects ?? []).Select(o => o.Key).ToList();
    }

    private async Task StartMinioAsync()
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
            await _s3Client.PutBucketAsync(new PutBucketRequest { BucketName = _bucket }, Ct);

            _storage = new S3BlobStorageService(_s3Client, new S3StorageOptions
            {
                Endpoint = endpoint,
                AccessKey = accessKey,
                SecretKey = secretKey,
                BucketName = _bucket,
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

    private void RequireMinio()
    {
        if (_storage is null)
        {
            L1Services.FailBecauseUnavailable("minio", $"the MinIO container did not start, or TEST_S3_ENDPOINT does not answer: {_minioFailure}");
        }
    }

    private async Task DeleteBucketAsync()
    {
        if (_storage is null || _minio is not null)
        {
            return; // A container of our own goes away with its bucket.
        }

        try
        {
            foreach (var key in await ListPdfKeysAsync())
            {
                await _s3Client!.DeleteObjectAsync(_bucket, key);
            }

            await _s3Client!.DeleteBucketAsync(_bucket);
        }
        catch (AmazonS3Exception)
        {
            // Best effort on a shared MinIO.
        }
    }
}
