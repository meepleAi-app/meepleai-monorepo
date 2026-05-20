using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using Api.Services.Pdf;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.Unit.Services;

/// <summary>
/// Unit tests for <see cref="StorageOperationOutboxService"/> (issue #1314 PR 2).
/// Verifies enqueue persistence, idempotency on duplicate LegacyKey, and the
/// LegacyKey → NewKey derivation rule.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Issue", "1314")]
public sealed class StorageOperationOutboxServiceTests
{
    [Fact]
    public async Task EnqueueAsync_ValidInputs_PersistsRowWithDerivedNewKey()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var sut = new StorageOperationOutboxService(db, TimeProvider.System, NullLogger<StorageOperationOutboxService>.Instance);

        var migrationId = Guid.NewGuid();
        var inserted = await sut.EnqueueAsync(
            migrationId,
            legacyKey: "pdf_uploads/game-abc/file123_rulebook.pdf",
            category: BlobCategory.Pdf,
            resourceKey: "game-abc");

        inserted.Should().BeTrue();

        var row = await db.StorageOperationOutbox.SingleAsync();
        row.MigrationId.Should().Be(migrationId);
        row.LegacyKey.Should().Be("pdf_uploads/game-abc/file123_rulebook.pdf");
        row.NewKey.Should().Be("pdfs/game-abc/file123_rulebook.pdf");
        row.Category.Should().Be("Pdf");
        row.ResourceKey.Should().Be("game-abc");
        row.Status.Should().Be("Pending");
        row.AttemptCount.Should().Be(0);
        row.SentAt.Should().BeNull();
    }

    [Theory]
    [InlineData(BlobCategory.Pdf, "pdfs")]
    [InlineData(BlobCategory.SessionPhoto, "session-photos")]
    [InlineData(BlobCategory.GameImage, "game-images")]
    [InlineData(BlobCategory.VisionSnapshot, "vision-snapshots")]
    [InlineData(BlobCategory.GamebookPhoto, "gamebook-photos")]
    [InlineData(BlobCategory.PhotoBatch, "photo-batches")]
    public async Task EnqueueAsync_DerivesNewKey_FromCategoryPrefix(BlobCategory category, string expectedPrefix)
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var sut = new StorageOperationOutboxService(db, TimeProvider.System, NullLogger<StorageOperationOutboxService>.Instance);

        await sut.EnqueueAsync(
            Guid.NewGuid(),
            legacyKey: "pdf_uploads/res-1/abc_file.bin",
            category: category,
            resourceKey: "res-1");

        var row = await db.StorageOperationOutbox.SingleAsync();
        row.NewKey.Should().StartWith($"{expectedPrefix}/");
        row.NewKey.Should().EndWith("/res-1/abc_file.bin");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EnqueueAsync_InvalidLegacyKey_ThrowsArgumentException(string? legacyKey)
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var sut = new StorageOperationOutboxService(db, TimeProvider.System, NullLogger<StorageOperationOutboxService>.Instance);

        var act = async () => await sut.EnqueueAsync(Guid.NewGuid(), legacyKey!, BlobCategory.Pdf, "res-1");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EnqueueAsync_InvalidResourceKey_ThrowsArgumentException(string? resourceKey)
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var sut = new StorageOperationOutboxService(db, TimeProvider.System, NullLogger<StorageOperationOutboxService>.Instance);

        var act = async () => await sut.EnqueueAsync(Guid.NewGuid(), "pdf_uploads/x/y_z.pdf", BlobCategory.Pdf, resourceKey!);

        await act.Should().ThrowAsync<ArgumentException>();
    }
}
