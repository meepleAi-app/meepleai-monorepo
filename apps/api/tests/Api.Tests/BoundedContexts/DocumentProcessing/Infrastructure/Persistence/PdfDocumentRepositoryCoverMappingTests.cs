using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Application.Services;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Infrastructure.Persistence;

/// <summary>
/// Issue #3401 — <see cref="PdfDocumentRepository"/> must round-trip the L4 PDF
/// cover fields (CoverR2Key, CoverGenerationStatus, CoverPageIndex,
/// CoverGenerationError, CoverGenerationAttempts) through both MapToDomain and
/// MapToPersistence. Before the fix, both mappers silently dropped these columns,
/// so <c>UpdateAsync</c> (which rebuilds the entity and calls DbSet.Update, marking
/// every column modified) zeroed the persisted cover state — a data-loss bug that
/// broke <c>MaterializePdfCoverCommandHandler</c>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "DocumentProcessing")]
public sealed class PdfDocumentRepositoryCoverMappingTests : IDisposable
{
    private readonly MeepleAiDbContext _db;

    public PdfDocumentRepositoryCoverMappingTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"PdfDocRepoCover_{Guid.NewGuid()}")
            .Options;
        _db = new MeepleAiDbContext(
            options,
            new Mock<IMediator>().Object,
            new Mock<IDomainEventCollector>().Object);
    }

    public void Dispose() => _db.Dispose();

    private PdfDocumentRepository CreateRepository() =>
        new(_db, new Mock<IDomainEventCollector>().Object);

    private PdfDocumentEntity SeedEntity(
        string? coverR2Key,
        string coverStatus,
        int? coverPageIndex,
        string? coverError,
        int coverAttempts)
    {
        var pdf = new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            FileName = "rules.pdf",
            FilePath = "/tmp/rules.pdf",
            FileSizeBytes = 2048,
            ContentType = "application/pdf",
            UploadedByUserId = Guid.NewGuid(),
            UploadedAt = DateTime.UtcNow,
            ProcessingState = "Ready",
            CoverR2Key = coverR2Key,
            CoverGenerationStatus = coverStatus,
            CoverPageIndex = coverPageIndex,
            CoverGenerationError = coverError,
            CoverGenerationAttempts = coverAttempts,
        };
        _db.PdfDocuments.Add(pdf);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
        return pdf;
    }

    [Fact]
    public async Task GetByIdAsync_MapsAllCoverFieldsToDomain()
    {
        var seeded = SeedEntity(
            coverR2Key: "covers/pdf/00000000-0000-0000-0000-000000000001/cover",
            coverStatus: nameof(PdfCoverGenerationStatus.Generated),
            coverPageIndex: 2,
            coverError: null,
            coverAttempts: 1);

        var domain = await CreateRepository().GetByIdAsync(seeded.Id);

        domain.Should().NotBeNull();
        domain!.CoverR2Key.Should().Be(seeded.CoverR2Key);
        domain.CoverGenerationStatus.Should().Be(PdfCoverGenerationStatus.Generated);
        domain.CoverPageIndex.Should().Be(2);
        domain.CoverGenerationError.Should().BeNull();
        domain.CoverGenerationAttempts.Should().Be(1);
    }

    [Fact]
    public async Task UpdateAsync_PreservesAllCoverFields_RoundTrip()
    {
        // A Failed cover mid-retry: attempts must NOT be zeroed by the round-trip.
        var seeded = SeedEntity(
            coverR2Key: null,
            coverStatus: nameof(PdfCoverGenerationStatus.Failed),
            coverPageIndex: 4,
            coverError: "boom",
            coverAttempts: 2);

        var repo = CreateRepository();
        var domain = await repo.GetByIdAsync(seeded.Id);
        domain.Should().NotBeNull();

        // Simulate a metadata edit that goes through the repository's UpdateAsync
        // (the exact path MaterializePdfCoverCommandHandler uses).
        await repo.UpdateAsync(domain!);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var reloaded = await _db.PdfDocuments.AsNoTracking().SingleAsync(p => p.Id == seeded.Id);
        reloaded.CoverGenerationStatus.Should().Be(nameof(PdfCoverGenerationStatus.Failed),
            "the persisted cover status must survive an UpdateAsync round-trip");
        reloaded.CoverPageIndex.Should().Be(4);
        reloaded.CoverGenerationError.Should().Be("boom");
        reloaded.CoverGenerationAttempts.Should().Be(2,
            "the retry budget must not be silently zeroed by MapToPersistence");
    }

    [Fact]
    public async Task UpdateAsync_AfterMarkCoverGenerated_PersistsGeneratedState()
    {
        // The concrete MaterializePdfCoverCommandHandler flow: MarkCoverGenerated then UpdateAsync.
        var seeded = SeedEntity(
            coverR2Key: null,
            coverStatus: nameof(PdfCoverGenerationStatus.Pending),
            coverPageIndex: null,
            coverError: null,
            coverAttempts: 0);

        var repo = CreateRepository();
        var domain = await repo.GetByIdAsync(seeded.Id);
        domain!.MarkCoverGenerated("covers/pdf/aaaa/cover", pageIndex: 0);

        await repo.UpdateAsync(domain);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var reloaded = await _db.PdfDocuments.AsNoTracking().SingleAsync(p => p.Id == seeded.Id);
        reloaded.CoverGenerationStatus.Should().Be(nameof(PdfCoverGenerationStatus.Generated));
        reloaded.CoverR2Key.Should().Be("covers/pdf/aaaa/cover");
        reloaded.CoverPageIndex.Should().Be(0);
    }
}
