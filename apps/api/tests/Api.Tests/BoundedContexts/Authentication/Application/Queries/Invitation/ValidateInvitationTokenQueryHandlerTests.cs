using System.Security.Cryptography;
using System.Text;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Application.Queries.Invitation;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Enums;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Tests.BoundedContexts.Authentication.TestHelpers;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application.Queries.Invitation;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
public class ValidateInvitationTokenQueryHandlerTests
{
    private readonly Mock<IInvitationTokenRepository> _invitationRepoMock = new();
    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly ValidateInvitationTokenQueryHandler _handler;

    public ValidateInvitationTokenQueryHandlerTests()
    {
        _handler = new ValidateInvitationTokenQueryHandler(
            _invitationRepoMock.Object,
            _userRepoMock.Object);
    }

    [Fact]
    public async Task Handle_ValidToken_ReturnsIsValidWithEmailAndDisplayName()
    {
        // Arrange
        var rawToken = "test-raw-token-123";
        var tokenHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
        var pendingUserId = Guid.NewGuid();

        var invitation = InvitationToken.Create("test@example.com", "User", tokenHash, Guid.NewGuid());
        // Set PendingUserId via RestoreState to simulate a provisioned invitation
        invitation.RestoreState(
            email: "test@example.com",
            role: "User",
            tokenHash: tokenHash,
            invitedByUserId: Guid.NewGuid(),
            status: InvitationStatus.Pending,
            expiresAt: DateTime.UtcNow.AddDays(7),
            acceptedAt: null,
            acceptedByUserId: null,
            createdAt: DateTime.UtcNow,
            pendingUserId: pendingUserId);

        var pendingUser = new UserBuilder()
            .WithId(pendingUserId)
            .WithEmail("test@example.com")
            .WithDisplayName("Test User")
            .Build();

        _invitationRepoMock
            .Setup(r => r.GetByTokenHashAsync(tokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);
        _userRepoMock
            .Setup(r => r.GetByIdAsync(pendingUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pendingUser);

        var query = new ValidateInvitationTokenQuery(rawToken);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Email.Should().Be("test@example.com");
        result.DisplayName.Should().Be("Test User");
        result.ErrorReason.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ValidTokenWithoutPendingUser_ReturnsIsValidWithNullDisplayName()
    {
        // Arrange
        var rawToken = "test-raw-token-456";
        var tokenHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

        // Old-style invitation without PendingUserId
        var invitation = InvitationToken.Create("old@example.com", "User", tokenHash, Guid.NewGuid());

        _invitationRepoMock
            .Setup(r => r.GetByTokenHashAsync(tokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);

        var query = new ValidateInvitationTokenQuery(rawToken);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Email.Should().Be("old@example.com");
        result.DisplayName.Should().BeNull();
        result.ErrorReason.Should().BeNull();
    }

    [Fact]
    public async Task Handle_TokenNotFound_ReturnsInvalid()
    {
        // Arrange
        _invitationRepoMock
            .Setup(r => r.GetByTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((InvitationToken?)null);

        var query = new ValidateInvitationTokenQuery("nonexistent-token");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Email.Should().BeNull();
        result.DisplayName.Should().BeNull();
        result.ErrorReason.Should().Be("invalid");
    }

    [Fact]
    public async Task Handle_ExpiredToken_ReturnsInvalid_NotExpired()
    {
        // Arrange — security: expired tokens return "invalid", NOT "expired"
        var rawToken = "expired-token-123";
        var tokenHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

        var invitation = InvitationToken.Create("expired@example.com", "User", tokenHash, Guid.NewGuid());
        // Restore with past expiry
        invitation.RestoreState(
            email: "expired@example.com",
            role: "User",
            tokenHash: tokenHash,
            invitedByUserId: Guid.NewGuid(),
            status: InvitationStatus.Pending,
            expiresAt: DateTime.UtcNow.AddDays(-1),
            acceptedAt: null,
            acceptedByUserId: null,
            createdAt: DateTime.UtcNow.AddDays(-8));

        _invitationRepoMock
            .Setup(r => r.GetByTokenHashAsync(tokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);

        var query = new ValidateInvitationTokenQuery(rawToken);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Email.Should().BeNull();
        result.DisplayName.Should().BeNull();
        result.ErrorReason.Should().Be("invalid", "expired tokens must return 'invalid' to prevent state enumeration");
    }

    [Fact]
    public async Task Handle_RevokedToken_ReturnsInvalid_NotRevoked()
    {
        // Arrange — security: revoked tokens return "invalid", NOT "revoked"
        var rawToken = "revoked-token-123";
        var tokenHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

        var invitation = InvitationToken.Create("revoked@example.com", "User", tokenHash, Guid.NewGuid());
        invitation.RestoreState(
            email: "revoked@example.com",
            role: "User",
            tokenHash: tokenHash,
            invitedByUserId: Guid.NewGuid(),
            status: InvitationStatus.Revoked,
            expiresAt: DateTime.UtcNow.AddDays(7),
            acceptedAt: null,
            acceptedByUserId: null,
            createdAt: DateTime.UtcNow,
            revokedAt: DateTime.UtcNow);

        _invitationRepoMock
            .Setup(r => r.GetByTokenHashAsync(tokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);

        var query = new ValidateInvitationTokenQuery(rawToken);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Email.Should().BeNull();
        result.DisplayName.Should().BeNull();
        result.ErrorReason.Should().Be("invalid", "revoked tokens must return 'invalid' to prevent state enumeration");
    }

    [Fact]
    public async Task Handle_AcceptedToken_ReturnsAlreadyUsed()
    {
        // Arrange — "already_used" is acceptable disclosure to allow redirect to login
        var rawToken = "accepted-token-123";
        var tokenHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

        var invitation = InvitationToken.Create("accepted@example.com", "User", tokenHash, Guid.NewGuid());
        invitation.RestoreState(
            email: "accepted@example.com",
            role: "User",
            tokenHash: tokenHash,
            invitedByUserId: Guid.NewGuid(),
            status: InvitationStatus.Accepted,
            expiresAt: DateTime.UtcNow.AddDays(7),
            acceptedAt: DateTime.UtcNow.AddHours(-1),
            acceptedByUserId: Guid.NewGuid(),
            createdAt: DateTime.UtcNow.AddDays(-1));

        _invitationRepoMock
            .Setup(r => r.GetByTokenHashAsync(tokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);

        var query = new ValidateInvitationTokenQuery(rawToken);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Email.Should().BeNull();
        result.DisplayName.Should().BeNull();
        result.ErrorReason.Should().Be("already_used");
    }

    [Fact]
    public async Task Handle_EmptyToken_ReturnsInvalid()
    {
        // Arrange
        var query = new ValidateInvitationTokenQuery("   ");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorReason.Should().Be("invalid");
    }

    [Fact]
    public async Task Handle_InvalidToken_EmailAndDisplayNameAreNull()
    {
        // Arrange — when IsValid=false, personal data must NOT be leaked
        _invitationRepoMock
            .Setup(r => r.GetByTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((InvitationToken?)null);

        var query = new ValidateInvitationTokenQuery("some-token");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Email.Should().BeNull("personal data must not be leaked for invalid tokens");
        result.DisplayName.Should().BeNull("personal data must not be leaked for invalid tokens");
    }

    [Fact]
    public async Task Handle_StatusExpiredEnum_ReturnsInvalid()
    {
        // Arrange — invitation with Expired status enum (set by background service)
        var rawToken = "status-expired-token";
        var tokenHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

        var invitation = InvitationToken.Create("statusexp@example.com", "User", tokenHash, Guid.NewGuid());
        invitation.RestoreState(
            email: "statusexp@example.com",
            role: "User",
            tokenHash: tokenHash,
            invitedByUserId: Guid.NewGuid(),
            status: InvitationStatus.Expired,
            expiresAt: DateTime.UtcNow.AddDays(7), // ExpiresAt in future but status is Expired
            acceptedAt: null,
            acceptedByUserId: null,
            createdAt: DateTime.UtcNow.AddDays(-8));

        _invitationRepoMock
            .Setup(r => r.GetByTokenHashAsync(tokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);

        var query = new ValidateInvitationTokenQuery(rawToken);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorReason.Should().Be("invalid");
    }
}
