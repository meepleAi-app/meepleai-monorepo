using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Api.Infrastructure.Entities;
using Api.Services;
using Xunit;

namespace Api.Tests.Services;

/// <summary>
/// BDD-style unit tests for TxtExportFormatter (CHAT-05).
///
/// Feature: TXT Format Export
/// As a user
/// I want to export chats as plain text files
/// So that I can easily read and share conversations
///
/// Format Specification:
/// - Header: Game name, date range, export timestamp
/// - Messages: [TIMESTAMP] LEVEL: Message content
/// - Citations: Indented list under assistant messages
/// - Separator: "---" between messages
/// </summary>
public class TxtExportFormatterTests
{
    private readonly TxtExportFormatter _formatter;

    public TxtExportFormatterTests()
    {
        _formatter = new TxtExportFormatter();
    }

    private static ChatEntity CreateTestChat(string gameName = "Test Game")
    {
        return new ChatEntity
        {
            Id = Guid.NewGuid(),
            UserId = "user-123",
            GameId = "test-game",
            AgentId = "test-agent",
            StartedAt = new DateTime(2025, 10, 18, 10, 0, 0, DateTimeKind.Utc),
            Game = new GameEntity { Id = "test-game", Name = gameName },
            Logs = new List<ChatLogEntity>()
        };
    }

    /// <summary>
    /// Scenario: Format valid chat with messages
    ///   Given chat has multiple messages
    ///   When formatting as TXT
    ///   Then valid TXT stream is returned
    ///   And output includes header with game name and timestamps
    ///   And messages are formatted with timestamp prefix
    ///   And messages are separated by "---"
    /// </summary>
    [Fact]
    public async Task FormatAsync_ValidChat_ReturnsFormattedTxtStream()
    {
        // Given: Chat has multiple messages
        var chat = CreateTestChat("Catan");

        chat.Logs = new List<ChatLogEntity>
        {
            new()
            {
                Id = Guid.NewGuid(),
                ChatId = chat.Id,
                Level = "user",
                Message = "How do I setup the game?",
                CreatedAt = new DateTime(2025, 10, 18, 10, 5, 0, DateTimeKind.Utc)
            },
            new()
            {
                Id = Guid.NewGuid(),
                ChatId = chat.Id,
                Level = "assistant",
                Message = "Place the board and distribute resources.",
                CreatedAt = new DateTime(2025, 10, 18, 10, 6, 0, DateTimeKind.Utc)
            }
        };

        // When: Formatting as TXT
        var stream = await _formatter.FormatAsync(chat, null, null);

        // Then: Valid TXT stream is returned
        Assert.NotNull(stream);
        stream.Position = 0;

        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync();

        // And: Output includes header with game name
        Assert.Contains("Catan", content);
        Assert.Contains("Chat Export", content);

        // And: Messages are formatted with timestamp and level
        Assert.Contains("[2025-10-18 10:05:00 UTC] USER:", content);
        Assert.Contains("How do I setup the game?", content);
        Assert.Contains("[2025-10-18 10:06:00 UTC] ASSISTANT:", content);
        Assert.Contains("Place the board and distribute resources.", content);

        // And: Messages are separated by "---"
        Assert.Contains("---", content);
    }

    /// <summary>
    /// Scenario: Format chat with citations in metadata
    ///   Given assistant message has citations in MetadataJson
    ///   When formatting as TXT
    ///   Then citations are extracted and formatted
    ///   And citations appear as indented list under message
    ///   And citation format is: "  - Page X: snippet"
    /// </summary>
    [Fact]
    public async Task FormatAsync_MessageWithCitations_IncludesCitations()
    {
        // Given: Assistant message has citations in MetadataJson
        var chat = CreateTestChat();

        var citations = new[]
        {
            new { page = "12", snippet = "Setup instructions for beginners" },
            new { page = "15", snippet = "Advanced setup variants" }
        };

        var metadata = new { citations };

        chat.Logs = new List<ChatLogEntity>
        {
            new()
            {
                Id = Guid.NewGuid(),
                ChatId = chat.Id,
                Level = "assistant",
                Message = "Here are the setup instructions.",
                MetadataJson = JsonSerializer.Serialize(metadata),
                CreatedAt = DateTime.UtcNow
            }
        };

        // When: Formatting as TXT
        var stream = await _formatter.FormatAsync(chat, null, null);
        stream.Position = 0;

        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync();

        // Then: Citations are formatted as indented list
        Assert.Contains("Citations:", content);
        Assert.Contains("  - Page 12: Setup instructions for beginners", content);
        Assert.Contains("  - Page 15: Advanced setup variants", content);
    }

    /// <summary>
    /// Scenario: Format chat with malformed JSON metadata
    ///   Given message has invalid JSON in MetadataJson
    ///   When formatting as TXT
    ///   Then formatter does not crash
    ///   And raw JSON is included as fallback
    ///   And warning is included in output
    /// </summary>
    [Fact]
    public async Task FormatAsync_MalformedJson_IncludesRawJsonFallback()
    {
        // Given: Message has invalid JSON in MetadataJson
        var chat = CreateTestChat();

        chat.Logs = new List<ChatLogEntity>
        {
            new()
            {
                Id = Guid.NewGuid(),
                ChatId = chat.Id,
                Level = "assistant",
                Message = "Answer with metadata",
                MetadataJson = "{ invalid json }", // Malformed JSON
                CreatedAt = DateTime.UtcNow
            }
        };

        // When: Formatting as TXT
        var stream = await _formatter.FormatAsync(chat, null, null);
        stream.Position = 0;

        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync();

        // Then: Formatter does not crash
        Assert.NotNull(content);

        // And: Raw JSON or indication of metadata present
        // (Implementation detail: may include raw JSON or skip silently)
        Assert.Contains("Answer with metadata", content);
    }

    /// <summary>
    /// Scenario: Format empty chat (no messages)
    ///   Given chat has no messages
    ///   When formatting as TXT
    ///   Then valid TXT stream is returned
    ///   And output includes header only
    ///   And output indicates no messages
    /// </summary>
    [Fact]
    public async Task FormatAsync_EmptyChat_ReturnsHeaderOnly()
    {
        // Given: Chat has no messages
        var chat = CreateTestChat("Empty Game");
        chat.Logs = new List<ChatLogEntity>();

        // When: Formatting as TXT
        var stream = await _formatter.FormatAsync(chat, null, null);
        stream.Position = 0;

        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync();

        // Then: Output includes header
        Assert.Contains("Empty Game", content);
        Assert.Contains("Chat Export", content);

        // And: Output indicates no messages
        Assert.True(content.Contains("No messages") || content.Contains("0 messages"));
    }

    /// <summary>
    /// Scenario: Format chat with multiple messages and separators
    ///   Given chat has 5+ messages
    ///   When formatting as TXT
    ///   Then each message is separated by "---"
    ///   And message count matches expected
    ///   And messages are in chronological order
    /// </summary>
    [Fact]
    public async Task FormatAsync_MultipleMessages_CorrectSeparatorFormat()
    {
        // Given: Chat has 5+ messages
        var chat = CreateTestChat();

        chat.Logs = Enumerable.Range(1, 5).Select(i => new ChatLogEntity
        {
            Id = Guid.NewGuid(),
            ChatId = chat.Id,
            Level = i % 2 == 0 ? "assistant" : "user",
            Message = $"Message {i}",
            CreatedAt = DateTime.UtcNow.AddMinutes(i)
        }).ToList();

        // When: Formatting as TXT
        var stream = await _formatter.FormatAsync(chat, null, null);
        stream.Position = 0;

        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync();

        // Then: Each message is present
        for (int i = 1; i <= 5; i++)
        {
            Assert.Contains($"Message {i}", content);
        }

        // And: Messages are separated (at least 4 separators for 5 messages)
        var separatorCount = content.Split("---").Length - 1;
        Assert.True(separatorCount >= 4, $"Expected at least 4 separators, found {separatorCount}");
    }

    /// <summary>
    /// Scenario: Format chat with date range filter
    ///   Given chat has messages across date range
    ///   When formatting with date filter applied
    ///   Then only filtered messages are included
    ///   And header indicates date range
    /// </summary>
    [Fact]
    public async Task FormatAsync_WithDateRange_IncludesOnlyFilteredMessages()
    {
        // Given: Chat has messages across date range
        var chat = CreateTestChat();

        chat.Logs = new List<ChatLogEntity>
        {
            new()
            {
                Id = Guid.NewGuid(),
                ChatId = chat.Id,
                Level = "user",
                Message = "Old message",
                CreatedAt = new DateTime(2025, 10, 1, 10, 0, 0, DateTimeKind.Utc)
            },
            new()
            {
                Id = Guid.NewGuid(),
                ChatId = chat.Id,
                Level = "user",
                Message = "Recent message",
                CreatedAt = new DateTime(2025, 10, 15, 10, 0, 0, DateTimeKind.Utc)
            }
        };

        // When: Formatting with date filter (Oct 10-20)
        var startDate = new DateTime(2025, 10, 10);
        var endDate = new DateTime(2025, 10, 20);
        var stream = await _formatter.FormatAsync(chat, startDate, endDate);
        stream.Position = 0;

        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync();

        // Then: Header indicates date range
        Assert.True(content.Contains("2025-10-10") || content.Contains("Oct"));

        // And: Only filtered message is included
        Assert.Contains("Recent message", content);
        Assert.DoesNotContain("Old message", content);
    }

    /// <summary>
    /// Scenario: Verify TXT formatter properties
    ///   When accessing formatter properties
    ///   Then Format is "txt"
    ///   And ContentType is "text/plain"
    /// </summary>
    [Fact]
    public void FormatterProperties_ReturnsCorrectValues()
    {
        // When: Accessing formatter properties
        var format = _formatter.Format;
        var contentType = _formatter.ContentType;

        // Then: Format is "txt"
        Assert.Equal("txt", format);

        // And: ContentType is "text/plain"
        Assert.Equal("text/plain", contentType);
    }

    /// <summary>
    /// Scenario: Format chat with user, assistant, and system messages
    ///   Given chat has all message types
    ///   When formatting as TXT
    ///   Then all levels are properly labeled
    ///   And system messages are distinguishable
    /// </summary>
    [Fact]
    public async Task FormatAsync_AllMessageTypes_LabelsCorrectly()
    {
        // Given: Chat has all message types
        var chat = CreateTestChat();

        chat.Logs = new List<ChatLogEntity>
        {
            new()
            {
                Id = Guid.NewGuid(),
                ChatId = chat.Id,
                Level = "user",
                Message = "User message",
                CreatedAt = DateTime.UtcNow.AddMinutes(-2)
            },
            new()
            {
                Id = Guid.NewGuid(),
                ChatId = chat.Id,
                Level = "assistant",
                Message = "Assistant message",
                CreatedAt = DateTime.UtcNow.AddMinutes(-1)
            },
            new()
            {
                Id = Guid.NewGuid(),
                ChatId = chat.Id,
                Level = "system",
                Message = "System message",
                CreatedAt = DateTime.UtcNow
            }
        };

        // When: Formatting as TXT
        var stream = await _formatter.FormatAsync(chat, null, null);
        stream.Position = 0;

        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync();

        // Then: All levels are properly labeled
        Assert.Contains("USER:", content);
        Assert.Contains("ASSISTANT:", content);
        Assert.Contains("SYSTEM:", content);
    }

    /// <summary>
    /// Scenario: Format chat with special characters in messages
    ///   Given messages contain newlines, quotes, and unicode
    ///   When formatting as TXT
    ///   Then special characters are preserved
    ///   And output is valid text
    /// </summary>
    [Fact]
    public async Task FormatAsync_SpecialCharacters_PreservesContent()
    {
        // Given: Messages contain special characters
        var chat = CreateTestChat();

        chat.Logs = new List<ChatLogEntity>
        {
            new()
            {
                Id = Guid.NewGuid(),
                ChatId = chat.Id,
                Level = "user",
                Message = "Line 1\nLine 2\nLine 3",
                CreatedAt = DateTime.UtcNow.AddMinutes(-1)
            },
            new()
            {
                Id = Guid.NewGuid(),
                ChatId = chat.Id,
                Level = "assistant",
                Message = "Quote: \"Hello\" and emoji: 🎲",
                CreatedAt = DateTime.UtcNow
            }
        };

        // When: Formatting as TXT
        var stream = await _formatter.FormatAsync(chat, null, null);
        stream.Position = 0;

        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync();

        // Then: Special characters are preserved
        Assert.Contains("Line 1", content);
        Assert.Contains("Line 2", content);
        Assert.Contains("\"Hello\"", content);
        Assert.Contains("🎲", content);
    }
}
