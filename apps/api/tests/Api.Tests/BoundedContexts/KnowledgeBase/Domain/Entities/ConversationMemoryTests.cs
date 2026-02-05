using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Tests.Constants;
using Xunit;

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
        Assert.Equal(id, memory.Id);
        Assert.Equal(sessionId, memory.SessionId);
        Assert.Equal(userId, memory.UserId);
        Assert.Equal(gameId, memory.GameId);
        Assert.Equal(content, memory.Content);
        Assert.Equal(messageType, memory.MessageType);
        Assert.Equal(timestamp, memory.Timestamp);
        Assert.Null(memory.Embedding);
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
        Assert.Null(memory.GameId);
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
        Assert.InRange(memory.Timestamp, before, DateTime.UtcNow.AddSeconds(1));
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
        Assert.NotNull(memory.Embedding);
        Assert.Equal(3, memory.Embedding.Values.Length);
    }

    [Fact]
    public void Create_WithEmptySessionId_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            new ConversationMemory(
                Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), null,
                "Test content", "user"));
        Assert.Contains("Session ID cannot be empty", ex.Message);
    }

    [Fact]
    public void Create_WithEmptyUserId_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            new ConversationMemory(
                Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, null,
                "Test content", "user"));
        Assert.Contains("User ID cannot be empty", ex.Message);
    }

    [Fact]
    public void Create_WithEmptyContent_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            new ConversationMemory(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
                "", "user"));
        Assert.Contains("Content cannot be empty", ex.Message);
    }

    [Fact]
    public void Create_WithWhitespaceContent_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            new ConversationMemory(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
                "   ", "user"));
        Assert.Contains("Content cannot be empty", ex.Message);
    }

    [Fact]
    public void Create_WithEmptyMessageType_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            new ConversationMemory(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
                "Test content", ""));
        Assert.Contains("Message type cannot be empty", ex.Message);
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
        Assert.NotNull(memory.Embedding);
        Assert.Equal(embedding, memory.Embedding);
    }

    [Fact]
    public void SetEmbedding_WithNull_ThrowsArgumentNullException()
    {
        // Arrange
        var memory = new ConversationMemory(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
            "Test content", "user");

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => memory.SetEmbedding(null!));
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
        Assert.Equal(1.0, score, precision: 2);
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
        Assert.True(score < 0.1, $"Score should be low for old memory, got {score}");
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
        Assert.Equal(0.0, score);
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
        Assert.Equal(0.0, score);
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
        Assert.Equal(1.0, score);
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
        Assert.InRange(score, 0.01, 0.99);
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
        Assert.True(score1h > score4h, $"1h score ({score1h}) should be > 4h score ({score4h})");
        Assert.True(score4h > score8h, $"4h score ({score4h}) should be > 8h score ({score8h})");
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
        Assert.InRange(score, expectedApproxScore - 0.05, expectedApproxScore + 0.05);
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
        Assert.True(score24h > score8h, "24h window should give higher score than 8h window");
        Assert.True(score48h > score24h, "48h window should give higher score than 24h window");
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
            Assert.InRange(score, 0.0, 1.0);
        }
    }

    #endregion
}
