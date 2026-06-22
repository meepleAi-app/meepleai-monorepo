using Api.BoundedContexts.AgentMemory.Application.Queries;
using Api.BoundedContexts.AgentMemory.Application.Services;
using Api.BoundedContexts.AgentMemory.Domain.Entities;
using Api.BoundedContexts.AgentMemory.Domain.Enums;
using Api.BoundedContexts.AgentMemory.Domain.Repositories;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.AgentMemory.Application.Handlers;

/// <summary>
/// Tests for GetHouseRulesMetadataQueryHandler covering the 4 ConnectionBar counts
/// (agent · game · session · kb) per issue #2492.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "AgentMemory")]
public class GetHouseRulesMetadataQueryHandlerTests
{
    private readonly Mock<IGameMemoryRepository> _gameMemoryRepoMock = new();
    private readonly Mock<IKbChunkCountReader> _kbChunkCountReaderMock = new();
    private readonly GetHouseRulesMetadataQueryHandler _handler;

    public GetHouseRulesMetadataQueryHandlerTests()
    {
        _handler = new GetHouseRulesMetadataQueryHandler(
            _gameMemoryRepoMock.Object,
            _kbChunkCountReaderMock.Object);
    }

    [Fact]
    public async Task Handle_MemoryNotFound_ReturnsZeroAgentCountWithKbAndGameCount()
    {
        // Arrange — user never opened the drawer before; no GameMemory exists yet.
        var gameId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();

        _gameMemoryRepoMock
            .Setup(r => r.GetByGameAndOwnerAsync(gameId, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameMemory?)null);

        _kbChunkCountReaderMock
            .Setup(r => r.CountByGameAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(7);

        var query = new GetHouseRulesMetadataQuery(gameId, ownerId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.AgentCount.Should().Be(0);
        result.GameCount.Should().Be(1, "gameCount is always 1 in current scope per issue #2492");
        result.SessionCount.Should().Be(0, "sessionCount placeholder — no session_house_rule_applications tracking yet");
        result.KbCount.Should().Be(7);
    }

    [Fact]
    public async Task Handle_MemoryWithNoHouseRules_ReturnsZeroAgentCount()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var memory = GameMemory.Create(gameId, ownerId);
        // Memory exists but no house rules added

        _gameMemoryRepoMock
            .Setup(r => r.GetByGameAndOwnerAsync(gameId, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(memory);

        _kbChunkCountReaderMock
            .Setup(r => r.CountByGameAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(12);

        var query = new GetHouseRulesMetadataQuery(gameId, ownerId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.AgentCount.Should().Be(0);
        result.GameCount.Should().Be(1);
        result.SessionCount.Should().Be(0);
        result.KbCount.Should().Be(12);
    }

    [Fact]
    public async Task Handle_MemoryWithThreeHouseRules_ReturnsAgentCountThree()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var memory = GameMemory.Create(gameId, ownerId);
        memory.AddHouseRule("No phones at table", HouseRuleSource.UserAdded);
        memory.AddHouseRule("Robber cannot rob on first turn", HouseRuleSource.UserAdded);
        memory.AddHouseRule("Trade with bank only after first roll", HouseRuleSource.DisputeOverride);

        _gameMemoryRepoMock
            .Setup(r => r.GetByGameAndOwnerAsync(gameId, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(memory);

        _kbChunkCountReaderMock
            .Setup(r => r.CountByGameAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(20);

        var query = new GetHouseRulesMetadataQuery(gameId, ownerId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.AgentCount.Should().Be(3);
        result.GameCount.Should().Be(1);
        result.SessionCount.Should().Be(0);
        result.KbCount.Should().Be(20);
    }

    [Fact]
    public async Task Handle_ZeroKbChunks_ReturnsKbCountZero()
    {
        // Arrange — early-onboarding scenario: PDFs not indexed yet
        var gameId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var memory = GameMemory.Create(gameId, ownerId);
        memory.AddHouseRule("Custom rule", HouseRuleSource.UserAdded);

        _gameMemoryRepoMock
            .Setup(r => r.GetByGameAndOwnerAsync(gameId, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(memory);

        _kbChunkCountReaderMock
            .Setup(r => r.CountByGameAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var query = new GetHouseRulesMetadataQuery(gameId, ownerId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.AgentCount.Should().Be(1);
        result.GameCount.Should().Be(1);
        result.SessionCount.Should().Be(0);
        result.KbCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_PassesGameAndOwnerCorrectly()
    {
        // Arrange — guard that the handler forwards the right ids to both deps
        var gameId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();

        _gameMemoryRepoMock
            .Setup(r => r.GetByGameAndOwnerAsync(gameId, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameMemory?)null);

        _kbChunkCountReaderMock
            .Setup(r => r.CountByGameAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var query = new GetHouseRulesMetadataQuery(gameId, ownerId);

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert — verify both deps were called with the right gameId; ownerId only flows
        // into the GameMemoryRepository (KB is per-game, not per-user)
        _gameMemoryRepoMock.Verify(
            r => r.GetByGameAndOwnerAsync(gameId, ownerId, It.IsAny<CancellationToken>()),
            Times.Once);
        _kbChunkCountReaderMock.Verify(
            r => r.CountByGameAsync(gameId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_NullQuery_ThrowsArgumentNullException()
    {
        // Act / Assert
        await FluentActions.Invoking(() => _handler.Handle(null!, CancellationToken.None))
            .Should()
            .ThrowAsync<ArgumentNullException>();
    }
}
