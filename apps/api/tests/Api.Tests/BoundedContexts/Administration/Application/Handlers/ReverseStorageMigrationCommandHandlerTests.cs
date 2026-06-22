using Amazon.S3;
using Amazon.S3.Model;
using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Entities;
using Api.Infrastructure;
using Api.Services.Pdf;
using Api.SharedKernel.Application.Services;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Defense-in-depth PII / info-leak guard for the per-row catch in
/// <c>ReverseStorageMigrationCommandHandler</c>. Mirrors the pattern enforced by
/// the BulkImportUsers / MigrateStorage / ImportRagData / ExportRagData /
/// EnqueueStorageMigration test suites (PRs #1532, #1543, #1545). The returned
/// <c>ReverseStorageMigrationResult.Errors</c> payload is exposed to admin API
/// callers and propagated to log shipping pipelines; <c>AmazonS3Exception.Message</c>
/// (raised by the underlying <c>CopyObjectAsync</c>/<c>DeleteObjectAsync</c> calls
/// in <c>ReverseSentRowAsync</c>) carries bucket names, region/endpoint info, and
/// internal infrastructure details.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class ReverseStorageMigrationCommandHandlerTests
{
    private const string TestBucket = "test-bucket";

    private static MeepleAiDbContext CreateInMemoryDb(string testName)
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"ReverseStorageTests_{testName}_{Guid.NewGuid()}")
            .Options;
        return new MeepleAiDbContext(
            options,
            Mock.Of<IMediator>(),
            Mock.Of<IDomainEventCollector>());
    }

    private static S3BlobStorageService CreateS3Storage(IAmazonS3 s3Client)
    {
        var options = new S3StorageOptions
        {
            Endpoint = "https://test.example.com",
            AccessKey = "AKIA-test-key",
            SecretKey = "test-secret",
            BucketName = TestBucket,
        };
        return new S3BlobStorageService(
            s3Client,
            options,
            Mock.Of<ILogger<S3BlobStorageService>>());
    }

    [Fact]
    public async Task Handle_WhenS3CopyObjectThrowsForSentRow_ShouldNotIncludeRawExceptionMessageInError()
    {
        // Arrange — seed one Sent outbox row; mock CopyObjectAsync (called by the
        // private ReverseSentRowAsync) to throw with a SECRET-MARKER message.
        const string secretInS3Message =
            "SECRET-MARKER bucket=internal-prod region=eu-west-1 RequestId=R3v3R5e123";

        var migrationId = Guid.NewGuid();
        await using var db = CreateInMemoryDb(nameof(Handle_WhenS3CopyObjectThrowsForSentRow_ShouldNotIncludeRawExceptionMessageInError));

        var row = new StorageOperationOutboxEntity
        {
            Id = Guid.NewGuid(),
            MigrationId = migrationId,
            LegacyKey = "pdf_uploads/game-abc/file-xyz_rules.pdf",
            NewKey = "pdfs/game-abc/file-xyz_rules.pdf",
            Category = nameof(BlobCategory.Pdf),
            ResourceKey = "game-abc",
            ScheduledAt = DateTime.UtcNow.AddMinutes(-10),
            CreatedAt = DateTime.UtcNow.AddMinutes(-10),
            Status = "Sent",
            SentAt = DateTime.UtcNow.AddMinutes(-5),
            AttemptCount = 1,
        };
        db.StorageOperationOutbox.Add(row);
        await db.SaveChangesAsync();

        var s3Mock = new Mock<IAmazonS3>();
        s3Mock
            .Setup(c => c.CopyObjectAsync(
                It.IsAny<CopyObjectRequest>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception(secretInS3Message)
            {
                ErrorCode = "AccessDenied",
            });

        var s3Storage = CreateS3Storage(s3Mock.Object);
        var handler = new ReverseStorageMigrationCommandHandler(
            db,
            s3Storage,
            Mock.Of<ILogger<ReverseStorageMigrationCommandHandler>>());

        var command = new ReverseStorageMigrationCommand(
            MigrationId: migrationId,
            DryRun: false);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Errors.Should().HaveCount(1,
            because: "the single Sent row triggers exactly one per-row reverse failure");

        result.Errors[0].Should().NotContain("SECRET-MARKER",
            because: "AmazonS3Exception.Message routinely carries bucket names, RequestId, " +
                     "region and other internal infrastructure data — the returned errors " +
                     "payload must not leak them to admin API callers or log shipping. " +
                     "Full diagnostic detail remains in ILogger.LogWarning output.");
    }
}
