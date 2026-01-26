using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using Api.BoundedContexts.SharedGameCatalog.Application.EventHandlers;
using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
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
/// Integration tests for ShareRequestRejectedDocumentHandler with in-memory database.
/// ISSUE-3005: Fixed DbContext mocking issues by using InMemoryDatabase.
/// </summary>
public sealed class ShareRequestRejectedDocumentHandlerTests : IAsyncLifetime
{
    private readonly DbContextOptions<MeepleAiDbContext> _options;
    private readonly Mock<IShareRequestDocumentService> _documentService;
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<IDomainEventCollector> _mockEventCollector;
    private readonly Mock<ILogger<ShareRequestRejectedDocumentHandler>> _logger;
    private MeepleAiDbContext _dbContext = null!;
    private ShareRequestRejectedDocumentHandler _sut = null!;

    private readonly Guid _shareRequestId = Guid.NewGuid();
    private readonly Guid _adminId = Guid.NewGuid();

    public ShareRequestRejectedDocumentHandlerTests()
    {
        _options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"ShareRequestRejectedDocTest_{Guid.NewGuid()}")
            .Options;

        _documentService = new Mock<IShareRequestDocumentService>();
        _mockMediator = new Mock<IMediator>();
        _mockEventCollector = new Mock<IDomainEventCollector>();
        _mockEventCollector.Setup(x => x.GetAndClearEvents())
            .Returns(Array.Empty<Api.SharedKernel.Domain.Interfaces.IDomainEvent>());
        _logger = new Mock<ILogger<ShareRequestRejectedDocumentHandler>>();
    }

    public async Task InitializeAsync()
    {
        _dbContext = new MeepleAiDbContext(_options, _mockMediator.Object, _mockEventCollector.Object);
        _sut = new ShareRequestRejectedDocumentHandler(
            _dbContext,
            _documentService.Object,
            _logger.Object);
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    [Fact]
    public async Task HandleEventAsync_CallsCleanupOrphanedDocuments()
    {
        // Arrange
        var domainEvent = new ShareRequestRejectedEvent(
            _shareRequestId,
            _adminId,
            "Not suitable");

        // Act
        await _sut.Handle(domainEvent, CancellationToken.None);

        // Assert
        _documentService.Verify(s => s.CleanupOrphanedDocuments(
            _shareRequestId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleEventAsync_WhenCleanupFails_DoesNotThrow()
    {
        // Arrange
        var domainEvent = new ShareRequestRejectedEvent(
            _shareRequestId,
            _adminId,
            "Not suitable");

        _documentService.Setup(s => s.CleanupOrphanedDocuments(
            It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Cleanup error"));

        // Act
        var act = async () => await _sut.Handle(domainEvent, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }
}
