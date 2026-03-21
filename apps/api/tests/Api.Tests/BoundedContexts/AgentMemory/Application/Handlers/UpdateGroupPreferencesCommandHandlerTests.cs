using Api.BoundedContexts.AgentMemory.Application.Commands;
using Api.BoundedContexts.AgentMemory.Domain.Entities;
using Api.BoundedContexts.AgentMemory.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.AgentMemory.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "AgentMemory")]
public class UpdateGroupPreferencesCommandHandlerTests
{
    private readonly Mock<IGroupMemoryRepository> _groupRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IFeatureFlagService> _featureFlagsMock = new();
    private readonly UpdateGroupPreferencesCommandHandler _handler;

    public UpdateGroupPreferencesCommandHandlerTests()
    {
        var loggerMock = new Mock<ILogger<UpdateGroupPreferencesCommandHandler>>();

        _featureFlagsMock
            .Setup(f => f.IsEnabledAsync("Features:AgentMemory.Enabled", null))
            .ReturnsAsync(true);

        _handler = new UpdateGroupPreferencesCommandHandler(
            _groupRepoMock.Object,
            _unitOfWorkMock.Object,
            _featureFlagsMock.Object,
            loggerMock.Object);
    }

    [Fact]
    public async Task Handle_ValidCommand_UpdatesGroupPreferences()
    {
        // Arrange
        var groupId = Guid.NewGuid();
        var group = GroupMemory.Create(Guid.NewGuid(), "Game Night Crew");

        _groupRepoMock
            .Setup(r => r.GetByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        var command = new UpdateGroupPreferencesCommand(
            groupId,
            TimeSpan.FromMinutes(90),
            "Medium",
            "We prefer cooperative games");

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _groupRepoMock.Verify(r => r.UpdateAsync(group, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_GroupNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var groupId = Guid.NewGuid();
        _groupRepoMock
            .Setup(r => r.GetByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GroupMemory?)null);

        var command = new UpdateGroupPreferencesCommand(groupId, null, null, null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_FeatureDisabled_ThrowsInvalidOperationException()
    {
        // Arrange
        _featureFlagsMock
            .Setup(f => f.IsEnabledAsync("Features:AgentMemory.Enabled", null))
            .ReturnsAsync(false);

        var command = new UpdateGroupPreferencesCommand(Guid.NewGuid(), null, null, null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, CancellationToken.None));
    }
}
