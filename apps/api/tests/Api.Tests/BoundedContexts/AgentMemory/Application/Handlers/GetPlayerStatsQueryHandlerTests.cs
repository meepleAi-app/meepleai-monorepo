using Api.BoundedContexts.AgentMemory.Application.Commands;
using Api.BoundedContexts.AgentMemory.Application.Queries;
using Api.BoundedContexts.AgentMemory.Application.Queries;
using Api.BoundedContexts.AgentMemory.Domain.Entities;
using Api.BoundedContexts.AgentMemory.Domain.Repositories;
using Api.Tests.Constants;
using Moq;
using Xunit;

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
        Assert.Single(result);
        var dto = result[0];
        Assert.Equal(groupId, dto.GroupId);
        Assert.Single(dto.GameStats);

        var gameStat = dto.GameStats[0];
        Assert.Equal(gameId, gameStat.GameId);
        Assert.Equal(1, gameStat.Wins);
        Assert.Equal(1, gameStat.Losses);
        Assert.Equal(2, gameStat.TotalPlayed);
        Assert.Equal(150, gameStat.BestScore);
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
        Assert.Empty(result);
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
        Assert.Equal(2, result.Count);
    }
}
