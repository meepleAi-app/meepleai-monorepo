using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Tests.Constants;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Entities;

/// <summary>
/// Tests for ConversationMemory entity.
/// Issue #3498: Conversation Memory - Temporal RAG Implementation.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class ConversationMemoryTests
{
    [Fact]
    public void Create_WithValidParameters_InitializesCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var content = "What are the rules for castling?";
        var messageType = "user";
        var timestamp = DateTime.UtcNow;

        // Act
        var memory = new ConversationMemory(
            id, sessionId, userId, gameId,
            content, messageType, timestamp);

        // Assert
        memory.Id.Should().Be(id);
        memory.SessionId.Should().Be(sessionId);
        memory.UserId.Should().Be(userId);
        memory.GameId.Should().Be(gameId);
        memory.Content.Should().Be(content);
        memory.MessageType.Should().Be(messageType);
        memory.Timestamp.Should().Be(timestamp);
        memory.Embedding.Should().BeNull();
    }

    [Fact]
    public void Create_WithNullGameId_InitializesCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Act
        var memory = new ConversationMemory(
            id, sessionId, userId, null,
            "Test content", "user");

        // Assert
        memory.GameId.Should().BeNull();
    }

    [Fact]
    public void Create_WithNullTimestamp_UsesUtcNow()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var memory = new ConversationMemory(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
            "Test content", "user");

        // Assert
        memory.Timestamp.Should().BeOnOrAfter(before).And.BeOnOrBefore(DateTime.UtcNow.AddSeconds(1));
    }

    [Fact]
    public void Create_WithEmbedding_SetsEmbedding()
    {
        // Arrange
        var embedding = new Vector(new float[] { 0.1f, 0.2f, 0.3f });

        // Act
        var memory = new ConversationMemory(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
            "Test content", "user", embedding: embedding);

        // Assert
        memory.Embedding.Should().NotBeNull();
        memory.Embedding.Values.Length.Should().Be(3);
    }

    [Fact]
    public void Create_WithEmptySessionId_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        Action act = () =>
            new ConversationMemory(
                Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), null,
                "Test content", "user");
        var ex = act.Should().Throw<ArgumentException>().Which;
        ex.Message.Should().Contain("Session ID cannot be empty");
    }

    [Fact]
    public void Create_WithEmptyUserId_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        Action act = () =>
            new ConversationMemory(
                Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, null,
                "Test content", "user");
        var ex = act.Should().Throw<ArgumentException>().Which;
        ex.Message.Should().Contain("User ID cannot be empty");
    }

    [Fact]
    public void Create_WithEmptyContent_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        Action act = () =>
            new ConversationMemory(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
                "", "user");
        var ex = act.Should().Throw<ArgumentException>().Which;
        ex.Message.Should().Contain("Content cannot be empty");
    }

    [Fact]
    public void Create_WithWhitespaceContent_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        Action act = () =>
            new ConversationMemory(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
                "   ", "user");
        var ex = act.Should().Throw<ArgumentException>().Which;
        ex.Message.Should().Contain("Content cannot be empty");
    }

    [Fact]
    public void Create_WithEmptyMessageType_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        Action act = () =>
            new ConversationMemory(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
                "Test content", "");
        var ex = act.Should().Throw<ArgumentException>().Which;
        ex.Message.Should().Contain("Message type cannot be empty");
    }

    [Fact]
    public void SetEmbedding_WithValidEmbedding_SetsEmbedding()
    {
        // Arrange
        var memory = new ConversationMemory(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
            "Test content", "user");
        var embedding = new Vector(new float[] { 0.5f, 0.6f, 0.7f });

        // Act
        memory.SetEmbedding(embedding);

        // Assert
        memory.Embedding.Should().NotBeNull();
        memory.Embedding.Should().Be(embedding);
    }

    [Fact]
    public void SetEmbedding_WithNull_ThrowsArgumentNullException()
    {
        // Arrange
        var memory = new ConversationMemory(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
            "Test content", "user");

        // Act & Assert
        Action act = () => memory.SetEmbedding(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    #region CalculateTemporalScore Tests

    [Fact]
    public void CalculateTemporalScore_RecentMemory_ReturnsHighScore()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var memory = new ConversationMemory(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
            "Test content", "user", now);

        // Act
        var score = memory.CalculateTemporalScore(now, TimeSpan.FromHours(24));

        // Assert
        score.Should().BeApproximately(1.0, 0.01);
    }

    [Fact]
    public void CalculateTemporalScore_OldMemory_ReturnsLowScore()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var oldTimestamp = now.AddHours(-23); // Almost at decay window
        var memory = new ConversationMemory(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
            "Test content", "user", oldTimestamp);

        // Act
        var score = memory.CalculateTemporalScore(now, TimeSpan.FromHours(24));

        // Assert
        (score < 0.1).Should().BeTrue($"Score should be low for old memory, got {score}");
    }

    [Fact]
    public void CalculateTemporalScore_MemoryAtDecayWindow_ReturnsZero()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var oldTimestamp = now.AddHours(-24); // Exactly at decay window
        var memory = new ConversationMemory(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
            "Test content", "user", oldTimestamp);

        // Act
        var score = memory.CalculateTemporalScore(now, TimeSpan.FromHours(24));

        // Assert
        score.Should().Be(0.0);
    }

    [Fact]
    public void CalculateTemporalScore_MemoryBeyondDecayWindow_ReturnsZero()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var oldTimestamp = now.AddDays(-7); // Way past decay window
        var memory = new ConversationMemory(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
            "Test content", "user", oldTimestamp);

        // Act
        var score = memory.CalculateTemporalScore(now, TimeSpan.FromHours(24));

        // Assert
        score.Should().Be(0.0);
    }

    [Fact]
    public void CalculateTemporalScore_FutureMemory_ReturnsOne()
    {
        // Arrange - edge case where timestamp is in the future
        var now = DateTime.UtcNow;
        var futureTimestamp = now.AddHours(1);
        var memory = new ConversationMemory(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
            "Test content", "user", futureTimestamp);

        // Act
        var score = memory.CalculateTemporalScore(now, TimeSpan.FromHours(24));

        // Assert
        score.Should().Be(1.0);
    }

    [Fact]
    public void CalculateTemporalScore_MiddleOfDecayWindow_ReturnsIntermediateScore()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var halfwayTimestamp = now.AddHours(-12); // Halfway through 24-hour window
        var memory = new ConversationMemory(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
            "Test content", "user", halfwayTimestamp);

        // Act
        var score = memory.CalculateTemporalScore(now, TimeSpan.FromHours(24));

        // Assert
        score.Should().BeInRange(0.01, 0.99);
    }

    [Fact]
    public void CalculateTemporalScore_ExponentialDecay_DecaysCorrectly()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var decayWindow = TimeSpan.FromHours(24);

        // Create memories at different ages
        var memory1h = new ConversationMemory(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
            "Test", "user", now.AddHours(-1));
        var memory4h = new ConversationMemory(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
            "Test", "user", now.AddHours(-4));
        var memory8h = new ConversationMemory(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
            "Test", "user", now.AddHours(-8));

        // Act
        var score1h = memory1h.CalculateTemporalScore(now, decayWindow);
        var score4h = memory4h.CalculateTemporalScore(now, decayWindow);
        var score8h = memory8h.CalculateTemporalScore(now, decayWindow);

        // Assert - scores should decrease monotonically
        (score1h > score4h).Should().BeTrue($"1h score ({score1h}) should be > 4h score ({score4h})");
        (score4h > score8h).Should().BeTrue($"4h score ({score4h}) should be > 8h score ({score8h})");
    }

    [Theory]
    [InlineData(0, 1.0)]
    [InlineData(8, 0.37)] // ~1/e at half-life (24/3 = 8 hours)
    [InlineData(16, 0.14)] // ~1/e^2
    [InlineData(24, 0.0)] // At decay window
    public void CalculateTemporalScore_AtKnownPoints_ReturnsExpectedValues(
        int hoursAgo, double expectedApproxScore)
    {
        // Arrange
        var now = DateTime.UtcNow;
        var timestamp = now.AddHours(-hoursAgo);
        var memory = new ConversationMemory(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
            "Test content", "user", timestamp);

        // Act
        var score = memory.CalculateTemporalScore(now, TimeSpan.FromHours(24));

        // Assert - allow tolerance for exponential calculation
        score.Should().BeInRange(expectedApproxScore - 0.05, expectedApproxScore + 0.05);
    }

    [Fact]
    public void CalculateTemporalScore_WithDifferentDecayWindows_ScalesCorrectly()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var timestamp = now.AddHours(-4);
        var memory = new ConversationMemory(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
            "Test content", "user", timestamp);

        // Act
        var score8h = memory.CalculateTemporalScore(now, TimeSpan.FromHours(8));
        var score24h = memory.CalculateTemporalScore(now, TimeSpan.FromHours(24));
        var score48h = memory.CalculateTemporalScore(now, TimeSpan.FromHours(48));

        // Assert - wider window = higher score for same age
        (score24h > score8h).Should().BeTrue("24h window should give higher score than 8h window");
        (score48h > score24h).Should().BeTrue("48h window should give higher score than 24h window");
    }

    #endregion

    #region Boundary Condition Tests

    [Fact]
    public void CalculateTemporalScore_ScoreAlwaysBetweenZeroAndOne()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var decayWindow = TimeSpan.FromHours(24);

        // Test various timestamps
        var timestamps = new[]
        {
            now,
            now.AddHours(-1),
            now.AddHours(-12),
            now.AddHours(-24),
            now.AddDays(-365),
            now.AddHours(1) // Future
        };

        foreach (var timestamp in timestamps)
        {
            var memory = new ConversationMemory(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
                "Test", "user", timestamp);

            // Act
            var score = memory.CalculateTemporalScore(now, decayWindow);

            // Assert
            score.Should().BeInRange(0.0, 1.0);
        }
    }

    #endregion
}
