using Api.BoundedContexts.AgentMemory.Application.Queries;
using Api.BoundedContexts.AgentMemory.Domain.Entities;
using Api.BoundedContexts.AgentMemory.Domain.Enums;
using Api.BoundedContexts.AgentMemory.Domain.Repositories;
using Api.Tests.Constants;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.AgentMemory.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "AgentMemory")]
public class GetGameMemoryQueryHandlerTests
{
    private readonly Mock<IGameMemoryRepository> _gameMemoryRepoMock = new();
    private readonly GetGameMemoryQueryHandler _handler;

    public GetGameMemoryQueryHandlerTests()
    {
        _handler = new GetGameMemoryQueryHandler(_gameMemoryRepoMock.Object);
    }

    [Fact]
    public async Task Handle_MemoryExists_ReturnsMappedDto()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var memory = GameMemory.Create(gameId, ownerId);
        memory.AddHouseRule("No phones at table", HouseRuleSource.UserAdded);
        memory.AddNote("Fun game last time", ownerId);

        _gameMemoryRepoMock
            .Setup(r => r.GetByGameAndOwnerAsync(gameId, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(memory);

        var query = new GetGameMemoryQuery(gameId, ownerId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(gameId, result!.GameId);
        Assert.Equal(ownerId, result.OwnerId);
        Assert.Single(result.HouseRules);
        Assert.Equal("No phones at table", result.HouseRules[0].Description);
        Assert.Single(result.Notes);
        Assert.Equal("Fun game last time", result.Notes[0].Content);
    }

    [Fact]
    public async Task Handle_MemoryNotFound_ReturnsNull()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();

        _gameMemoryRepoMock
            .Setup(r => r.GetByGameAndOwnerAsync(gameId, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameMemory?)null);

        var query = new GetGameMemoryQuery(gameId, ownerId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }
}
