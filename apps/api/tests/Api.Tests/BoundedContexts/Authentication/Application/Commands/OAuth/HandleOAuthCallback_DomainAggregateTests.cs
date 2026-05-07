using System.Threading;
using Api.BoundedContexts.Authentication.Application.Commands;
using Api.BoundedContexts.Authentication.Application.Commands.OAuth;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Events;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Models;
using Api.Services;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ConflictException = Api.Middleware.Exceptions.ConflictException;

namespace Api.Tests.BoundedContexts.Authentication.Application.Commands.OAuth;

/// <summary>
/// Tests for C3+I8 — OAuth Domain Aggregate refactor.
/// Spec: docs/superpowers/specs/2026-05-06-auth-flow-security-fixes-design.md (C3 + I8).
///
/// C3 — OAuth callback was stamping a 32-byte random Base64 string into the
/// PasswordHash column via the now-removed <c>GenerateRandomPasswordHash</c>
/// helper. That string is not a valid PBKDF2 hash, leaving the account silently
/// non-recoverable through the standard ChangePassword flow.
///
/// I8 — OAuth callback created <c>UserEntity</c> rows directly, bypassing the
/// <see cref="User"/> aggregate's invariants, value objects, and domain events.
/// The fix introduces <c>User.CreateForOAuth</c> as the single authoritative
/// path for OAuth provisioning and emits <c>UserCreatedViaOAuthEvent</c>.
///
/// The aggregate-level cases below run as fast unit tests; the
/// callback-handler case mirrors the existing
/// <c>HandleOAuthCallbackCommandHandlerTests</c> setup using an in-memory
/// DbContext so we can assert end-to-end persistence semantics without a real
/// PostgreSQL container.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
public sealed class HandleOAuthCallback_DomainAggregateTests : IDisposable
{
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    private readonly Mock<IOAuthService> _oauthServiceMock;
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<ILogger<HandleOAuthCallbackCommandHandler>> _loggerMock;
    private readonly Mock<IEncryptionService> _encryptionServiceMock;
    private readonly Mock<TimeProvider> _timeProviderMock;
    private readonly MeepleAiDbContext _dbContext;
    private readonly HandleOAuthCallbackCommandHandler _handler;

    public HandleOAuthCallback_DomainAggregateTests()
    {
        _oauthServiceMock = new Mock<IOAuthService>();
        _mediatorMock = new Mock<IMediator>();
        _loggerMock = new Mock<ILogger<HandleOAuthCallbackCommandHandler>>();
        _encryptionServiceMock = new Mock<IEncryptionService>();
        _timeProviderMock = new Mock<TimeProvider>();
        _timeProviderMock.Setup(t => t.GetUtcNow()).Returns(DateTimeOffset.UtcNow);

        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();

        _handler = new HandleOAuthCallbackCommandHandler(
            _oauthServiceMock.Object,
            _mediatorMock.Object,
            _loggerMock.Object,
            _encryptionServiceMock.Object,
            _timeProviderMock.Object,
            _dbContext);
    }

    public void Dispose() => _dbContext.Dispose();

    // -------------------------------------------------------------------------
    // User aggregate — CreateForOAuth invariants (regression guards)
    // -------------------------------------------------------------------------

    [Fact]
    public void CreateForOAuth_ProducesUserWithNullPasswordHash()
    {
        // C3 invariant: OAuth-only users have no local password — null is the
        // canonical marker, not a random Base64 placeholder.
        var user = User.CreateForOAuth(
            id: Guid.NewGuid(),
            email: new Email("oauth@test.local"),
            displayName: "OAuth User",
            role: Role.User,
            tier: null,
            oauthProvider: "google",
            timeProvider: TimeProvider.System);

        user.PasswordHash.Should().BeNull(
            "OAuth-only users must have null PasswordHash; the legacy 32-byte " +
            "random Base64 string is removed by C3.");
    }

    [Fact]
    public void CreateForOAuth_HasActiveStatusAndVerifiedEmail()
    {
        var user = User.CreateForOAuth(
            id: Guid.NewGuid(),
            email: new Email("oauth@test.local"),
            displayName: "OAuth User",
            role: Role.User,
            tier: UserTier.Free,
            oauthProvider: "github",
            timeProvider: TimeProvider.System);

        user.Status.Should().Be(Api.SharedKernel.Domain.Enums.UserAccountStatus.Active,
            "OAuth provisioning yields an immediately-active account.");
        user.EmailVerified.Should().BeTrue(
            "the OAuth provider has already verified the address; we must not " +
            "subject these accounts to a verification grace period.");
        user.EmailVerifiedAt.Should().NotBeNull();
        user.VerificationGracePeriodEndsAt.Should().BeNull();
    }

    [Fact]
    public void CreateForOAuth_EmitsUserCreatedViaOAuthEvent()
    {
        var userId = Guid.NewGuid();
        var email = new Email("oauth@test.local");

        var user = User.CreateForOAuth(
            id: userId,
            email: email,
            displayName: "OAuth User",
            role: Role.User,
            tier: null,
            oauthProvider: "discord",
            timeProvider: TimeProvider.System);

        var domainEvent = user.DomainEvents
            .OfType<UserCreatedViaOAuthEvent>()
            .Should().ContainSingle("CreateForOAuth must emit exactly one " +
                "UserCreatedViaOAuthEvent so listeners can distinguish OAuth-provisioned " +
                "accounts from /auth/register accounts.")
            .Subject;

        domainEvent.UserId.Should().Be(userId);
        domainEvent.Email.Should().Be(email.Value);
        domainEvent.Provider.Should().Be("discord");
    }

    // -------------------------------------------------------------------------
    // User aggregate — ChangePassword guard for OAuth-only users (RED pre-fix)
    // -------------------------------------------------------------------------

    [Fact]
    public void OAuthOnlyUser_ChangePassword_ThrowsConflictException()
    {
        // Pre-fix the User aggregate has no null-PasswordHash short-circuit, so
        // ChangePassword falls through to VerifyPassword (null-safe → returns false)
        // and surfaces a misleading "Current password is incorrect" DomainException.
        // Post-fix it must surface a 409 ConflictException with a clear,
        // actionable message that does not lie about the cause.
        var oauthUser = User.CreateForOAuth(
            id: Guid.NewGuid(),
            email: new Email("oauth@test.local"),
            displayName: "OAuth User",
            role: Role.User,
            tier: null,
            oauthProvider: "google",
            timeProvider: TimeProvider.System);

        var newHash = PasswordHash.Create("BrandNewPassword123!");

        var act = () => oauthUser.ChangePassword("anything", newHash);

        act.Should().Throw<ConflictException>()
            .WithMessage("*OAuth-only*",
                "ChangePassword on a user with null PasswordHash must surface a " +
                "ConflictException explaining the OAuth-only state — not the " +
                "generic 'Current password is incorrect' DomainException.");
    }

    // -------------------------------------------------------------------------
    // OAuth callback handler — persistence contract (RED pre-fix)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task OAuthCallback_NewUser_PersistedWithNullPasswordHash()
    {
        // Pre-fix the handler stamps PasswordHash with a 32-byte random Base64
        // string via the now-removed GenerateRandomPasswordHash helper, so this
        // assertion FAILS. Post-fix the handler routes through
        // User.CreateForOAuth which sets PasswordHash = null.
        const string newEmail = "oauth-new-user@test.local";
        var command = CreateTestCommand("google");
        var tokenResponse = CreateTokenResponse();
        var userInfo = new OAuthUserInfo("provider_user_456", newEmail, "OAuth New User");

        SetupSuccessfulOAuthFlow(command, tokenResponse, userInfo);

        _encryptionServiceMock
            .Setup(e => e.EncryptAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("encrypted_token");

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreateSessionResponse(
                User: new Api.BoundedContexts.Authentication.Application.DTOs.UserDto(
                    Id: Guid.NewGuid(),
                    Email: newEmail,
                    DisplayName: "OAuth New User",
                    Role: "User",
                    Tier: "free",
                    CreatedAt: DateTime.UtcNow,
                    IsTwoFactorEnabled: false,
                    TwoFactorEnabledAt: null,
                    Level: 1,
                    ExperiencePoints: 0),
                SessionToken: "session_token",
                ExpiresAt: DateTime.UtcNow.AddHours(24)));

        var result = await _handler.Handle(command, TestCancellationToken);

        result.Success.Should().BeTrue();
        result.IsNewUser.Should().BeTrue();

        var persisted = await _dbContext.Users
            .AsNoTracking()
            .SingleAsync(u => u.Email == newEmail.ToLowerInvariant(), TestCancellationToken);

        persisted.PasswordHash.Should().BeNull(
            "C3+I8: OAuth callback must persist new users with null PasswordHash. " +
            "A non-null value indicates the legacy GenerateRandomPasswordHash path " +
            "is still in effect.");
    }

    // -------------------------------------------------------------------------
    // Helpers (mirrors HandleOAuthCallbackCommandHandlerTests setup style)
    // -------------------------------------------------------------------------

    private static HandleOAuthCallbackCommand CreateTestCommand(string provider) =>
        new()
        {
            Provider = provider,
            Code = "auth_code_12345",
            State = "csrf_state_token",
            IpAddress = "127.0.0.1",
            UserAgent = "TestAgent/1.0"
        };

    private static OAuthTokenResponse CreateTokenResponse() =>
        new(
            AccessToken: "access_token_12345",
            RefreshToken: "refresh_token_12345",
            ExpiresIn: 3600,
            TokenType: "Bearer");

    private void SetupSuccessfulOAuthFlow(
        HandleOAuthCallbackCommand command,
        OAuthTokenResponse tokenResponse,
        OAuthUserInfo userInfo)
    {
        _oauthServiceMock
            .Setup(s => s.ValidateStateAsync(command.State))
            .ReturnsAsync(true);

        _oauthServiceMock
            .Setup(s => s.ExchangeCodeForTokenAsync(command.Provider, command.Code))
            .ReturnsAsync(tokenResponse);

        _oauthServiceMock
            .Setup(s => s.GetUserInfoAsync(command.Provider, tokenResponse.AccessToken))
            .ReturnsAsync(userInfo);
    }
}
