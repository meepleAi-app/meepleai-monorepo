using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using Api.BoundedContexts.SharedGameCatalog.Application.EventHandlers;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.SharedGameCatalog.Application.EventHandlers;

/// <summary>
/// Integration tests for ShareRequestApprovedDocumentHandler with in-memory database.
/// ISSUE-3005: Fixed DbContext mocking issues by using InMemoryDatabase.
/// </summary>
public sealed class ShareRequestApprovedDocumentHandlerTests : IAsyncLifetime
{
    private readonly DbContextOptions<MeepleAiDbContext> _options;
    private readonly Mock<IShareRequestDocumentService> _documentService;
    private readonly Mock<IShareRequestRepository> _shareRequestRepo;
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<IDomainEventCollector> _mockEventCollector;
    private readonly Mock<ILogger<ShareRequestApprovedDocumentHandler>> _logger;
    private MeepleAiDbContext _dbContext = null!;
    private ShareRequestApprovedDocumentHandler _sut = null!;

    private readonly Guid _shareRequestId = Guid.NewGuid();
    private readonly Guid _adminId = Guid.NewGuid();
    private readonly Guid _sharedGameId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public ShareRequestApprovedDocumentHandlerTests()
    {
        _options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"ShareRequestApprovedDocTest_{Guid.NewGuid()}")
            .Options;

        _documentService = new Mock<IShareRequestDocumentService>();
        _shareRequestRepo = new Mock<IShareRequestRepository>();
        _mockMediator = new Mock<IMediator>();
        _mockEventCollector = new Mock<IDomainEventCollector>();
        _mockEventCollector.Setup(x => x.GetAndClearEvents())
            .Returns(Array.Empty<Api.SharedKernel.Domain.Interfaces.IDomainEvent>());
        _logger = new Mock<ILogger<ShareRequestApprovedDocumentHandler>>();
    }

    public async Task InitializeAsync()
    {
        _dbContext = new MeepleAiDbContext(_options, _mockMediator.Object, _mockEventCollector.Object);
        _sut = new ShareRequestApprovedDocumentHandler(
            _dbContext,
            _documentService.Object,
            _shareRequestRepo.Object,
            _logger.Object);
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    [Fact]
    public async Task HandleEventAsync_WithDocuments_CopiesDocuments()
    {
        // Arrange
        var shareRequest = CreateShareRequestWithDocuments();
        var domainEvent = new ShareRequestApprovedEvent(
            _shareRequestId,
            _adminId,
            _sharedGameId);

        _shareRequestRepo.Setup(r => r.GetByIdAsync(_shareRequestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareRequest);

        _documentService.Setup(s => s.CopyDocumentsToSharedGame(
            It.IsAny<List<Guid>>(),
            _sharedGameId,
            _userId,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid> { Guid.NewGuid(), Guid.NewGuid() });

        // Act
        await _sut.Handle(domainEvent, CancellationToken.None);

        // Assert
        _documentService.Verify(s => s.CopyDocumentsToSharedGame(
            It.Is<List<Guid>>(ids => ids.Count == 2),
            _sharedGameId,
            _userId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleEventAsync_WithoutDocuments_SkipsCopy()
    {
        // Arrange
        var shareRequest = CreateShareRequestWithoutDocuments();
        var domainEvent = new ShareRequestApprovedEvent(
            _shareRequestId,
            _adminId,
            _sharedGameId);

        _shareRequestRepo.Setup(r => r.GetByIdAsync(_shareRequestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareRequest);

        // Act
        await _sut.Handle(domainEvent, CancellationToken.None);

        // Assert
        _documentService.Verify(s => s.CopyDocumentsToSharedGame(
            It.IsAny<List<Guid>>(),
            It.IsAny<Guid>(),
            It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private ShareRequest CreateShareRequestWithDocuments()
    {
        var request = ShareRequest.Create(
            _userId,
            Guid.NewGuid(),
            ContributionType.NewGame,
            "Test notes");

        request.AttachDocument(Guid.NewGuid(), "doc1.pdf", "application/pdf", 1024);
        request.AttachDocument(Guid.NewGuid(), "doc2.pdf", "application/pdf", 2048);

        return request;
    }

    private ShareRequest CreateShareRequestWithoutDocuments()
    {
        return ShareRequest.Create(
            _userId,
            Guid.NewGuid(),
            ContributionType.NewGame,
            "Test notes");
    }
}
