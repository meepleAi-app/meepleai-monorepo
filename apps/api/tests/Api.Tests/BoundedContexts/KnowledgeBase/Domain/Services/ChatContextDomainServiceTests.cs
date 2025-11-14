using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Services;

public class ChatContextDomainServiceTests
{
    private readonly ChatContextDomainService _service = new();

    [Fact]
    public void BuildChatHistoryContext_WithEmptyThread_ReturnsEmpty()
    {
        var thread = new ChatThread(Guid.NewGuid(), Guid.NewGuid());
        var result = _service.BuildChatHistoryContext(thread);
        Assert.Empty(result);
    }

    [Fact]
    public void BuildChatHistoryContext_WithMessages_FormatsCorrectly()
    {
        var thread = new ChatThread(Guid.NewGuid(), Guid.NewGuid());
        thread.AddUserMessage("First question");
        thread.AddAssistantMessage("First answer");

        var result = _service.BuildChatHistoryContext(thread);

        Assert.Contains("Previous conversation:", result);
        Assert.Contains("user: First question", result);
        Assert.Contains("assistant: First answer", result);
    }

    [Fact]
    public void BuildChatHistoryContext_RespectsMaxMessages()
    {
        var thread = new ChatThread(Guid.NewGuid(), Guid.NewGuid());
        for (int i = 0; i < 15; i++)
        {
            thread.AddUserMessage($"Message {i}");
        }

        var result = _service.BuildChatHistoryContext(thread);

        // Should limit to last 10 messages
        Assert.Contains("Message 14", result); // Most recent
        Assert.DoesNotContain("Message 0", result); // Too old
    }

    [Fact]
    public void ValidateGameContext_WithValidGuid_ReturnsTrue()
    {
        var gameId = Guid.NewGuid();
        Assert.True(_service.ValidateGameContext(gameId));
    }

    [Fact]
    public void ValidateGameContext_WithEmpty_ReturnsFalse()
    {
        Assert.False(_service.ValidateGameContext(Guid.Empty));
        Assert.False(_service.ValidateGameContext(null));
    }

    [Fact]
    public void ShouldIncludeChatHistory_WithActiveThreadAndMessages_ReturnsTrue()
    {
        var thread = new ChatThread(Guid.NewGuid(), Guid.NewGuid());
        thread.AddUserMessage("Test");

        Assert.True(_service.ShouldIncludeChatHistory(thread));
    }

    [Fact]
    public void ShouldIncludeChatHistory_WithClosedThread_ReturnsFalse()
    {
        var thread = new ChatThread(Guid.NewGuid(), Guid.NewGuid());
        thread.AddUserMessage("Test");
        thread.CloseThread();

        Assert.False(_service.ShouldIncludeChatHistory(thread));
    }

    [Fact]
    public void ShouldIncludeChatHistory_WithEmptyThread_ReturnsFalse()
    {
        var thread = new ChatThread(Guid.NewGuid(), Guid.NewGuid());
        Assert.False(_service.ShouldIncludeChatHistory(thread));
    }

    [Fact]
    public void EnrichPromptWithHistory_WithHistory_CombinesCorrectly()
    {
        var question = "What is the rule?";
        var history = "Previous conversation:\nuser: How many players?\nassistant: 2-4 players";

        var result = _service.EnrichPromptWithHistory(question, history);

        Assert.Contains(history, result);
        Assert.Contains("Current question: What is the rule?", result);
    }

    [Fact]
    public void EnrichPromptWithHistory_WithoutHistory_ReturnsOriginal()
    {
        var question = "What is the rule?";
        var result = _service.EnrichPromptWithHistory(question, "");

        Assert.Equal(question, result);
    }
}
