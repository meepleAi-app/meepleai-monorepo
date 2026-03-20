using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// Tests for SessionChatMessage value object.
/// Issue #3483: Chat Session Persistence Service.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class SessionChatMessageTests
{
    [Fact]
    public void SessionChatMessage_WithValidContent_CreatesSuccessfully()
    {
        // Arrange & Act
        var message = new SessionChatMessage("Hello", SessionChatMessage.UserRole, sequenceNumber: 0);

        // Assert
        message.Content.Should().Be("Hello");
        message.Role.Should().Be(SessionChatMessage.UserRole);
        message.SequenceNumber.Should().Be(0);
        Assert.True(message.IsUserMessage);
        Assert.False(message.IsAssistantMessage);
        Assert.False(message.IsSystemMessage);
        message.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void SessionChatMessage_AssistantRole_SetsCorrectly()
    {
        // Arrange & Act
        var message = new SessionChatMessage("Response", SessionChatMessage.AssistantRole, sequenceNumber: 1);

        // Assert
        message.Role.Should().Be(SessionChatMessage.AssistantRole);
        Assert.True(message.IsAssistantMessage);
        Assert.False(message.IsUserMessage);
        Assert.False(message.IsSystemMessage);
    }

    [Fact]
    public void SessionChatMessage_SystemRole_SetsCorrectly()
    {
        // Arrange & Act
        var message = new SessionChatMessage("System prompt", SessionChatMessage.SystemRole, sequenceNumber: 0);

        // Assert
        message.Role.Should().Be(SessionChatMessage.SystemRole);
        Assert.True(message.IsSystemMessage);
        Assert.False(message.IsUserMessage);
        Assert.False(message.IsAssistantMessage);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void SessionChatMessage_WithEmptyContent_ThrowsValidationException(string invalidContent)
    {
        // Act & Assert
        var ex = Assert.Throws<ValidationException>(() =>
            new SessionChatMessage(invalidContent, SessionChatMessage.UserRole, sequenceNumber: 0));
        ex.Message.Should().Contain("content cannot be empty");
    }

    [Fact]
    public void SessionChatMessage_ExceedingMaxLength_ThrowsValidationException()
    {
        // Arrange
        var longContent = new string('a', 50001);

        // Act & Assert
        var ex = Assert.Throws<ValidationException>(() =>
            new SessionChatMessage(longContent, SessionChatMessage.UserRole, sequenceNumber: 0));
        ex.Message.Should().Contain("cannot exceed 50,000 characters");
    }

    [Fact]
    public void SessionChatMessage_AtMaxLength_CreatesSuccessfully()
    {
        // Arrange
        var maxContent = new string('a', 50000);

        // Act
        var message = new SessionChatMessage(maxContent, SessionChatMessage.UserRole, sequenceNumber: 0);

        // Assert
        message.Content.Length.Should().Be(50000);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("USER")]
    [InlineData("ASSISTANT")]
    [InlineData("admin")]
    [InlineData("")]
    public void SessionChatMessage_WithInvalidRole_ThrowsValidationException(string invalidRole)
    {
        // Act & Assert
        var ex = Assert.Throws<ValidationException>(() =>
            new SessionChatMessage("Content", invalidRole, sequenceNumber: 0));
        ex.Message.Should().Contain("Role must be");
    }

    [Fact]
    public void SessionChatMessage_TrimsWhitespace()
    {
        // Arrange & Act
        var message = new SessionChatMessage("  Trimmed content  ", SessionChatMessage.UserRole, sequenceNumber: 0);

        // Assert
        message.Content.Should().Be("Trimmed content");
    }

    [Fact]
    public void SessionChatMessage_SetsTimestamp()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var message = new SessionChatMessage("Test", SessionChatMessage.UserRole, sequenceNumber: 0);

        // Assert
        Assert.InRange(message.Timestamp, before, DateTime.UtcNow.AddSeconds(1));
    }

    [Fact]
    public void SessionChatMessage_WithCustomTimestamp_UsesProvidedTimestamp()
    {
        // Arrange
        var customTimestamp = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);

        // Act
        var message = new SessionChatMessage(
            "Test",
            SessionChatMessage.UserRole,
            sequenceNumber: 0,
            timestamp: customTimestamp);

        // Assert
        message.Timestamp.Should().Be(customTimestamp);
    }

    [Fact]
    public void SessionChatMessage_WithCustomId_UsesProvidedId()
    {
        // Arrange
        var customId = Guid.NewGuid();

        // Act
        var message = new SessionChatMessage(
            "Test",
            SessionChatMessage.UserRole,
            sequenceNumber: 0,
            id: customId);

        // Assert
        message.Id.Should().Be(customId);
    }

    [Fact]
    public void SessionChatMessage_WithMetadata_SerializesToJson()
    {
        // Arrange
        var metadata = new Dictionary<string, object>
        {
            { "tokenCount", 150 },
            { "model", "claude-3" },
            { "latencyMs", 250.5 }
        };

        // Act
        var message = new SessionChatMessage(
            "Test",
            SessionChatMessage.UserRole,
            sequenceNumber: 0,
            metadata: metadata);

        // Assert
        Assert.NotNull(message.MetadataJson);
        message.MetadataJson.Should().Contain("tokenCount");
        message.MetadataJson.Should().Contain("claude-3");
    }

    [Fact]
    public void SessionChatMessage_WithoutMetadata_HasNullMetadataJson()
    {
        // Arrange & Act
        var message = new SessionChatMessage("Test", SessionChatMessage.UserRole, sequenceNumber: 0);

        // Assert
        Assert.Null(message.MetadataJson);
    }

    [Fact]
    public void GetMetadata_WithMetadata_ReturnsDeserializedDictionary()
    {
        // Arrange
        var metadata = new Dictionary<string, object>
        {
            { "tokenCount", 150 },
            { "model", "claude-3" }
        };
        var message = new SessionChatMessage(
            "Test",
            SessionChatMessage.UserRole,
            sequenceNumber: 0,
            metadata: metadata);

        // Act
        var result = message.GetMetadata();

        // Assert
        Assert.NotNull(result);
        result.Count.Should().Be(2);
    }

    [Fact]
    public void GetMetadata_WithoutMetadata_ReturnsNull()
    {
        // Arrange
        var message = new SessionChatMessage("Test", SessionChatMessage.UserRole, sequenceNumber: 0);

        // Act
        var result = message.GetMetadata();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void FromPersistence_CreatesMessageCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid();
        var content = "Persisted message";
        var role = SessionChatMessage.AssistantRole;
        var sequenceNumber = 5;
        var timestamp = new DateTime(2024, 6, 15, 14, 30, 0, DateTimeKind.Utc);
        var metadataJson = "{\"key\":\"value\"}";

        // Act
        var message = SessionChatMessage.FromPersistence(id, content, role, sequenceNumber, timestamp, metadataJson);

        // Assert
        message.Id.Should().Be(id);
        message.Content.Should().Be(content);
        message.Role.Should().Be(role);
        message.SequenceNumber.Should().Be(sequenceNumber);
        message.Timestamp.Should().Be(timestamp);
        message.MetadataJson.Should().Be(metadataJson);
        Assert.True(message.IsAssistantMessage);
    }

    [Fact]
    public void FromPersistence_WithNullMetadata_CreatesMessageCorrectly()
    {
        // Arrange & Act
        var message = SessionChatMessage.FromPersistence(
            Guid.NewGuid(),
            "Content",
            SessionChatMessage.UserRole,
            0,
            DateTime.UtcNow,
            null);

        // Assert
        Assert.Null(message.MetadataJson);
    }

    [Fact]
    public void ToString_TruncatesLongContent()
    {
        // Arrange
        var longContent = new string('a', 100);
        var message = new SessionChatMessage(longContent, SessionChatMessage.UserRole, sequenceNumber: 0);

        // Act
        var result = message.ToString();

        // Assert
        result.Should().Contain("[user]");
        Assert.True(result.Length < longContent.Length + 20);
        result.Should().Contain("...");
    }

    [Fact]
    public void ToString_WithShortContent_IncludesFullContent()
    {
        // Arrange
        var message = new SessionChatMessage("Short", SessionChatMessage.UserRole, sequenceNumber: 0);

        // Act
        var result = message.ToString();

        // Assert
        result.Should().Contain("[user]");
        result.Should().Contain("Short");
    }

    [Theory]
    [InlineData("user")]
    [InlineData("assistant")]
    [InlineData("system")]
    public void ValidRoles_AreAccepted(string role)
    {
        // Act
        var message = new SessionChatMessage("Content", role, sequenceNumber: 0);

        // Assert
        message.Role.Should().Be(role);
    }

    [Fact]
    public void SequenceNumber_CanBeNegative()
    {
        // This tests that we don't enforce positive sequence numbers
        // (validation happens at the aggregate level if needed)

        // Act
        var message = new SessionChatMessage("Content", SessionChatMessage.UserRole, sequenceNumber: -1);

        // Assert
        message.SequenceNumber.Should().Be(-1);
    }
}
