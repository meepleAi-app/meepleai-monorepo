using Api.BoundedContexts.AgentMemory.Application.Commands;
using Api.BoundedContexts.AgentMemory.Application.Commands;
using Api.BoundedContexts.AgentMemory.Application.Queries;
using Api.BoundedContexts.AgentMemory.Domain.Entities;
using Api.BoundedContexts.AgentMemory.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.AgentMemory.Application.Handlers;

/// <summary>
/// Tests for ClaimGuestPlayerCommandHandler covering claim success, already claimed, and not found scenarios.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "AgentMemory")]
public class ClaimGuestPlayerCommandHandlerTests
{
    private readonly Mock<IPlayerMemoryRepository> _playerRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IFeatureFlagService> _featureFlagsMock;
    private readonly ClaimGuestPlayerCommandHandler _handler;

    public ClaimGuestPlayerCommandHandlerTests()
    {
        _playerRepoMock = new Mock<IPlayerMemoryRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _featureFlagsMock = new Mock<IFeatureFlagService>();
        var loggerMock = new Mock<ILogger<ClaimGuestPlayerCommandHandler>>();

        _featureFlagsMock
            .Setup(f => f.IsEnabledAsync("Features:AgentMemory.GuestClaim", null))
            .ReturnsAsync(true);

        _handler = new ClaimGuestPlayerCommandHandler(
            _playerRepoMock.Object,
            _unitOfWorkMock.Object,
            _featureFlagsMock.Object,
            loggerMock.Object);
    }

    [Fact]
    public async Task Handle_ValidGuestClaim_ClaimsSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var guestMemory = PlayerMemory.CreateForGuest("GuestBob");
        var command = new ClaimGuestPlayerCommand(userId, guestMemory.Id);

        _playerRepoMock
            .Setup(r => r.GetByIdAsync(guestMemory.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(guestMemory);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        guestMemory.UserId.Should().Be(userId);
        guestMemory.ClaimedAt.Should().NotBeNull();

        _playerRepoMock.Verify(r => r.UpdateAsync(guestMemory, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_AlreadyClaimedGuest_ThrowsInvalidOperationException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var guestMemory = PlayerMemory.CreateForGuest("GuestAlice");
        guestMemory.ClaimByUser(Guid.NewGuid()); // Already claimed by another user

        var command = new ClaimGuestPlayerCommand(userId, guestMemory.Id);

        _playerRepoMock
            .Setup(r => r.GetByIdAsync(guestMemory.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(guestMemory);

        // Act & Assert
        var act = () => _handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Handle_PlayerMemoryNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        var command = new ClaimGuestPlayerCommand(Guid.NewGuid(), nonExistentId);

        _playerRepoMock
            .Setup(r => r.GetByIdAsync(nonExistentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PlayerMemory?)null);

        // Act & Assert
        var act2 = () => _handler.Handle(command, CancellationToken.None);
        await act2.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_FeatureDisabled_ThrowsInvalidOperationException()
    {
        // Arrange
        _featureFlagsMock
            .Setup(f => f.IsEnabledAsync("Features:AgentMemory.GuestClaim", null))
            .ReturnsAsync(false);

        var command = new ClaimGuestPlayerCommand(Guid.NewGuid(), Guid.NewGuid());

        // Act & Assert
        var act3 = () => _handler.Handle(command, CancellationToken.None);
        await act3.Should().ThrowAsync<InvalidOperationException>();
    }
}
