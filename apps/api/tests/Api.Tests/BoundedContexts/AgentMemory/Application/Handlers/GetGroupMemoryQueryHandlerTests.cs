using Api.BoundedContexts.AgentMemory.Application.Queries;
using Api.BoundedContexts.AgentMemory.Domain.Entities;
using Api.BoundedContexts.AgentMemory.Domain.Repositories;
using Api.Tests.Constants;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.AgentMemory.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "AgentMemory")]
public class GetGroupMemoryQueryHandlerTests
{
    private readonly Mock<IGroupMemoryRepository> _groupRepoMock = new();
    private readonly GetGroupMemoryQueryHandler _handler;

    public GetGroupMemoryQueryHandlerTests()
    {
        _handler = new GetGroupMemoryQueryHandler(_groupRepoMock.Object);
    }

    [Fact]
    public async Task Handle_GroupExists_ReturnsMappedDto()
    {
        // Arrange
        var groupId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var group = GroupMemory.Create(creatorId, "Weekend Warriors");
        group.AddMember(creatorId);

        _groupRepoMock
            .Setup(r => r.GetByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        var query = new GetGroupMemoryQuery(groupId, creatorId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Weekend Warriors", result!.Name);
        Assert.NotEmpty(result.Members);
    }

    [Fact]
    public async Task Handle_RequesterIsMemberNotCreator_ReturnsMappedDto()
    {
        // Arrange — a plain member (not the creator) may read the group.
        var groupId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var group = GroupMemory.Create(creatorId, "Weekend Warriors");
        group.AddMember(creatorId);
        group.AddMember(memberId);

        _groupRepoMock
            .Setup(r => r.GetByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        var query = new GetGroupMemoryQuery(groupId, memberId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task Handle_RequesterNotMember_ReturnsNull()
    {
        // Arrange — #2655 IDOR fix: a non-member must not read another group's
        // memory. Returning null (→ 404) also hides the group's existence.
        var groupId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var group = GroupMemory.Create(creatorId, "Weekend Warriors");
        group.AddMember(creatorId);

        _groupRepoMock
            .Setup(r => r.GetByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        var query = new GetGroupMemoryQuery(groupId, Guid.NewGuid()); // outsider

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_GroupNotFound_ReturnsNull()
    {
        // Arrange
        var groupId = Guid.NewGuid();
        _groupRepoMock
            .Setup(r => r.GetByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GroupMemory?)null);

        var query = new GetGroupMemoryQuery(groupId, Guid.NewGuid());

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }
}
