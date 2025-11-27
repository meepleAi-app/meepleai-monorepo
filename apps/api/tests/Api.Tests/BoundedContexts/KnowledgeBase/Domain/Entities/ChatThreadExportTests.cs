using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Entities;

/// <summary>
/// Tests for ChatThread.Export() domain method.
/// ISSUE-860: Chat Export Functionality
/// </summary>
public class ChatThreadExportTests
{
    [Fact]
    public void Export_AsJson_ReturnsValidJsonExportedData()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var thread = new ChatThread(threadId, userId, gameId, "Test Chat");

        thread.AddUserMessage("Hello, how do I play this game?");
        thread.AddAssistantMessage("Welcome! Let me help you with the rules.");

        // Act
        var exported = thread.Export(ExportFormat.Json);

        // Assert
        Assert.NotNull(exported);
        Assert.Equal(ExportFormat.Json, exported.Format);
        Assert.Equal("application/json", exported.ContentType);
        Assert.Equal("json", exported.FileExtension);
        Assert.False(string.IsNullOrWhiteSpace(exported.Content));

        // Verify JSON structure
        Assert.Contains("\"id\":", exported.Content);
        Assert.Contains("\"userId\":", exported.Content);
        Assert.Contains("\"gameId\":", exported.Content);
        Assert.Contains("\"title\": \"Test Chat\"", exported.Content);
        Assert.Contains("\"messages\":", exported.Content);
        Assert.Contains("\"role\": \"user\"", exported.Content);
        Assert.Contains("\"role\": \"assistant\"", exported.Content);
    }

    [Fact]
    public void Export_AsMarkdown_ReturnsValidMarkdownExportedData()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var thread = new ChatThread(threadId, userId, gameId, "Epic Battle");

        thread.AddUserMessage("What are the combat rules?");
        thread.AddAssistantMessage("Combat uses dice rolls...");

        // Act
        var exported = thread.Export(ExportFormat.Markdown);

        // Assert
        Assert.NotNull(exported);
        Assert.Equal(ExportFormat.Markdown, exported.Format);
        Assert.Equal("text/markdown", exported.ContentType);
        Assert.Equal("md", exported.FileExtension);
        Assert.False(string.IsNullOrWhiteSpace(exported.Content));

        // Verify Markdown structure
        Assert.Contains("# Epic Battle", exported.Content);
        Assert.Contains("**Created:**", exported.Content);
        Assert.Contains("**Messages:**", exported.Content);
        Assert.Contains("## 👤 User", exported.Content);
        Assert.Contains("## 🤖 Assistant", exported.Content);
        Assert.Contains("What are the combat rules?", exported.Content);
        Assert.Contains("Combat uses dice rolls...", exported.Content);
    }

    [Fact]
    public void Export_EmptyThread_ReturnsEmptyMessagesContent()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var thread = new ChatThread(threadId, userId, null, "Empty Chat");

        // Act
        var exportedJson = thread.Export(ExportFormat.Json);
        var exportedMd = thread.Export(ExportFormat.Markdown);

        // Assert
        Assert.Contains("\"messages\": []", exportedJson.Content);
        Assert.Contains("*No messages in this conversation.*", exportedMd.Content);
    }

    [Fact]
    public void Export_WithoutTitle_UsesDefaultTitle()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var thread = new ChatThread(threadId, userId);

        // Act
        var exportedJson = thread.Export(ExportFormat.Json);
        var exportedMd = thread.Export(ExportFormat.Markdown);

        // Assert
        Assert.Contains("\"title\": \"Untitled Chat\"", exportedJson.Content);
        Assert.Contains("# Untitled Chat", exportedMd.Content);
    }

    [Fact]
    public void Export_ClosedThread_ExportsSuccessfully()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var thread = new ChatThread(threadId, userId, null, "Closed Chat");
        thread.AddUserMessage("Final message");
        thread.CloseThread();

        // Act
        var exported = thread.Export(ExportFormat.Json);

        // Assert
        Assert.NotNull(exported);
        Assert.Contains("\"status\": \"closed\"", exported.Content);
    }

    [Fact]
    public void Export_InvalidFormat_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var thread = new ChatThread(threadId, userId);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            thread.Export((ExportFormat)999));
    }

    [Fact]
    public void Export_MultipleMessages_PreservesOrder()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var thread = new ChatThread(threadId, userId, null, "Multi-Message Chat");

        thread.AddUserMessage("Message 1");
        thread.AddAssistantMessage("Response 1");
        thread.AddUserMessage("Message 2");
        thread.AddAssistantMessage("Response 2");

        // Act
        var exported = thread.Export(ExportFormat.Json);

        // Assert
        var content = exported.Content;
        var msg1Index = content.IndexOf("Message 1", StringComparison.Ordinal);
        var resp1Index = content.IndexOf("Response 1", StringComparison.Ordinal);
        var msg2Index = content.IndexOf("Message 2", StringComparison.Ordinal);
        var resp2Index = content.IndexOf("Response 2", StringComparison.Ordinal);

        Assert.True(msg1Index < resp1Index);
        Assert.True(resp1Index < msg2Index);
        Assert.True(msg2Index < resp2Index);
    }

    [Fact]
    public void Export_SetsExportedAtTimestamp()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var thread = new ChatThread(threadId, userId);
        var beforeExport = DateTime.UtcNow;

        // Act
        var exported = thread.Export(ExportFormat.Json);
        var afterExport = DateTime.UtcNow;

        // Assert
        Assert.True(exported.ExportedAt >= beforeExport);
        Assert.True(exported.ExportedAt <= afterExport);
    }
}

