using Api.Infrastructure;
using Api.Tests.TestHelpers;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.Interfaces;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.Services;

/// <summary>
/// Unit tests for ChatExportService covering all code paths.
/// Tests: chat lookup, format selection, date filtering, filename generation, error handling.
/// Issue: #2192 - Add unit tests for Chat Export feature
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class ChatExportServiceTests
{
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    #region Chat Not Found Tests

    [Fact]
    public async Task ExportChatAsync_ChatNotFound_ReturnsNotFound()
    {
        // Arrange
        await using var context = CreateDbContext();
        var service = CreateService(context, CreateMockFormatters());
        var chatId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Act
        var result = await service.ExportChatAsync(chatId, userId, "json", ct: TestCancellationToken);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Be("not_found");
    }

    [Fact]
    public async Task ExportChatAsync_ChatExistsButDifferentUser_ReturnsNotFound()
    {
        // Arrange
        await using var context = CreateDbContext();
        var ownerId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var chat = await CreateChatWithGame(context, ownerId, "Test Game");

        var service = CreateService(context, CreateMockFormatters());

        // Act
        var result = await service.ExportChatAsync(chat.Id, requesterId, "json", ct: TestCancellationToken);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Be("not_found");
    }

    #endregion

    #region Unsupported Format Tests

    [Fact]
    public async Task ExportChatAsync_UnsupportedFormat_ReturnsUnsupportedFormat()
    {
        // Arrange
        await using var context = CreateDbContext();
        var userId = Guid.NewGuid();
        var chat = await CreateChatWithGame(context, userId, "Test Game");

        var service = CreateService(context, CreateMockFormatters("json", "md"));

        // Act
        var result = await service.ExportChatAsync(chat.Id, userId, "pdf", ct: TestCancellationToken);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Be("unsupported_format");
        result.ErrorDetails.Should().Contain("pdf");
    }

    [Fact]
    public async Task ExportChatAsync_EmptyFormat_ReturnsUnsupportedFormat()
    {
        // Arrange
        await using var context = CreateDbContext();
        var userId = Guid.NewGuid();
        var chat = await CreateChatWithGame(context, userId, "Test Game");

        var service = CreateService(context, CreateMockFormatters("json"));

        // Act
        var result = await service.ExportChatAsync(chat.Id, userId, "", ct: TestCancellationToken);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Be("unsupported_format");
    }

    #endregion

    #region Successful Export Tests

    [Fact]
    public async Task ExportChatAsync_ValidRequest_ReturnsSuccessWithStream()
    {
        // Arrange
        await using var context = CreateDbContext();
        var userId = Guid.NewGuid();
        var chat = await CreateChatWithGame(context, userId, "Test Game");

        var expectedStream = new MemoryStream([1, 2, 3]);
        var mockFormatter = CreateMockFormatter("json", "application/json", ".json", expectedStream);
        var service = CreateService(context, [mockFormatter.Object]);

        // Act
        var result = await service.ExportChatAsync(chat.Id, userId, "json", ct: TestCancellationToken);

        // Assert
        result.Success.Should().BeTrue();
        result.Stream.Should().NotBeNull();
        result.ContentType.Should().Be("application/json");
        result.Filename.Should().Contain(".json");
    }

    [Fact]
    public async Task ExportChatAsync_CaseInsensitiveFormat_MatchesFormatter()
    {
        // Arrange
        await using var context = CreateDbContext();
        var userId = Guid.NewGuid();
        var chat = await CreateChatWithGame(context, userId, "Test Game");

        var mockFormatter = CreateMockFormatter("json", "application/json", ".json", new MemoryStream());
        var service = CreateService(context, [mockFormatter.Object]);

        // Act - Use uppercase format
        var result = await service.ExportChatAsync(chat.Id, userId, "JSON", ct: TestCancellationToken);

        // Assert
        result.Success.Should().BeTrue();
        mockFormatter.Verify(f => f.FormatAsync(It.IsAny<ChatEntity>(), null, null), Times.Once);
    }

    [Fact]
    public async Task ExportChatAsync_MixedCaseFormat_MatchesFormatter()
    {
        // Arrange
        await using var context = CreateDbContext();
        var userId = Guid.NewGuid();
        var chat = await CreateChatWithGame(context, userId, "Test Game");

        var mockFormatter = CreateMockFormatter("markdown", "text/markdown", ".md", new MemoryStream());
        var service = CreateService(context, [mockFormatter.Object]);

        // Act - Use mixed case
        var result = await service.ExportChatAsync(chat.Id, userId, "MarkDown", ct: TestCancellationToken);

        // Assert
        result.Success.Should().BeTrue();
    }

    #endregion

    #region Filename Generation Tests

    [Fact]
    public async Task ExportChatAsync_GeneratesFilenameWithGameName()
    {
        // Arrange
        await using var context = CreateDbContext();
        var userId = Guid.NewGuid();
        var chat = await CreateChatWithGame(context, userId, "Catan");

        var mockFormatter = CreateMockFormatter("json", "application/json", ".json", new MemoryStream());
        var service = CreateService(context, [mockFormatter.Object]);

        // Act
        var result = await service.ExportChatAsync(chat.Id, userId, "json", ct: TestCancellationToken);

        // Assert
        result.Success.Should().BeTrue();
        result.Filename.Should().NotBeNull();
        result.Filename.ToLowerInvariant().Should().Contain("catan");
        result.Filename.Should().EndWith(".json");
    }

    [Fact]
    public async Task ExportChatAsync_ValidGame_IncludesGameNameInFilename()
    {
        // Arrange
        await using var context = CreateDbContext();
        var userId = Guid.NewGuid();
        // This test validates that the filename includes the game name.
        // Game.Name is a required field that's always populated in the database.
        var chat = await CreateChatWithGame(context, userId, "TestGame");

        var mockFormatter = CreateMockFormatter("json", "application/json", ".json", new MemoryStream());
        var service = CreateService(context, [mockFormatter.Object]);

        // Act
        var result = await service.ExportChatAsync(chat.Id, userId, "json", ct: TestCancellationToken);

        // Assert
        result.Success.Should().BeTrue();
        result.Filename.Should().NotBeNull();
        result.Filename.ToLowerInvariant().Should().Contain("testgame");
    }

    [Fact]
    public async Task ExportChatAsync_GameWithSpecialCharacters_GeneratesSafeFilename()
    {
        // Arrange
        await using var context = CreateDbContext();
        var userId = Guid.NewGuid();
        var chat = await CreateChatWithGame(context, userId, "Game: Test/Version <2.0>");

        var mockFormatter = CreateMockFormatter("json", "application/json", ".json", new MemoryStream());
        var service = CreateService(context, [mockFormatter.Object]);

        // Act
        var result = await service.ExportChatAsync(chat.Id, userId, "json", ct: TestCancellationToken);

        // Assert
        result.Success.Should().BeTrue();
        result.Filename.Should().NotBeNull();
        // Should not contain dangerous characters
        result.Filename.Should().NotContain(":");
        result.Filename.Should().NotContain("/");
        result.Filename.Should().NotContain("<");
        result.Filename.Should().NotContain(">");
    }

    #endregion

    #region Date Filtering Tests

    [Fact]
    public async Task ExportChatAsync_WithDateFrom_PassesToFormatter()
    {
        // Arrange
        await using var context = CreateDbContext();
        var userId = Guid.NewGuid();
        var chat = await CreateChatWithGame(context, userId, "Test Game");

        var dateFrom = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var mockFormatter = CreateMockFormatter("json", "application/json", ".json", new MemoryStream());
        var service = CreateService(context, [mockFormatter.Object]);

        // Act
        var result = await service.ExportChatAsync(chat.Id, userId, "json", dateFrom: dateFrom, ct: TestCancellationToken);

        // Assert
        result.Success.Should().BeTrue();
        mockFormatter.Verify(f => f.FormatAsync(It.IsAny<ChatEntity>(), dateFrom, null), Times.Once);
    }

    [Fact]
    public async Task ExportChatAsync_WithDateTo_PassesToFormatter()
    {
        // Arrange
        await using var context = CreateDbContext();
        var userId = Guid.NewGuid();
        var chat = await CreateChatWithGame(context, userId, "Test Game");

        var dateTo = new DateTime(2024, 12, 31, 23, 59, 59, DateTimeKind.Utc);
        var mockFormatter = CreateMockFormatter("json", "application/json", ".json", new MemoryStream());
        var service = CreateService(context, [mockFormatter.Object]);

        // Act
        var result = await service.ExportChatAsync(chat.Id, userId, "json", dateTo: dateTo, ct: TestCancellationToken);

        // Assert
        result.Success.Should().BeTrue();
        mockFormatter.Verify(f => f.FormatAsync(It.IsAny<ChatEntity>(), null, dateTo), Times.Once);
    }

    [Fact]
    public async Task ExportChatAsync_WithDateRange_PassesBothDatesToFormatter()
    {
        // Arrange
        await using var context = CreateDbContext();
        var userId = Guid.NewGuid();
        var chat = await CreateChatWithGame(context, userId, "Test Game");

        var dateFrom = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var dateTo = new DateTime(2024, 12, 31, 23, 59, 59, DateTimeKind.Utc);
        var mockFormatter = CreateMockFormatter("json", "application/json", ".json", new MemoryStream());
        var service = CreateService(context, [mockFormatter.Object]);

        // Act
        var result = await service.ExportChatAsync(chat.Id, userId, "json", dateFrom, dateTo, TestCancellationToken);

        // Assert
        result.Success.Should().BeTrue();
        mockFormatter.Verify(f => f.FormatAsync(It.IsAny<ChatEntity>(), dateFrom, dateTo), Times.Once);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task ExportChatAsync_FormatterThrowsException_ReturnsGenerationFailed()
    {
        // Arrange
        await using var context = CreateDbContext();
        var userId = Guid.NewGuid();
        var chat = await CreateChatWithGame(context, userId, "Test Game");

        var mockFormatter = new Mock<IExportFormatter>();
        mockFormatter.Setup(f => f.Format).Returns("json");
        mockFormatter.Setup(f => f.FormatAsync(It.IsAny<ChatEntity>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .ThrowsAsync(new InvalidOperationException("Formatter error"));

        var service = CreateService(context, [mockFormatter.Object]);

        // Act
        var result = await service.ExportChatAsync(chat.Id, userId, "json", ct: TestCancellationToken);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Be("generation_failed");
        result.ErrorDetails.Should().Contain("Formatter error");
    }

    [Fact]
    public async Task ExportChatAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        await using var context = CreateDbContext();
        var userId = Guid.NewGuid();
        var chat = await CreateChatWithGame(context, userId, "Test Game");

        var service = CreateService(context, CreateMockFormatters("json"));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        var act = async () => await service.ExportChatAsync(chat.Id, userId, "json", ct: cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion

    #region Multiple Formatters Tests

    [Fact]
    public async Task ExportChatAsync_MultipleFormatters_SelectsCorrectOne()
    {
        // Arrange
        await using var context = CreateDbContext();
        var userId = Guid.NewGuid();
        var chat = await CreateChatWithGame(context, userId, "Test Game");

        var jsonStream = new MemoryStream([1, 2, 3]);
        var mdStream = new MemoryStream([4, 5, 6]);

        var jsonFormatter = CreateMockFormatter("json", "application/json", ".json", jsonStream);
        var mdFormatter = CreateMockFormatter("md", "text/markdown", ".md", mdStream);

        var service = CreateService(context, [jsonFormatter.Object, mdFormatter.Object]);

        // Act
        var result = await service.ExportChatAsync(chat.Id, userId, "md", ct: TestCancellationToken);

        // Assert
        result.Success.Should().BeTrue();
        result.ContentType.Should().Be("text/markdown");
        jsonFormatter.Verify(f => f.FormatAsync(It.IsAny<ChatEntity>(), null, null), Times.Never);
        mdFormatter.Verify(f => f.FormatAsync(It.IsAny<ChatEntity>(), null, null), Times.Once);
    }

    [Fact]
    public async Task ExportChatAsync_NoFormatters_ReturnsUnsupportedFormat()
    {
        // Arrange
        await using var context = CreateDbContext();
        var userId = Guid.NewGuid();
        var chat = await CreateChatWithGame(context, userId, "Test Game");

        var service = CreateService(context, []);

        // Act
        var result = await service.ExportChatAsync(chat.Id, userId, "json", ct: TestCancellationToken);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Be("unsupported_format");
    }

    #endregion

    #region Chat with Logs Tests

    [Fact]
    public async Task ExportChatAsync_ChatWithLogs_PassesChatWithLogsToFormatter()
    {
        // Arrange
        await using var context = CreateDbContext();
        var userId = Guid.NewGuid();
        var chat = await CreateChatWithGameAndLogs(context, userId, "Test Game", 5);

        ChatEntity? capturedChat = null;
        var mockFormatter = new Mock<IExportFormatter>();
        mockFormatter.Setup(f => f.Format).Returns("json");
        mockFormatter.Setup(f => f.ContentType).Returns("application/json");
        mockFormatter.Setup(f => f.FileExtension).Returns(".json");
        mockFormatter.Setup(f => f.FormatAsync(It.IsAny<ChatEntity>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .Callback<ChatEntity, DateTime?, DateTime?>((c, _, _) => capturedChat = c)
            .ReturnsAsync(new MemoryStream());

        var service = CreateService(context, [mockFormatter.Object]);

        // Act
        var result = await service.ExportChatAsync(chat.Id, userId, "json", ct: TestCancellationToken);

        // Assert
        result.Success.Should().BeTrue();
        capturedChat.Should().NotBeNull();
        capturedChat!.Logs.Should().HaveCount(5);
    }

    #endregion

    #region Helper Methods

    private static ChatExportService CreateService(
        MeepleAiDbContext context,
        IEnumerable<IExportFormatter> formatters)
    {
        return new ChatExportService(
            context,
            formatters,
            NullLogger<ChatExportService>.Instance);
    }

    private static MeepleAiDbContext CreateDbContext()
    {
        return TestDbContextFactory.CreateInMemoryDbContext();
    }

    private static IEnumerable<IExportFormatter> CreateMockFormatters(params string[] formats)
    {
        return formats.Select(format =>
        {
            var mock = new Mock<IExportFormatter>();
            mock.Setup(f => f.Format).Returns(format);
            mock.Setup(f => f.ContentType).Returns($"application/{format}");
            mock.Setup(f => f.FileExtension).Returns($".{format}");
            mock.Setup(f => f.FormatAsync(It.IsAny<ChatEntity>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .ReturnsAsync(new MemoryStream());
            return mock.Object;
        }).ToList();
    }

    private static Mock<IExportFormatter> CreateMockFormatter(
        string format,
        string contentType,
        string extension,
        Stream stream)
    {
        var mock = new Mock<IExportFormatter>();
        mock.Setup(f => f.Format).Returns(format);
        mock.Setup(f => f.ContentType).Returns(contentType);
        mock.Setup(f => f.FileExtension).Returns(extension);
        mock.Setup(f => f.FormatAsync(It.IsAny<ChatEntity>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(stream);
        return mock;
    }

    private static async Task<ChatEntity> CreateChatWithGame(
        MeepleAiDbContext context,
        Guid userId,
        string gameName)
    {
        var user = new UserEntity
        {
            Id = userId,
            Email = $"user-{userId}@test.com",
            DisplayName = "Test User",
            Role = "user",
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(user);

        var game = new GameEntity
        {
            Id = Guid.NewGuid(),
            Name = gameName,
            CreatedAt = DateTime.UtcNow
        };
        context.Games.Add(game);

        var agent = new AgentEntity
        {
            Id = Guid.NewGuid(),
            Name = "Test Agent",
            Type = "test",
            StrategyName = "default",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Agents.Add(agent);

        var chat = new ChatEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            GameId = game.Id,
            AgentId = agent.Id,
            StartedAt = DateTime.UtcNow,
            User = user,
            Game = game,
            Agent = agent
        };
        context.Chats.Add(chat);

        await context.SaveChangesAsync();
        return chat;
    }

    private static async Task<ChatEntity> CreateChatWithGameAndLogs(
        MeepleAiDbContext context,
        Guid userId,
        string gameName,
        int logCount)
    {
        var chat = await CreateChatWithGame(context, userId, gameName);

        for (var i = 0; i < logCount; i++)
        {
            var log = new ChatLogEntity
            {
                Id = Guid.NewGuid(),
                ChatId = chat.Id,
                UserId = i % 2 == 0 ? userId : null, // Alternate between user and AI messages
                Level = "info",
                Message = $"Test message {i + 1}",
                SequenceNumber = i,
                CreatedAt = DateTime.UtcNow.AddMinutes(i),
                Chat = chat
            };
            context.ChatLogs.Add(log);
            chat.Logs.Add(log);
        }

        await context.SaveChangesAsync();
        return chat;
    }

    #endregion
}
