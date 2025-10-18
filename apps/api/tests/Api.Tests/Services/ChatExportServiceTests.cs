using System;
using System.Linq;
using System.Threading.Tasks;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.Services;

/// <summary>
/// BDD-style unit tests for ChatExportService (CHAT-05).
///
/// Feature: Chat Export Service
/// As a user
/// I want to export my chat conversations in multiple formats
/// So that I can archive and review conversations offline
///
/// Test Strategy: SQLite in-memory database for fast, isolated unit tests
/// </summary>
public class ChatExportServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly MeepleAiDbContext _dbContext;
    private readonly Mock<IExportFormatter> _mockTxtFormatter;
    private readonly Mock<IExportFormatter> _mockPdfFormatter;
    private readonly Mock<IExportFormatter> _mockMdFormatter;
    private readonly ChatExportService _service;

    public ChatExportServiceTests()
    {
        // Setup SQLite in-memory database
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        // Enable foreign keys for SQLite
        using (var command = _connection.CreateCommand())
        {
            command.CommandText = "PRAGMA foreign_keys = ON;";
            command.ExecuteNonQuery();
        }

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new MeepleAiDbContext(options);
        _dbContext.Database.EnsureCreated();

        // Setup mock formatters
        _mockTxtFormatter = new Mock<IExportFormatter>();
        _mockTxtFormatter.Setup(f => f.Format).Returns("txt");
        _mockTxtFormatter.Setup(f => f.ContentType).Returns("text/plain");

        _mockPdfFormatter = new Mock<IExportFormatter>();
        _mockPdfFormatter.Setup(f => f.Format).Returns("pdf");
        _mockPdfFormatter.Setup(f => f.ContentType).Returns("application/pdf");

        _mockMdFormatter = new Mock<IExportFormatter>();
        _mockMdFormatter.Setup(f => f.Format).Returns("md");
        _mockMdFormatter.Setup(f => f.ContentType).Returns("text/markdown");

        var formatters = new[]
        {
            _mockTxtFormatter.Object,
            _mockPdfFormatter.Object,
            _mockMdFormatter.Object
        };

        _service = new ChatExportService(
            _dbContext,
            formatters,
            NullLogger<ChatExportService>.Instance);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    private async Task<ChatEntity> CreateTestChatAsync(string userId = "user-123", string gameName = "Test Game")
    {
        var user = new UserEntity
        {
            Id = userId,
            Email = $"{userId}@test.com",
            PasswordHash = "hash",
            Role = UserRole.User,
            CreatedAt = DateTime.UtcNow
        };

        var game = new GameEntity
        {
            Id = "test-game",
            Name = gameName
        };

        var agent = new AgentEntity
        {
            Id = "test-agent",
            GameId = "test-game",
            Name = "Q&A Agent",
            Kind = "qa",
            CreatedAt = DateTime.UtcNow
        };

        var chat = new ChatEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            GameId = "test-game",
            AgentId = "test-agent",
            StartedAt = DateTime.UtcNow.AddHours(-1)
        };

        _dbContext.Users.Add(user);
        _dbContext.Games.Add(game);
        _dbContext.Agents.Add(agent);
        _dbContext.Chats.Add(chat);
        await _dbContext.SaveChangesAsync();

        return chat;
    }

    /// <summary>
    /// Scenario: User exports non-existent chat
    ///   Given a chat ID that doesn't exist
    ///   When user attempts to export
    ///   Then NotFound result is returned
    ///   And no formatter is called
    /// </summary>
    [Fact]
    public async Task ExportChatAsync_ChatNotFound_ReturnsNotFound()
    {
        // Given: A chat ID that doesn't exist
        var nonExistentChatId = Guid.NewGuid();

        // When: User attempts to export
        var result = await _service.ExportChatAsync(nonExistentChatId, "user-123", "pdf");

        // Then: NotFound result is returned
        Assert.False(result.Success);
        Assert.Equal("not_found", result.Error);
        Assert.Null(result.Stream);

        // And: No formatter is called
        _mockPdfFormatter.Verify(
            f => f.FormatAsync(It.IsAny<ChatEntity>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()),
            Times.Never);
    }

    /// <summary>
    /// Scenario: User exports chat with unsupported format
    ///   Given a valid chat exists
    ///   When user requests export with unsupported format
    ///   Then UnsupportedFormat result is returned
    ///   And error message lists supported formats
    /// </summary>
    [Fact]
    public async Task ExportChatAsync_UnsupportedFormat_ReturnsUnsupportedFormat()
    {
        // Given: A valid chat exists
        var chat = await CreateTestChatAsync();

        // When: User requests export with unsupported format
        var result = await _service.ExportChatAsync(chat.Id, "user-123", "xml");

        // Then: UnsupportedFormat result is returned
        Assert.False(result.Success);
        Assert.Equal("unsupported_format", result.Error);
        Assert.Null(result.Stream);

        // And: Error message lists supported formats
        Assert.Contains("pdf", result.ErrorDetails ?? "");
        Assert.Contains("txt", result.ErrorDetails ?? "");
        Assert.Contains("md", result.ErrorDetails ?? "");
    }

    /// <summary>
    /// Scenario: User exports their own chat with valid format
    ///   Given user owns a chat
    ///   When user requests export in TXT format
    ///   Then correct formatter is selected
    ///   And formatter receives full chat with logs
    ///   And success result with stream is returned
    /// </summary>
    [Fact]
    public async Task ExportChatAsync_ValidChat_SelectsCorrectFormatter()
    {
        // Given: User owns a chat
        var chat = await CreateTestChatAsync("user-123");

        var log = new ChatLogEntity
        {
            Id = Guid.NewGuid(),
            ChatId = chat.Id,
            Level = "user",
            Message = "Test message",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.ChatLogs.Add(log);
        await _dbContext.SaveChangesAsync();

        var exportStream = new MemoryStream();
        _mockTxtFormatter
            .Setup(f => f.FormatAsync(It.IsAny<ChatEntity>(), null, null))
            .ReturnsAsync(exportStream);

        // When: User requests export in TXT format
        var result = await _service.ExportChatAsync(chat.Id, "user-123", "txt");

        // Then: Correct formatter is selected
        _mockTxtFormatter.Verify(
            f => f.FormatAsync(
                It.Is<ChatEntity>(c => c.Id == chat.Id && c.Logs.Any()),
                null,
                null),
            Times.Once);

        // And: Success result with stream is returned
        Assert.True(result.Success);
        Assert.Equal(exportStream, result.Stream);
        Assert.Equal("text/plain", result.ContentType);
    }

    /// <summary>
    /// Scenario: User exports another user's chat
    ///   Given chat belongs to different user
    ///   When user attempts to export
    ///   Then NotFound result is returned (not Forbidden for security)
    ///   And no formatter is called
    /// </summary>
    [Fact]
    public async Task ExportChatAsync_OtherUsersChat_ReturnsNotFound()
    {
        // Given: Chat belongs to different user
        var chat = await CreateTestChatAsync("owner-456");

        // When: Different user attempts to export
        var result = await _service.ExportChatAsync(chat.Id, "attacker-789", "pdf");

        // Then: NotFound result is returned (not Forbidden for security)
        Assert.False(result.Success);
        Assert.Equal("not_found", result.Error);

        // And: No formatter is called
        _mockPdfFormatter.Verify(
            f => f.FormatAsync(It.IsAny<ChatEntity>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()),
            Times.Never);
    }

    /// <summary>
    /// Scenario: User exports chat with date range filter
    ///   Given chat has messages across multiple days
    ///   When user requests export with date range
    ///   Then only logs within date range are included
    ///   And formatter receives filtered logs
    /// </summary>
    [Fact]
    public async Task ExportChatAsync_DateRangeFilter_FiltersLogsCorrectly()
    {
        // Given: Chat has messages across multiple days
        var chat = await CreateTestChatAsync();

        var oldLog = new ChatLogEntity
        {
            Id = Guid.NewGuid(),
            ChatId = chat.Id,
            Level = "user",
            Message = "Old message",
            CreatedAt = DateTime.UtcNow.AddDays(-10)
        };

        var recentLog = new ChatLogEntity
        {
            Id = Guid.NewGuid(),
            ChatId = chat.Id,
            Level = "user",
            Message = "Recent message",
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };

        _dbContext.ChatLogs.AddRange(oldLog, recentLog);
        await _dbContext.SaveChangesAsync();

        var exportStream = new MemoryStream();
        _mockPdfFormatter
            .Setup(f => f.FormatAsync(It.IsAny<ChatEntity>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(exportStream);

        // When: User requests export with date range (last 5 days)
        var startDate = DateTime.UtcNow.AddDays(-5);
        var endDate = DateTime.UtcNow;
        var result = await _service.ExportChatAsync(chat.Id, "user-123", "pdf", startDate, endDate);

        // Then: Formatter receives date range parameters
        _mockPdfFormatter.Verify(
            f => f.FormatAsync(
                It.Is<ChatEntity>(c => c.Id == chat.Id),
                It.Is<DateTime?>(d => d.HasValue && (d.Value - startDate).TotalSeconds < 1),
                It.Is<DateTime?>(d => d.HasValue && (d.Value - endDate).TotalSeconds < 1)),
            Times.Once);

        Assert.True(result.Success);
    }

    /// <summary>
    /// Scenario: Formatter throws exception during export
    ///   Given valid chat exists
    ///   When formatter throws exception
    ///   Then GenerationFailed result is returned
    ///   And error message includes exception details
    /// </summary>
    [Fact]
    public async Task ExportChatAsync_FormatterThrows_ReturnsGenerationFailed()
    {
        // Given: Valid chat exists
        var chat = await CreateTestChatAsync();

        _mockPdfFormatter
            .Setup(f => f.FormatAsync(It.IsAny<ChatEntity>(), null, null))
            .ThrowsAsync(new InvalidOperationException("PDF generation failed"));

        // When: Formatter throws exception
        var result = await _service.ExportChatAsync(chat.Id, "user-123", "pdf");

        // Then: GenerationFailed result is returned
        Assert.False(result.Success);
        Assert.Equal("generation_failed", result.Error);

        // And: Error message includes exception details
        Assert.Contains("PDF generation failed", result.ErrorDetails ?? "");
    }

    /// <summary>
    /// Scenario: Generate filename with special characters
    ///   Given game name contains special characters
    ///   When generating export filename
    ///   Then special characters are sanitized
    ///   And path traversal attempts are blocked
    ///   And filename follows pattern: chat-{gameName}-{date}.{ext}
    /// </summary>
    [Theory]
    [InlineData("Dungeons & Dragons", "Dungeons--Dragons")]
    [InlineData("../../etc/passwd", "..-..-etc-passwd")]
    [InlineData("Game: The Reckoning!", "Game--The-Reckoning-")]
    [InlineData("テスト", "テスト")] // Unicode should be preserved
    public async Task GenerateFilename_SpecialCharacters_SanitizesCorrectly(string gameName, string expectedSanitized)
    {
        // Given: Game name contains special characters
        var chat = await CreateTestChatAsync("user-123", gameName);

        var exportStream = new MemoryStream();
        _mockTxtFormatter
            .Setup(f => f.FormatAsync(It.IsAny<ChatEntity>(), null, null))
            .ReturnsAsync(exportStream);

        // When: Generating export filename
        var result = await _service.ExportChatAsync(chat.Id, "user-123", "txt");

        // Then: Filename is sanitized and follows pattern
        Assert.True(result.Success);
        Assert.NotNull(result.Filename);
        Assert.Contains(expectedSanitized, result.Filename);
        Assert.StartsWith("chat-", result.Filename);
        Assert.EndsWith(".txt", result.Filename);

        // And: Path traversal attempts are blocked
        Assert.DoesNotContain("..", result.Filename);
        Assert.DoesNotContain("/", result.Filename);
        Assert.DoesNotContain("\\", result.Filename);
    }

    /// <summary>
    /// Scenario: Generate filename includes date range when specified
    ///   Given user exports with date range
    ///   When generating filename
    ///   Then filename includes date range information
    ///   And format is: chat-{game}-{startDate}-to-{endDate}.{ext}
    /// </summary>
    [Fact]
    public async Task GenerateFilename_WithDateRange_IncludesDateRange()
    {
        // Given: User exports with date range
        var chat = await CreateTestChatAsync();

        var exportStream = new MemoryStream();
        _mockPdfFormatter
            .Setup(f => f.FormatAsync(It.IsAny<ChatEntity>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(exportStream);

        var startDate = new DateTime(2025, 10, 1);
        var endDate = new DateTime(2025, 10, 15);

        // When: Generating filename
        var result = await _service.ExportChatAsync(chat.Id, "user-123", "pdf", startDate, endDate);

        // Then: Filename includes date range
        Assert.True(result.Success);
        Assert.NotNull(result.Filename);
        Assert.Contains("2025-10-01", result.Filename);
        Assert.Contains("2025-10-15", result.Filename);
        Assert.Contains("to", result.Filename);
    }

    /// <summary>
    /// Scenario: Export empty chat (no messages)
    ///   Given chat exists but has no messages
    ///   When user exports chat
    ///   Then formatter is still called
    ///   And export succeeds with empty content
    /// </summary>
    [Fact]
    public async Task ExportChatAsync_EmptyChat_SucceedsWithEmptyContent()
    {
        // Given: Chat exists but has no messages
        var chat = await CreateTestChatAsync();

        var exportStream = new MemoryStream();
        _mockMdFormatter
            .Setup(f => f.FormatAsync(It.IsAny<ChatEntity>(), null, null))
            .ReturnsAsync(exportStream);

        // When: User exports chat
        var result = await _service.ExportChatAsync(chat.Id, "user-123", "md");

        // Then: Formatter is still called
        _mockMdFormatter.Verify(
            f => f.FormatAsync(
                It.Is<ChatEntity>(c => c.Id == chat.Id && !c.Logs.Any()),
                null,
                null),
            Times.Once);

        // And: Export succeeds
        Assert.True(result.Success);
        Assert.Equal("text/markdown", result.ContentType);
    }

    /// <summary>
    /// Scenario: Export chat with multiple message types
    ///   Given chat has user, assistant, and system messages
    ///   When user exports chat
    ///   Then all message types are included in export
    ///   And formatter receives complete log collection
    /// </summary>
    [Fact]
    public async Task ExportChatAsync_MultipleMessageTypes_IncludesAll()
    {
        // Given: Chat has user, assistant, and system messages
        var chat = await CreateTestChatAsync();

        var userLog = new ChatLogEntity
        {
            Id = Guid.NewGuid(),
            ChatId = chat.Id,
            Level = "user",
            Message = "User question",
            CreatedAt = DateTime.UtcNow.AddMinutes(-3)
        };

        var assistantLog = new ChatLogEntity
        {
            Id = Guid.NewGuid(),
            ChatId = chat.Id,
            Level = "assistant",
            Message = "Assistant response",
            CreatedAt = DateTime.UtcNow.AddMinutes(-2)
        };

        var systemLog = new ChatLogEntity
        {
            Id = Guid.NewGuid(),
            ChatId = chat.Id,
            Level = "system",
            Message = "System notification",
            CreatedAt = DateTime.UtcNow.AddMinutes(-1)
        };

        _dbContext.ChatLogs.AddRange(userLog, assistantLog, systemLog);
        await _dbContext.SaveChangesAsync();

        var exportStream = new MemoryStream();
        _mockPdfFormatter
            .Setup(f => f.FormatAsync(It.IsAny<ChatEntity>(), null, null))
            .ReturnsAsync(exportStream);

        // When: User exports chat
        var result = await _service.ExportChatAsync(chat.Id, "user-123", "pdf");

        // Then: All message types are included
        _mockPdfFormatter.Verify(
            f => f.FormatAsync(
                It.Is<ChatEntity>(c => c.Logs.Count == 3 &&
                                      c.Logs.Any(l => l.Level == "user") &&
                                      c.Logs.Any(l => l.Level == "assistant") &&
                                      c.Logs.Any(l => l.Level == "system")),
                null,
                null),
            Times.Once);

        Assert.True(result.Success);
    }
}
