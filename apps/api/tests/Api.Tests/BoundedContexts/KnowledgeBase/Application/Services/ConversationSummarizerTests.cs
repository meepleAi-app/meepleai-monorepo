using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Models;
using Api.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Unit tests for ConversationSummarizer.
/// Issue #5259: Progressive conversation summarization.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ConversationSummarizerTests
{
    private readonly Mock<ILlmService> _mockLlmService = new();
    private readonly Mock<ILogger<ConversationSummarizer>> _mockLogger = new();
    private readonly ConversationSummarizer _summarizer;

    public ConversationSummarizerTests()
    {
        _summarizer = new ConversationSummarizer(
            _mockLlmService.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task SummarizeAsync_WithMessages_ReturnsSummary()
    {
        // Arrange
        var messages = new List<ChatMessage>
        {
            new("What is Catan?", "user", 1),
            new("Catan is a board game about trading and building.", "assistant", 2),
            new("How many players?", "user", 3),
            new("2 to 4 players, or 5-6 with expansion.", "assistant", 4)
        };

        _mockLlmService
            .Setup(s => s.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmCompletionResult
            {
                Success = true,
                Response = "The user asked about Catan, a trading/building board game for 2-4 players (5-6 with expansion)."
            });

        // Act
        var result = await _summarizer.SummarizeAsync(messages, existingSummary: null);

        // Assert
        result.Should().Contain("Catan");
        result.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task SummarizeAsync_WithExistingSummary_MergesSummaries()
    {
        // Arrange
        var messages = new List<ChatMessage>
        {
            new("What about robber rules?", "user", 5),
            new("When you roll a 7, move the robber.", "assistant", 6)
        };

        string? capturedPrompt = null;
        _mockLlmService
            .Setup(s => s.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, RequestSource, CancellationToken>((_, userPrompt, _, _) => capturedPrompt = userPrompt)
            .ReturnsAsync(new LlmCompletionResult
            {
                Success = true,
                Response = "User discussed Catan basics (2-4 players) and robber rules (move on rolling 7)."
            });

        // Act
        var result = await _summarizer.SummarizeAsync(
            messages,
            existingSummary: "User asked about Catan, a board game for 2-4 players.");

        // Assert
        result.Should().NotBeNullOrWhiteSpace();
        capturedPrompt.Should().Contain("Existing summary:");
        capturedPrompt.Should().Contain("2-4 players");
        capturedPrompt.Should().Contain("robber rules");
    }

    [Fact]
    public async Task SummarizeAsync_WhenLlmFails_ReturnsExistingSummary()
    {
        // Arrange
        var messages = new List<ChatMessage> { new("Test", "user", 1) };
        _mockLlmService
            .Setup(s => s.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmCompletionResult { Success = false, ErrorMessage = "LLM error" });

        // Act
        var result = await _summarizer.SummarizeAsync(messages, "Previous summary.");

        // Assert
        result.Should().Be("Previous summary.");
    }

    [Fact]
    public async Task SummarizeAsync_WhenLlmThrows_ReturnsExistingSummary()
    {
        // Arrange
        var messages = new List<ChatMessage> { new("Test", "user", 1) };
        _mockLlmService
            .Setup(s => s.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("LLM timeout"));

        // Act
        var result = await _summarizer.SummarizeAsync(messages, "Existing context.");

        // Assert
        result.Should().Be("Existing context.");
    }

    [Fact]
    public async Task SummarizeAsync_WithEmptyMessages_ReturnsExistingSummary()
    {
        // Act
        var result = await _summarizer.SummarizeAsync(
            Array.Empty<ChatMessage>(),
            "Existing summary.");

        // Assert
        result.Should().Be("Existing summary.");
        _mockLlmService.Verify(
            s => s.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SummarizeAsync_WithEmptyMessagesAndNoSummary_ReturnsEmpty()
    {
        // Act
        var result = await _summarizer.SummarizeAsync(
            Array.Empty<ChatMessage>(),
            existingSummary: null);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithNullLlmService_Throws()
    {
        var act = () => new ConversationSummarizer(null!, _mockLogger.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task SummarizeAsync_WithNullMessages_Throws()
    {
        var act = () => _summarizer.SummarizeAsync(null!, "summary");
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
