using Amazon.S3;
using Amazon.S3.Model;
using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Entities;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services.Pdf;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// #4514: the files under <c>pdfs/</c> that no row references are listed, and deleted only when
/// older than the threshold, only outside a dry run, and never when nothing at all is referenced.
/// S3 is a mock behind the real <see cref="S3BlobStorageService"/>; MinIO is in the integration test.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "4514")]
public sealed class CleanupOrphanPdfFilesCommandHandlerTests : IDisposable
{
    private const string Bucket = "test-bucket";
    private const string ReferencedKey = "pdfs/game-1/aaa_rules.pdf";
    private const string OrphanKey = "pdfs/game-1/bbb_rules.pdf";
    private const string RecentOrphanKey = "pdfs/wizard-temp/ccc_upload.pdf";

    private static readonly DateTime Now = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Old = Now.AddDays(-3);

    private readonly MeepleAiDbContext _db = TestDbContextFactory.CreateInMemoryDbContext($"orphan-pdf-files-{Guid.NewGuid()}");
    private readonly Mock<IAmazonS3> _s3 = new();
    private readonly List<string> _deletedKeys = [];

    public CleanupOrphanPdfFilesCommandHandlerTests()
    {
        _s3.Setup(c => c.DeleteObjectAsync(It.IsAny<DeleteObjectRequest>(), It.IsAny<CancellationToken>()))
            .Callback<DeleteObjectRequest, CancellationToken>((r, _) => _deletedKeys.Add(r.Key))
            .ReturnsAsync(new DeleteObjectResponse());
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task DryRun_ListsTheOldUnreferencedFiles_AndDeletesNothing()
    {
        await SeedPdfRowAsync(ReferencedKey);
        AnswerListing([Obj(ReferencedKey, Old), Obj(OrphanKey, Old), Obj(RecentOrphanKey, Now.AddHours(-1)), Obj("pdfs/game-1/", Old)]);

        var result = await Handler().Handle(new CleanupOrphanPdfFilesCommand(DryRun: true), TestContext.Current.CancellationToken);

        result.DryRun.Should().BeTrue();
        result.Scanned.Should().Be(3, "the folder marker is not a file");
        result.Referenced.Should().Be(1);
        result.TooRecent.Should().Be(1);
        result.OrphanKeys.Should().Equal(OrphanKey);
        result.Deleted.Should().Be(0);
        result.Errors.Should().BeEmpty();
        _deletedKeys.Should().BeEmpty();
    }

    [Fact]
    public async Task Run_DeletesOnlyTheOldUnreferencedFiles()
    {
        await SeedPdfRowAsync(ReferencedKey);
        AnswerListing([Obj(ReferencedKey, Old), Obj(OrphanKey, Old), Obj(RecentOrphanKey, Now.AddHours(-1))]);

        var result = await Handler().Handle(new CleanupOrphanPdfFilesCommand(DryRun: false), TestContext.Current.CancellationToken);

        result.Deleted.Should().Be(1);
        result.Errors.Should().BeEmpty();
        _deletedKeys.Should().Equal(OrphanKey);
    }

    [Fact]
    public async Task Run_KeepsBothKeysOfAStorageMoveStillInTheOutbox()
    {
        const string legacyKey = "pdfs/legacy/ddd_rules.pdf";
        const string newKey = "pdfs/game-2/ddd_rules.pdf";
        await SeedPdfRowAsync(ReferencedKey);
        _db.StorageOperationOutbox.Add(new StorageOperationOutboxEntity
        {
            Id = Guid.NewGuid(),
            MigrationId = Guid.NewGuid(),
            LegacyKey = legacyKey,
            NewKey = newKey,
            Category = nameof(BlobCategory.Pdf),
            ResourceKey = "game-2",
            ScheduledAt = Now,
            CreatedAt = Now,
        });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        AnswerListing([Obj(ReferencedKey, Old), Obj(legacyKey, Old), Obj(newKey, Old), Obj(OrphanKey, Old)]);

        var result = await Handler().Handle(new CleanupOrphanPdfFilesCommand(DryRun: false), TestContext.Current.CancellationToken);

        result.Referenced.Should().Be(3);
        _deletedKeys.Should().Equal(OrphanKey);
    }

    [Fact]
    public async Task Run_ReadsEveryPageOfTheListing()
    {
        await SeedPdfRowAsync(ReferencedKey);
        _s3.Setup(c => c.ListObjectsV2Async(
                It.Is<ListObjectsV2Request>(r => r.ContinuationToken == null), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListObjectsV2Response
            {
                S3Objects = [Obj(ReferencedKey, Old)],
                IsTruncated = true,
                NextContinuationToken = "page-2",
            });
        _s3.Setup(c => c.ListObjectsV2Async(
                It.Is<ListObjectsV2Request>(r => r.ContinuationToken == "page-2"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListObjectsV2Response { S3Objects = [Obj(OrphanKey, Old)], IsTruncated = false });

        var result = await Handler().Handle(new CleanupOrphanPdfFilesCommand(DryRun: false), TestContext.Current.CancellationToken);

        result.Scanned.Should().Be(2);
        _deletedKeys.Should().Equal(OrphanKey);
    }

    [Fact]
    public async Task Run_ListsUnderPdfsOnly_InTheConfiguredBucket()
    {
        await SeedPdfRowAsync(ReferencedKey);
        AnswerListing([Obj(ReferencedKey, Old)]);

        await Handler().Handle(new CleanupOrphanPdfFilesCommand(DryRun: true), TestContext.Current.CancellationToken);

        _s3.Verify(c => c.ListObjectsV2Async(
            It.Is<ListObjectsV2Request>(r => r.Prefix == "pdfs/" && r.BucketName == Bucket), It.IsAny<CancellationToken>()));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task NothingReferenced_RefusesToDelete(bool dryRun)
    {
        // Rows exist, but none points at what the bucket holds: the shape of a sweep against the
        // wrong database, which would otherwise empty the bucket.
        await SeedPdfRowAsync("pdfs/elsewhere/eee_rules.pdf");
        AnswerListing([Obj(ReferencedKey, Old), Obj(OrphanKey, Old)]);

        var result = await Handler().Handle(new CleanupOrphanPdfFilesCommand(dryRun), TestContext.Current.CancellationToken);

        result.OrphanKeys.Should().HaveCount(2);
        result.Deleted.Should().Be(0);
        result.Errors.Should().Equal(CleanupOrphanPdfFilesCommandHandler.NothingReferencedError);
        _deletedKeys.Should().BeEmpty();
    }

    [Fact]
    public async Task ListingFails_DeletesNothing_AndKeepsTheExceptionMessageOut()
    {
        const string secret = "SECRET-MARKER bucket=internal RequestId=ABC123";
        await SeedPdfRowAsync(ReferencedKey);
        _s3.Setup(c => c.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception(secret) { ErrorCode = "AccessDenied" });

        var result = await Handler().Handle(new CleanupOrphanPdfFilesCommand(DryRun: false), TestContext.Current.CancellationToken);

        result.Deleted.Should().Be(0);
        result.Errors.Should().ContainSingle().Which.Should().Contain("AccessDenied").And.NotContain("SECRET-MARKER");
        _deletedKeys.Should().BeEmpty();
    }

    [Fact]
    public async Task ADeleteThatFails_IsReported_AndTheOthersGoAhead()
    {
        const string secondOrphan = "pdfs/game-1/fff_rules.pdf";
        await SeedPdfRowAsync(ReferencedKey);
        AnswerListing([Obj(ReferencedKey, Old), Obj(OrphanKey, Old), Obj(secondOrphan, Old)]);
        _s3.Setup(c => c.DeleteObjectAsync(It.Is<DeleteObjectRequest>(r => r.Key == OrphanKey), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("boom") { ErrorCode = "InternalError" });

        var result = await Handler().Handle(new CleanupOrphanPdfFilesCommand(DryRun: false), TestContext.Current.CancellationToken);

        result.Deleted.Should().Be(1);
        result.Errors.Should().Equal($"Delete failed for {OrphanKey}");
        _deletedKeys.Should().Equal(secondOrphan);
    }

    [Fact]
    public async Task NotS3_ReportsTheProvider_AndDoesNothing()
    {
        var handler = new CleanupOrphanPdfFilesCommandHandler(
            _db,
            Mock.Of<IBlobStorageService>(),
            new FakeTimeProvider(Now),
            NullLogger<CleanupOrphanPdfFilesCommandHandler>.Instance);

        var result = await handler.Handle(new CleanupOrphanPdfFilesCommand(DryRun: false), TestContext.Current.CancellationToken);

        result.Errors.Should().Equal(CleanupOrphanPdfFilesCommandHandler.NotS3Error);
        result.Scanned.Should().Be(0);
    }

    private CleanupOrphanPdfFilesCommandHandler Handler() => new(
        _db,
        new S3BlobStorageService(
            _s3.Object,
            new S3StorageOptions
            {
                Endpoint = "https://s3.test.example",
                AccessKey = "test-access-key",
                SecretKey = "test-secret-key",
                BucketName = Bucket,
            },
            NullLogger<S3BlobStorageService>.Instance),
        new FakeTimeProvider(Now),
        NullLogger<CleanupOrphanPdfFilesCommandHandler>.Instance);

    private void AnswerListing(List<S3Object> objects) =>
        _s3.Setup(c => c.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListObjectsV2Response { S3Objects = objects, IsTruncated = false });

    private static S3Object Obj(string key, DateTime lastModified) => new() { Key = key, LastModified = lastModified };

    private async Task SeedPdfRowAsync(string filePath)
    {
        _db.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            FileName = "rules.pdf",
            FilePath = filePath,
            FileSizeBytes = 1,
            UploadedByUserId = Guid.NewGuid(),
        });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        _db.ChangeTracker.Clear();
    }
}
