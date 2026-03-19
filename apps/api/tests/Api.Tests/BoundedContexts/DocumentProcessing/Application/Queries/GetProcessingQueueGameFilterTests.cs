using Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue;
using Api.BoundedContexts.DocumentProcessing.Application.Queries.Queue;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.DocumentProcessing;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Queries;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public sealed class GetProcessingQueueGameFilterTests : IDisposable
{
    private readonly Api.Infrastructure.MeepleAiDbContext _dbContext;
    private readonly GetProcessingQueueQueryHandler _handler;

    // Shared test data
    private readonly Guid _gameId = Guid.NewGuid();
    private readonly Guid _otherGameId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public GetProcessingQueueGameFilterTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        _handler = new GetProcessingQueueQueryHandler(_dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task Handle_WithGameId_ReturnsOnlyJobsForThatGame()
    {
        // Arrange — handler filters by PdfDocument.SharedGameId directly (not junction table)
        var pdfForGame = CreatePdfDocument("game-rules.pdf");
        pdfForGame.SharedGameId = _gameId;
        var pdfForOtherGame = CreatePdfDocument("other-rules.pdf");
        pdfForOtherGame.SharedGameId = _otherGameId;
        var pdfUnlinked = CreatePdfDocument("unlinked.pdf");

        _dbContext.PdfDocuments.AddRange(pdfForGame, pdfForOtherGame, pdfUnlinked);

        var jobForGame = CreateProcessingJob(pdfForGame.Id);
        var jobForOtherGame = CreateProcessingJob(pdfForOtherGame.Id);
        var jobUnlinked = CreateProcessingJob(pdfUnlinked.Id);

        _dbContext.ProcessingJobs.AddRange(jobForGame, jobForOtherGame, jobUnlinked);

        await _dbContext.SaveChangesAsync();

        var query = new GetProcessingQueueQuery(GameId: _gameId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Jobs.Should().HaveCount(1);
        result.Jobs[0].PdfDocumentId.Should().Be(pdfForGame.Id);
        result.Total.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WithoutGameId_ReturnsAllJobs()
    {
        // Arrange
        var pdf1 = CreatePdfDocument("rules-1.pdf");
        pdf1.SharedGameId = _gameId;
        var pdf2 = CreatePdfDocument("rules-2.pdf");
        var pdf3 = CreatePdfDocument("rules-3.pdf");

        _dbContext.PdfDocuments.AddRange(pdf1, pdf2, pdf3);

        _dbContext.ProcessingJobs.AddRange(
            CreateProcessingJob(pdf1.Id),
            CreateProcessingJob(pdf2.Id),
            CreateProcessingJob(pdf3.Id));

        await _dbContext.SaveChangesAsync();

        var query = new GetProcessingQueueQuery(); // No GameId

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Jobs.Should().HaveCount(3);
        result.Total.Should().Be(3);
    }

    [Fact]
    public async Task Handle_WithGameId_NoMatchingDocuments_ReturnsEmpty()
    {
        // Arrange — link to a different game via SharedGameId
        var pdf = CreatePdfDocument("some-rules.pdf");
        pdf.SharedGameId = _otherGameId;
        _dbContext.PdfDocuments.Add(pdf);
        _dbContext.ProcessingJobs.Add(CreateProcessingJob(pdf.Id));

        await _dbContext.SaveChangesAsync();

        var query = new GetProcessingQueueQuery(GameId: _gameId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Jobs.Should().BeEmpty();
        result.Total.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithGameIdAndStatusFilter_CombinesFilters()
    {
        // Arrange — handler filters by PdfDocument.SharedGameId directly
        var pdfQueued = CreatePdfDocument("queued-rules.pdf");
        pdfQueued.SharedGameId = _gameId;
        var pdfFailed = CreatePdfDocument("failed-rules.pdf");
        pdfFailed.SharedGameId = _gameId;

        _dbContext.PdfDocuments.AddRange(pdfQueued, pdfFailed);

        var jobQueued = CreateProcessingJob(pdfQueued.Id, status: "Queued");
        var jobFailed = CreateProcessingJob(pdfFailed.Id, status: "Failed");

        _dbContext.ProcessingJobs.AddRange(jobQueued, jobFailed);

        await _dbContext.SaveChangesAsync();

        var query = new GetProcessingQueueQuery(StatusFilter: "Queued", GameId: _gameId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Jobs.Should().HaveCount(1);
        result.Jobs[0].PdfDocumentId.Should().Be(pdfQueued.Id);
    }

    private PdfDocumentEntity CreatePdfDocument(string fileName)
    {
        return new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            FileName = fileName,
            FilePath = $"/uploads/{fileName}",
            FileSizeBytes = 1024,
            ContentType = "application/pdf",
            UploadedByUserId = _userId,
            UploadedAt = DateTime.UtcNow
        };
    }

    private ProcessingJobEntity CreateProcessingJob(Guid pdfDocumentId, string status = "Queued")
    {
        return new ProcessingJobEntity
        {
            Id = Guid.NewGuid(),
            PdfDocumentId = pdfDocumentId,
            UserId = _userId,
            Status = status,
            Priority = 0,
            CreatedAt = DateTimeOffset.UtcNow,
            RetryCount = 0,
            MaxRetries = 3
        };
    }
}
