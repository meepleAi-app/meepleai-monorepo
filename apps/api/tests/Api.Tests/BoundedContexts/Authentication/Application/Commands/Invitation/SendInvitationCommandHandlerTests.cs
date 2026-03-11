using Api.BoundedContexts.Authentication.Application.Commands.Invitation;
using Api.BoundedContexts.Authentication.Application.DTOs;
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
public class SendInvitationCommandHandlerTests
{
    private readonly Mock<IInvitationTokenRepository> _invitationRepoMock = new();
    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IEmailService> _emailServiceMock = new();
    private readonly Mock<ILogger<SendInvitationCommandHandler>> _loggerMock = new();
    private readonly SendInvitationCommandHandler _handler;

    public SendInvitationCommandHandlerTests()
    {
        _handler = new SendInvitationCommandHandler(
            _invitationRepoMock.Object,
            _userRepoMock.Object,
            _unitOfWorkMock.Object,
            _emailServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidData_CreatesInvitationAndSendsEmail()
    {
        // Arrange
        var command = new SendInvitationCommand("test@example.com", "User", Guid.NewGuid());

        _invitationRepoMock
            .Setup(r => r.GetPendingByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((InvitationToken?)null);

        _userRepoMock
            .Setup(r => r.ExistsByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Email.Should().Be("test@example.com");
        result.Role.Should().Be("User");
        result.Status.Should().Be("Pending");
        result.Should().BeOfType<InvitationDto>();

        _invitationRepoMock.Verify(r => r.AddAsync(It.IsAny<InvitationToken>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _emailServiceMock.Verify(e => e.SendInvitationEmailAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithExistingPendingInvitation_ThrowsConflict()
    {
        // Arrange
        var command = new SendInvitationCommand("test@example.com", "User", Guid.NewGuid());

        var existingInvitation = InvitationToken.Create("test@example.com", "User", "somehash", Guid.NewGuid());
        _invitationRepoMock
            .Setup(r => r.GetPendingByEmailAsync("test@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingInvitation);

        // Act & Assert
        await Assert.ThrowsAsync<ConflictException>(() => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WithExistingUser_ThrowsConflict()
    {
        // Arrange
        var command = new SendInvitationCommand("test@example.com", "User", Guid.NewGuid());

        _invitationRepoMock
            .Setup(r => r.GetPendingByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((InvitationToken?)null);

        _userRepoMock
            .Setup(r => r.ExistsByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act & Assert
        await Assert.ThrowsAsync<ConflictException>(() => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WithSuperAdminRole_ThrowsArgumentException()
    {
        // Arrange
        var command = new SendInvitationCommand("test@example.com", "SuperAdmin", Guid.NewGuid());

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_NormalizesEmailToLowercase()
    {
        // Arrange
        var command = new SendInvitationCommand("TEST@EXAMPLE.COM", "User", Guid.NewGuid());

        _invitationRepoMock
            .Setup(r => r.GetPendingByEmailAsync("test@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((InvitationToken?)null);

        _userRepoMock
            .Setup(r => r.ExistsByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Email.Should().Be("test@example.com");
    }
}
