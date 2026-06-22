using Amazon.S3;
using Amazon.S3.Model;
using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.Services.Pdf;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Defense-in-depth PII / info-leak guards for EnqueueStorageMigrationCommandHandler.
/// Mirrors the pattern enforced by the BulkImportUsers / MigrateStorage / ImportRagData
/// / ExportRagData test suites (PRs #1532, #1543, #1545). The returned
/// <c>EnqueueStorageMigrationResult.Errors</c> payload reaches admin API callers and
/// log shipping pipelines; raw <c>AmazonS3Exception.Message</c> in particular routinely
/// carries bucket names, region/endpoint info, request IDs, and presigned-URL fragments.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class EnqueueStorageMigrationCommandHandlerTests
{
    private const string TestBucket = "test-bucket";

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

    /// <summary>
    /// Guard for the catch around <c>s3Client.ListObjectsV2Async</c> (catch on
    /// <see cref="AmazonS3Exception"/>). Pre-fix the per-line error embedded
    /// both <c>ex.ErrorCode</c> and <c>ex.Message</c> — AmazonS3Exception
    /// messages routinely include bucket names + RequestId + service endpoint.
    /// </summary>
    [Fact]
    public async Task Handle_WhenS3ListObjectsThrows_ShouldNotIncludeRawExceptionMessageInError()
    {
        // Arrange
        const string secretInS3Message =
            "SECRET-MARKER bucket=internal-prod region=us-east-1 RequestId=ABC123";

        var s3Mock = new Mock<IAmazonS3>();
        s3Mock
            .Setup(c => c.ListObjectsV2Async(
                It.IsAny<ListObjectsV2Request>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception(secretInS3Message)
            {
                ErrorCode = "SignatureDoesNotMatch",
            });

        var s3Storage = CreateS3Storage(s3Mock.Object);
        var handler = new EnqueueStorageMigrationCommandHandler(
            s3Storage,
            Mock.Of<IStorageOperationOutboxService>(),
            Mock.Of<ILogger<EnqueueStorageMigrationCommandHandler>>());

        var command = new EnqueueStorageMigrationCommand(
            MigrationId: Guid.NewGuid(),
            LegacyPrefix: "pdf_uploads/",
            Category: BlobCategory.Pdf,
            DryRun: false);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Errors.Should().HaveCount(1,
            because: "the S3 list call throws once and the handler then breaks the loop");

        result.Errors[0].Should().NotContain("SECRET-MARKER",
            because: "AmazonS3Exception.Message carries bucket names, RequestId, region " +
                     "and other internal infrastructure data — embedding it in the returned " +
                     "errors payload leaks to admin API callers and log shipping pipelines. " +
                     "Full diagnostic detail remains in the ILogger.LogError output.");
    }

    /// <summary>
    /// Guard for the catch around <c>_outbox.EnqueueAsync</c>. Pre-fix embedded
    /// <c>ex.Message</c> for each per-object enqueue failure — EF Core / npgsql
    /// exceptions carry SQL fragments and parameter values.
    /// </summary>
    [Fact]
    public async Task Handle_WhenOutboxEnqueueThrows_ShouldNotIncludeRawExceptionMessageInError()
    {
        // Arrange — S3 list returns one valid object whose key parses into a
        // resourceKey; the outbox enqueue then throws with a SECRET-MARKER message.
        const string secretInDbMessage =
            "SECRET-MARKER duplicate key value violates unique constraint";

        var s3Mock = new Mock<IAmazonS3>();
        s3Mock
            .Setup(c => c.ListObjectsV2Async(
                It.IsAny<ListObjectsV2Request>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListObjectsV2Response
            {
                IsTruncated = false,
                S3Objects = new List<S3Object>
                {
                    new()
                    {
                        Key = "pdf_uploads/game-abc/file-xyz_rules.pdf",
                        BucketName = TestBucket,
                    },
                },
                NextContinuationToken = null,
            });

        var outboxMock = new Mock<IStorageOperationOutboxService>();
        outboxMock
            .Setup(o => o.EnqueueAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<BlobCategory>(),
                It.IsAny<string>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(secretInDbMessage));

        var s3Storage = CreateS3Storage(s3Mock.Object);
        var handler = new EnqueueStorageMigrationCommandHandler(
            s3Storage,
            outboxMock.Object,
            Mock.Of<ILogger<EnqueueStorageMigrationCommandHandler>>());

        var command = new EnqueueStorageMigrationCommand(
            MigrationId: Guid.NewGuid(),
            LegacyPrefix: "pdf_uploads/",
            Category: BlobCategory.Pdf,
            DryRun: false);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Errors.Should().HaveCount(1,
            because: "the single S3 object triggered one enqueue failure");

        result.Errors[0].Should().NotContain("SECRET-MARKER",
            because: "EF Core / npgsql exception messages embed SQL fragments and parameter " +
                     "values; the returned Errors payload must not leak them. The full " +
                     "exception is still captured server-side by ILogger.LogWarning.");
    }
}
