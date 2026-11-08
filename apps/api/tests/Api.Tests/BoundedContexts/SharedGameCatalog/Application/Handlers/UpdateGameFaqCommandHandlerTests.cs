using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class UpdateGameFaqCommandHandlerTests
{
    private readonly Mock<ISharedGameRepository> _repositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<UpdateGameFaqCommandHandler>> _loggerMock;
    private readonly DetailCacheEvictionDoubles _eviction = new();
    private readonly UpdateGameFaqCommandHandler _handler;

    public UpdateGameFaqCommandHandlerTests()
    {
        _repositoryMock = new Mock<ISharedGameRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<UpdateGameFaqCommandHandler>>();
        _handler = new UpdateGameFaqCommandHandler(
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            _eviction.Invalidator,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_UpdatesFaqSuccessfully()
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

        var faq = GameFaq.Create(game.Id, "Original question?", "Original answer", 1);
        game.AddFaq(faq);

        var command = new UpdateGameFaqCommand(
            SharedGameId: game.Id,
            FaqId: faq.Id,
            Question: "Updated question?",
            Answer: "Updated answer",
            Order: 2);

        _repositoryMock
            .Setup(r => r.GetGameByFaqIdAsync(faq.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().Be(MediatR.Unit.Value);
        _repositoryMock.Verify(r => r.Update(game), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistentFaq_ThrowsNotFoundException()
    {
        // Arrange
        var faqId = Guid.NewGuid();
        var command = new UpdateGameFaqCommand(
            SharedGameId: Guid.NewGuid(),
            FaqId: faqId,
            Question: "Question?",
            Answer: "Answer",
            Order: 1);

        _repositoryMock
            .Setup(r => r.GetGameByFaqIdAsync(faqId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SharedGame?)null);

        // Act & Assert
        var act = () =>
            _handler.Handle(command, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<NotFoundException>();

        _repositoryMock.Verify(r => r.Update(It.IsAny<SharedGame>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithFaqOfAnotherGame_ThrowsNotFoundException()
    {
        // #4557: il gioco della rotta non è quello della FAQ.
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
        var faq = GameFaq.Create(game.Id, "Original question?", "Original answer", 1);
        game.AddFaq(faq);

        _repositoryMock
            .Setup(r => r.GetGameByFaqIdAsync(faq.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        var command = new UpdateGameFaqCommand(
            SharedGameId: Guid.NewGuid(),
            FaqId: faq.Id,
            Question: "Altra?",
            Answer: "Altra",
            Order: 1);

        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<NotFoundException>();

        _repositoryMock.Verify(r => r.Update(It.IsAny<SharedGame>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// #4565: il dettaglio del gioco riporta le FAQ e sta in cache. La prova end-to-end è
    /// <c>Detail_AfterUpdatingAFaq_ReportsTheNewText</c>, su Postgres.
    /// </summary>
    [Fact]
    public async Task Handle_AfterSave_EvictsTheDetailCacheOfTheGame()
    {
        var game = DetailCacheEvictionDoubles.NewGame();
        var faq = GameFaq.Create(game.Id, "Original question?", "Original answer", 1);
        game.AddFaq(faq);
        _repositoryMock
            .Setup(r => r.GetGameByFaqIdAsync(faq.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);
        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(_eviction.MarkWritten)
            .ReturnsAsync(1);

        await _handler.Handle(
            new UpdateGameFaqCommand(game.Id, faq.Id, "Updated question?", "Updated answer", 2),
            TestContext.Current.CancellationToken);

        _eviction.AssertEvictedAfterTheWrite(game.Id);
    }

    [Fact]
    public async Task Handle_WithAFaqOfAnotherGame_EvictsNothing()
    {
        var owner = DetailCacheEvictionDoubles.NewGame();
        var faq = GameFaq.Create(owner.Id, "Of another game?", "Yes", 1);
        owner.AddFaq(faq);
        _repositoryMock
            .Setup(r => r.GetGameByFaqIdAsync(faq.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(owner);

        var act = () => _handler.Handle(
            new UpdateGameFaqCommand(Guid.NewGuid(), faq.Id, "x", "y", 2),
            TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<NotFoundException>();

        _eviction.AssertNothingEvicted();
    }
}
