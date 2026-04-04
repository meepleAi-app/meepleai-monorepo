using Api.BoundedContexts.AgentMemory.Application.Queries;
using Api.BoundedContexts.AgentMemory.Domain.Entities;
using Api.BoundedContexts.AgentMemory.Domain.Repositories;
using Api.Tests.Constants;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.AgentMemory.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "AgentMemory")]
public class GetClaimableGuestsQueryHandlerTests
{
    private readonly Mock<IPlayerMemoryRepository> _playerRepoMock = new();
    private readonly Mock<IGroupMemoryRepository> _groupRepoMock = new();
    private readonly GetClaimableGuestsQueryHandler _handler;

    public GetClaimableGuestsQueryHandlerTests()
    {
        _handler = new GetClaimableGuestsQueryHandler(
            _playerRepoMock.Object,
            _groupRepoMock.Object);
    }

    [Fact]
    public async Task Handle_GuestsFound_ReturnsDtoList()
    {
        // Arrange
        var groupId = Guid.NewGuid();
        var guest = PlayerMemory.CreateForGuest("Alice", groupId);
        var group = GroupMemory.Create(Guid.NewGuid(), "Friday Gamers");

        _playerRepoMock
            .Setup(r => r.GetAllByGuestNameAsync("Alice", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PlayerMemory> { guest });

        _groupRepoMock
            .Setup(r => r.GetByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        var query = new GetClaimableGuestsQuery(Guid.NewGuid(), "Alice");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Single(result);
        Assert.Equal("Alice", result[0].GuestName);
        Assert.Equal(groupId, result[0].GroupId);
        Assert.Equal("Friday Gamers", result[0].GroupName);
    }

    [Fact]
    public async Task Handle_GuestWithoutGroup_ReturnsNullGroupName()
    {
        // Arrange
        var guest = PlayerMemory.CreateForGuest("Bob");

        _playerRepoMock
            .Setup(r => r.GetAllByGuestNameAsync("Bob", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PlayerMemory> { guest });

        var query = new GetClaimableGuestsQuery(Guid.NewGuid(), "Bob");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Single(result);
        Assert.Equal("Bob", result[0].GuestName);
        Assert.Null(result[0].GroupId);
        Assert.Null(result[0].GroupName);
    }

    [Fact]
    public async Task Handle_NoGuestsFound_ReturnsEmptyList()
    {
        // Arrange
        _playerRepoMock
            .Setup(r => r.GetAllByGuestNameAsync("Nobody", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PlayerMemory>());

        var query = new GetClaimableGuestsQuery(Guid.NewGuid(), "Nobody");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Empty(result);
    }
}
