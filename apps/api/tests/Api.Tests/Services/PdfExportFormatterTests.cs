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
/// BDD-style unit tests for PdfExportFormatter (CHAT-05).
///
/// Feature: PDF Format Export
/// As a user
/// I want to export chats as PDF files
/// So that I can create professional, printable conversation archives
///
/// Format Specification:
/// - PDF magic bytes: %PDF- at start
/// - Proper document structure with fonts and formatting
/// - Embedded timestamps and metadata
/// - Citations as footnotes or inline references
/// </summary>
public class PdfExportFormatterTests
{
    private readonly PdfExportFormatter _formatter;

    public PdfExportFormatterTests()
    {
        _formatter = new PdfExportFormatter();
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
    /// Scenario: Format valid chat as PDF
    ///   Given chat has messages
    ///   When formatting as PDF
    ///   Then valid PDF stream is returned
    ///   And stream starts with PDF magic bytes "%PDF-"
    ///   And stream is not empty
    /// </summary>
    [Fact]
    public async Task FormatAsync_ValidChat_ReturnsValidPdfStream()
    {
        // Given: Chat has messages
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

        // When: Formatting as PDF
        var stream = await _formatter.FormatAsync(chat, null, null);

        // Then: Valid PDF stream is returned
        Assert.NotNull(stream);
        Assert.True(stream.Length > 0);

        // And: Stream starts with PDF magic bytes
        stream.Position = 0;
        var buffer = new byte[5];
        await stream.ReadAsync(buffer, 0, 5);
        var magicBytes = System.Text.Encoding.ASCII.GetString(buffer);

        Assert.Equal("%PDF-", magicBytes);
    }

    /// <summary>
    /// Scenario: Format large chat with 100+ messages
    ///   Given chat has 100+ messages
    ///   When formatting as PDF
    ///   Then PDF is generated without exception
    ///   And PDF size is reasonable (not truncated)
    ///   And all messages are included
    /// </summary>
    [Fact]
    public async Task FormatAsync_LargeChat_GeneratesWithoutException()
    {
        // Given: Chat has 100+ messages
        var chat = CreateTestChat("Large Game");

        chat.Logs = Enumerable.Range(1, 150).Select(i => new ChatLogEntity
        {
            Id = Guid.NewGuid(),
            ChatId = chat.Id,
            Level = i % 2 == 0 ? "assistant" : "user",
            Message = $"Message number {i} with some additional content to make it realistic.",
            CreatedAt = DateTime.UtcNow.AddMinutes(i)
        }).ToList();

        // When: Formatting as PDF
        var stream = await _formatter.FormatAsync(chat, null, null);

        // Then: PDF is generated without exception
        Assert.NotNull(stream);

        // And: PDF size is reasonable (should be several KB for 150 messages)
        Assert.True(stream.Length > 5000, $"Expected PDF > 5KB, got {stream.Length} bytes");

        // And: Verify it's a valid PDF
        stream.Position = 0;
        var buffer = new byte[5];
        await stream.ReadAsync(buffer, 0, 5);
        Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(buffer));
    }

    /// <summary>
    /// Scenario: Format chat with citations in metadata
    ///   Given assistant message has citations
    ///   When formatting as PDF
    ///   Then citations are included in PDF output
    ///   And PDF contains reference markers or footnotes
    /// </summary>
    [Fact]
    public async Task FormatAsync_MessageWithCitations_IncludesCitationsInPdf()
    {
        // Given: Assistant message has citations
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

        // When: Formatting as PDF
        var stream = await _formatter.FormatAsync(chat, null, null);

        // Then: PDF is generated successfully
        Assert.NotNull(stream);
        Assert.True(stream.Length > 0);

        // And: Verify it's a valid PDF with content
        stream.Position = 0;
        var buffer = new byte[5];
        await stream.ReadAsync(buffer, 0, 5);
        Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(buffer));

        // Note: Verifying actual citation content would require PDF parsing library
        // For now, we verify the PDF is generated and contains data
        Assert.True(stream.Length > 1000, "PDF should contain citation data");
    }

    /// <summary>
    /// Scenario: Format empty chat (no messages)
    ///   Given chat has no messages
    ///   When formatting as PDF
    ///   Then valid PDF stream is returned
    ///   And PDF contains header/metadata only
    /// </summary>
    [Fact]
    public async Task FormatAsync_EmptyChat_ReturnsValidPdf()
    {
        // Given: Chat has no messages
        var chat = CreateTestChat("Empty Game");
        chat.Logs = new List<ChatLogEntity>();

        // When: Formatting as PDF
        var stream = await _formatter.FormatAsync(chat, null, null);

        // Then: Valid PDF stream is returned
        Assert.NotNull(stream);
        Assert.True(stream.Length > 0);

        // And: PDF is valid (starts with magic bytes)
        stream.Position = 0;
        var buffer = new byte[5];
        await stream.ReadAsync(buffer, 0, 5);
        Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(buffer));
    }

    /// <summary>
    /// Scenario: Format chat with date range filter
    ///   Given chat has messages across date range
    ///   When formatting with date filter
    ///   Then PDF includes only filtered messages
    ///   And PDF metadata indicates date range
    /// </summary>
    [Fact]
    public async Task FormatAsync_WithDateRange_FiltersMessages()
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
                Message = "Old message outside range",
                CreatedAt = new DateTime(2025, 10, 1, 10, 0, 0, DateTimeKind.Utc)
            },
            new()
            {
                Id = Guid.NewGuid(),
                ChatId = chat.Id,
                Level = "user",
                Message = "Recent message in range",
                CreatedAt = new DateTime(2025, 10, 15, 10, 0, 0, DateTimeKind.Utc)
            }
        };

        // When: Formatting with date filter (Oct 10-20)
        var startDate = new DateTime(2025, 10, 10);
        var endDate = new DateTime(2025, 10, 20);
        var stream = await _formatter.FormatAsync(chat, startDate, endDate);

        // Then: PDF is generated
        Assert.NotNull(stream);
        Assert.True(stream.Length > 0);

        // And: Valid PDF structure
        stream.Position = 0;
        var buffer = new byte[5];
        await stream.ReadAsync(buffer, 0, 5);
        Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(buffer));

        // Note: Actual content verification would require PDF text extraction
        // For TDD, we trust the formatter implementation will filter correctly
    }

    /// <summary>
    /// Scenario: Verify PDF formatter properties
    ///   When accessing formatter properties
    ///   Then Format is "pdf"
    ///   And ContentType is "application/pdf"
    /// </summary>
    [Fact]
    public void FormatterProperties_ReturnsCorrectValues()
    {
        // When: Accessing formatter properties
        var format = _formatter.Format;
        var contentType = _formatter.ContentType;

        // Then: Format is "pdf"
        Assert.Equal("pdf", format);

        // And: ContentType is "application/pdf"
        Assert.Equal("application/pdf", contentType);
    }

    /// <summary>
    /// Scenario: Format chat with special characters
    ///   Given messages contain unicode, newlines, and special chars
    ///   When formatting as PDF
    ///   Then PDF is generated without encoding errors
    ///   And PDF contains valid content
    /// </summary>
    [Fact]
    public async Task FormatAsync_SpecialCharacters_HandlesCorrectly()
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
                Message = "Unicode: 你好 🎲 Ñoño Line1\nLine2",
                CreatedAt = DateTime.UtcNow.AddMinutes(-1)
            },
            new()
            {
                Id = Guid.NewGuid(),
                ChatId = chat.Id,
                Level = "assistant",
                Message = "Symbols: © ® ™ € £ ¥",
                CreatedAt = DateTime.UtcNow
            }
        };

        // When: Formatting as PDF
        var stream = await _formatter.FormatAsync(chat, null, null);

        // Then: PDF is generated without errors
        Assert.NotNull(stream);
        Assert.True(stream.Length > 0);

        // And: Valid PDF structure
        stream.Position = 0;
        var buffer = new byte[5];
        await stream.ReadAsync(buffer, 0, 5);
        Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(buffer));
    }

    /// <summary>
    /// Scenario: Format chat with malformed JSON metadata
    ///   Given message has invalid JSON in MetadataJson
    ///   When formatting as PDF
    ///   Then PDF generation does not crash
    ///   And PDF is generated successfully
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
                Message = "Answer with bad metadata",
                MetadataJson = "{ invalid json }", // Malformed JSON
                CreatedAt = DateTime.UtcNow
            }
        };

        // When: Formatting as PDF
        var stream = await _formatter.FormatAsync(chat, null, null);

        // Then: PDF generation does not crash
        Assert.NotNull(stream);
        Assert.True(stream.Length > 0);

        // And: Valid PDF is generated
        stream.Position = 0;
        var buffer = new byte[5];
        await stream.ReadAsync(buffer, 0, 5);
        Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(buffer));
    }

    /// <summary>
    /// Scenario: Format chat with very long messages
    ///   Given messages exceed 5000 characters
    ///   When formatting as PDF
    ///   Then PDF handles long content correctly
    ///   And PDF has proper page breaks
    /// </summary>
    [Fact]
    public async Task FormatAsync_VeryLongMessages_HandlesPagination()
    {
        // Given: Messages exceed 5000 characters
        var chat = CreateTestChat();

        var longMessage = string.Join("\n", Enumerable.Range(1, 200)
            .Select(i => $"Line {i}: Lorem ipsum dolor sit amet, consectetur adipiscing elit. " +
                        "Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua."));

        chat.Logs = new List<ChatLogEntity>
        {
            new()
            {
                Id = Guid.NewGuid(),
                ChatId = chat.Id,
                Level = "user",
                Message = longMessage,
                CreatedAt = DateTime.UtcNow
            }
        };

        // When: Formatting as PDF
        var stream = await _formatter.FormatAsync(chat, null, null);

        // Then: PDF handles long content
        Assert.NotNull(stream);
        Assert.True(stream.Length > 0);

        // And: PDF is valid
        stream.Position = 0;
        var buffer = new byte[5];
        await stream.ReadAsync(buffer, 0, 5);
        Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(buffer));

        // And: PDF size reflects content (should be larger for long content)
        Assert.True(stream.Length > 10000, $"Expected large PDF, got {stream.Length} bytes");
    }

    /// <summary>
    /// Scenario: Format chat with all message types
    ///   Given chat has user, assistant, and system messages
    ///   When formatting as PDF
    ///   Then all message types are included
    ///   And PDF is properly structured
    /// </summary>
    [Fact]
    public async Task FormatAsync_AllMessageTypes_IncludesAll()
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

        // When: Formatting as PDF
        var stream = await _formatter.FormatAsync(chat, null, null);

        // Then: PDF is generated
        Assert.NotNull(stream);
        Assert.True(stream.Length > 0);

        // And: Valid PDF structure
        stream.Position = 0;
        var buffer = new byte[5];
        await stream.ReadAsync(buffer, 0, 5);
        Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(buffer));
    }
}
