using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using FluentAssertions;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Services;

[Trait("Category", TestCategories.Unit)]
public class ChatContextDomainServiceTests
{
    private readonly ChatContextDomainService _service = new();

    // ========================================================================
    // BuildChatHistoryContext
    // ========================================================================

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

        result.Should().Contain("Previous conversation:");
        result.Should().Contain("user: First question");
        result.Should().Contain("assistant: First answer");
    }

    [Fact]
    public void BuildChatHistoryContext_WithFewMessages_ReturnsAllVerbatim()
    {
        var thread = new ChatThread(Guid.NewGuid(), Guid.NewGuid());
        for (int i = 0; i < ChatContextDomainService.VerbatimWindowSize; i++)
        {
            thread.AddUserMessage($"Message {i}");
        }

        var result = _service.BuildChatHistoryContext(thread);

        // All messages within verbatim window should be present
        result.Should().Contain("Message 0");
        result.Should().Contain($"Message {ChatContextDomainService.VerbatimWindowSize - 1}");
    }

    [Fact]
    public void BuildChatHistoryContext_BeyondWindow_KeepsOnlyRecentVerbatim()
    {
        var thread = new ChatThread(Guid.NewGuid(), Guid.NewGuid());
        for (int i = 0; i < 15; i++)
        {
            thread.AddUserMessage($"Message {i}");
        }

        var result = _service.BuildChatHistoryContext(thread);

        // Most recent messages should be present (verbatim window = 6)
        result.Should().Contain("Message 14"); // Most recent
        result.Should().Contain("Message 9");  // 6th from end

        // Old messages outside verbatim window should NOT be present
        // (no summary exists, so they're just dropped)
        result.Should().NotContain("Message 0");
    }

    [Fact]
    public void BuildChatHistoryContext_WithSummary_IncludesBothSummaryAndRecent()
    {
        var thread = new ChatThread(Guid.NewGuid(), Guid.NewGuid());
        for (int i = 0; i < 10; i++)
        {
            thread.AddUserMessage($"Message {i}");
        }

        thread.UpdateConversationSummary("The user asked about Catan setup rules and trading mechanics.");

        var result = _service.BuildChatHistoryContext(thread);

        result.Should().Contain("Conversation summary (earlier context):");
        result.Should().Contain("Catan setup rules");
        result.Should().Contain("Message 9"); // Most recent still verbatim
    }

    // ========================================================================
    // GetMessagesToSummarize
    // ========================================================================

    [Fact]
    public void GetMessagesToSummarize_BelowThreshold_ReturnsEmpty()
    {
        var thread = new ChatThread(Guid.NewGuid(), Guid.NewGuid());
        for (int i = 0; i < ChatContextDomainService.SummarizationThreshold - 1; i++)
        {
            thread.AddUserMessage($"Message {i}");
        }

        var result = _service.GetMessagesToSummarize(thread);
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetMessagesToSummarize_AboveThreshold_ReturnsOlderMessages()
    {
        var thread = new ChatThread(Guid.NewGuid(), Guid.NewGuid());
        for (int i = 0; i < 12; i++)
        {
            thread.AddUserMessage($"Message {i}");
        }

        var result = _service.GetMessagesToSummarize(thread);

        // Messages outside the verbatim window (12 - 6 = 6 messages)
        result.Should().NotBeEmpty();
        result.Should().Contain(m => m.Content == "Message 0");
        result.Should().NotContain(m => m.Content == "Message 11"); // Within window
    }

    [Fact]
    public void GetMessagesToSummarize_EmptyThread_ReturnsEmpty()
    {
        var thread = new ChatThread(Guid.NewGuid(), Guid.NewGuid());
        var result = _service.GetMessagesToSummarize(thread);
        result.Should().BeEmpty();
    }

    // ========================================================================
    // ShouldIncludeChatHistory
    // ========================================================================

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
    public void ShouldIncludeChatHistory_WithNull_ReturnsFalse()
    {
        Assert.False(_service.ShouldIncludeChatHistory(null!));
    }

    // ========================================================================
    // EnrichPromptWithHistory
    // ========================================================================

    [Fact]
    public void EnrichPromptWithHistory_WithHistory_CombinesCorrectly()
    {
        var question = "What is the rule?";
        var history = "Previous conversation:\nuser: How many players?\nassistant: 2-4 players";

        var result = _service.EnrichPromptWithHistory(question, history);

        result.Should().Contain(history);
        result.Should().Contain("Current question: What is the rule?");
    }

    [Fact]
    public void EnrichPromptWithHistory_WithoutHistory_ReturnsOriginal()
    {
        var question = "What is the rule?";
        var result = _service.EnrichPromptWithHistory(question, "");
        result.Should().Be(question);
    }

    [Fact]
    public void EnrichPromptWithHistory_WithEmptyQuestion_Throws()
    {
        var act = () => _service.EnrichPromptWithHistory("", "some history");
        act.Should().Throw<ArgumentException>();
    }

    // ========================================================================
    // ValidateGameContext
    // ========================================================================

    [Fact]
    public void ValidateGameContext_WithValidGuid_ReturnsTrue()
    {
        Assert.True(_service.ValidateGameContext(Guid.NewGuid()));
    }

    [Fact]
    public void ValidateGameContext_WithEmpty_ReturnsFalse()
    {
        Assert.False(_service.ValidateGameContext(Guid.Empty));
        Assert.False(_service.ValidateGameContext(null));
    }

    // ========================================================================
    // BuildSystemPrompt (Issue #5260)
    // ========================================================================

    [Fact]
    public void BuildSystemPrompt_WithoutHistory_ReturnsBasePrompt()
    {
        var result = _service.BuildSystemPrompt("CatanTutor", hasConversationHistory: false);

        result.Should().Contain("CatanTutor");
        result.Should().Contain("board game AI assistant");
        result.Should().NotContain("Conversation Guidelines");
    }

    [Fact]
    public void BuildSystemPrompt_WithHistory_IncludesMultiTurnGuidelines()
    {
        var result = _service.BuildSystemPrompt("CatanTutor", hasConversationHistory: true);

        result.Should().Contain("CatanTutor");
        result.Should().Contain("Conversation Guidelines");
        result.Should().Contain("conversation history");
        result.Should().Contain("follow-up");
    }

    [Fact]
    public void BuildSystemPrompt_WithEmptyAgentName_Throws()
    {
        var act = () => _service.BuildSystemPrompt("", hasConversationHistory: false);
        act.Should().Throw<ArgumentException>();
    }

    // ========================================================================
    // BuildStructuredUserPrompt (Issue #5260)
    // ========================================================================

    [Fact]
    public void BuildStructuredUserPrompt_WithAllSections_IncludesAll()
    {
        var result = _service.BuildStructuredUserPrompt(
            "What are the rules?",
            "Page 5: Setup requires placing settlements...",
            "Previous conversation:\nuser: How many players?\nassistant: 2-4 players");

        result.Should().Contain("=== Game Documentation ===");
        result.Should().Contain("=== Conversation History ===");
        result.Should().Contain("=== Current Question ===");
        result.Should().Contain("What are the rules?");
        result.Should().Contain("Page 5: Setup requires");
        result.Should().Contain("How many players?");
    }

    [Fact]
    public void BuildStructuredUserPrompt_WithoutRagContext_ShowsNoContextNote()
    {
        var result = _service.BuildStructuredUserPrompt(
            "What about trading?",
            null,
            "Previous conversation:\nuser: How to setup?\nassistant: Place settlements.");

        result.Should().NotContain("=== Game Documentation ===");
        result.Should().Contain("=== Conversation History ===");
        result.Should().Contain("=== Current Question ===");
        result.Should().Contain("No relevant context found");
    }

    [Fact]
    public void BuildStructuredUserPrompt_WithoutHistory_OmitsHistorySection()
    {
        var result = _service.BuildStructuredUserPrompt(
            "First question about Catan",
            "Page 1: Catan is a board game...",
            null);

        result.Should().Contain("=== Game Documentation ===");
        result.Should().NotContain("=== Conversation History ===");
        result.Should().Contain("=== Current Question ===");
    }

    [Fact]
    public void BuildStructuredUserPrompt_OnlyQuestion_MinimalPrompt()
    {
        var result = _service.BuildStructuredUserPrompt("Hello", null, null);

        result.Should().Contain("=== Current Question ===");
        result.Should().Contain("Hello");
        result.Should().NotContain("=== Game Documentation ===");
        result.Should().NotContain("=== Conversation History ===");
    }

    [Fact]
    public void BuildStructuredUserPrompt_EmptyQuestion_Throws()
    {
        var act = () => _service.BuildStructuredUserPrompt("", "rag", "history");
        act.Should().Throw<ArgumentException>();
    }

    // ========================================================================
    // BuildSystemPrompt (typology-aware overload) — Issue #5278
    // ========================================================================

    [Fact]
    public void BuildSystemPrompt_WithTypology_UsesTypologyTemplate()
    {
        var prompt = _service.BuildSystemPrompt("TestAgent", "Tutor", "Catan", hasConversationHistory: false);
        prompt.Should().Contain("Tutor");
        prompt.Should().Contain("TestAgent");
        prompt.Should().Contain("Catan");
    }

    [Fact]
    public void BuildSystemPrompt_WithNullTypology_FallsBackToCustom()
    {
        var prompt = _service.BuildSystemPrompt("TestAgent", null, "Catan", hasConversationHistory: false);
        prompt.Should().Contain("TestAgent");
        prompt.Should().Contain("Catan");
    }

    [Fact]
    public void BuildSystemPrompt_OldOverload_StillWorks()
    {
        var prompt = _service.BuildSystemPrompt("TestAgent", hasConversationHistory: false);
        prompt.Should().Contain("TestAgent");
    }

    [Theory]
    [InlineData("Tutor", "spiega")]
    [InlineData("Arbitro", "verdetto")]
    [InlineData("Stratega", "tattico")]
    [InlineData("Narratore", "evocativo")]
    public void BuildSystemPrompt_EachTypology_HasDistinctKeywords(string typology, string keyword)
    {
        var prompt = _service.BuildSystemPrompt("Agent", typology, "TestGame", false);
        prompt.ToLowerInvariant().Should().Contain(keyword);
    }
}

