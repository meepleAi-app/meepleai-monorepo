using System.Security.Cryptography;
using System.Text;
using Api.BoundedContexts.Authentication.Application.Commands.Invitation;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Enums;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.Authentication.Infrastructure.Services;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Domain.Enums;
using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application.Commands.Invitation;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
public class ActivateInvitedAccountCommandHandlerTests
{
    private readonly Mock<IInvitationTokenRepository> _invitationRepoMock = new();
    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly Mock<ISessionRepository> _sessionRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly GameSuggestionChannel _gameSuggestionChannel = new();
    private readonly FakeTimeProvider _timeProvider;
    private readonly Mock<ILogger<ActivateInvitedAccountCommandHandler>> _loggerMock = new();
    private readonly ActivateInvitedAccountCommandHandler _handler;

    private static readonly string ValidPassword = "Test@1234";
    private static readonly string RawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        .Replace('+', '-').Replace('/', '_').TrimEnd('=');

    public ActivateInvitedAccountCommandHandlerTests()
    {
        _timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 3, 17, 12, 0, 0, TimeSpan.Zero));

        _handler = new ActivateInvitedAccountCommandHandler(
            _invitationRepoMock.Object,
            _userRepoMock.Object,
            _sessionRepoMock.Object,
            _unitOfWorkMock.Object,
            _gameSuggestionChannel,
            _timeProvider,
            _loggerMock.Object);
    }

    #region Helpers

    private static string ComputeTokenHash(string rawToken)
    {
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
    }

    /// <summary>
    /// Creates a pending user via the domain factory method so all invariants are satisfied.
    /// </summary>
    private User CreatePendingUser(
        Guid? userId = null,
        string email = "invited@example.com",
        Guid? invitedByUserId = null)
    {
        var id = userId ?? Guid.NewGuid();
        var adminId = invitedByUserId ?? Guid.NewGuid();
        var expiresAt = _timeProvider.GetUtcNow().UtcDateTime.AddDays(7);

        var user = User.CreatePending(
            new Email(email),
            "Invited User",
            Role.Parse("User"),
            UserTier.Free,
            adminId,
            expiresAt,
            _timeProvider);

        // Overwrite the Id via reflection since CreatePending generates its own
        // We need a controlled Id to match invitation.PendingUserId
        var idProp = typeof(User).GetProperty("Id");
        if (idProp != null && idProp.CanWrite)
        {
            idProp.SetValue(user, id);
        }
        else
        {
            // Fall back — AggregateRoot<Guid>.Id may have a protected setter
            var field = typeof(User).BaseType?.BaseType?.GetField("<Id>k__BackingField",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            field?.SetValue(user, id);
        }

        return user;
    }

    /// <summary>
    /// Creates a valid InvitationToken with PendingUserId set.
    /// Uses the extended Create overload (TimeProvider-based).
    /// </summary>
    private InvitationToken CreateValidInvitation(
        Guid pendingUserId,
        string email = "invited@example.com",
        Guid? invitedByUserId = null,
        bool addGameSuggestions = false)
    {
        var tokenHash = ComputeTokenHash(RawToken);
        var invitation = InvitationToken.Create(
            new Email(email),
            Role.Parse("User"),
            pendingUserId,
            null,
            7,
            _timeProvider,
            tokenHash);

        // Set InvitedByUserId (the extended Create sets it to Guid.Empty)
        if (invitedByUserId.HasValue)
        {
            var prop = typeof(InvitationToken).GetProperty("InvitedByUserId");
            prop?.SetValue(invitation, invitedByUserId.Value);
        }

        if (addGameSuggestions)
        {
            invitation.AddGameSuggestion(Guid.NewGuid(), GameSuggestionType.PreAdded);
            invitation.AddGameSuggestion(Guid.NewGuid(), GameSuggestionType.Suggested);
        }

        return invitation;
    }

    private void SetupValidTokenLookup(InvitationToken invitation)
    {
        var tokenHash = ComputeTokenHash(RawToken);
        _invitationRepoMock
            .Setup(r => r.GetByTokenHashAsync(tokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);
    }

    private void SetupUserLookup(User user)
    {
        _userRepoMock
            .Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
    }

    #endregion

    #region Happy Path

    [Fact]
    public async Task Handle_ValidTokenAndPassword_ActivatesUserAndReturnsSessionToken()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var user = CreatePendingUser(userId: userId, invitedByUserId: adminId);
        var invitation = CreateValidInvitation(userId, invitedByUserId: adminId);

        SetupValidTokenLookup(invitation);
        SetupUserLookup(user);

        var command = new ActivateInvitedAccountCommand(RawToken, ValidPassword);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.SessionToken.Should().NotBeNullOrWhiteSpace();
        result.RequiresOnboarding.Should().BeTrue();

        // User should have been activated
        user.Status.Should().Be(UserAccountStatus.Active);
        user.EmailVerified.Should().BeTrue();

        // Invitation should be marked accepted
        invitation.Status.Should().Be(InvitationStatus.Accepted);
        invitation.AcceptedByUserId.Should().Be(userId);

        // Session should have been added
        _sessionRepoMock.Verify(
            r => r.AddAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()),
            Times.Once);

        // SaveChanges called
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ValidTokenAndPassword_PasswordIsVerifiable()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreatePendingUser(userId: userId);
        var invitation = CreateValidInvitation(userId);

        SetupValidTokenLookup(invitation);
        SetupUserLookup(user);

        var command = new ActivateInvitedAccountCommand(RawToken, ValidPassword);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert — password should be verifiable after activation
        user.VerifyPassword(ValidPassword).Should().BeTrue();
        user.VerifyPassword("WrongPassword1!").Should().BeFalse();
    }

    #endregion

    #region Invalid / Expired Token

    [Fact]
    public async Task Handle_TokenNotFound_ThrowsNotFoundException()
    {
        // Arrange
        _invitationRepoMock
            .Setup(r => r.GetByTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((InvitationToken?)null);

        var command = new ActivateInvitedAccountCommand("nonexistent-token", ValidPassword);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_ExpiredToken_ThrowsNotFoundException()
    {
        // Arrange — create token then advance time past expiry
        var userId = Guid.NewGuid();
        var invitation = CreateValidInvitation(userId);

        // Advance time by 8 days (token expires in 7)
        _timeProvider.Advance(TimeSpan.FromDays(8));

        SetupValidTokenLookup(invitation);

        var command = new ActivateInvitedAccountCommand(RawToken, ValidPassword);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    #endregion

    #region Already-Used Token

    [Fact]
    public async Task Handle_AlreadyAcceptedToken_ThrowsNotFoundException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var invitation = CreateValidInvitation(userId);
        invitation.MarkAccepted(userId); // Already accepted

        SetupValidTokenLookup(invitation);

        var command = new ActivateInvitedAccountCommand(RawToken, ValidPassword);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert — IsValid will be false since Status != Pending
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_RevokedToken_ThrowsNotFoundException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var invitation = CreateValidInvitation(userId);
        invitation.Revoke(); // Revoked

        SetupValidTokenLookup(invitation);

        var command = new ActivateInvitedAccountCommand(RawToken, ValidPassword);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    #endregion

    #region User Not Pending

    [Fact]
    public async Task Handle_UserAlreadyActive_ThrowsConflictException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var user = CreatePendingUser(userId: userId, invitedByUserId: adminId);

        // Activate the user first to make them non-Pending
        user.ActivateFromInvitation(PasswordHash.Create("OldPass@123"), _timeProvider);

        var invitation = CreateValidInvitation(userId, invitedByUserId: adminId);
        SetupValidTokenLookup(invitation);
        SetupUserLookup(user);

        var command = new ActivateInvitedAccountCommand(RawToken, ValidPassword);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*not in Pending status*");
    }

    [Fact]
    public async Task Handle_UserNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var invitation = CreateValidInvitation(userId);
        SetupValidTokenLookup(invitation);

        _userRepoMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var command = new ActivateInvitedAccountCommand(RawToken, ValidPassword);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    #endregion

    #region Game Suggestion Channel

    [Fact]
    public async Task Handle_WithGameSuggestions_WritesToChannel()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var user = CreatePendingUser(userId: userId, invitedByUserId: adminId);
        var invitation = CreateValidInvitation(userId, invitedByUserId: adminId, addGameSuggestions: true);

        SetupValidTokenLookup(invitation);
        SetupUserLookup(user);

        var command = new ActivateInvitedAccountCommand(RawToken, ValidPassword);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert — activation still succeeds
        result.Should().NotBeNull();
        result.SessionToken.Should().NotBeNullOrWhiteSpace();

        // Channel should have one event
        _gameSuggestionChannel.Reader.TryRead(out var evt).Should().BeTrue();
        evt.Should().NotBeNull();
        evt!.UserId.Should().Be(userId);
        evt.InvitedByUserId.Should().Be(adminId);
        evt.Suggestions.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_WithoutGameSuggestions_DoesNotWriteToChannel()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var user = CreatePendingUser(userId: userId, invitedByUserId: adminId);
        var invitation = CreateValidInvitation(userId, invitedByUserId: adminId, addGameSuggestions: false);

        SetupValidTokenLookup(invitation);
        SetupUserLookup(user);

        var command = new ActivateInvitedAccountCommand(RawToken, ValidPassword);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert — channel should be empty
        _gameSuggestionChannel.Reader.TryRead(out _).Should().BeFalse();
    }

    #endregion

    #region Null Command

    [Fact]
    public async Task Handle_NullCommand_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _handler.Handle(null!, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region Constructor Null Guards

    [Fact]
    public void Constructor_NullInvitationRepo_ThrowsArgumentNullException()
    {
        var act = () => new ActivateInvitedAccountCommandHandler(
            null!, _userRepoMock.Object, _sessionRepoMock.Object,
            _unitOfWorkMock.Object, _gameSuggestionChannel, _timeProvider, _loggerMock.Object);

        act.Should().Throw<ArgumentNullException>().WithParameterName("invitationRepo");
    }

    [Fact]
    public void Constructor_NullUserRepo_ThrowsArgumentNullException()
    {
        var act = () => new ActivateInvitedAccountCommandHandler(
            _invitationRepoMock.Object, null!, _sessionRepoMock.Object,
            _unitOfWorkMock.Object, _gameSuggestionChannel, _timeProvider, _loggerMock.Object);

        act.Should().Throw<ArgumentNullException>().WithParameterName("userRepo");
    }

    [Fact]
    public void Constructor_NullSessionRepo_ThrowsArgumentNullException()
    {
        var act = () => new ActivateInvitedAccountCommandHandler(
            _invitationRepoMock.Object, _userRepoMock.Object, null!,
            _unitOfWorkMock.Object, _gameSuggestionChannel, _timeProvider, _loggerMock.Object);

        act.Should().Throw<ArgumentNullException>().WithParameterName("sessionRepo");
    }

    [Fact]
    public void Constructor_NullUnitOfWork_ThrowsArgumentNullException()
    {
        var act = () => new ActivateInvitedAccountCommandHandler(
            _invitationRepoMock.Object, _userRepoMock.Object, _sessionRepoMock.Object,
            null!, _gameSuggestionChannel, _timeProvider, _loggerMock.Object);

        act.Should().Throw<ArgumentNullException>().WithParameterName("unitOfWork");
    }

    [Fact]
    public void Constructor_NullChannel_ThrowsArgumentNullException()
    {
        var act = () => new ActivateInvitedAccountCommandHandler(
            _invitationRepoMock.Object, _userRepoMock.Object, _sessionRepoMock.Object,
            _unitOfWorkMock.Object, null!, _timeProvider, _loggerMock.Object);

        act.Should().Throw<ArgumentNullException>().WithParameterName("gameSuggestionChannel");
    }

    [Fact]
    public void Constructor_NullTimeProvider_ThrowsArgumentNullException()
    {
        var act = () => new ActivateInvitedAccountCommandHandler(
            _invitationRepoMock.Object, _userRepoMock.Object, _sessionRepoMock.Object,
            _unitOfWorkMock.Object, _gameSuggestionChannel, null!, _loggerMock.Object);

        act.Should().Throw<ArgumentNullException>().WithParameterName("timeProvider");
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        var act = () => new ActivateInvitedAccountCommandHandler(
            _invitationRepoMock.Object, _userRepoMock.Object, _sessionRepoMock.Object,
            _unitOfWorkMock.Object, _gameSuggestionChannel, _timeProvider, null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    #endregion
}
