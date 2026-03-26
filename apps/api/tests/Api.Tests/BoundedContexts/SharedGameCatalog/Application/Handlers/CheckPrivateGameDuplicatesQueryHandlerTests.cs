using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.CheckPrivateGameDuplicates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class CheckPrivateGameDuplicatesQueryHandlerTests
{
    private readonly Mock<IPrivateGameRepository> _privateGameRepoMock;
    private readonly Mock<ISharedGameRepository> _sharedGameRepoMock;
    private readonly Mock<ILogger<CheckPrivateGameDuplicatesQueryHandler>> _loggerMock;
    private readonly CheckPrivateGameDuplicatesQueryHandler _handler;

    private static readonly Guid TestUserId = Guid.NewGuid();
    private static readonly Guid TestPrivateGameId = Guid.NewGuid();
    private static readonly int TestBggId = 12345;

    public CheckPrivateGameDuplicatesQueryHandlerTests()
    {
        _privateGameRepoMock = new Mock<IPrivateGameRepository>();
        _sharedGameRepoMock = new Mock<ISharedGameRepository>();
        _loggerMock = new Mock<ILogger<CheckPrivateGameDuplicatesQueryHandler>>();

        _handler = new CheckPrivateGameDuplicatesQueryHandler(
            _privateGameRepoMock.Object,
            _sharedGameRepoMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public void Constructor_CreatesQueryWithPrivateGameId()
    {
        // Arrange & Act
        var query = new CheckPrivateGameDuplicatesQuery(TestPrivateGameId);

        // Assert
        query.PrivateGameId.Should().Be(TestPrivateGameId);
    }

    [Fact]
    public async Task Handle_WithNonExistentPrivateGame_ThrowsNotFoundException()
    {
        // Arrange
        var query = new CheckPrivateGameDuplicatesQuery(Guid.NewGuid());

        _privateGameRepoMock
            .Setup(r => r.GetByIdAsync(query.PrivateGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PrivateGame?)null);

        // Act & Assert
        var act = () => _handler.Handle(query, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_WithNoBggId_ReturnsNoDuplicatesAndRecommendsApproveAsNew()
    {
        // Arrange
        var privateGame = CreatePrivateGameWithoutBggId();
        var query = new CheckPrivateGameDuplicatesQuery(TestPrivateGameId);

        _privateGameRepoMock
            .Setup(r => r.GetByIdAsync(query.PrivateGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(privateGame);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.HasExactDuplicate.Should().BeFalse();
        result.ExactDuplicateId.Should().BeNull();
        result.ExactDuplicateTitle.Should().BeNull();
        result.HasFuzzyDuplicates.Should().BeFalse();
        result.FuzzyDuplicates.Should().BeEmpty();
        result.RecommendedAction.Should().Be(ProposalApprovalAction.ApproveAsNew);

        // Verify BggId lookup was NOT attempted
        _sharedGameRepoMock.Verify(
            r => r.GetByBggIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithBggId_NoExactMatch_ReturnsNoDuplicatesAndRecommendsApproveAsNew()
    {
        // Arrange
        var privateGame = CreatePrivateGameWithBggId();
        var query = new CheckPrivateGameDuplicatesQuery(TestPrivateGameId);

        _privateGameRepoMock
            .Setup(r => r.GetByIdAsync(query.PrivateGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(privateGame);

        _sharedGameRepoMock
            .Setup(r => r.GetByBggIdAsync(TestBggId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SharedGame?)null);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.HasExactDuplicate.Should().BeFalse();
        result.ExactDuplicateId.Should().BeNull();
        result.ExactDuplicateTitle.Should().BeNull();
        result.HasFuzzyDuplicates.Should().BeFalse();
        result.FuzzyDuplicates.Should().BeEmpty();
        result.RecommendedAction.Should().Be(ProposalApprovalAction.ApproveAsNew);

        _sharedGameRepoMock.Verify(
            r => r.GetByBggIdAsync(TestBggId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithBggId_ExactMatch_ReturnsExactDuplicateAndRecommendsMergeKnowledgeBase()
    {
        // Arrange
        var privateGame = CreatePrivateGameWithBggId();
        var existingSharedGame = SharedGame.Create(
            title: "Existing Shared Game",
            yearPublished: 2020,
            description: "A test game",
            minPlayers: 2,
            maxPlayers: 4,
            playingTimeMinutes: 60,
            minAge: 10,
            complexityRating: 2.5m,
            averageRating: 7.5m,
            imageUrl: "https://example.com/image.jpg",
            thumbnailUrl: "https://example.com/thumb.jpg",
            rules: null,
            createdBy: TestUserId,
            bggId: TestBggId); // Same BggId as private game

        var query = new CheckPrivateGameDuplicatesQuery(TestPrivateGameId);

        _privateGameRepoMock
            .Setup(r => r.GetByIdAsync(query.PrivateGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(privateGame);

        _sharedGameRepoMock
            .Setup(r => r.GetByBggIdAsync(TestBggId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingSharedGame);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.HasExactDuplicate.Should().BeTrue();
        result.ExactDuplicateId.Should().Be(existingSharedGame.Id);
        result.ExactDuplicateTitle.Should().Be(existingSharedGame.Title);
        result.HasFuzzyDuplicates.Should().BeFalse(); // Not implemented yet
        result.FuzzyDuplicates.Should().BeEmpty();
        result.RecommendedAction.Should().Be(ProposalApprovalAction.MergeKnowledgeBase);

        _sharedGameRepoMock.Verify(
            r => r.GetByBggIdAsync(TestBggId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithNullQuery_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => _handler.Handle(null!, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    private static PrivateGame CreatePrivateGameWithBggId()
    {
        return PrivateGame.CreateFromBgg(
            ownerId: TestUserId,
            bggId: TestBggId,
            title: "Test Private Game",
            description: "A test game",
            yearPublished: 2020,
            imageUrl: "https://example.com/image.jpg",
            thumbnailUrl: "https://example.com/thumb.jpg",
            minPlayers: 2,
            maxPlayers: 4,
            playingTimeMinutes: 60,
            minAge: 10,
            complexityRating: 2.5m);
    }

    private static PrivateGame CreatePrivateGameWithoutBggId()
    {
        return PrivateGame.CreateManual(
            ownerId: TestUserId,
            title: "Manual Private Game",
            minPlayers: 2,
            maxPlayers: 4);
    }
}
