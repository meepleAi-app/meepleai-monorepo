using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Application.EventHandlers;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.SharedKernel.Application.IntegrationEvents;
using Api.Tests.BoundedContexts.DocumentProcessing.TestHelpers;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.EventHandlers;

/// <summary>
/// Unit tests for VectorDocumentIndexedEventHandler.
/// Issue #4942: Original notification handler.
/// Issue #5237: Refactored to publish VectorDocumentReadyIntegrationEvent
/// instead of directly creating notifications (cross-context decoupling).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class VectorDocumentIndexedEventHandlerTests
{
    private readonly Mock<IVectorDocumentRepository> _vectorDocRepo;
    private readonly Mock<IPdfDocumentRepository> _pdfDocRepo;
    private readonly Mock<IMediator> _mediator;
    private readonly Mock<ILogger<VectorDocumentIndexedEventHandler>> _logger;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _gameId = Guid.NewGuid();
    private readonly Guid _documentId = Guid.NewGuid();
    private readonly Guid _pdfDocumentId = Guid.NewGuid();
    private const string TestFileName = "twilight-imperium-rulebook.pdf";
    private const int TestChunkCount = 247;

    public VectorDocumentIndexedEventHandlerTests()
    {
        _vectorDocRepo = new Mock<IVectorDocumentRepository>();
        _pdfDocRepo = new Mock<IPdfDocumentRepository>();
        _mediator = new Mock<IMediator>();
        _logger = new Mock<ILogger<VectorDocumentIndexedEventHandler>>();
    }

    private VectorDocumentIndexedEventHandler CreateHandler()
    {
        var dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        return new VectorDocumentIndexedEventHandler(
            dbContext,
            _logger.Object,
            _vectorDocRepo.Object,
            _pdfDocRepo.Object,
            _mediator.Object);
    }

    private VectorDocumentIndexedEvent CreateEvent() =>
        new(_documentId, _gameId, TestChunkCount);

    private VectorDocument CreateVectorDocument() =>
        new(_documentId, _gameId, _pdfDocumentId, "it", TestChunkCount);

    private PdfDocument CreatePdfDocument() =>
        new PdfDocumentBuilder()
            .WithId(_pdfDocumentId)
            .WithGameId(_gameId)
            .WithFileName(TestFileName)
            .WithUploadedBy(_userId)
            .Build();

    #region VectorDocument Not Found

    [Fact]
    public async Task Handle_VectorDocumentNotFound_DoesNotPublishIntegrationEvent()
    {
        // Arrange
        _vectorDocRepo.Setup(r => r.GetByIdAsync(_documentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((VectorDocument?)null);

        var sut = CreateHandler();
        var evt = CreateEvent();

        // Act
        await sut.Handle(evt, CancellationToken.None);

        // Assert
        _mediator.Verify(m => m.Publish(
            It.IsAny<VectorDocumentReadyIntegrationEvent>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region PDF Document Not Found

    [Fact]
    public async Task Handle_PdfDocumentNotFound_DoesNotPublishIntegrationEvent()
    {
        // Arrange
        var vectorDoc = CreateVectorDocument();
        _vectorDocRepo.Setup(r => r.GetByIdAsync(_documentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vectorDoc);
        _pdfDocRepo.Setup(r => r.GetByIdAsync(_pdfDocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PdfDocument?)null);

        var sut = CreateHandler();
        var evt = CreateEvent();

        // Act
        await sut.Handle(evt, CancellationToken.None);

        // Assert
        _mediator.Verify(m => m.Publish(
            It.IsAny<VectorDocumentReadyIntegrationEvent>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Integration Event Publishing

    [Fact]
    public async Task Handle_BothDocumentsFound_PublishesIntegrationEventWithCorrectData()
    {
        // Arrange
        var vectorDoc = CreateVectorDocument();
        var pdfDoc = CreatePdfDocument();

        _vectorDocRepo.Setup(r => r.GetByIdAsync(_documentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vectorDoc);
        _pdfDocRepo.Setup(r => r.GetByIdAsync(_pdfDocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pdfDoc);

        VectorDocumentReadyIntegrationEvent? capturedEvent = null;
        _mediator.Setup(m => m.Publish(
                It.IsAny<VectorDocumentReadyIntegrationEvent>(),
                It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((e, _) =>
                capturedEvent = (VectorDocumentReadyIntegrationEvent)e);

        var sut = CreateHandler();
        var evt = CreateEvent();

        // Act
        await sut.Handle(evt, CancellationToken.None);

        // Assert
        _mediator.Verify(m => m.Publish(
            It.IsAny<VectorDocumentReadyIntegrationEvent>(),
            It.IsAny<CancellationToken>()), Times.Once);

        capturedEvent.Should().NotBeNull();
        capturedEvent!.DocumentId.Should().Be(_documentId);
        capturedEvent.GameId.Should().Be(_gameId);
        capturedEvent.ChunkCount.Should().Be(TestChunkCount);
        capturedEvent.PdfDocumentId.Should().Be(_pdfDocumentId);
        capturedEvent.UploadedByUserId.Should().Be(_userId);
        capturedEvent.FileName.Should().Be(TestFileName);
        capturedEvent.SourceContext.Should().Be("KnowledgeBase");
    }

    [Fact]
    public async Task Handle_BothDocumentsFound_IntegrationEventHasValidMetadata()
    {
        // Arrange
        var vectorDoc = CreateVectorDocument();
        var pdfDoc = CreatePdfDocument();

        _vectorDocRepo.Setup(r => r.GetByIdAsync(_documentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vectorDoc);
        _pdfDocRepo.Setup(r => r.GetByIdAsync(_pdfDocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pdfDoc);

        VectorDocumentReadyIntegrationEvent? capturedEvent = null;
        _mediator.Setup(m => m.Publish(
                It.IsAny<VectorDocumentReadyIntegrationEvent>(),
                It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((e, _) =>
                capturedEvent = (VectorDocumentReadyIntegrationEvent)e);

        var sut = CreateHandler();
        var evt = CreateEvent();

        // Act
        await sut.Handle(evt, CancellationToken.None);

        // Assert
        capturedEvent.Should().NotBeNull();
        capturedEvent!.EventId.Should().NotBeEmpty();
        capturedEvent.OccurredAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Handle_BothDocumentsFound_IncludesCurrentProcessingState()
    {
        // Arrange
        var vectorDoc = CreateVectorDocument();
        var pdfDoc = CreatePdfDocument();

        _vectorDocRepo.Setup(r => r.GetByIdAsync(_documentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vectorDoc);
        _pdfDocRepo.Setup(r => r.GetByIdAsync(_pdfDocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pdfDoc);

        VectorDocumentReadyIntegrationEvent? capturedEvent = null;
        _mediator.Setup(m => m.Publish(
                It.IsAny<VectorDocumentReadyIntegrationEvent>(),
                It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((e, _) =>
                capturedEvent = (VectorDocumentReadyIntegrationEvent)e);

        var sut = CreateHandler();
        var evt = CreateEvent();

        // Act
        await sut.Handle(evt, CancellationToken.None);

        // Assert
        capturedEvent.Should().NotBeNull();
        capturedEvent!.CurrentProcessingState.Should().NotBeNullOrWhiteSpace();
    }

    #endregion

    #region Repository Calls

    [Fact]
    public async Task Handle_VectorDocumentNotFound_DoesNotQueryPdfDocument()
    {
        // Arrange
        _vectorDocRepo.Setup(r => r.GetByIdAsync(_documentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((VectorDocument?)null);

        var sut = CreateHandler();
        var evt = CreateEvent();

        // Act
        await sut.Handle(evt, CancellationToken.None);

        // Assert
        _pdfDocRepo.Verify(r => r.GetByIdAsync(
            It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_VectorDocumentFound_QueriesPdfDocumentWithCorrectId()
    {
        // Arrange
        var vectorDoc = CreateVectorDocument();
        _vectorDocRepo.Setup(r => r.GetByIdAsync(_documentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vectorDoc);
        _pdfDocRepo.Setup(r => r.GetByIdAsync(_pdfDocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PdfDocument?)null);

        var sut = CreateHandler();
        var evt = CreateEvent();

        // Act
        await sut.Handle(evt, CancellationToken.None);

        // Assert
        _pdfDocRepo.Verify(r => r.GetByIdAsync(
            _pdfDocumentId, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}
