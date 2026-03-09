using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Models;
using Api.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Unit tests for ConversationQueryRewriter.
/// Issue #5258: Query rewriting for multi-turn conversations.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ConversationQueryRewriterTests
{
    private readonly Mock<ILlmService> _mockLlmService = new();
    private readonly Mock<ILogger<ConversationQueryRewriter>> _mockLogger = new();
    private readonly ConversationQueryRewriter _rewriter;

    public ConversationQueryRewriterTests()
    {
        _rewriter = new ConversationQueryRewriter(
            _mockLlmService.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task RewriteQueryAsync_WithSuccessfulLlm_ReturnsRewrittenQuery()
    {
        // Arrange
        var llmResponse = new LlmCompletionResult
        {
            Success = true,
            Response = "What are the trading rules in Catan?"
        };
        _mockLlmService
            .Setup(s => s.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(llmResponse);

        // Act
        var result = await _rewriter.RewriteQueryAsync(
            "What about trading?",
            "Previous conversation:\nuser: Tell me about Catan\nassistant: Catan is a board game...");

        // Assert
        result.Should().Be("What are the trading rules in Catan?");
    }

    [Fact]
    public async Task RewriteQueryAsync_WhenLlmFails_ReturnsOriginalQuery()
    {
        // Arrange
        _mockLlmService
            .Setup(s => s.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmCompletionResult { Success = false, ErrorMessage = "LLM unavailable" });

        // Act
        var result = await _rewriter.RewriteQueryAsync("What about that?", "some history");

        // Assert
        result.Should().Be("What about that?");
    }

    [Fact]
    public async Task RewriteQueryAsync_WhenLlmReturnsEmpty_ReturnsOriginalQuery()
    {
        // Arrange
        _mockLlmService
            .Setup(s => s.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmCompletionResult { Success = true, Response = "   " });

        // Act
        var result = await _rewriter.RewriteQueryAsync("Original question", "some history");

        // Assert
        result.Should().Be("Original question");
    }

    [Fact]
    public async Task RewriteQueryAsync_WhenLlmThrows_ReturnsOriginalQuery()
    {
        // Arrange
        _mockLlmService
            .Setup(s => s.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Connection failed"));

        // Act
        var result = await _rewriter.RewriteQueryAsync("Fallback test", "some history");

        // Assert
        result.Should().Be("Fallback test");
    }

    [Fact]
    public async Task RewriteQueryAsync_WhenRewrittenTooLong_ReturnsOriginalQuery()
    {
        // Arrange - LLM returns a very long rewrite (exceeds sanity check)
        var longRewrite = new string('x', 2000);
        _mockLlmService
            .Setup(s => s.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmCompletionResult { Success = true, Response = longRewrite });

        // Act
        var result = await _rewriter.RewriteQueryAsync("Short question", "some history");

        // Assert - should fall back to original since rewrite is suspiciously long
        result.Should().Be("Short question");
    }

    [Fact]
    public async Task RewriteQueryAsync_IncludesHistoryInPrompt()
    {
        // Arrange
        string? capturedPrompt = null;
        _mockLlmService
            .Setup(s => s.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, RequestSource, CancellationToken>((_, userPrompt, _, _) => capturedPrompt = userPrompt)
            .ReturnsAsync(new LlmCompletionResult { Success = true, Response = "Rewritten query" });

        // Act
        await _rewriter.RewriteQueryAsync("What else?", "user: What is Catan?\nassistant: A board game.");

        // Assert
        capturedPrompt.Should().NotBeNull();
        capturedPrompt.Should().Contain("What is Catan?");
        capturedPrompt.Should().Contain("What else?");
    }
}
