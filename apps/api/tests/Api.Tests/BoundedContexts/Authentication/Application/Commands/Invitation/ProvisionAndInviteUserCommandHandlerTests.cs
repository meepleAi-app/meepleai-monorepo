using Api.BoundedContexts.Authentication.Application.Commands.Invitation;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Middleware.Exceptions;
using Api.Services;
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
public class ProvisionAndInviteUserCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly Mock<IInvitationTokenRepository> _invitationRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IEmailService> _emailServiceMock = new();
    private readonly FakeTimeProvider _timeProvider;
    private readonly Mock<ILogger<ProvisionAndInviteUserCommandHandler>> _loggerMock = new();
    private readonly ProvisionAndInviteUserCommandHandler _handler;

    public ProvisionAndInviteUserCommandHandlerTests()
    {
        _timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 3, 17, 12, 0, 0, TimeSpan.Zero));

        _handler = new ProvisionAndInviteUserCommandHandler(
            _userRepoMock.Object,
            _invitationRepoMock.Object,
            _unitOfWorkMock.Object,
            _emailServiceMock.Object,
            _timeProvider,
            _loggerMock.Object);
    }

    private static ProvisionAndInviteUserCommand CreateValidCommand(
        string email = "newuser@example.com",
        string displayName = "New User",
        string role = "User",
        string tier = "Free",
        string? customMessage = null,
        int expiresInDays = 7,
        List<GameSuggestionDto>? gameSuggestions = null,
        Guid? invitedByUserId = null)
    {
        return new ProvisionAndInviteUserCommand(
            Email: email,
            DisplayName: displayName,
            Role: role,
            Tier: tier,
            CustomMessage: customMessage,
            ExpiresInDays: expiresInDays,
            GameSuggestions: gameSuggestions ?? new List<GameSuggestionDto>(),
            InvitedByUserId: invitedByUserId ?? Guid.NewGuid());
    }

    private void SetupNoConflicts()
    {
        _userRepoMock
            .Setup(r => r.ExistsByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _invitationRepoMock
            .Setup(r => r.GetPendingByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((InvitationToken?)null);
    }

    [Fact]
    public async Task Handle_WithValidData_CreatesPendingUserAndTokenAndReturnsDto()
    {
        // Arrange
        SetupNoConflicts();
        var command = CreateValidCommand(
            email: "alice@example.com",
            displayName: "Alice",
            role: "Editor",
            tier: "Premium",
            customMessage: "Welcome aboard!",
            expiresInDays: 14);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Email.Should().Be("alice@example.com");
        result.Role.Should().Be("editor"); // Role.Parse normalizes to lowercase
        result.Status.Should().Be("Pending");
        result.EmailSent.Should().BeTrue();
        result.ExpiresAt.Should().BeCloseTo(
            _timeProvider.GetUtcNow().UtcDateTime.AddDays(14),
            TimeSpan.FromSeconds(1));

        _userRepoMock.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
        _invitationRepoMock.Verify(r => r.AddAsync(It.IsAny<InvitationToken>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _emailServiceMock.Verify(e => e.SendInvitationEmailAsync(
            "alice@example.com",
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithExistingUser_ThrowsConflictException()
    {
        // Arrange
        var command = CreateValidCommand(email: "existing@example.com");

        _userRepoMock
            .Setup(r => r.ExistsByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => _handler.Handle(command, CancellationToken.None));

        ex.Message.Should().Contain("existing@example.com");

        // Should NOT create any entities
        _userRepoMock.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _invitationRepoMock.Verify(r => r.AddAsync(It.IsAny<InvitationToken>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithPendingInvitation_ThrowsConflictException()
    {
        // Arrange
        var command = CreateValidCommand(email: "pending@example.com");

        _userRepoMock
            .Setup(r => r.ExistsByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var existingInvitation = InvitationToken.Create("pending@example.com", "User", "somehash", Guid.NewGuid());
        _invitationRepoMock
            .Setup(r => r.GetPendingByEmailAsync("pending@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingInvitation);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => _handler.Handle(command, CancellationToken.None));

        ex.Message.Should().Contain("pending@example.com");

        // Should NOT create any entities
        _userRepoMock.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _invitationRepoMock.Verify(r => r.AddAsync(It.IsAny<InvitationToken>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenEmailFails_ReturnsEmailSentFalseAndStillCreatesEntities()
    {
        // Arrange
        SetupNoConflicts();
        var command = CreateValidCommand();

        _emailServiceMock
            .Setup(e => e.SendInvitationEmailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SMTP connection failed"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.EmailSent.Should().BeFalse();

        // Entities should still be created and saved
        _userRepoMock.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
        _invitationRepoMock.Verify(r => r.AddAsync(It.IsAny<InvitationToken>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithGameSuggestions_AttachesSuggestionsToToken()
    {
        // Arrange
        SetupNoConflicts();
        var gameId1 = Guid.NewGuid();
        var gameId2 = Guid.NewGuid();
        var suggestions = new List<GameSuggestionDto>
        {
            new(gameId1, "PreAdded"),
            new(gameId2, "Suggested")
        };
        var command = CreateValidCommand(gameSuggestions: suggestions);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.GameSuggestions.Should().NotBeNull();
        result.GameSuggestions.Should().HaveCount(2);
        result.GameSuggestions!.Should().Contain(gs => gs.GameId == gameId1 && gs.Type == "PreAdded");
        result.GameSuggestions!.Should().Contain(gs => gs.GameId == gameId2 && gs.Type == "Suggested");
    }

    [Fact]
    public async Task Handle_WithNoGameSuggestions_ReturnsEmptyList()
    {
        // Arrange
        SetupNoConflicts();
        var command = CreateValidCommand(gameSuggestions: new List<GameSuggestionDto>());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.GameSuggestions.Should().NotBeNull();
        result.GameSuggestions.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_NormalizesEmailToLowercase()
    {
        // Arrange
        SetupNoConflicts();
        var command = CreateValidCommand(email: "TEST@EXAMPLE.COM");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Email.Should().Be("test@example.com");
    }

    [Fact]
    public async Task Handle_SavesBeforeSendingEmail()
    {
        // Arrange
        SetupNoConflicts();
        var command = CreateValidCommand();

        var callOrder = new List<string>();

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("SaveChanges"))
            .ReturnsAsync(1);

        _emailServiceMock
            .Setup(e => e.SendInvitationEmailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("SendEmail"))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert — SaveChanges must happen before SendEmail
        callOrder.Should().ContainInOrder("SaveChanges", "SendEmail");
    }

    [Fact]
    public async Task Handle_WithCustomMessage_PassesItThrough()
    {
        // Arrange
        SetupNoConflicts();
        var command = CreateValidCommand(customMessage: "Welcome to MeepleAI!");

        InvitationToken? capturedToken = null;
        _invitationRepoMock
            .Setup(r => r.AddAsync(It.IsAny<InvitationToken>(), It.IsAny<CancellationToken>()))
            .Callback<InvitationToken, CancellationToken>((token, _) => capturedToken = token)
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        capturedToken.Should().NotBeNull();
        capturedToken!.CustomMessage.Should().Be("Welcome to MeepleAI!");
    }

    [Fact]
    public async Task Handle_CreatesPendingUserWithCorrectProperties()
    {
        // Arrange
        SetupNoConflicts();
        var adminId = Guid.NewGuid();
        var command = CreateValidCommand(
            email: "bob@example.com",
            displayName: "Bob Smith",
            role: "Admin",
            tier: "Pro",
            expiresInDays: 10,
            invitedByUserId: adminId);

        User? capturedUser = null;
        _userRepoMock
            .Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((user, _) => capturedUser = user)
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        capturedUser.Should().NotBeNull();
        capturedUser!.Email.Value.Should().Be("bob@example.com");
        capturedUser.DisplayName.Should().Be("Bob Smith");
        capturedUser.Role.Value.Should().Be("admin");
        capturedUser.Tier.Value.Should().Be("pro");
        capturedUser.InvitedByUserId.Should().Be(adminId);
        capturedUser.Status.Should().Be(Api.SharedKernel.Domain.Enums.UserAccountStatus.Pending);
    }
}
