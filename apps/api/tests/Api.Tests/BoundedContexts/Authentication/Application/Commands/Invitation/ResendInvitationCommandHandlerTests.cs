using Api.BoundedContexts.Authentication.Application.Commands.Invitation;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Enums;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application.Commands.Invitation;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
public class ResendInvitationCommandHandlerTests
{
    private readonly Mock<IInvitationTokenRepository> _invitationRepoMock = new();
    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IEmailService> _emailServiceMock = new();
    private readonly Mock<ILogger<ResendInvitationCommandHandler>> _loggerMock = new();
    private readonly ResendInvitationCommandHandler _handler;

    public ResendInvitationCommandHandlerTests()
    {
        _handler = new ResendInvitationCommandHandler(
            _invitationRepoMock.Object,
            _userRepoMock.Object,
            _unitOfWorkMock.Object,
            _emailServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_PendingInvitation_ExpiresOldAndCreatesNew()
    {
        // Arrange
        var invitationId = Guid.NewGuid();
        var existingInvitation = InvitationToken.Create("test@example.com", "User", "oldhash", Guid.NewGuid());

        _invitationRepoMock
            .Setup(r => r.GetByIdAsync(invitationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingInvitation);

        _userRepoMock
            .Setup(r => r.ExistsByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var command = new ResendInvitationCommand(invitationId, Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Email.Should().Be("test@example.com");
        result.Status.Should().Be("Pending");

        // Old invitation should be updated (expired), new one added
        _invitationRepoMock.Verify(r => r.UpdateAsync(It.IsAny<InvitationToken>(), It.IsAny<CancellationToken>()), Times.Once);
        _invitationRepoMock.Verify(r => r.AddAsync(It.IsAny<InvitationToken>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_AcceptedInvitation_ThrowsConflict()
    {
        // Arrange
        var invitationId = Guid.NewGuid();
        var invitation = InvitationToken.Create("test@example.com", "User", "hash", Guid.NewGuid());
        // Mark as accepted
        invitation.MarkAccepted(Guid.NewGuid());

        _invitationRepoMock
            .Setup(r => r.GetByIdAsync(invitationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);

        var command = new ResendInvitationCommand(invitationId, Guid.NewGuid());

        // Act & Assert
        await Assert.ThrowsAsync<ConflictException>(() => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_UserAlreadyExists_ThrowsConflict()
    {
        // Arrange
        var invitationId = Guid.NewGuid();
        var invitation = InvitationToken.Create("test@example.com", "User", "hash", Guid.NewGuid());

        _invitationRepoMock
            .Setup(r => r.GetByIdAsync(invitationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);

        _userRepoMock
            .Setup(r => r.ExistsByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var command = new ResendInvitationCommand(invitationId, Guid.NewGuid());

        // Act & Assert
        await Assert.ThrowsAsync<ConflictException>(() => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_InvitationNotFound_ThrowsNotFound()
    {
        // Arrange
        var invitationId = Guid.NewGuid();

        _invitationRepoMock
            .Setup(r => r.GetByIdAsync(invitationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((InvitationToken?)null);

        var command = new ResendInvitationCommand(invitationId, Guid.NewGuid());

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => _handler.Handle(command, CancellationToken.None));
    }
}
