using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetRecentlyProcessedDocuments;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.DocumentProcessing;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Queries.GetRecentlyProcessedDocuments;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class GetRecentlyProcessedDocumentsQueryHandlerTests : IDisposable
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly GetRecentlyProcessedDocumentsQueryHandler _sut;

    public GetRecentlyProcessedDocumentsQueryHandlerTests()
    {
        _dbContext = CreateInMemoryContext();
        _sut = new GetRecentlyProcessedDocumentsQueryHandler(_dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task Handle_ReturnsDocumentsOrderedByTimestampDesc()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        SeedSharedGame(gameId, "Test Game");

        var oldPdfId = Guid.NewGuid();
        var newPdfId = Guid.NewGuid();
        var newestPdfId = Guid.NewGuid();

        SeedPdfDocument(oldPdfId, "old-rules.pdf", "Ready",
            processedAt: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        SeedPdfDocument(newPdfId, "new-rules.pdf", "Ready",
            processedAt: new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc));
        SeedPdfDocument(newestPdfId, "newest-rules.pdf", "Extracting",
            uploadedAt: new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc));

        SeedSharedGameDocument(gameId, oldPdfId);
        SeedSharedGameDocument(gameId, newPdfId);
        SeedSharedGameDocument(gameId, newestPdfId);

        await _dbContext.SaveChangesAsync();

        var query = new GetRecentlyProcessedDocumentsQuery(Limit: 2);

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        result[0].FileName.Should().Be("newest-rules.pdf");
        result[1].FileName.Should().Be("new-rules.pdf");
    }

    [Fact]
    public async Task Handle_RespectsLimit()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        SeedSharedGame(gameId, "Limit Game");

        for (var i = 0; i < 5; i++)
        {
            var pdfId = Guid.NewGuid();
            SeedPdfDocument(pdfId, $"doc-{i}.pdf", "Ready",
                processedAt: DateTime.UtcNow.AddMinutes(-i));
            SeedSharedGameDocument(gameId, pdfId);
        }

        await _dbContext.SaveChangesAsync();

        var query = new GetRecentlyProcessedDocumentsQuery(Limit: 3);

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task Handle_ExcludesDocumentsWithoutSharedGame()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        SeedSharedGame(gameId, "Linked Game");

        var linkedPdfId = Guid.NewGuid();
        var orphanPdfId = Guid.NewGuid();

        SeedPdfDocument(linkedPdfId, "linked.pdf", "Ready", processedAt: DateTime.UtcNow);
        SeedPdfDocument(orphanPdfId, "orphan.pdf", "Ready", processedAt: DateTime.UtcNow);

        // Only link the first one
        SeedSharedGameDocument(gameId, linkedPdfId);

        await _dbContext.SaveChangesAsync();

        var query = new GetRecentlyProcessedDocumentsQuery(Limit: 10);

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].PdfDocumentId.Should().Be(linkedPdfId);
    }

    [Fact]
    public async Task Handle_MapsFieldsCorrectly()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();
        var thumbUrl = "https://example.com/thumb.jpg";
        var processedAt = new DateTime(2026, 3, 15, 12, 0, 0, DateTimeKind.Utc);

        SeedSharedGame(gameId, "Catan", thumbnailUrl: thumbUrl);
        SeedPdfDocument(pdfId, "catan-rules.pdf", "Ready",
            processedAt: processedAt, errorCategory: null, retryCount: 0);
        SeedSharedGameDocument(gameId, pdfId);

        await _dbContext.SaveChangesAsync();

        var query = new GetRecentlyProcessedDocumentsQuery(Limit: 10);

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        var dto = result[0];
        dto.PdfDocumentId.Should().Be(pdfId);
        dto.FileName.Should().Be("catan-rules.pdf");
        dto.ProcessingState.Should().Be("Ready");
        dto.Timestamp.Should().Be(processedAt);
        dto.ErrorCategory.Should().BeNull();
        dto.CanRetry.Should().BeFalse();
        dto.SharedGameId.Should().Be(gameId);
        dto.GameName.Should().Be("Catan");
        dto.ThumbnailUrl.Should().Be(thumbUrl);
    }

    [Fact]
    public async Task Handle_FailedDocument_SetsCanRetryCorrectly()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        SeedSharedGame(gameId, "Failed Game");

        var retryablePdfId = Guid.NewGuid();
        var exhaustedPdfId = Guid.NewGuid();

        SeedPdfDocument(retryablePdfId, "retryable.pdf", "Failed",
            processedAt: DateTime.UtcNow, errorCategory: "Network", retryCount: 1);
        SeedPdfDocument(exhaustedPdfId, "exhausted.pdf", "Failed",
            processedAt: DateTime.UtcNow.AddMinutes(-1), errorCategory: "Parsing", retryCount: 3);

        SeedSharedGameDocument(gameId, retryablePdfId);
        SeedSharedGameDocument(gameId, exhaustedPdfId);

        await _dbContext.SaveChangesAsync();

        var query = new GetRecentlyProcessedDocumentsQuery(Limit: 10);

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);

        var retryable = result.First(d => d.PdfDocumentId == retryablePdfId);
        retryable.CanRetry.Should().BeTrue();
        retryable.ErrorCategory.Should().Be("Network");

        var exhausted = result.First(d => d.PdfDocumentId == exhaustedPdfId);
        exhausted.CanRetry.Should().BeFalse();
        exhausted.ErrorCategory.Should().Be("Parsing");
    }

    [Fact]
    public async Task Handle_IncludesJobIdWhenProcessingJobExists()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();
        var jobId = Guid.NewGuid();

        SeedSharedGame(gameId, "Job Game");
        SeedPdfDocument(pdfId, "with-job.pdf", "Extracting", uploadedAt: DateTime.UtcNow);
        SeedSharedGameDocument(gameId, pdfId);
        SeedProcessingJob(jobId, pdfId);

        await _dbContext.SaveChangesAsync();

        var query = new GetRecentlyProcessedDocumentsQuery(Limit: 10);

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].JobId.Should().Be(jobId);
    }

    [Fact]
    public async Task Handle_JobIdIsNullWhenNoProcessingJob()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();

        SeedSharedGame(gameId, "No Job Game");
        SeedPdfDocument(pdfId, "no-job.pdf", "Ready", processedAt: DateTime.UtcNow);
        SeedSharedGameDocument(gameId, pdfId);

        await _dbContext.SaveChangesAsync();

        var query = new GetRecentlyProcessedDocumentsQuery(Limit: 10);

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].JobId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ExcludesDeletedSharedGames()
    {
        // Arrange
        var activeGameId = Guid.NewGuid();
        var deletedGameId = Guid.NewGuid();

        SeedSharedGame(activeGameId, "Active Game");
        SeedSharedGame(deletedGameId, "Deleted Game", isDeleted: true);

        var activePdfId = Guid.NewGuid();
        var deletedPdfId = Guid.NewGuid();

        SeedPdfDocument(activePdfId, "active.pdf", "Ready", processedAt: DateTime.UtcNow);
        SeedPdfDocument(deletedPdfId, "deleted.pdf", "Ready", processedAt: DateTime.UtcNow);

        SeedSharedGameDocument(activeGameId, activePdfId);
        SeedSharedGameDocument(deletedGameId, deletedPdfId);

        await _dbContext.SaveChangesAsync();

        var query = new GetRecentlyProcessedDocumentsQuery(Limit: 10);

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].GameName.Should().Be("Active Game");
    }

    [Fact]
    public async Task Handle_UsesUploadedAtWhenProcessedAtIsNull()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();
        var uploadedAt = new DateTime(2026, 3, 10, 8, 0, 0, DateTimeKind.Utc);

        SeedSharedGame(gameId, "Pending Game");
        SeedPdfDocument(pdfId, "pending.pdf", "Extracting",
            uploadedAt: uploadedAt, processedAt: null);
        SeedSharedGameDocument(gameId, pdfId);

        await _dbContext.SaveChangesAsync();

        var query = new GetRecentlyProcessedDocumentsQuery(Limit: 10);

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].Timestamp.Should().Be(uploadedAt);
    }

    // ==========================================
    // Helper methods
    // ==========================================

    private static MeepleAiDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new MeepleAiDbContext(
            options,
            new Mock<IMediator>().Object,
            new Mock<IDomainEventCollector>().Object);
    }

    private void SeedSharedGame(
        Guid id,
        string title,
        string? thumbnailUrl = null,
        bool isDeleted = false)
    {
        _dbContext.SharedGames.Add(new SharedGameEntity
        {
            Id = id,
            Title = title,
            YearPublished = 2024,
            Description = "Test game",
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60,
            MinAge = 10,
            ImageUrl = "https://example.com/image.jpg",
            ThumbnailUrl = thumbnailUrl ?? "https://example.com/thumb.jpg",
            Status = 1, // Published
            CreatedBy = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            IsDeleted = isDeleted
        });
    }

    private void SeedPdfDocument(
        Guid id,
        string fileName,
        string processingState,
        DateTime? uploadedAt = null,
        DateTime? processedAt = null,
        string? errorCategory = null,
        int retryCount = 0)
    {
        _dbContext.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = id,
            FileName = fileName,
            FilePath = $"/uploads/{fileName}",
            FileSizeBytes = 1024,
            ContentType = "application/pdf",
            UploadedByUserId = Guid.NewGuid(),
            UploadedAt = uploadedAt ?? DateTime.UtcNow,
            ProcessingState = processingState,
            ProcessedAt = processedAt,
            ErrorCategory = errorCategory,
            RetryCount = retryCount,
            Language = "en"
        });
    }

    private void SeedSharedGameDocument(Guid sharedGameId, Guid pdfDocumentId)
    {
        _dbContext.SharedGameDocuments.Add(new SharedGameDocumentEntity
        {
            Id = Guid.NewGuid(),
            SharedGameId = sharedGameId,
            PdfDocumentId = pdfDocumentId,
            DocumentType = 0, // Rulebook
            Version = "1.0",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        });
    }

    private void SeedProcessingJob(Guid jobId, Guid pdfDocumentId)
    {
        _dbContext.ProcessingJobs.Add(new ProcessingJobEntity
        {
            Id = jobId,
            PdfDocumentId = pdfDocumentId,
            UserId = Guid.NewGuid(),
            Status = "Processing",
            Priority = 0,
            CreatedAt = DateTimeOffset.UtcNow
        });
    }
}
