using System.IO;
using System.Threading.Tasks;
using Api.DevTools.MockImpls;
using Api.Services.Pdf;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Unit.Services;

/// <summary>
/// Smoke test for issue #1314 PR 1 (signature refactor).
///
/// PR 1 introduces <see cref="BlobCategory"/> + <c>resourceKey</c> into the
/// <see cref="IBlobStorageService"/> contract while preserving the legacy
/// <c>pdf_uploads/{resourceKey}/{fileId}_{filename}</c> S3 key layout
/// (PR 2 will wire <see cref="BlobCategoryExtensions.ToS3Folder"/> into key
/// construction behind the <c>STORAGE_WRITE_MODE</c> feature flag).
///
/// What THIS test guards:
/// - <see cref="BlobCategoryExtensions.ToS3Folder"/> canonical prefix mapping
///   (the contract that PR 2's migration runbook + outbox layer will consume).
///   Any rename of these strings before PR 2 ships breaks the migration plan.
/// - <see cref="MockBlobStorageService"/> path format is category-agnostic
///   in PR 1 — guards against an accidental category-dependent path mutation
///   in the mock that would silently break test fixtures relying on path
///   stability.
///
/// What this test does NOT guard (covered elsewhere):
/// - <see cref="S3BlobStorageService.GetS3Key"/> byte-identity of the actual
///   S3 key produced pre/post PR 1 → covered by
///   <c>S3BlobStorageIntegrationTests</c> + <c>S3BlobStorageServiceTests</c>
///   (Testcontainers MinIO + mocked AmazonS3 client respectively). The
///   private <c>GetS3Key</c> method is exercised end-to-end by those suites
///   via Store/Retrieve/Delete round-trips; not reachable from a pure-unit
///   smoke test without reflection.
/// - <see cref="BlobStorageService"/> local filesystem path byte-identity →
///   no dedicated unit suite today (gap acknowledged for PR 2 test matrix
///   item "Integration steady-state").
/// </summary>
[Trait("Category", "Unit")]
[Trait("Issue", "1314")]
public sealed class BlobCategorySignatureSmokeTest
{
    [Theory]
    [InlineData(BlobCategory.Pdf, "pdfs")]
    [InlineData(BlobCategory.SessionPhoto, "session-photos")]
    [InlineData(BlobCategory.GameImage, "game-images")]
    [InlineData(BlobCategory.VisionSnapshot, "vision-snapshots")]
    [InlineData(BlobCategory.GamebookPhoto, "gamebook-photos")]
    [InlineData(BlobCategory.PhotoBatch, "photo-batches")]
    public void ToS3Folder_ReturnsCanonicalPrefix(BlobCategory category, string expectedPrefix)
    {
        // PR 2 contract guard: the canonical S3 folder mapping must remain stable
        // because the migration runbook (#1314 PR 2 Phase 1-3) targets these exact
        // string values when rebucket-ing legacy objects.
        category.ToS3Folder().Should().Be(expectedPrefix);
    }

    [Fact]
    public async Task MockBlobStorage_StoreAsync_PathShape_IsCategoryAgnostic_InPR1()
    {
        // PR 1 behavior invariant: MockBlobStorageService.StoreAsync must produce
        // the same FilePath shape `mock://{resourceKey}/{fileId}/{fileName}`
        // regardless of category — category is discarded in PR 1.
        // This guards against an accidental category-dependent path mutation
        // that would silently break tests relying on path stability.

        var svc = new MockBlobStorageService();
        var content = new byte[] { 0x42 };

        var categories = new[]
        {
            BlobCategory.Pdf,
            BlobCategory.SessionPhoto,
            BlobCategory.GameImage,
            BlobCategory.VisionSnapshot,
            BlobCategory.GamebookPhoto,
            BlobCategory.PhotoBatch,
        };

        var paths = new System.Collections.Generic.List<(BlobCategory category, string filePath)>();
        foreach (var category in categories)
        {
            // Upload twice per category (10 total uploads = smoke set spec)
            for (int i = 0; i < 2; i++)
            {
                using var ms = new MemoryStream(content);
                var result = await svc.StoreAsync(
                    ms,
                    fileName: $"sample-{i}.bin",
                    category: category,
                    resourceKey: "fixedResourceKey",
                    ct: default);

                result.Success.Should().BeTrue();
                result.FilePath.Should().NotBeNullOrEmpty();
                paths.Add((category, result.FilePath!));
            }
        }

        // Invariant: every FilePath must start with the same fixed prefix and
        // share the same shape — the only varying segment is the random fileId.
        foreach (var (category, filePath) in paths)
        {
            filePath.Should().StartWith(
                "mock://fixedResourceKey/MOCK-",
                because: $"category {category} should NOT leak into the path in PR 1 (behavior preservation)");
            filePath.Should().EndWith(".bin");
        }
    }

    [Theory]
    [InlineData(BlobCategory.Pdf)]
    [InlineData(BlobCategory.SessionPhoto)]
    [InlineData(BlobCategory.GameImage)]
    [InlineData(BlobCategory.VisionSnapshot)]
    [InlineData(BlobCategory.GamebookPhoto)]
    [InlineData(BlobCategory.PhotoBatch)]
    public void MockBlobStorage_GetStoragePath_IsCategoryAgnostic_InPR1(BlobCategory category)
    {
        // Companion guard: GetStoragePath must also be category-agnostic in PR 1.
        var svc = new MockBlobStorageService();

        var path = svc.GetStoragePath(
            fileId: "stableFileId",
            category: category,
            resourceKey: "stableResourceKey",
            fileName: "doc.pdf");

        path.Should().Be("mock://stableResourceKey/stableFileId/doc.pdf",
            because: "MockBlobStorageService discards category in PR 1 — path depends only on resourceKey + fileId + fileName");
    }
}
