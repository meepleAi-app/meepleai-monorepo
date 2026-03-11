using System.Security.Cryptography;
using System.Text;
using Api.BoundedContexts.Authentication.Application.Commands.Invitation;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application.Commands.Invitation;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
public class AcceptInvitationCommandHandlerTests
{
    private readonly Mock<IInvitationTokenRepository> _invitationRepoMock = new();
    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly Mock<ISessionRepository> _sessionRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<ILogger<AcceptInvitationCommandHandler>> _loggerMock = new();
    private readonly AcceptInvitationCommandHandler _handler;

    public AcceptInvitationCommandHandlerTests()
    {
        _handler = new AcceptInvitationCommandHandler(
            _invitationRepoMock.Object,
            _userRepoMock.Object,
            _sessionRepoMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object);
    }

    private static (string rawToken, string tokenHash, InvitationToken invitation) CreateTestInvitation(
        string email = "test@example.com", string role = "User")
    {
        var rawToken = "test-raw-token-accept";
        var tokenHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
        var invitation = InvitationToken.Create(email, role, tokenHash, Guid.NewGuid());
        return (rawToken, tokenHash, invitation);
    }

    [Fact]
    public async Task Handle_ValidToken_CreatesUserAndSession()
    {
        // Arrange
        var (rawToken, tokenHash, invitation) = CreateTestInvitation();

        _invitationRepoMock
            .Setup(r => r.GetByTokenHashAsync(tokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);

        var command = new AcceptInvitationCommand(rawToken, "Password1!", "Password1!");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.User.Should().NotBeNull();
        result.User.Email.Should().Be("test@example.com");
        result.User.Role.Should().Be("user"); // Role.Parse normalizes to lowercase
        result.SessionToken.Should().NotBeNullOrEmpty();
        result.ExpiresAt.Should().BeAfter(DateTime.UtcNow);

        _userRepoMock.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
        _sessionRepoMock.Verify(r => r.AddAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_InvalidToken_Throws()
    {
        // Arrange
        _invitationRepoMock
            .Setup(r => r.GetByTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((InvitationToken?)null);

        var command = new AcceptInvitationCommand("invalid-token", "Password1!", "Password1!");

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_SetsEmailVerifiedTrue()
    {
        // Arrange
        var (rawToken, tokenHash, invitation) = CreateTestInvitation();

        _invitationRepoMock
            .Setup(r => r.GetByTokenHashAsync(tokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);

        var command = new AcceptInvitationCommand(rawToken, "Password1!", "Password1!");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.User.EmailVerified.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_MarksInvitationAsAccepted()
    {
        // Arrange
        var (rawToken, tokenHash, invitation) = CreateTestInvitation();

        _invitationRepoMock
            .Setup(r => r.GetByTokenHashAsync(tokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);

        var command = new AcceptInvitationCommand(rawToken, "Password1!", "Password1!");

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _invitationRepoMock.Verify(
            r => r.UpdateAsync(It.Is<InvitationToken>(i => i.AcceptedAt != null), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_PasswordsMismatch_Throws()
    {
        // Arrange
        var command = new AcceptInvitationCommand("some-token", "Password1!", "DifferentPassword1!");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _handler.Handle(command, CancellationToken.None));
    }
}
