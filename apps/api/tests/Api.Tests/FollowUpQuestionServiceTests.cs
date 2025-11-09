using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Api.Configuration;
using Api.Models;
using Api.Services;
using Api.Services.Chat;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using FluentAssertions;
using Xunit;

namespace Api.Tests;

/// <summary>
/// CHAT-02: Unit tests for FollowUpQuestionService
/// BDD Scenario: Generate Follow-Up Questions After QA Response
/// </summary>
public class FollowUpQuestionServiceTests
{
    private readonly ITestOutputHelper _output;

    private readonly Mock<ILlmService> _llmServiceMock;
    private readonly Mock<ILogger<FollowUpQuestionService>> _mockLogger;
    private readonly FollowUpQuestionsConfiguration _defaultConfig;

    public FollowUpQuestionServiceTests(ITestOutputHelper output)
    {
        _output = output;
        _llmServiceMock = new Mock<ILlmService>();
        _mockLogger = new Mock<ILogger<FollowUpQuestionService>>();
        _defaultConfig = new FollowUpQuestionsConfiguration
        {
            Enabled = true,
            MaxQuestionsPerResponse = 5,
            GenerationTimeoutMs = 10000,
            MaxRetries = 2,
            FailOnGenerationError = false
        };
    }

    [Fact]
    public async Task GenerateQuestionsAsync_ValidInput_ReturnsQuestions()
    {
        // Arrange
        var expectedQuestions = new List<string>
        {
            "How many players can play?",
            "What happens if there's a tie?",
            "Can you explain the scoring?"
        };

        _llmServiceMock
            .Setup(x => x.GenerateJsonAsync<FollowUpQuestionsDto>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FollowUpQuestionsDto(expectedQuestions));

        var service = CreateService();

        // Act
        var result = await service.GenerateQuestionsAsync(
            originalQuestion: "How do I win Tic-Tac-Toe?",
            generatedAnswer: "You win by getting three in a row",
            ragContext: new List<Snippet>(),
            gameName: "Tic-Tac-Toe",
            ct: CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Count.Should().Be(3);
        result[0].Should().Contain("How many players");
        result[1].Should().Contain("tie");
        result[2].Should().Contain("scoring");

        // Verify LLM was called with correct prompt structure
        _llmServiceMock.Verify(
            x => x.GenerateJsonAsync<FollowUpQuestionsDto>(
                It.Is<string>(s => s.Contains("follow-up questions")),
                It.Is<string>(s => s.Contains("Tic-Tac-Toe") && s.Contains("How do I win")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GenerateQuestionsAsync_LlmReturnsNull_ReturnsEmptyList()
    {
        // Arrange: LLM JSON parsing failed
        _llmServiceMock
            .Setup(x => x.GenerateJsonAsync<FollowUpQuestionsDto>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((FollowUpQuestionsDto?)null);

        var service = CreateService();

        // Act
        var result = await service.GenerateQuestionsAsync(
            "Test question",
            "Test answer",
            new List<Snippet>(),
            "Test Game");

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();

        // Verify retry logic (should retry MaxRetries times)
        _llmServiceMock.Verify(
            x => x.GenerateJsonAsync<FollowUpQuestionsDto>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(_defaultConfig.MaxRetries));
    }

    [Fact]
    public async Task GenerateQuestionsAsync_Timeout_ReturnsEmptyList()
    {
        // Arrange: Simulate slow LLM (exceeds timeout)
        var config = new FollowUpQuestionsConfiguration
        {
            GenerationTimeoutMs = 100, // 100ms timeout for test
            MaxRetries = 1
        };

        _llmServiceMock
            .Setup(x => x.GenerateJsonAsync<FollowUpQuestionsDto>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(async (string s1, string s2, CancellationToken ct) =>
            {
                await Task.Delay(5000, ct); // 5 seconds (longer than timeout)
                return new FollowUpQuestionsDto(new List<string> { "Question" });
            });

        var service = CreateService(config: Options.Create(config));

        // Act
        var result = await service.GenerateQuestionsAsync(
            "Test",
            "Answer",
            new List<Snippet>(),
            "Game");

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty(); // Should return empty due to timeout
    }

    [Fact]
    public async Task GenerateQuestionsAsync_RespectsMaxQuestionsConfig()
    {
        // Arrange: LLM returns 10 questions, config limits to 3
        var config = new FollowUpQuestionsConfiguration
        {
            MaxQuestionsPerResponse = 3
        };

        var tenQuestions = Enumerable.Range(1, 10).Select(i => $"Question {i}").ToList();

        _llmServiceMock
            .Setup(x => x.GenerateJsonAsync<FollowUpQuestionsDto>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FollowUpQuestionsDto(tenQuestions));

        var service = CreateService(config: Options.Create(config));

        // Act
        var result = await service.GenerateQuestionsAsync(
            "Test",
            "Answer",
            new List<Snippet>(),
            "Game",
            maxQuestions: 10); // Request 10, but config should limit to 3

        // Assert
        result.Count.Should().Be(3);
        result[0].Should().Be("Question 1");
        result[1].Should().Be("Question 2");
        result[2].Should().Be("Question 3");
    }

    [Fact]
    public async Task GenerateQuestionsAsync_FiltersEmptyQuestions()
    {
        // Arrange: LLM returns mixed valid/invalid questions
        var mixedQuestions = new List<string>
        {
            "Valid question 1",
            "",
            "   ",
            "Valid question 2",
            "Valid question 3"
        };

        _llmServiceMock
            .Setup(x => x.GenerateJsonAsync<FollowUpQuestionsDto>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FollowUpQuestionsDto(mixedQuestions));

        var service = CreateService();

        // Act
        var result = await service.GenerateQuestionsAsync(
            "Test",
            "Answer",
            new List<Snippet>(),
            "Game");

        // Assert
        result.Count.Should().Be(3); // Only valid questions
        result.Should().OnlyContain(q => !string.IsNullOrWhiteSpace(q));
    }

    [Fact]
    public async Task GenerateQuestionsAsync_IncludesRAGContextInPrompt()
    {
        // Arrange
        var ragContext = new List<Snippet>
        {
            new Snippet("Rule text about winning", "rulebook.pdf", 5, 1, 0.85f),
            new Snippet("Rule text about setup", "rulebook.pdf", 10, 1, 0.85f)
        };

        _llmServiceMock
            .Setup(x => x.GenerateJsonAsync<FollowUpQuestionsDto>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FollowUpQuestionsDto(new List<string> { "Question" }));

        var service = CreateService();

        // Act
        await service.GenerateQuestionsAsync(
            "How do I win?",
            "Answer",
            ragContext,
            "Chess");

        // Assert: Verify RAG context was included in user prompt
        _llmServiceMock.Verify(
            x => x.GenerateJsonAsync<FollowUpQuestionsDto>(
                It.IsAny<string>(),
                It.Is<string>(s => s.Contains("Rule text about winning") || s.Contains("Relevant Rule Context")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GenerateQuestionsAsync_RetriesOnceOnFailure()
    {
        // Arrange: First call fails (returns null), second succeeds
        _llmServiceMock
            .SetupSequence(x => x.GenerateJsonAsync<FollowUpQuestionsDto>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((FollowUpQuestionsDto?)null) // First attempt fails
            .ReturnsAsync(new FollowUpQuestionsDto(new List<string> { "Retry success" })); // Second succeeds

        var service = CreateService();

        // Act
        var result = await service.GenerateQuestionsAsync(
            "Test",
            "Answer",
            new List<Snippet>(),
            "Game");

        // Assert
        result.Should().ContainSingle();
        result[0].Should().Be("Retry success");
        // Verify retry behavior implicitly - if no retry, would get null and fail
    }

    [Fact]
    public async Task GenerateQuestionsAsync_ExceptionWithFailOnErrorFalse_ReturnsEmptyList()
    {
        // Arrange: LLM throws exception
        var config = new FollowUpQuestionsConfiguration
        {
            FailOnGenerationError = false
        };

        _llmServiceMock
            .Setup(x => x.GenerateJsonAsync<FollowUpQuestionsDto>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("LLM service unavailable"));

        var service = CreateService(config: Options.Create(config));

        // Act
        var result = await service.GenerateQuestionsAsync(
            "Test",
            "Answer",
            new List<Snippet>(),
            "Game");

        // Assert: Should gracefully degrade
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateQuestionsAsync_ExceptionWithFailOnErrorTrue_ThrowsException()
    {
        // Arrange
        var config = new FollowUpQuestionsConfiguration
        {
            FailOnGenerationError = true
        };

        _llmServiceMock
            .Setup(x => x.GenerateJsonAsync<FollowUpQuestionsDto>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("LLM service unavailable"));

        var service = CreateService(config: Options.Create(config));

        // Act & Assert
        var act = async () =>
        {
            await service.GenerateQuestionsAsync(
                "Test",
                "Answer",
                new List<Snippet>(),
                "Game");
        };

        await act.Should().ThrowAsync<Exception>().WithMessage("LLM service unavailable");
    }

    [Fact]
    public async Task GenerateQuestionsAsync_UserCancellation_PropagatesCancellation()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel(); // Pre-cancelled

        _llmServiceMock
            .Setup(x => x.GenerateJsonAsync<FollowUpQuestionsDto>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var service = CreateService();

        // Act & Assert
        var act = async () =>
        {
            await service.GenerateQuestionsAsync(
                "Test",
                "Answer",
                new List<Snippet>(),
                "Game",
                ct: cts.Token);
        };

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private FollowUpQuestionService CreateService(
        ILlmService? llmService = null,
        IOptions<FollowUpQuestionsConfiguration>? config = null)
    {
        return new FollowUpQuestionService(
            llmService ?? _llmServiceMock.Object,
            _mockLogger.Object,
            config ?? Options.Create(_defaultConfig),
            new TestMeterFactory());
    }
}

/// <summary>
/// Test implementation of IMeterFactory for unit tests
/// </summary>
public class TestMeterFactory : System.Diagnostics.Metrics.IMeterFactory
{
    public System.Diagnostics.Metrics.Meter Create(System.Diagnostics.Metrics.MeterOptions options)
    {
        return new System.Diagnostics.Metrics.Meter(options.Name ?? "Test");
    }

    public void Dispose() { }
}
