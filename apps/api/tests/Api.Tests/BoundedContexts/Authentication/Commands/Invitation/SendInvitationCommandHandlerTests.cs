using Api.BoundedContexts.Authentication.Application.Commands.Invitation;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Commands.Invitation;

/// <summary>
/// Unit tests for SendInvitationCommandHandler.
/// Bug #D (happy-path testing 2026-07-10): the DTO's EmailSent flag must reflect the real SMTP
/// outcome instead of always reporting true — the admin "Invite User" UI reports "invito inviato"
/// off this flag, so a silent SMTP failure previously showed a false success.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
public sealed class SendInvitationCommandHandlerTests
{
    private readonly Mock<IInvitationTokenRepository> _invitationRepo = new();
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IEmailService> _emailService = new();
    private readonly Mock<ILogger<SendInvitationCommandHandler>> _logger = new();
    private readonly SendInvitationCommandHandler _handler;

    public SendInvitationCommandHandlerTests()
    {
        // No pending invitation, no existing user → the happy-path branch that reaches email send.
        _invitationRepo
            .Setup(r => r.GetPendingByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((InvitationToken?)null);
        _invitationRepo
            .Setup(r => r.AddAsync(It.IsAny<InvitationToken>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _userRepo
            .Setup(r => r.ExistsByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _unitOfWork
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _handler = new SendInvitationCommandHandler(
            _invitationRepo.Object,
            _userRepo.Object,
            _unitOfWork.Object,
            _emailService.Object,
            _logger.Object);
    }

    private static SendInvitationCommand ValidCommand() =>
        new("newuser@example.com", "User", Guid.NewGuid());

    [Fact]
    public async Task Handle_WhenEmailSendSucceeds_ReturnsEmailSentTrue()
    {
        // Arrange
        _emailService
            .Setup(e => e.SendInvitationEmailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(ValidCommand(), CancellationToken.None);

        // Assert
        result.EmailSent.Should().BeTrue();
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenEmailSendFails_ReturnsEmailSentFalse_ButStillPersistsInvitation()
    {
        // Arrange — an SMTP transport failure surfaces as InvalidOperationException from EmailService.
        _emailService
            .Setup(e => e.SendInvitationEmailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Failed to send invitation email"));

        // Act
        var result = await _handler.Handle(ValidCommand(), CancellationToken.None);

        // Assert — the invitation is still saved (fire-and-forget), but EmailSent MUST be false
        // so the caller can surface the delivery failure instead of a false positive.
        result.EmailSent.Should().BeFalse();
        _invitationRepo.Verify(
            r => r.AddAsync(It.IsAny<InvitationToken>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
