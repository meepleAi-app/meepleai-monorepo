using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Enums;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.ValueObjects;
using Microsoft.Extensions.Time.Testing;
using Xunit;

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
        Assert.Equal(expectedExpiry, token.ExpiresAt);
        Assert.Equal(_timeProvider.GetUtcNow().UtcDateTime, token.CreatedAt);
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
        Assert.Equal(TestCustomMessage, token.CustomMessage);
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
        Assert.Equal(TestPendingUserId, token.PendingUserId);
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
        Assert.Equal(TestEmail.Value, token.Email);
        Assert.Equal(TestRole.Value, token.Role);
        Assert.Equal(TestTokenHash, token.TokenHash);
        Assert.Equal(InvitationStatus.Pending, token.Status);
        Assert.NotEqual(Guid.Empty, token.Id);
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
        Assert.Null(token.CustomMessage);
    }

    [Fact]
    public void Create_WithTimeProvider_EmptyTokenHash_Throws()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            InvitationToken.Create(
                TestEmail,
                TestRole,
                TestPendingUserId,
                TestCustomMessage,
                expiresInDays: 7,
                _timeProvider,
                tokenHash: ""));
    }

    [Fact]
    public void Create_WithTimeProvider_EmptyPendingUserId_Throws()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            InvitationToken.Create(
                TestEmail,
                TestRole,
                pendingUserId: Guid.Empty,
                TestCustomMessage,
                expiresInDays: 7,
                _timeProvider,
                TestTokenHash));
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
        Assert.Single(token.GameSuggestions);
        Assert.Equal(gameId, token.GameSuggestions[0].GameId);
        Assert.Equal(GameSuggestionType.PreAdded, token.GameSuggestions[0].Type);
        Assert.Equal(token.Id, token.GameSuggestions[0].InvitationTokenId);
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
        Assert.Equal(2, token.GameSuggestions.Count);
        Assert.Contains(token.GameSuggestions, s => s.GameId == preAddedGameId && s.Type == GameSuggestionType.PreAdded);
        Assert.Contains(token.GameSuggestions, s => s.GameId == suggestedGameId && s.Type == GameSuggestionType.Suggested);
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
        Assert.Empty(token.GameSuggestions);
    }

    #endregion

    #region Backward Compatibility Tests

    [Fact]
    public void Create_OriginalOverload_StillWorks()
    {
        // The original Create(string, string, string, Guid) must still function
        var adminId = Guid.NewGuid();
        var token = InvitationToken.Create("test@example.com", "User", "hash123", adminId);

        Assert.Equal("test@example.com", token.Email);
        Assert.Equal("User", token.Role);
        Assert.Equal(InvitationStatus.Pending, token.Status);
        Assert.Null(token.CustomMessage);
        Assert.Null(token.PendingUserId);
        Assert.Empty(token.GameSuggestions);
    }

    #endregion
}
