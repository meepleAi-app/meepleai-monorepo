using Api.BoundedContexts.AgentMemory.Application.Commands;
using Api.BoundedContexts.AgentMemory.Domain.Entities;
using Api.BoundedContexts.AgentMemory.Domain.Repositories;
using Api.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.AgentMemory.Application.Commands;

/// <summary>
/// Tests for CreateGroupMemoryCommandHandler covering creation, members, and feature flag check.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "AgentMemory")]
public class CreateGroupMemoryCommandHandlerTests
{
    private readonly Mock<IGroupMemoryRepository> _groupRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IFeatureFlagService> _featureFlagsMock;
    private readonly CreateGroupMemoryCommandHandler _handler;

    public CreateGroupMemoryCommandHandlerTests()
    {
        _groupRepoMock = new Mock<IGroupMemoryRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _featureFlagsMock = new Mock<IFeatureFlagService>();
        var loggerMock = new Mock<ILogger<CreateGroupMemoryCommandHandler>>();

        _featureFlagsMock
            .Setup(f => f.IsEnabledAsync("Features:AgentMemory.Enabled", null))
            .ReturnsAsync(true);

        _handler = new CreateGroupMemoryCommandHandler(
            _groupRepoMock.Object,
            _unitOfWorkMock.Object,
            _featureFlagsMock.Object,
            loggerMock.Object);
    }

    [Fact]
    public async Task Handle_ValidCommand_CreatesGroupWithCreatorAsMember()
    {
        // Arrange
        var creatorId = Guid.NewGuid();
        var command = new CreateGroupMemoryCommand(creatorId, "Friday Night Gamers");

        GroupMemory? capturedGroup = null;
        _groupRepoMock
            .Setup(r => r.AddAsync(It.IsAny<GroupMemory>(), It.IsAny<CancellationToken>()))
            .Callback<GroupMemory, CancellationToken>((g, _) => capturedGroup = g)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotEqual(Guid.Empty, result);
        Assert.NotNull(capturedGroup);
        Assert.Equal("Friday Night Gamers", capturedGroup!.Name);
        Assert.Equal(creatorId, capturedGroup.CreatorId);
        Assert.Single(capturedGroup.Members); // Creator only
        Assert.Equal(creatorId, capturedGroup.Members[0].UserId);

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithInitialMembers_AddsAllMembers()
    {
        // Arrange
        var creatorId = Guid.NewGuid();
        var member1 = Guid.NewGuid();
        var member2 = Guid.NewGuid();
        var command = new CreateGroupMemoryCommand(
            creatorId,
            "Board Game Club",
            InitialMemberUserIds: new List<Guid> { member1, member2 });

        GroupMemory? capturedGroup = null;
        _groupRepoMock
            .Setup(r => r.AddAsync(It.IsAny<GroupMemory>(), It.IsAny<CancellationToken>()))
            .Callback<GroupMemory, CancellationToken>((g, _) => capturedGroup = g)
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(capturedGroup);
        Assert.Equal(3, capturedGroup!.Members.Count); // Creator + 2 members
    }

    [Fact]
    public async Task Handle_WithGuestNames_AddsGuestMembers()
    {
        // Arrange
        var creatorId = Guid.NewGuid();
        var command = new CreateGroupMemoryCommand(
            creatorId,
            "Family Game Night",
            InitialGuestNames: new List<string> { "Uncle Bob", "Cousin Alice" });

        GroupMemory? capturedGroup = null;
        _groupRepoMock
            .Setup(r => r.AddAsync(It.IsAny<GroupMemory>(), It.IsAny<CancellationToken>()))
            .Callback<GroupMemory, CancellationToken>((g, _) => capturedGroup = g)
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(capturedGroup);
        Assert.Equal(3, capturedGroup!.Members.Count); // Creator + 2 guests
        Assert.Contains(capturedGroup.Members, m => m.GuestName == "Uncle Bob");
        Assert.Contains(capturedGroup.Members, m => m.GuestName == "Cousin Alice");
    }

    [Fact]
    public async Task Handle_CreatorInMemberList_DoesNotDuplicate()
    {
        // Arrange
        var creatorId = Guid.NewGuid();
        var command = new CreateGroupMemoryCommand(
            creatorId,
            "Small Group",
            InitialMemberUserIds: new List<Guid> { creatorId }); // Creator already in list

        GroupMemory? capturedGroup = null;
        _groupRepoMock
            .Setup(r => r.AddAsync(It.IsAny<GroupMemory>(), It.IsAny<CancellationToken>()))
            .Callback<GroupMemory, CancellationToken>((g, _) => capturedGroup = g)
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(capturedGroup);
        Assert.Single(capturedGroup!.Members); // Only creator, no duplicate
    }

    [Fact]
    public async Task Handle_FeatureDisabled_ThrowsInvalidOperationException()
    {
        // Arrange
        _featureFlagsMock
            .Setup(f => f.IsEnabledAsync("Features:AgentMemory.Enabled", null))
            .ReturnsAsync(false);

        var command = new CreateGroupMemoryCommand(Guid.NewGuid(), "Test Group");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, CancellationToken.None));
    }
}
