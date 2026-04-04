using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Xunit;
using FluentAssertions;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Entities;

/// <summary>
/// Tests for ChatThread.Export() domain method.
/// ISSUE-860: Chat Export Functionality
/// </summary>
[Trait("Category", TestCategories.Unit)]
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
        exported.Should().NotBeNull();
        exported.Format.Should().Be(ExportFormat.Json);
        exported.ContentType.Should().Be("application/json");
        exported.FileExtension.Should().Be("json");
        string.IsNullOrWhiteSpace(exported.Content).Should().BeFalse();

        // Verify JSON structure
        exported.Content.Should().Contain("\"id\":");
        exported.Content.Should().Contain("\"userId\":");
        exported.Content.Should().Contain("\"gameId\":");
        exported.Content.Should().Contain("\"title\": \"Test Chat\"");
        exported.Content.Should().Contain("\"messages\":");
        exported.Content.Should().Contain("\"role\": \"user\"");
        exported.Content.Should().Contain("\"role\": \"assistant\"");
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
        exported.Should().NotBeNull();
        exported.Format.Should().Be(ExportFormat.Markdown);
        exported.ContentType.Should().Be("text/markdown");
        exported.FileExtension.Should().Be("md");
        string.IsNullOrWhiteSpace(exported.Content).Should().BeFalse();

        // Verify Markdown structure
        exported.Content.Should().Contain("# Epic Battle");
        exported.Content.Should().Contain("**Created:**");
        exported.Content.Should().Contain("**Messages:**");
        exported.Content.Should().Contain("## 👤 User");
        exported.Content.Should().Contain("## 🤖 Assistant");
        exported.Content.Should().Contain("What are the combat rules?");
        exported.Content.Should().Contain("Combat uses dice rolls...");
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
        exportedJson.Content.Should().Contain("\"messages\": []");
        exportedMd.Content.Should().Contain("*No messages in this conversation.*");
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
        exportedJson.Content.Should().Contain("\"title\": \"Untitled Chat\"");
        exportedMd.Content.Should().Contain("# Untitled Chat");
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
        exported.Should().NotBeNull();
        exported.Content.Should().Contain("\"status\": \"closed\"");
    }

    [Fact]
    public void Export_InvalidFormat_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var thread = new ChatThread(threadId, userId);

        // Act & Assert
        Action act = () =>
            thread.Export((ExportFormat)999);
        act.Should().Throw<ArgumentOutOfRangeException>();
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

        (msg1Index < resp1Index).Should().BeTrue();
        (resp1Index < msg2Index).Should().BeTrue();
        (msg2Index < resp2Index).Should().BeTrue();
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
        (exported.ExportedAt >= beforeExport).Should().BeTrue();
        (exported.ExportedAt <= afterExport).Should().BeTrue();
    }
}

