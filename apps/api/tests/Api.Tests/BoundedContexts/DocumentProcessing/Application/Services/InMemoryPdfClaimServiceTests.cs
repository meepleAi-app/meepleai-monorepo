using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// Unit tests for <see cref="InMemoryPdfClaimService"/> — the test-only implementation
/// of <c>IPdfClaimService</c> backing tests that use the EF Core InMemory provider.
///
/// Issue #892: pins down the claim semantics so the production
/// <c>RelationalPdfClaimService</c> and this helper stay observably equivalent.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public sealed class InMemoryPdfClaimServiceTests : IDisposable
{
    private readonly MeepleAiDbContext _db;

    public InMemoryPdfClaimServiceTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(databaseName: $"InMemoryPdfClaimTest_{Guid.NewGuid()}")
            .Options;
        _db = new MeepleAiDbContext(
            options,
            new Mock<IMediator>().Object,
            new Mock<IDomainEventCollector>().Object);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task TryClaimPendingAsync_WhenPdfIsPending_ReturnsTrueAndTransitionsToExtracting()
    {
        // Arrange
        var pdfId = SeedPdf("Pending");
        var sut = new InMemoryPdfClaimService(_db);

        // Act
        var claimed = await sut.TryClaimPendingAsync(pdfId, CancellationToken.None);

        // Assert
        claimed.Should().BeTrue();
        var doc = await _db.PdfDocuments.FindAsync(pdfId);
        doc!.ProcessingState.Should().Be("Extracting");
    }

    [Fact]
    public async Task TryClaimPendingAsync_WhenPdfIsPendingWithStaleError_ClearsProcessingError()
    {
        // Arrange — PDF in Pending with a stale ProcessingError left over from a prior failed run
        var pdfId = SeedPdf("Pending", processingError: "stale failure from prior worker");
        var sut = new InMemoryPdfClaimService(_db);

        // Act
        var claimed = await sut.TryClaimPendingAsync(pdfId, CancellationToken.None);

        // Assert
        claimed.Should().BeTrue();
        var doc = await _db.PdfDocuments.FindAsync(pdfId);
        doc!.ProcessingState.Should().Be("Extracting");
        doc.ProcessingError.Should().BeNull();
    }

    [Theory]
    [InlineData("Uploading")]   // upload still in progress on another worker
    [InlineData("Extracting")]  // already claimed
    [InlineData("Chunking")]
    [InlineData("Embedding")]
    [InlineData("Indexing")]
    [InlineData("Ready")]       // terminal
    [InlineData("Failed")]      // terminal
    public async Task TryClaimPendingAsync_WhenPdfIsNotPending_ReturnsFalseAndDoesNotMutateState(string state)
    {
        // Arrange
        var pdfId = SeedPdf(state);
        var sut = new InMemoryPdfClaimService(_db);

        // Act
        var claimed = await sut.TryClaimPendingAsync(pdfId, CancellationToken.None);

        // Assert
        claimed.Should().BeFalse();
        var doc = await _db.PdfDocuments.FindAsync(pdfId);
        doc!.ProcessingState.Should().Be(state);
    }

    [Fact]
    public async Task TryClaimPendingAsync_WhenPdfDoesNotExist_ReturnsFalse()
    {
        // Arrange — no PDF seeded
        var sut = new InMemoryPdfClaimService(_db);

        // Act
        var claimed = await sut.TryClaimPendingAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        claimed.Should().BeFalse();
    }

    private Guid SeedPdf(string processingState, string? processingError = null)
    {
        var id = Guid.NewGuid();
        _db.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = id,
            FileName = "test.pdf",
            FilePath = "/tmp/test.pdf",
            FileSizeBytes = 1024,
            UploadedByUserId = Guid.NewGuid(),
            ProcessingState = processingState,
            ProcessingError = processingError
        });
        _db.SaveChanges();
        return id;
    }
}
