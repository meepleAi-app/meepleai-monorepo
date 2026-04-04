using Api.BoundedContexts.AgentMemory.Application.Commands;
using Api.BoundedContexts.AgentMemory.Application.Queries;
using Api.BoundedContexts.AgentMemory.Domain.Entities;
using Api.BoundedContexts.AgentMemory.Domain.Repositories;
using Api.Tests.Constants;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.AgentMemory.Application.Handlers;

/// <summary>
/// Tests for GetPlayerStatsQueryHandler covering stats retrieval and mapping.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "AgentMemory")]
public class GetPlayerStatsQueryHandlerTests
{
    private readonly Mock<IPlayerMemoryRepository> _playerRepoMock;
    private readonly GetPlayerStatsQueryHandler _handler;

    public GetPlayerStatsQueryHandlerTests()
    {
        _playerRepoMock = new Mock<IPlayerMemoryRepository>();
        _handler = new GetPlayerStatsQueryHandler(_playerRepoMock.Object);
    }

    [Fact]
    public async Task Handle_UserWithStats_ReturnsPlayerMemoryDtos()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        var memory = PlayerMemory.CreateForUser(userId, groupId);
        memory.UpdateGameStats(gameId, won: true, score: 150);
        memory.UpdateGameStats(gameId, won: false, score: 120);

        _playerRepoMock
            .Setup(r => r.GetAllByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PlayerMemory> { memory });

        var query = new GetPlayerStatsQuery(userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().ContainSingle();
        var dto = result[0];
        dto.GroupId.Should().Be(groupId);
        dto.GameStats.Should().ContainSingle();

        var gameStat = dto.GameStats[0];
        gameStat.GameId.Should().Be(gameId);
        gameStat.Wins.Should().Be(1);
        gameStat.Losses.Should().Be(1);
        gameStat.TotalPlayed.Should().Be(2);
        gameStat.BestScore.Should().Be(150);
    }

    [Fact]
    public async Task Handle_UserWithNoStats_ReturnsEmptyList()
    {
        // Arrange
        var userId = Guid.NewGuid();

        _playerRepoMock
            .Setup(r => r.GetAllByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PlayerMemory>());

        var query = new GetPlayerStatsQuery(userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_UserWithMultipleMemories_ReturnsAll()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var group1 = Guid.NewGuid();
        var group2 = Guid.NewGuid();

        var memory1 = PlayerMemory.CreateForUser(userId, group1);
        var memory2 = PlayerMemory.CreateForUser(userId, group2);

        _playerRepoMock
            .Setup(r => r.GetAllByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PlayerMemory> { memory1, memory2 });

        var query = new GetPlayerStatsQuery(userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Count.Should().Be(2);
    }
}
