using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using Api.Models;
using ProcessingStep = Api.Models.ProcessingStep;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.Unit.DocumentProcessing;

/// <summary>
/// Unit tests for StreamPdfProgressQueryHandler.
/// Issue #4209: SSE Progress Stream for Public PDFs.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "DocumentProcessing")]
public sealed class StreamPdfProgressQueryHandlerTests
{
    private readonly Mock<IPdfDocumentRepository> _mockPdfRepository;
    private readonly Mock<IPdfProgressStreamService> _mockProgressService;
    private readonly StreamPdfProgressQueryHandler _handler;

    private static readonly Guid TestPdfId = Guid.NewGuid();
    private static readonly Guid TestUserId = Guid.NewGuid();
    private static readonly Guid OtherUserId = Guid.NewGuid();

    public StreamPdfProgressQueryHandlerTests()
    {
        _mockPdfRepository = new Mock<IPdfDocumentRepository>();
        _mockProgressService = new Mock<IPdfProgressStreamService>();

        _handler = new StreamPdfProgressQueryHandler(
            _mockPdfRepository.Object,
            _mockProgressService.Object,
            NullLogger<StreamPdfProgressQueryHandler>.Instance
        );
    }

    [Fact]
    public async Task Handle_PdfNotFound_YieldsNothing()
    {
        // Arrange
        var query = new StreamPdfProgressQuery(TestPdfId, TestUserId, IsAdmin: false);
        _mockPdfRepository.Setup(r => r.GetByIdAsync(TestPdfId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PdfDocument?)null);

        // Act
        var results = new List<ProcessingProgressJson>();
        await foreach (var progress in _handler.Handle(query, CancellationToken.None))
        {
            results.Add(progress);
        }

        // Assert
        Assert.Empty(results);
        _mockPdfRepository.Verify(r => r.GetByIdAsync(TestPdfId, It.IsAny<CancellationToken>()), Times.Once);
        _mockProgressService.Verify(s => s.SubscribeToProgress(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_NonOwnerNonAdmin_YieldsNothing()
    {
        // Arrange
        var query = new StreamPdfProgressQuery(TestPdfId, OtherUserId, IsAdmin: false);
        var pdf = CreateTestPdf(ownerId: TestUserId); // Different owner

        _mockPdfRepository.Setup(r => r.GetByIdAsync(TestPdfId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pdf);

        // Act
        var results = new List<ProcessingProgressJson>();
        await foreach (var progress in _handler.Handle(query, CancellationToken.None))
        {
            results.Add(progress);
        }

        // Assert
        Assert.Empty(results);
        _mockProgressService.Verify(s => s.SubscribeToProgress(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_OwnerUser_StreamsProgress()
    {
        // Arrange
        var query = new StreamPdfProgressQuery(TestPdfId, TestUserId, IsAdmin: false);
        var pdf = CreateTestPdf(ownerId: TestUserId);

        _mockPdfRepository.Setup(r => r.GetByIdAsync(TestPdfId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pdf);

        var progressEvents = new[]
        {
            new ProcessingProgressJson { Step = ProcessingStep.Uploading, Percent = 10, Message = "Uploading" },
            new ProcessingProgressJson { Step = ProcessingStep.Extracting, Percent = 50, Message = "Extracting" },
            new ProcessingProgressJson { Step = ProcessingStep.Completed, Percent = 100, Message = "Completed" }
        };

        _mockProgressService.Setup(s => s.SubscribeToProgress(TestPdfId, It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(progressEvents));

        // Act
        var results = new List<ProcessingProgressJson>();
        await foreach (var progress in _handler.Handle(query, CancellationToken.None))
        {
            results.Add(progress);
        }

        // Assert
        Assert.Equal(3, results.Count);
        Assert.Equal(ProcessingStep.Uploading, results[0].Step);
        Assert.Equal(ProcessingStep.Extracting, results[1].Step);
        Assert.Equal(ProcessingStep.Completed, results[2].Step);
        _mockProgressService.Verify(s => s.SubscribeToProgress(TestPdfId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_AdminUser_StreamsProgress()
    {
        // Arrange
        var query = new StreamPdfProgressQuery(TestPdfId, OtherUserId, IsAdmin: true); // Admin, not owner
        var pdf = CreateTestPdf(ownerId: TestUserId); // Different owner

        _mockPdfRepository.Setup(r => r.GetByIdAsync(TestPdfId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pdf);

        var progressEvents = new[]
        {
            new ProcessingProgressJson { Step = ProcessingStep.Extracting, Percent = 50, Message = "Extracting" }
        };

        _mockProgressService.Setup(s => s.SubscribeToProgress(TestPdfId, It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(progressEvents));

        // Act
        var results = new List<ProcessingProgressJson>();
        await foreach (var progress in _handler.Handle(query, CancellationToken.None))
        {
            results.Add(progress);
        }

        // Assert
        Assert.Single(results);
        Assert.Equal(ProcessingStep.Extracting, results[0].Step);
        _mockProgressService.Verify(s => s.SubscribeToProgress(TestPdfId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_HeartbeatEvent_IsStreamed()
    {
        // Arrange
        var query = new StreamPdfProgressQuery(TestPdfId, TestUserId, IsAdmin: false);
        var pdf = CreateTestPdf(ownerId: TestUserId);

        _mockPdfRepository.Setup(r => r.GetByIdAsync(TestPdfId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pdf);

        var progressEvents = new[]
        {
            new ProcessingProgressJson { Step = ProcessingStep.Uploading, Percent = -1, Message = "heartbeat" } // Heartbeat
        };

        _mockProgressService.Setup(s => s.SubscribeToProgress(TestPdfId, It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(progressEvents));

        // Act
        var results = new List<ProcessingProgressJson>();
        await foreach (var progress in _handler.Handle(query, CancellationToken.None))
        {
            results.Add(progress);
        }

        // Assert
        Assert.Single(results);
        Assert.Equal(-1, results[0].Percent);
        Assert.Equal("heartbeat", results[0].Message);
    }

    private static PdfDocument CreateTestPdf(Guid ownerId)
    {
        return new PdfDocument(
            id: TestPdfId,
            gameId: Guid.NewGuid(),
            fileName: new FileName("test.pdf"),
            filePath: "/test/test.pdf",
            fileSize: new FileSize(1000),
            uploadedByUserId: ownerId
        );
    }

    private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            yield return item;
            await Task.CompletedTask;
        }
    }
}
