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
using FluentAssertions;
using Xunit.Abstractions;

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
    private readonly ITestOutputHelper _output;

    private readonly SqliteConnection _connection;
    private readonly MeepleAiDbContext _dbContext;
    private readonly Mock<IExportFormatter> _txtFormatterMock;
    private readonly Mock<IExportFormatter> _pdfFormatterMock;
    private readonly Mock<IExportFormatter> _mdFormatterMock;
    private readonly ChatExportService _service;

    public ChatExportServiceTests(ITestOutputHelper output)
    {
        _output = output;
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
        _txtFormatterMock = new Mock<IExportFormatter>();
        _txtFormatterMock.Setup(f => f.Format).Returns("txt");
        _txtFormatterMock.Setup(f => f.ContentType).Returns("text/plain");
        _txtFormatterMock.Setup(f => f.FileExtension).Returns("txt");

        _pdfFormatterMock = new Mock<IExportFormatter>();
        _pdfFormatterMock.Setup(f => f.Format).Returns("pdf");
        _pdfFormatterMock.Setup(f => f.ContentType).Returns("application/pdf");
        _pdfFormatterMock.Setup(f => f.FileExtension).Returns("pdf");

        _mdFormatterMock = new Mock<IExportFormatter>();
        _mdFormatterMock.Setup(f => f.Format).Returns("md");
        _mdFormatterMock.Setup(f => f.ContentType).Returns("text/markdown");
        _mdFormatterMock.Setup(f => f.FileExtension).Returns("md");

        var formatters = new[]
        {
            _txtFormatterMock.Object,
            _pdfFormatterMock.Object,
            _mdFormatterMock.Object
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
        result.Success.Should().BeFalse();
        result.Error.Should().Be("not_found");
        result.Stream.Should().BeNull();

        // And: No formatter is called
        _pdfFormatterMock.Verify(
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
        result.Success.Should().BeFalse();
        result.Error.Should().Be("unsupported_format");
        result.Stream.Should().BeNull();

        // And: Error message lists supported formats
        result.ErrorDetails ?? "".Should().Contain("pdf");
        result.ErrorDetails ?? "".Should().Contain("txt");
        result.ErrorDetails ?? "".Should().Contain("md");
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
        _txtFormatterMock
            .Setup(f => f.FormatAsync(It.IsAny<ChatEntity>(), null, null))
            .ReturnsAsync(exportStream);

        // When: User requests export in TXT format
        var result = await _service.ExportChatAsync(chat.Id, "user-123", "txt");

        // Then: Correct formatter is selected
        _txtFormatterMock.Verify(
            f => f.FormatAsync(
                It.Is<ChatEntity>(c => c.Id == chat.Id && c.Logs.Any()),
                null,
                null),
            Times.Once);

        // And: Success result with stream is returned
        result.Success.Should().BeTrue();
        result.Stream.Should().Be(exportStream);
        result.ContentType.Should().Be("text/plain");
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
        result.Success.Should().BeFalse();
        result.Error.Should().Be("not_found");

        // And: No formatter is called
        _pdfFormatterMock.Verify(
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
        _pdfFormatterMock
            .Setup(f => f.FormatAsync(It.IsAny<ChatEntity>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(exportStream);

        // When: User requests export with date range (last 5 days)
        var startDate = DateTime.UtcNow.AddDays(-5);
        var endDate = DateTime.UtcNow;
        var result = await _service.ExportChatAsync(chat.Id, "user-123", "pdf", startDate, endDate);

        // Then: Formatter receives date range parameters
        _pdfFormatterMock.Verify(
            f => f.FormatAsync(
                It.Is<ChatEntity>(c => c.Id == chat.Id),
                It.Is<DateTime?>(d => d.HasValue && (d.Value - startDate).TotalSeconds < 1),
                It.Is<DateTime?>(d => d.HasValue && (d.Value - endDate).TotalSeconds < 1)),
            Times.Once);

        result.Success.Should().BeTrue();
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

        _pdfFormatterMock
            .Setup(f => f.FormatAsync(It.IsAny<ChatEntity>(), null, null))
            .ThrowsAsync(new InvalidOperationException("PDF generation failed"));

        // When: Formatter throws exception
        var result = await _service.ExportChatAsync(chat.Id, "user-123", "pdf");

        // Then: GenerationFailed result is returned
        result.Success.Should().BeFalse();
        result.Error.Should().Be("generation_failed");

        // And: Error message includes exception details
        result.ErrorDetails ?? "".Should().Contain("PDF generation failed");
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
        _txtFormatterMock
            .Setup(f => f.FormatAsync(It.IsAny<ChatEntity>(), null, null))
            .ReturnsAsync(exportStream);

        // When: Generating export filename
        var result = await _service.ExportChatAsync(chat.Id, "user-123", "txt");

        // Then: Filename is sanitized and follows pattern
        result.Success.Should().BeTrue();
        result.Filename.Should().NotBeNull();
        result.Filename.Should().Contain(expectedSanitized);
        result.Filename.Should().StartWith("chat-");
        result.Filename.Should().EndWith(".txt");

        // And: Path traversal attempts are blocked
        result.Filename.Should().NotContain("..");
        result.Filename.Should().NotContain("/");
        result.Filename.Should().NotContain("\\");
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
        _pdfFormatterMock
            .Setup(f => f.FormatAsync(It.IsAny<ChatEntity>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(exportStream);

        var startDate = new DateTime(2025, 10, 1);
        var endDate = new DateTime(2025, 10, 15);

        // When: Generating filename
        var result = await _service.ExportChatAsync(chat.Id, "user-123", "pdf", startDate, endDate);

        // Then: Filename includes date range
        result.Success.Should().BeTrue();
        result.Filename.Should().NotBeNull();
        result.Filename.Should().Contain("2025-10-01");
        result.Filename.Should().Contain("2025-10-15");
        result.Filename.Should().Contain("to");
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
        _mdFormatterMock
            .Setup(f => f.FormatAsync(It.IsAny<ChatEntity>(), null, null))
            .ReturnsAsync(exportStream);

        // When: User exports chat
        var result = await _service.ExportChatAsync(chat.Id, "user-123", "md");

        // Then: Formatter is still called
        _mdFormatterMock.Verify(
            f => f.FormatAsync(
                It.Is<ChatEntity>(c => c.Id == chat.Id && !c.Logs.Any()),
                null,
                null),
            Times.Once);

        // And: Export succeeds
        result.Success.Should().BeTrue();
        result.ContentType.Should().Be("text/markdown");
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
        _pdfFormatterMock
            .Setup(f => f.FormatAsync(It.IsAny<ChatEntity>(), null, null))
            .ReturnsAsync(exportStream);

        // When: User exports chat
        var result = await _service.ExportChatAsync(chat.Id, "user-123", "pdf");

        // Then: All message types are included
        _pdfFormatterMock.Verify(
            f => f.FormatAsync(
                It.Is<ChatEntity>(c => c.Logs.Count == 3 &&
                                      c.Logs.Any(l => l.Level == "user") &&
                                      c.Logs.Any(l => l.Level == "assistant") &&
                                      c.Logs.Any(l => l.Level == "system")),
                null,
                null),
            Times.Once);

        result.Success.Should().BeTrue();
    }

    /// <summary>
    /// Scenario: User requests export with different format casing
    ///   Given chat exists with messages
    ///   When user requests export with uppercase/mixed-case format (e.g., "PDF", "Pdf")
    ///   Then correct formatter is selected (case-insensitive match)
    ///   And export succeeds with correct content type
    /// </summary>
    [Theory]
    [InlineData("pdf", "application/pdf")]
    [InlineData("PDF", "application/pdf")]
    [InlineData("Pdf", "application/pdf")]
    [InlineData("txt", "text/plain")]
    [InlineData("TXT", "text/plain")]
    [InlineData("md", "text/markdown")]
    [InlineData("MD", "text/markdown")]
    public async Task ExportChatAsync_FormatCaseInsensitive_SelectsCorrectFormatter(string format, string expectedContentType)
    {
        // Given: Chat exists with messages
        var chat = await CreateTestChatAsync();

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
        var mockFormatter = format.ToLower() == "pdf" ? _pdfFormatterMock :
                            format.ToLower() == "txt" ? _txtFormatterMock :
                            _mdFormatterMock;

        mockFormatter
            .Setup(f => f.FormatAsync(It.IsAny<ChatEntity>(), null, null))
            .ReturnsAsync(exportStream);

        // When: User requests export with case variant
        var result = await _service.ExportChatAsync(chat.Id, "user-123", format);

        // Then: Correct formatter is selected
        mockFormatter.Verify(
            f => f.FormatAsync(It.Is<ChatEntity>(c => c.Id == chat.Id), null, null),
            Times.Once);

        // And: Export succeeds with correct content type
        result.Success.Should().BeTrue();
        result.ContentType.Should().Be(expectedContentType);
    }

    /// <summary>
    /// Scenario: Export operation is cancelled mid-operation
    ///   Given export operation is in progress
    ///   When cancellation token is triggered
    ///   Then OperationCanceledException is thrown
    ///   And operation is aborted gracefully
    /// </summary>
    [Fact]
    public async Task ExportChatAsync_CancellationTokenTriggered_ThrowsOperationCanceledException()
    {
        // Given: Chat exists
        var chat = await CreateTestChatAsync();

        // When: Cancellation token is already cancelled
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Then: OperationCanceledException is thrown
        var act = async () => await _service.ExportChatAsync(chat.Id, "user-123", "pdf", ct: cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    /// <summary>
    /// Scenario: Generate filename with empty/whitespace game name
    ///   Given game name is empty, null, or whitespace-only
    ///   When generating export filename
    ///   Then filename uses "chat" as fallback
    ///   And format is "chat-chat-{id}.{ext}"
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\t")]
    [InlineData("\n")]
    [InlineData("     \t\n")]
    public async Task ExportChatAsync_EmptyGameName_UsesChatFallback(string emptyGameName)
    {
        // Given: Game name is empty/whitespace
        var chat = await CreateTestChatAsync("user-123", emptyGameName);

        var exportStream = new MemoryStream();
        _txtFormatterMock
            .Setup(f => f.FormatAsync(It.IsAny<ChatEntity>(), null, null))
            .ReturnsAsync(exportStream);

        // When: Generating export filename
        var result = await _service.ExportChatAsync(chat.Id, "user-123", "txt");

        // Then: Filename uses "chat" as fallback
        result.Success.Should().BeTrue();
        result.Filename.Should().NotBeNull();
        result.Filename.Should().StartWith("chat-chat-");
        result.Filename.Should().EndWith(".txt");
    }

    /// <summary>
    /// Scenario: Generate filename with control characters in game name
    ///   Given game name contains control characters (ASCII 0-31, 127)
    ///   When generating filename
    ///   Then control characters are removed
    ///   And filename is sanitized and valid
    /// </summary>
    [Theory]
    [InlineData("Game\tName", "GameName")]  // Tab (ASCII 9)
    [InlineData("Game\x00Name", "GameName")]  // Null (ASCII 0)
    [InlineData("Game\x07Name", "GameName")]  // Bell (ASCII 7)
    [InlineData("Game\x1BName", "GameName")]  // Escape (ASCII 27)
    [InlineData("Game\x7FName", "GameName")]  // DEL (ASCII 127)
    public async Task ExportChatAsync_ControlCharactersInGameName_RemovesControlChars(string gameName, string expectedSanitized)
    {
        // Given: Game name contains control characters
        var chat = await CreateTestChatAsync("user-123", gameName);

        var exportStream = new MemoryStream();
        _pdfFormatterMock
            .Setup(f => f.FormatAsync(It.IsAny<ChatEntity>(), null, null))
            .ReturnsAsync(exportStream);

        // When: Generating filename
        var result = await _service.ExportChatAsync(chat.Id, "user-123", "pdf");

        // Then: Control characters are removed
        result.Success.Should().BeTrue();
        result.Filename.Should().NotBeNull();
        result.Filename.Should().Contain(expectedSanitized);

        // And: Filename starts with sanitized game name
        result.Filename.Split("-chat-")[0].Should().StartWith(expectedSanitized);
    }

    /// <summary>
    /// Scenario: Generate filename with very long game name
    ///   Given game name exceeds 50 characters
    ///   When generating filename
    ///   Then game name is truncated to 50 chars
    ///   And filename includes truncated name + chat ID
    ///   And filename is valid and not excessively long
    /// </summary>
    [Fact]
    public async Task ExportChatAsync_VeryLongGameName_TruncatesTo50Chars()
    {
        // Given: Game name exceeds 50 characters
        var longGameName = "This is an extremely long game name that definitely exceeds the fifty character limit for filenames";
        var chat = await CreateTestChatAsync("user-123", longGameName);

        var exportStream = new MemoryStream();
        _mdFormatterMock
            .Setup(f => f.FormatAsync(It.IsAny<ChatEntity>(), null, null))
            .ReturnsAsync(exportStream);

        // When: Generating filename
        var result = await _service.ExportChatAsync(chat.Id, "user-123", "md");

        // Then: Game name is truncated
        result.Success.Should().BeTrue();
        result.Filename.Should().NotBeNull();

        // And: Filename contains truncated portion (max 50 chars of game name)
        // Format: {gameName(<=50)}-chat-{id(8)}.{ext}
        var gameNamePart = result.Filename.Split("-chat-")[0];
        gameNamePart.Length <= 50, $"Game name part '{gameNamePart}' exceeds 50 chars: {gameNamePart.Length}".Should().BeTrue();

        // And: Filename is reasonable length (truncated game name + "-chat-" + 8-char ID + ".md")
        result.Filename.Length < 80, $"Filename too long: {result.Filename.Length} chars".Should().BeTrue();
    }

    /// <summary>
    /// Scenario: Generate filename with newlines in game name
    ///   Given game name contains \n, \r, or \r\n
    ///   When generating filename
    ///   Then newlines are removed
    ///   And filename is valid without line breaks
    /// </summary>
    [Theory]
    [InlineData("Game\nName", "GameName")]
    [InlineData("Game\rName", "GameName")]
    [InlineData("Game\r\nName", "GameName")]
    [InlineData("Multi\nLine\nGame", "MultiLineGame")]
    public async Task ExportChatAsync_NewlinesInGameName_RemovesNewlines(string gameName, string expectedSanitized)
    {
        // Given: Game name contains newlines
        var chat = await CreateTestChatAsync("user-123", gameName);

        var exportStream = new MemoryStream();
        _txtFormatterMock
            .Setup(f => f.FormatAsync(It.IsAny<ChatEntity>(), null, null))
            .ReturnsAsync(exportStream);

        // When: Generating filename
        var result = await _service.ExportChatAsync(chat.Id, "user-123", "txt");

        // Then: Newlines are removed
        result.Success.Should().BeTrue();
        result.Filename.Should().NotBeNull();
        result.Filename.Should().Contain(expectedSanitized);
        result.Filename.Should().NotContain("\n");
        result.Filename.Should().NotContain("\r");
    }

    /// <summary>
    /// Scenario: Multiple users export same chat concurrently
    ///   Given chat exists with multiple messages
    ///   When 5 concurrent export requests are made
    ///   Then all exports succeed independently
    ///   And no race conditions occur
    ///   And each export returns valid stream
    /// </summary>
    [Fact]
    public async Task ExportChatAsync_ConcurrentRequests_AllSucceedIndependently()
    {
        // Given: Chat exists with messages
        var chat = await CreateTestChatAsync();

        var log = new ChatLogEntity
        {
            Id = Guid.NewGuid(),
            ChatId = chat.Id,
            Level = "user",
            Message = "Concurrent test message",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.ChatLogs.Add(log);
        await _dbContext.SaveChangesAsync();

        // Setup formatter to return unique streams
        _pdfFormatterMock
            .Setup(f => f.FormatAsync(It.IsAny<ChatEntity>(), null, null))
            .ReturnsAsync(() => new MemoryStream());

        // When: 5 concurrent export requests are made
        var tasks = Enumerable.Range(0, 5).Select(_ =>
            _service.ExportChatAsync(chat.Id, "user-123", "pdf")
        ).ToArray();

        var results = await Task.WhenAll(tasks);

        // Then: All exports succeed
        results.Should().OnlyContain(result => result.Success);

        // And: Each has a valid stream
        results.Should().OnlyContain(result => result.Stream != null);

        // And: Formatter was called 5 times (once per request)
        _pdfFormatterMock.Verify(
            f => f.FormatAsync(It.IsAny<ChatEntity>(), null, null),
            Times.Exactly(5));
    }

    /// <summary>
    /// Scenario: Export chat with 100+ messages
    ///   Given chat has 150 messages
    ///   When user exports chat
    ///   Then export completes successfully
    ///   And all 150 messages are loaded
    ///   And formatter receives complete chat entity
    /// </summary>
    [Fact]
    public async Task ExportChatAsync_LargeChatWith150Messages_CompletesSuccessfully()
    {
        // Given: Chat has 150 messages
        var chat = await CreateTestChatAsync();

        var logs = Enumerable.Range(1, 150).Select(i => new ChatLogEntity
        {
            Id = Guid.NewGuid(),
            ChatId = chat.Id,
            Level = i % 2 == 0 ? "assistant" : "user",
            Message = $"Message number {i} with some content to simulate realistic chat data.",
            CreatedAt = DateTime.UtcNow.AddMinutes(i)
        }).ToList();

        _dbContext.ChatLogs.AddRange(logs);
        await _dbContext.SaveChangesAsync();

        var exportStream = new MemoryStream();
        _pdfFormatterMock
            .Setup(f => f.FormatAsync(It.IsAny<ChatEntity>(), null, null))
            .ReturnsAsync(exportStream);

        // When: User exports chat
        var result = await _service.ExportChatAsync(chat.Id, "user-123", "pdf");

        // Then: Export completes successfully
        result.Success.Should().BeTrue();

        // And: All 150 messages are loaded and passed to formatter
        _pdfFormatterMock.Verify(
            f => f.FormatAsync(
                It.Is<ChatEntity>(c => c.Logs.Count == 150),
                null,
                null),
            Times.Once);
    }
}
