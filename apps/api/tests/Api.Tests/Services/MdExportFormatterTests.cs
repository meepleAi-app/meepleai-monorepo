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
/// BDD-style unit tests for MdExportFormatter (CHAT-05).
///
/// Feature: Markdown Format Export
/// As a developer or technical user
/// I want to export chats as Markdown files
/// So that I can easily integrate with documentation tools and version control
///
/// Format Specification:
/// - Header: ## Chat Export - {Game Name}
/// - Metadata: Date range, export timestamp (blockquote)
/// - Messages: ### Level (timestamp) followed by message content
/// - Citations: Bullet list under assistant messages
/// - Separator: --- (horizontal rule) between messages
/// </summary>
public class MdExportFormatterTests
{
    private readonly MdExportFormatter _formatter;

    public MdExportFormatterTests()
    {
        _formatter = new MdExportFormatter();
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
    /// Scenario: Format valid chat as Markdown
    ///   Given chat has multiple messages
    ///   When formatting as Markdown
    ///   Then valid Markdown stream is returned
    ///   And output includes level 2 header (##) for title
    ///   And messages have level 3 headers (###)
    ///   And messages are separated by horizontal rules (---)
    /// </summary>
    [Fact]
    public async Task FormatAsync_ValidChat_ReturnsFormattedMarkdown()
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

        // When: Formatting as Markdown
        var stream = await _formatter.FormatAsync(chat, null, null);

        // Then: Valid Markdown stream is returned
        Assert.NotNull(stream);
        stream.Position = 0;

        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync();

        // And: Output includes level 2 header for title
        Assert.Contains("## Chat Export", content);
        Assert.Contains("Catan", content);

        // And: Messages have level 3 headers
        Assert.True(content.Contains("### User", StringComparison.OrdinalIgnoreCase));
        Assert.True(content.Contains("### Assistant", StringComparison.OrdinalIgnoreCase));

        // And: Messages are separated by horizontal rules
        Assert.Contains("---", content);

        // And: Message content is present
        Assert.Contains("How do I setup the game?", content);
        Assert.Contains("Place the board and distribute resources.", content);
    }

    /// <summary>
    /// Scenario: Format chat with citations in metadata
    ///   Given assistant message has citations in MetadataJson
    ///   When formatting as Markdown
    ///   Then citations are formatted as bullet list
    ///   And citation format is: "- **Page X:** snippet"
    ///   And citations appear under "**Citations:**" subheading
    /// </summary>
    [Fact]
    public async Task FormatAsync_MessageWithCitations_FormatsAsBulletList()
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

        // When: Formatting as Markdown
        var stream = await _formatter.FormatAsync(chat, null, null);
        stream.Position = 0;

        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync();

        // Then: Citations are formatted as bullet list
        Assert.True(content.Contains("**Citations:**") || content.Contains("Citations:"));
        Assert.True(content.Contains("- **Page 12:**") || content.Contains("- Page 12:"));
        Assert.Contains("Setup instructions for beginners", content);
        Assert.True(content.Contains("- **Page 15:**") || content.Contains("- Page 15:"));
        Assert.Contains("Advanced setup variants", content);
    }

    /// <summary>
    /// Scenario: Format chat with malformed JSON metadata
    ///   Given message has invalid JSON in MetadataJson
    ///   When formatting as Markdown
    ///   Then formatter does not crash
    ///   And message content is still included
    /// </summary>
    [Fact]
    public async Task FormatAsync_MalformedJson_DoesNotCrash()
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

        // When: Formatting as Markdown
        var stream = await _formatter.FormatAsync(chat, null, null);
        stream.Position = 0;

        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync();

        // Then: Formatter does not crash
        Assert.NotNull(content);

        // And: Message content is still included
        Assert.Contains("Answer with metadata", content);
    }

    /// <summary>
    /// Scenario: Format empty chat (no messages)
    ///   Given chat has no messages
    ///   When formatting as Markdown
    ///   Then valid Markdown stream is returned
    ///   And output includes header only
    ///   And output indicates no messages
    /// </summary>
    [Fact]
    public async Task FormatAsync_EmptyChat_ReturnsHeaderOnly()
    {
        // Given: Chat has no messages
        var chat = CreateTestChat("Empty Game");
        chat.Logs = new List<ChatLogEntity>();

        // When: Formatting as Markdown
        var stream = await _formatter.FormatAsync(chat, null, null);
        stream.Position = 0;

        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync();

        // Then: Output includes header
        Assert.Contains("## Chat Export", content);
        Assert.Contains("Empty Game", content);

        // And: Output indicates no messages
        Assert.True(content.Contains("No messages") || content.Contains("0 messages"));
    }

    /// <summary>
    /// Scenario: Format chat with code blocks in messages
    ///   Given messages contain code snippets
    ///   When formatting as Markdown
    ///   Then code blocks are properly escaped or formatted
    ///   And Markdown syntax is preserved
    /// </summary>
    [Fact]
    public async Task FormatAsync_CodeBlocks_PreservesFormatting()
    {
        // Given: Messages contain code snippets
        var chat = CreateTestChat();

        chat.Logs = new List<ChatLogEntity>
        {
            new()
            {
                Id = Guid.NewGuid(),
                ChatId = chat.Id,
                Level = "assistant",
                Message = "Here's an example:\n```csharp\nvar game = new Game();\n```",
                CreatedAt = DateTime.UtcNow
            }
        };

        // When: Formatting as Markdown
        var stream = await _formatter.FormatAsync(chat, null, null);
        stream.Position = 0;

        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync();

        // Then: Code blocks are preserved
        Assert.Contains("```", content);
        Assert.Contains("var game = new Game();", content);
    }

    /// <summary>
    /// Scenario: Format chat with date range filter
    ///   Given chat has messages across date range
    ///   When formatting with date filter applied
    ///   Then only filtered messages are included
    ///   And header includes date range information
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

        // Then: Header includes date range
        Assert.True(content.Contains("2025-10-10") || content.Contains("Oct"));

        // And: Only filtered message is included
        Assert.Contains("Recent message", content);
        Assert.DoesNotContain("Old message", content);
    }

    /// <summary>
    /// Scenario: Verify Markdown formatter properties
    ///   When accessing formatter properties
    ///   Then Format is "md"
    ///   And ContentType is "text/markdown"
    /// </summary>
    [Fact]
    public void FormatterProperties_ReturnsCorrectValues()
    {
        // When: Accessing formatter properties
        var format = _formatter.Format;
        var contentType = _formatter.ContentType;

        // Then: Format is "md"
        Assert.Equal("md", format);

        // And: ContentType is "text/markdown"
        Assert.Equal("text/markdown", contentType);
    }

    /// <summary>
    /// Scenario: Format chat with Markdown special characters in messages
    ///   Given messages contain Markdown syntax characters
    ///   When formatting as Markdown
    ///   Then special characters are properly escaped or handled
    ///   And output is valid Markdown
    /// </summary>
    [Fact]
    public async Task FormatAsync_MarkdownSpecialChars_HandlesCorrectly()
    {
        // Given: Messages contain Markdown syntax characters
        var chat = CreateTestChat();

        chat.Logs = new List<ChatLogEntity>
        {
            new()
            {
                Id = Guid.NewGuid(),
                ChatId = chat.Id,
                Level = "user",
                Message = "Text with *asterisks*, _underscores_, and #hashtags",
                CreatedAt = DateTime.UtcNow.AddMinutes(-1)
            },
            new()
            {
                Id = Guid.NewGuid(),
                ChatId = chat.Id,
                Level = "assistant",
                Message = "Lists:\n- Item 1\n- Item 2",
                CreatedAt = DateTime.UtcNow
            }
        };

        // When: Formatting as Markdown
        var stream = await _formatter.FormatAsync(chat, null, null);
        stream.Position = 0;

        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync();

        // Then: Message content is preserved (may be escaped or in code blocks)
        Assert.Contains("asterisks", content);
        Assert.Contains("underscores", content);
        Assert.Contains("hashtags", content);
        Assert.Contains("Item 1", content);
        Assert.Contains("Item 2", content);
    }

    /// <summary>
    /// Scenario: Format chat with multiple messages and proper structure
    ///   Given chat has 5+ messages
    ///   When formatting as Markdown
    ///   Then each message has proper Markdown heading
    ///   And messages are separated by horizontal rules
    ///   And output is well-structured
    /// </summary>
    [Fact]
    public async Task FormatAsync_MultipleMessages_ProperMarkdownStructure()
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

        // When: Formatting as Markdown
        var stream = await _formatter.FormatAsync(chat, null, null);
        stream.Position = 0;

        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync();

        // Then: Each message is present
        for (int i = 1; i <= 5; i++)
        {
            Assert.Contains($"Message {i}", content);
        }

        // And: Messages have proper headings (###)
        var headingCount = content.Split("###").Length - 1;
        Assert.True(headingCount >= 5, $"Expected at least 5 headings, found {headingCount}");

        // And: Messages are separated by horizontal rules
        var separatorCount = content.Split("---").Length - 1;
        Assert.True(separatorCount >= 4, $"Expected at least 4 separators, found {separatorCount}");
    }

    /// <summary>
    /// Scenario: Format chat with all message types
    ///   Given chat has user, assistant, and system messages
    ///   When formatting as Markdown
    ///   Then all levels have distinct headings
    ///   And system messages are distinguishable
    /// </summary>
    [Fact]
    public async Task FormatAsync_AllMessageTypes_DistinctHeadings()
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

        // When: Formatting as Markdown
        var stream = await _formatter.FormatAsync(chat, null, null);
        stream.Position = 0;

        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync();

        // Then: All message types have headings
        Assert.Contains("User", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Assistant", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("System", content, StringComparison.OrdinalIgnoreCase);

        // And: Message content is present
        Assert.Contains("User message", content);
        Assert.Contains("Assistant message", content);
        Assert.Contains("System message", content);
    }

    /// <summary>
    /// Scenario: Format chat with blockquote metadata
    ///   When formatting chat metadata
    ///   Then metadata uses blockquote formatting (>)
    ///   And includes export timestamp
    /// </summary>
    [Fact]
    public async Task FormatAsync_Metadata_UsesBlockquote()
    {
        // Given: Chat with messages
        var chat = CreateTestChat();

        chat.Logs = new List<ChatLogEntity>
        {
            new()
            {
                Id = Guid.NewGuid(),
                ChatId = chat.Id,
                Level = "user",
                Message = "Test",
                CreatedAt = DateTime.UtcNow
            }
        };

        // When: Formatting as Markdown
        var stream = await _formatter.FormatAsync(chat, null, null);
        stream.Position = 0;

        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync();

        // Then: Metadata uses blockquote
        Assert.Contains(">", content);

        // And: Includes timestamp information
        Assert.True(content.Contains("Exported") || content.Contains("Generated"));
    }
}
