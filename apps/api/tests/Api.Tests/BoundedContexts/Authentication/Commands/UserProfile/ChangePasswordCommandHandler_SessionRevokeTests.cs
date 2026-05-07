using Api.BoundedContexts.Authentication.Application.Commands;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Commands.UserProfile;

/// <summary>
/// Tests for the C7 fix on <see cref="ChangePasswordCommandHandler"/>.
/// Spec: docs/superpowers/specs/2026-05-06-auth-flow-security-fixes-design.md (C7).
///
/// Pre-fix the handler updated the password and called it a day. Any other
/// device that had previously logged in with the OLD password kept its
/// session cookie alive — so changing the password did NOT invalidate
/// sessions that an attacker may have stolen. C7 closes that gap by
/// revoking every other session for the user atomically with the password
/// update, while preserving the *current* session so the user isn't
/// kicked out of the device they just changed the password from.
///
/// A "logout everywhere" opt-in flag (<c>IncludeCurrentInRevoke</c>) covers
/// the "I think I'm compromised" UX where the user wants to invalidate
/// every session including the one they're holding.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
public sealed class ChangePasswordCommandHandler_SessionRevokeTests
{
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<ISessionRepository> _sessionRepo = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly ChangePasswordCommandHandler _handler;

    public ChangePasswordCommandHandler_SessionRevokeTests()
    {
        _handler = new ChangePasswordCommandHandler(
            _userRepo.Object,
            _sessionRepo.Object,
            _uow.Object);
    }

    [Fact]
    public async Task ChangePassword_RevokesOtherSessions_KeepsCurrent()
    {
        // Arrange — one user with three "live" sessions (A, B, C); A is the
        // session the user is currently signed in with and is initiating the
        // password change.
        var (user, currentPassword) = SeedUserWithPassword();
        var currentSessionId = Guid.NewGuid();
        _userRepo.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var command = new ChangePasswordCommand
        {
            UserId = user.Id,
            CurrentPassword = currentPassword,
            NewPassword = "BrandNewUnusualPwd123!",
            CurrentSessionId = currentSessionId,
            IncludeCurrentInRevoke = false,
        };

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert — the "except current" path must be taken.
        _sessionRepo.Verify(
            r => r.RevokeAllUserSessionsExceptAsync(
                user.Id,
                currentSessionId,
                It.IsAny<CancellationToken>()),
            Times.Once,
            "C7: a normal password change must revoke every other session for " +
            "the user, but keep the *current* one alive so the user isn't " +
            "logged out of the device they just changed the password from.");

        _sessionRepo.Verify(
            r => r.RevokeAllUserSessionsAsync(user.Id, It.IsAny<CancellationToken>()),
            Times.Never,
            "the unconditional revoke-all path must NOT fire when " +
            "IncludeCurrentInRevoke=false.");
    }

    [Fact]
    public async Task ChangePassword_WithIncludeCurrentInRevoke_RevokesAllSessions()
    {
        // Arrange — same setup, but the user opts into "logout everywhere"
        // (e.g. they suspect their account is compromised).
        var (user, currentPassword) = SeedUserWithPassword();
        _userRepo.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var command = new ChangePasswordCommand
        {
            UserId = user.Id,
            CurrentPassword = currentPassword,
            NewPassword = "BrandNewUnusualPwd123!",
            CurrentSessionId = Guid.NewGuid(),
            IncludeCurrentInRevoke = true,
        };

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert — the unconditional revoke-all path is taken; the
        // current-session exclusion is intentionally ignored.
        _sessionRepo.Verify(
            r => r.RevokeAllUserSessionsAsync(user.Id, It.IsAny<CancellationToken>()),
            Times.Once);
        _sessionRepo.Verify(
            r => r.RevokeAllUserSessionsExceptAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ChangePassword_WithoutCurrentSessionId_RevokesAllSessions()
    {
        // Arrange — change-password reached the handler without a known
        // current session (defensive: e.g. service-to-service password reset
        // flow). Default to the safer "revoke everything" path so we never
        // leave a stale session alive that the caller couldn't pin down.
        var (user, currentPassword) = SeedUserWithPassword();
        _userRepo.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var command = new ChangePasswordCommand
        {
            UserId = user.Id,
            CurrentPassword = currentPassword,
            NewPassword = "BrandNewUnusualPwd123!",
            CurrentSessionId = null,
            IncludeCurrentInRevoke = false,
        };

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert — fallback to revoke-all when we can't identify the current
        // session: better to over-revoke than to leak.
        _sessionRepo.Verify(
            r => r.RevokeAllUserSessionsAsync(user.Id, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static (User user, string currentPassword) SeedUserWithPassword()
    {
        const string password = "OldUnusualPwd123!";
        var user = new User(
            id: Guid.NewGuid(),
            email: new Email("user@test.local"),
            displayName: "Test User",
            passwordHash: PasswordHash.Create(password),
            role: Role.User,
            tier: UserTier.Free);
        return (user, password);
    }
}
