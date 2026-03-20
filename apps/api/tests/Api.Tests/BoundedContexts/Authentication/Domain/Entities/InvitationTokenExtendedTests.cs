using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Enums;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.ValueObjects;
using Microsoft.Extensions.Time.Testing;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.Authentication.Domain.Entities;

/// <summary>
/// Tests for InvitationToken extended functionality: CustomMessage, PendingUserId,
/// GameSuggestions collection, and TimeProvider-based factory method.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "Authentication")]
public sealed class InvitationTokenExtendedTests
{
    private readonly FakeTimeProvider _timeProvider = new(
        new DateTimeOffset(2026, 3, 17, 12, 0, 0, TimeSpan.Zero));

    private static readonly Email TestEmail = Email.Parse("invitee@example.com");
    private static readonly Role TestRole = Role.User;
    private static readonly Guid TestPendingUserId = Guid.NewGuid();
    private const string TestTokenHash = "abc123hash";
    private const string TestCustomMessage = "Welcome to MeepleAI! We have some games ready for you.";

    #region Create with TimeProvider Tests

    [Fact]
    public void Create_WithTimeProvider_SetsCorrectExpiry()
    {
        // Act
        var token = InvitationToken.Create(
            TestEmail,
            TestRole,
            TestPendingUserId,
            TestCustomMessage,
            expiresInDays: 14,
            _timeProvider,
            TestTokenHash);

        // Assert
        var expectedExpiry = new DateTime(2026, 3, 31, 12, 0, 0, DateTimeKind.Utc);
        token.ExpiresAt.Should().Be(expectedExpiry);
        token.CreatedAt.Should().Be(_timeProvider.GetUtcNow().UtcDateTime);
    }

    [Fact]
    public void Create_WithTimeProvider_SetsCustomMessage()
    {
        // Act
        var token = InvitationToken.Create(
            TestEmail,
            TestRole,
            TestPendingUserId,
            TestCustomMessage,
            expiresInDays: 7,
            _timeProvider,
            TestTokenHash);

        // Assert
        token.CustomMessage.Should().Be(TestCustomMessage);
    }

    [Fact]
    public void Create_WithTimeProvider_SetsPendingUserId()
    {
        // Act
        var token = InvitationToken.Create(
            TestEmail,
            TestRole,
            TestPendingUserId,
            TestCustomMessage,
            expiresInDays: 7,
            _timeProvider,
            TestTokenHash);

        // Assert
        token.PendingUserId.Should().Be(TestPendingUserId);
    }

    [Fact]
    public void Create_WithTimeProvider_SetsBasicProperties()
    {
        // Act
        var token = InvitationToken.Create(
            TestEmail,
            TestRole,
            TestPendingUserId,
            TestCustomMessage,
            expiresInDays: 7,
            _timeProvider,
            TestTokenHash);

        // Assert
        token.Email.Should().Be(TestEmail.Value);
        token.Role.Should().Be(TestRole.Value);
        token.TokenHash.Should().Be(TestTokenHash);
        token.Status.Should().Be(InvitationStatus.Pending);
        token.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Create_WithTimeProvider_NullCustomMessage_Allowed()
    {
        // Act
        var token = InvitationToken.Create(
            TestEmail,
            TestRole,
            TestPendingUserId,
            customMessage: null,
            expiresInDays: 7,
            _timeProvider,
            TestTokenHash);

        // Assert
        token.CustomMessage.Should().BeNull();
    }

    [Fact]
    public void Create_WithTimeProvider_EmptyTokenHash_Throws()
    {
        // Act & Assert
        var act = () =>
            InvitationToken.Create(
                TestEmail,
                TestRole,
                TestPendingUserId,
                TestCustomMessage,
                expiresInDays: 7,
                _timeProvider,
                tokenHash: "");
act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithTimeProvider_EmptyPendingUserId_Throws()
    {
        // Act & Assert
        var act = () =>
            InvitationToken.Create(
                TestEmail,
                TestRole,
                pendingUserId: Guid.Empty,
                TestCustomMessage,
                expiresInDays: 7,
                _timeProvider,
                TestTokenHash);
act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region GameSuggestions Tests

    [Fact]
    public void AddGameSuggestion_AttachesSuggestion()
    {
        // Arrange
        var token = InvitationToken.Create(
            TestEmail,
            TestRole,
            TestPendingUserId,
            TestCustomMessage,
            expiresInDays: 7,
            _timeProvider,
            TestTokenHash);

        var gameId = Guid.NewGuid();

        // Act
        token.AddGameSuggestion(gameId, GameSuggestionType.PreAdded);

        // Assert
        token.GameSuggestions.Should().ContainSingle();
        token.GameSuggestions[0].GameId.Should().Be(gameId);
        token.GameSuggestions[0].Type.Should().Be(GameSuggestionType.PreAdded);
        token.GameSuggestions[0].InvitationTokenId.Should().Be(token.Id);
    }

    [Fact]
    public void AddGameSuggestion_MultipleTypes_AttachesAll()
    {
        // Arrange
        var token = InvitationToken.Create(
            TestEmail,
            TestRole,
            TestPendingUserId,
            TestCustomMessage,
            expiresInDays: 7,
            _timeProvider,
            TestTokenHash);

        var preAddedGameId = Guid.NewGuid();
        var suggestedGameId = Guid.NewGuid();

        // Act
        token.AddGameSuggestion(preAddedGameId, GameSuggestionType.PreAdded);
        token.AddGameSuggestion(suggestedGameId, GameSuggestionType.Suggested);

        // Assert
        token.GameSuggestions.Count.Should().Be(2);
        token.GameSuggestions.Should().Contain(s => s.GameId == preAddedGameId && s.Type == GameSuggestionType.PreAdded);
        token.GameSuggestions.Should().Contain(s => s.GameId == suggestedGameId && s.Type == GameSuggestionType.Suggested);
    }

    [Fact]
    public void GameSuggestions_InitiallyEmpty()
    {
        // Arrange & Act
        var token = InvitationToken.Create(
            TestEmail,
            TestRole,
            TestPendingUserId,
            TestCustomMessage,
            expiresInDays: 7,
            _timeProvider,
            TestTokenHash);

        // Assert
        token.GameSuggestions.Should().BeEmpty();
    }

    #endregion

    #region Backward Compatibility Tests

    [Fact]
    public void Create_OriginalOverload_StillWorks()
    {
        // The original Create(string, string, string, Guid) must still function
        var adminId = Guid.NewGuid();
        var token = InvitationToken.Create("test@example.com", "User", "hash123", adminId);

        token.Email.Should().Be("test@example.com");
        token.Role.Should().Be("User");
        token.Status.Should().Be(InvitationStatus.Pending);
        token.CustomMessage.Should().BeNull();
        token.PendingUserId.Should().BeNull();
        token.GameSuggestions.Should().BeEmpty();
    }

    #endregion
}