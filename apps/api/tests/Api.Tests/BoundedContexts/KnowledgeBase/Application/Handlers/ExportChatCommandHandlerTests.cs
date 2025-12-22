using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Handlers;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Unit tests for ExportChatCommandHandler.
/// Tests export functionality for chat threads in JSON and Markdown formats.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class ExportChatCommandHandlerTests
{
    private readonly Mock<IChatThreadRepository> _mockThreadRepository;
    private readonly ExportChatCommandHandler _handler;

    public ExportChatCommandHandlerTests()
    {
        _mockThreadRepository = new Mock<IChatThreadRepository>();
        _handler = new ExportChatCommandHandler(_mockThreadRepository.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new ExportChatCommandHandler(null!));

        Assert.Equal("threadRepository", ex.ParamName);
    }

    #endregion

    #region JSON Export Tests

    [Fact]
    public async Task Handle_WithValidThreadAndJsonFormat_ReturnsJsonExport()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var thread = CreateChatThreadWithMessages(threadId, userId, 3);

        _mockThreadRepository
            .Setup(r => r.GetByIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(thread);

        var command = new ExportChatCommand(threadId, "json");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Json", result.Format);
        Assert.Equal("application/json", result.ContentType);
        Assert.Equal("json", result.FileExtension);
        Assert.Contains("\"messages\"", result.Content);
        Assert.Contains("\"role\"", result.Content);
        Assert.Contains("\"content\"", result.Content);

        _mockThreadRepository.Verify(r => r.GetByIdAsync(threadId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithDefaultFormat_ReturnsJsonExport()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var thread = CreateChatThreadWithMessages(threadId, userId, 1);

        _mockThreadRepository
            .Setup(r => r.GetByIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(thread);

        // ExportChatCommand default format is "json"
        var command = new ExportChatCommand(threadId);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Json", result.Format);
        Assert.Equal("application/json", result.ContentType);
    }

    #endregion

    #region Markdown Export Tests

    [Fact]
    public async Task Handle_WithValidThreadAndMarkdownFormat_ReturnsMarkdownExport()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var thread = CreateChatThreadWithMessages(threadId, userId, 2, "Test Chat");

        _mockThreadRepository
            .Setup(r => r.GetByIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(thread);

        var command = new ExportChatCommand(threadId, "markdown");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Markdown", result.Format);
        Assert.Equal("text/markdown", result.ContentType);
        Assert.Equal("md", result.FileExtension);
        Assert.Contains("# Test Chat", result.Content);
        Assert.Contains("## ", result.Content); // Message headers
        Assert.Contains("---", result.Content); // Separators
    }

    [Fact]
    public async Task Handle_WithMdFormat_ReturnsMarkdownExport()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var thread = CreateChatThreadWithMessages(threadId, userId, 1);

        _mockThreadRepository
            .Setup(r => r.GetByIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(thread);

        var command = new ExportChatCommand(threadId, "md");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Markdown", result.Format);
        Assert.Equal("text/markdown", result.ContentType);
    }

    #endregion

    #region Thread Not Found Tests

    [Fact]
    public async Task Handle_WithNonExistentThread_ThrowsInvalidOperationException()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        _mockThreadRepository
            .Setup(r => r.GetByIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChatThread?)null);

        var command = new ExportChatCommand(threadId, "json");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _handler.Handle(command, TestContext.Current.CancellationToken));

        Assert.Contains(threadId.ToString(), ex.Message);
        Assert.Contains("not found", ex.Message);
    }

    #endregion

    #region Invalid Format Tests

    [Theory]
    [InlineData("pdf")]
    [InlineData("txt")]
    [InlineData("csv")]
    [InlineData("invalid")]
    [InlineData("")]
    public async Task Handle_WithInvalidFormat_ThrowsArgumentException(string invalidFormat)
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var thread = CreateChatThreadWithMessages(threadId, userId, 1);

        _mockThreadRepository
            .Setup(r => r.GetByIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(thread);

        var command = new ExportChatCommand(threadId, invalidFormat);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _handler.Handle(command, TestContext.Current.CancellationToken));

        Assert.Contains("Invalid export format", ex.Message);
        Assert.Contains("Supported formats: json, markdown", ex.Message);
    }

    #endregion

    #region Message Order Tests

    [Fact]
    public async Task Handle_WithMultipleMessages_IncludesAllMessagesInOrder()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var thread = CreateChatThreadWithMessages(threadId, userId, 5);

        _mockThreadRepository
            .Setup(r => r.GetByIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(thread);

        var command = new ExportChatCommand(threadId, "json");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        // Verify all messages are included by checking for message patterns
        for (int i = 0; i < 5; i++)
        {
            Assert.Contains($"Message {i}", result.Content);
        }
    }

    #endregion

    #region Empty Thread Tests

    [Fact]
    public async Task Handle_WithEmptyThread_ReturnsExportWithNoMessages()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var thread = new ChatThread(threadId, userId, null, "Empty Chat");

        _mockThreadRepository
            .Setup(r => r.GetByIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(thread);

        var command = new ExportChatCommand(threadId, "json");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("\"messages\"", result.Content);
        Assert.Contains("\"messageCount\":0", result.Content.Replace(" ", ""));
    }

    [Fact]
    public async Task Handle_WithEmptyThreadMarkdown_ShowsNoMessagesIndicator()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var thread = new ChatThread(threadId, userId, null, "Empty Chat");

        _mockThreadRepository
            .Setup(r => r.GetByIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(thread);

        var command = new ExportChatCommand(threadId, "markdown");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("No messages", result.Content);
    }

    #endregion

    #region Metadata Tests

    [Fact]
    public async Task Handle_JsonExport_IncludesMetadata()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var thread = new ChatThread(threadId, userId, gameId, "Game Discussion");
        thread.AddUserMessage("How do I play?");
        thread.AddAssistantMessage("Here are the rules...");

        _mockThreadRepository
            .Setup(r => r.GetByIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(thread);

        var command = new ExportChatCommand(threadId, "json");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("\"id\"", result.Content);
        Assert.Contains("\"userId\"", result.Content);
        Assert.Contains("\"gameId\"", result.Content);
        Assert.Contains("\"title\"", result.Content);
        Assert.Contains("\"status\"", result.Content);
        Assert.Contains("\"createdAt\"", result.Content);
        Assert.Contains("\"lastMessageAt\"", result.Content);
        Assert.Contains("\"messageCount\"", result.Content);
    }

    [Fact]
    public async Task Handle_MarkdownExport_IncludesMetadataHeaders()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var thread = CreateChatThreadWithMessages(threadId, userId, 2, "Test Thread");

        _mockThreadRepository
            .Setup(r => r.GetByIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(thread);

        var command = new ExportChatCommand(threadId, "markdown");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("**Created:**", result.Content);
        Assert.Contains("**Last Activity:**", result.Content);
        Assert.Contains("**Status:**", result.Content);
        Assert.Contains("**Messages:**", result.Content);
        Assert.Contains("*Exported on", result.Content);
    }

    #endregion

    #region Case Insensitive Format Tests

    [Theory]
    [InlineData("JSON")]
    [InlineData("Json")]
    [InlineData("json")]
    public async Task Handle_WithCaseInsensitiveJsonFormat_ReturnsJsonExport(string format)
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var thread = CreateChatThreadWithMessages(threadId, userId, 1);

        _mockThreadRepository
            .Setup(r => r.GetByIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(thread);

        var command = new ExportChatCommand(threadId, format);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Json", result.Format);
    }

    [Theory]
    [InlineData("MARKDOWN")]
    [InlineData("Markdown")]
    [InlineData("markdown")]
    [InlineData("MD")]
    [InlineData("Md")]
    [InlineData("md")]
    public async Task Handle_WithCaseInsensitiveMarkdownFormat_ReturnsMarkdownExport(string format)
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var thread = CreateChatThreadWithMessages(threadId, userId, 1);

        _mockThreadRepository
            .Setup(r => r.GetByIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(thread);

        var command = new ExportChatCommand(threadId, format);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Markdown", result.Format);
    }

    #endregion

    #region Null Command Tests

    [Fact]
    public async Task Handle_WithNullCommand_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await _handler.Handle(null!, TestContext.Current.CancellationToken));
    }

    #endregion

    #region Export Timestamp Tests

    [Fact]
    public async Task Handle_SetsExportedAtTimestamp()
    {
        // Arrange
        var beforeExport = DateTime.UtcNow.AddSeconds(-1);
        var threadId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var thread = CreateChatThreadWithMessages(threadId, userId, 1);

        _mockThreadRepository
            .Setup(r => r.GetByIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(thread);

        var command = new ExportChatCommand(threadId, "json");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.ExportedAt >= beforeExport);
        Assert.True(result.ExportedAt <= DateTime.UtcNow.AddSeconds(1));
    }

    #endregion

    #region Title Handling Tests

    [Fact]
    public async Task Handle_WithNullTitle_UsesUntitledDefault()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var thread = new ChatThread(threadId, userId, null, null); // No title
        thread.AddUserMessage("Test message");

        _mockThreadRepository
            .Setup(r => r.GetByIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(thread);

        var command = new ExportChatCommand(threadId, "json");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Untitled Chat", result.Content);
    }

    [Fact]
    public async Task Handle_MarkdownWithNullTitle_UsesUntitledDefault()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var thread = new ChatThread(threadId, userId, null, null); // No title
        thread.AddUserMessage("Test message");

        _mockThreadRepository
            .Setup(r => r.GetByIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(thread);

        var command = new ExportChatCommand(threadId, "markdown");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("# Untitled Chat", result.Content);
    }

    #endregion

    #region User and Assistant Message Tests

    [Fact]
    public async Task Handle_WithMixedRoleMessages_ExportsCorrectRoles()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var thread = new ChatThread(threadId, userId, null, "Mixed Roles Chat");
        thread.AddUserMessage("User question");
        thread.AddAssistantMessage("Assistant answer");
        thread.AddUserMessage("Follow-up question");
        thread.AddAssistantMessage("Follow-up answer");

        _mockThreadRepository
            .Setup(r => r.GetByIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(thread);

        var command = new ExportChatCommand(threadId, "json");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("\"role\":\"user\"", result.Content.Replace(" ", ""));
        Assert.Contains("\"role\":\"assistant\"", result.Content.Replace(" ", ""));
        Assert.Contains("User question", result.Content);
        Assert.Contains("Assistant answer", result.Content);
    }

    [Fact]
    public async Task Handle_MarkdownExport_ShowsUserAndAssistantLabels()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var thread = new ChatThread(threadId, userId, null, "Test Chat");
        thread.AddUserMessage("Hello");
        thread.AddAssistantMessage("Hi there!");

        _mockThreadRepository
            .Setup(r => r.GetByIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(thread);

        var command = new ExportChatCommand(threadId, "markdown");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("User", result.Content);
        Assert.Contains("Assistant", result.Content);
    }

    #endregion

    #region Repository Interaction Tests

    [Fact]
    public async Task Handle_CallsRepositoryGetByIdAsyncOnce()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var thread = CreateChatThreadWithMessages(threadId, userId, 1);

        _mockThreadRepository
            .Setup(r => r.GetByIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(thread);

        var command = new ExportChatCommand(threadId, "json");

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        _mockThreadRepository.Verify(r => r.GetByIdAsync(threadId, It.IsAny<CancellationToken>()), Times.Once);
        _mockThreadRepository.VerifyNoOtherCalls();
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a ChatThread with the specified number of alternating user/assistant messages.
    /// </summary>
    private static ChatThread CreateChatThreadWithMessages(Guid threadId, Guid userId, int messageCount, string? title = null)
    {
        var thread = new ChatThread(threadId, userId, null, title ?? "Test Thread");

        for (int i = 0; i < messageCount; i++)
        {
            if (i % 2 == 0)
            {
                thread.AddUserMessage($"Message {i}");
            }
            else
            {
                thread.AddAssistantMessage($"Message {i}");
            }
        }

        return thread;
    }

    #endregion
}
