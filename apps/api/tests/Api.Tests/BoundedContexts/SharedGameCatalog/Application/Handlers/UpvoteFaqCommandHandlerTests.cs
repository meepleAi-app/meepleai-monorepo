using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class UpvoteFaqCommandHandlerTests
{
    private readonly Mock<ISharedGameRepository> _repositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<UpvoteFaqCommandHandler>> _loggerMock;
    private readonly UpvoteFaqCommandHandler _handler;

    public UpvoteFaqCommandHandlerTests()
    {
        _repositoryMock = new Mock<ISharedGameRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<UpvoteFaqCommandHandler>>();
        _handler = new UpvoteFaqCommandHandler(
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidFaq_UpvotesSuccessfully()
    {
        // Arrange
        var game = SharedGame.Create(
            title: "Test Game",
            yearPublished: 2020,
            description: "Test",
            minPlayers: 2,
            maxPlayers: 4,
            playingTimeMinutes: 60,
            minAge: 10,
            complexityRating: null,
            averageRating: null,
            imageUrl: "https://example.com/game.jpg",
            thumbnailUrl: "https://example.com/thumb.jpg",
            rules: null,
            createdBy: Guid.NewGuid(),
            bggId: null);

        var faq = GameFaq.Create(game.Id, "Test question?", "Test answer", 0);
        game.AddFaq(faq);

        var command = new UpvoteFaqCommand(faq.Id);

        _repositoryMock
            .Setup(r => r.GetGameByFaqIdAsync(faq.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(faq.Id);
        result.UpvoteCount.Should().Be(1);
        _repositoryMock.Verify(r => r.Update(game), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithMultipleUpvotes_IncrementsCount()
    {
        // Arrange
        var game = SharedGame.Create(
            title: "Test Game",
            yearPublished: 2020,
            description: "Test",
            minPlayers: 2,
            maxPlayers: 4,
            playingTimeMinutes: 60,
            minAge: 10,
            complexityRating: null,
            averageRating: null,
            imageUrl: "https://example.com/game.jpg",
            thumbnailUrl: "https://example.com/thumb.jpg",
            rules: null,
            createdBy: Guid.NewGuid(),
            bggId: null);

        var faq = GameFaq.Create(game.Id, "Test question?", "Test answer", 0);
        game.AddFaq(faq);

        // Simulate existing upvotes
        game.UpvoteFaq(faq.Id);
        game.UpvoteFaq(faq.Id);

        var command = new UpvoteFaqCommand(faq.Id);

        _repositoryMock
            .Setup(r => r.GetGameByFaqIdAsync(faq.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.UpvoteCount.Should().Be(3);
    }

    [Fact]
    public async Task Handle_WithNonExistentFaq_ThrowsInvalidOperationException()
    {
        // Arrange
        var faqId = Guid.NewGuid();
        var command = new UpvoteFaqCommand(faqId);

        _repositoryMock
            .Setup(r => r.GetGameByFaqIdAsync(faqId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SharedGame?)null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(command, TestContext.Current.CancellationToken));

        _repositoryMock.Verify(r => r.Update(It.IsAny<SharedGame>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
