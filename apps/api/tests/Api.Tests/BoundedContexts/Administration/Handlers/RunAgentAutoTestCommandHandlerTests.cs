using Api.BoundedContexts.Administration.Application.Commands.GameWizard;
using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Handlers;

/// <summary>
/// Unit tests for RunAgentAutoTestCommandHandler.
/// Issue #4673: Verifies test suite execution, grading logic, NotFoundException on missing game.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Administration")]
public class RunAgentAutoTestCommandHandlerTests
{
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<ISharedGameRepository> _mockGameRepo;
    private readonly RunAgentAutoTestCommandHandler _handler;

    private static readonly Guid GameId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    public RunAgentAutoTestCommandHandlerTests()
    {
        _mockMediator = new Mock<IMediator>();
        _mockGameRepo = new Mock<ISharedGameRepository>();

        _handler = new RunAgentAutoTestCommandHandler(
            _mockMediator.Object,
            _mockGameRepo.Object,
            NullLogger<RunAgentAutoTestCommandHandler>.Instance);
    }

    // ────────────────────────────────────────────────────────────────────────
    // NotFoundException when game is missing
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenGameNotFound_ThrowsNotFoundException()
    {
        // Arrange
        _mockGameRepo
            .Setup(r => r.GetByIdAsync(GameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SharedGame?)null);

        var command = new RunAgentAutoTestCommand(GameId: GameId, RequestedByUserId: UserId);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ────────────────────────────────────────────────────────────────────────
    // Executes all 8 standard test questions
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithValidGame_ExecutesAllEightTestQuestions()
    {
        // Arrange
        var game = CreateFakeSharedGame(GameId, "Gloomhaven", 174430);
        _mockGameRepo
            .Setup(r => r.GetByIdAsync(GameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        _mockMediator
            .Setup(m => m.Send(It.IsAny<AskAgentQuestionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreatePassingResponse());

        var command = new RunAgentAutoTestCommand(GameId: GameId, RequestedByUserId: UserId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.TestCases.Should().HaveCount(8);
        _mockMediator.Verify(
            m => m.Send(It.IsAny<AskAgentQuestionCommand>(), It.IsAny<CancellationToken>()),
            Times.Exactly(8));
    }

    // ────────────────────────────────────────────────────────────────────────
    // Result structure: GameId, GameTitle, ExecutedAt
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithValidGame_ReturnsCorrectGameMetadata()
    {
        // Arrange
        var game = CreateFakeSharedGame(GameId, "Gloomhaven", 174430);
        _mockGameRepo
            .Setup(r => r.GetByIdAsync(GameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);
        _mockMediator
            .Setup(m => m.Send(It.IsAny<AskAgentQuestionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreatePassingResponse());

        var before = DateTime.UtcNow;
        var command = new RunAgentAutoTestCommand(GameId: GameId, RequestedByUserId: UserId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.GameId.Should().Be(GameId);
        result.GameTitle.Should().Be("Gloomhaven");
        result.ExecutedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(DateTime.UtcNow);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Grading: A grade when >80% pass and avg confidence >0.7
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenAllTestsPassWithHighConfidence_GradesA()
    {
        // Arrange
        var game = CreateFakeSharedGame(GameId, "Gloomhaven", 174430);
        _mockGameRepo
            .Setup(r => r.GetByIdAsync(GameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        // High-confidence passing response
        _mockMediator
            .Setup(m => m.Send(It.IsAny<AskAgentQuestionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreatePassingResponse(confidence: 0.9));

        var command = new RunAgentAutoTestCommand(GameId: GameId, RequestedByUserId: UserId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Report.OverallGrade.Should().Be("A");
        result.Report.Passed.Should().Be(8);
        result.Report.Failed.Should().Be(0);
        result.Report.PassRate.Should().Be(1.0);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Grading: B grade when >80% pass but avg confidence ≤0.7
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenAllTestsPassWithLowConfidence_GradesB()
    {
        // Arrange
        var game = CreateFakeSharedGame(GameId, "Catan", 13);
        _mockGameRepo
            .Setup(r => r.GetByIdAsync(GameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        // Passes (confidence 0.5 ≥ 0.3 threshold, latency ok) but avg confidence ≤0.7
        _mockMediator
            .Setup(m => m.Send(It.IsAny<AskAgentQuestionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreatePassingResponse(confidence: 0.5));

        var command = new RunAgentAutoTestCommand(GameId: GameId, RequestedByUserId: UserId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Report.OverallGrade.Should().Be("B");
    }

    // ────────────────────────────────────────────────────────────────────────
    // Grading: F grade when all tests fail (no answer returned)
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenAllTestsFail_GradesF()
    {
        // Arrange
        var game = CreateFakeSharedGame(GameId, "Pandemic", 30549);
        _mockGameRepo
            .Setup(r => r.GetByIdAsync(GameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        // No answer → Passed = false
        _mockMediator
            .Setup(m => m.Send(It.IsAny<AskAgentQuestionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateFailingResponse_NoAnswer());

        var command = new RunAgentAutoTestCommand(GameId: GameId, RequestedByUserId: UserId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Report.OverallGrade.Should().Be("F");
        result.Report.Passed.Should().Be(0);
        result.Report.Failed.Should().Be(8);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Individual test case: pass/fail fields populated correctly
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenResponseHasNoChunks_TestCaseFails()
    {
        // Arrange
        var game = CreateFakeSharedGame(GameId, "7 Wonders", 68448);
        _mockGameRepo
            .Setup(r => r.GetByIdAsync(GameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        _mockMediator
            .Setup(m => m.Send(It.IsAny<AskAgentQuestionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateFailingResponse_NoChunks());

        var command = new RunAgentAutoTestCommand(GameId: GameId, RequestedByUserId: UserId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert: all test cases fail + grade reflects total failure
        result.TestCases.Should().AllSatisfy(tc =>
        {
            tc.Passed.Should().BeFalse();
            tc.FailureReason.Should().NotBeNullOrEmpty();
        });
        result.Report.OverallGrade.Should().Be("F");
    }

    // ────────────────────────────────────────────────────────────────────────
    // Grading: C grade when 40–60% of tests pass (mixed pass/fail)
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenHalfTestsPass_GradesC()
    {
        // Arrange: alternating pass/fail → 4/8 pass (passRate = 0.5 → C grade)
        var game = CreateFakeSharedGame(GameId, "Agricola", 31260);
        _mockGameRepo
            .Setup(r => r.GetByIdAsync(GameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        var callCount = 0;
        _mockMediator
            .Setup(m => m.Send(It.IsAny<AskAgentQuestionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount % 2 == 0 ? CreatePassingResponse() : CreateFailingResponse_NoAnswer();
            });

        var command = new RunAgentAutoTestCommand(GameId: GameId, RequestedByUserId: UserId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert: 4/8 pass = passRate 0.5 → C (>0.4 but ≤0.6)
        result.Report.OverallGrade.Should().Be("C");
        result.Report.Passed.Should().Be(4);
        result.Report.Failed.Should().Be(4);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Individual test case: correct index assignment
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_TestCases_HaveSequentialIndices()
    {
        // Arrange
        var game = CreateFakeSharedGame(GameId, "Wingspan", 266192);
        _mockGameRepo
            .Setup(r => r.GetByIdAsync(GameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);
        _mockMediator
            .Setup(m => m.Send(It.IsAny<AskAgentQuestionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreatePassingResponse());

        var command = new RunAgentAutoTestCommand(GameId: GameId, RequestedByUserId: UserId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.TestCases.Select(t => t.Index).Should().BeEquivalentTo(Enumerable.Range(0, 8));
    }

    // ────────────────────────────────────────────────────────────────────────
    // AskAgentQuestionCommand sent with correct parameters
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_SendsAskAgentQuestionCommandWithCorrectStrategy()
    {
        // Arrange
        var game = CreateFakeSharedGame(GameId, "Gloomhaven", 174430);
        _mockGameRepo
            .Setup(r => r.GetByIdAsync(GameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);
        _mockMediator
            .Setup(m => m.Send(It.IsAny<AskAgentQuestionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreatePassingResponse());

        var command = new RunAgentAutoTestCommand(GameId: GameId, RequestedByUserId: UserId);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert: every call uses SingleModel strategy with correct GameId
        _mockMediator.Verify(
            m => m.Send(
                It.Is<AskAgentQuestionCommand>(c =>
                    c.Strategy == AgentSearchStrategy.SingleModel &&
                    c.GameId == GameId &&
                    c.TopK == 5),
                It.IsAny<CancellationToken>()),
            Times.Exactly(8));
    }

    // ────────────────────────────────────────────────────────────────────────
    // Exception from AskAgentQuestionCommand → test case marked as failed (no throw)
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenSingleQuestionThrows_TestCaseMarkedFailedWithoutAbort()
    {
        // Arrange
        var game = CreateFakeSharedGame(GameId, "Gloomhaven", 174430);
        _mockGameRepo
            .Setup(r => r.GetByIdAsync(GameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        var callCount = 0;
        _mockMediator
            .Setup(m => m.Send(It.IsAny<AskAgentQuestionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 3)
                    throw new InvalidOperationException("Vector search unavailable");
                return CreatePassingResponse();
            });

        var command = new RunAgentAutoTestCommand(GameId: GameId, RequestedByUserId: UserId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert: all 8 tests run, question 3 (index 2) is failed, others pass
        result.TestCases.Should().HaveCount(8);
        result.TestCases[2].Passed.Should().BeFalse();
        result.TestCases[2].FailureReason.Should().Contain("Error");
        result.TestCases.Count(t => t.Passed).Should().Be(7);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Report totals match individual test cases
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ReportTotals_MatchTestCaseOutcomes()
    {
        // Arrange
        var game = CreateFakeSharedGame(GameId, "Gloomhaven", 174430);
        _mockGameRepo
            .Setup(r => r.GetByIdAsync(GameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);
        _mockMediator
            .Setup(m => m.Send(It.IsAny<AskAgentQuestionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreatePassingResponse());

        var command = new RunAgentAutoTestCommand(GameId: GameId, RequestedByUserId: UserId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        var expectedPassed = result.TestCases.Count(t => t.Passed);
        var expectedFailed = result.TestCases.Count(t => !t.Passed);
        result.Report.TotalTests.Should().Be(8);
        result.Report.Passed.Should().Be(expectedPassed);
        result.Report.Failed.Should().Be(expectedFailed);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Helpers
    // ────────────────────────────────────────────────────────────────────────

    private static SharedGame CreateFakeSharedGame(Guid id, string title, int bggId) =>
        new(
            id: id,
            title: title,
            yearPublished: 2017,
            description: "Test description",
            minPlayers: 1,
            maxPlayers: 4,
            playingTimeMinutes: 90,
            minAge: 14,
            complexityRating: null,
            averageRating: null,
            imageUrl: "",
            thumbnailUrl: "",
            rules: null,
            status: GameStatus.Published,
            createdBy: Guid.NewGuid(),
            modifiedBy: null,
            createdAt: DateTime.UtcNow,
            modifiedAt: null,
            isDeleted: false,
            bggId: bggId);

    private static AgentChatResponse CreatePassingResponse(double confidence = 0.85) =>
        new()
        {
            Strategy = AgentSearchStrategy.SingleModel,
            StrategyDescription = "Single model",
            Answer = "The setup involves placing tiles and tokens on the board.",
            RetrievedChunks =
            [
                new CodeChunkDto
                {
                    FilePath = "rulebook.pdf",
                    StartLine = 1,
                    EndLine = 10,
                    CodePreview = "Setup: place the board in the center of the table.",
                    RelevanceScore = confidence,
                    BoundedContext = "rules",
                    ChunkIndex = 0
                }
            ],
            TokenUsage = TokenUsageDto.Empty,
            CostBreakdown = CostBreakdownDto.Zero(),
            LatencyMs = 500,
            SessionId = Guid.NewGuid().ToString(),
            Timestamp = DateTime.UtcNow
        };

    private static AgentChatResponse CreateFailingResponse_NoAnswer() =>
        new()
        {
            Strategy = AgentSearchStrategy.SingleModel,
            StrategyDescription = "Single model",
            Answer = null,
            RetrievedChunks =
            [
                new CodeChunkDto
                {
                    FilePath = "rulebook.pdf",
                    StartLine = 1,
                    EndLine = 5,
                    CodePreview = "Some content.",
                    RelevanceScore = 0.8,
                    BoundedContext = "rules",
                    ChunkIndex = 0
                }
            ],
            TokenUsage = TokenUsageDto.Empty,
            CostBreakdown = CostBreakdownDto.Zero(),
            LatencyMs = 200,
            SessionId = Guid.NewGuid().ToString(),
            Timestamp = DateTime.UtcNow
        };

    private static AgentChatResponse CreateFailingResponse_NoChunks() =>
        new()
        {
            Strategy = AgentSearchStrategy.SingleModel,
            StrategyDescription = "Single model",
            Answer = "I found some information.",
            RetrievedChunks = [],
            TokenUsage = TokenUsageDto.Empty,
            CostBreakdown = CostBreakdownDto.Zero(),
            LatencyMs = 150,
            SessionId = Guid.NewGuid().ToString(),
            Timestamp = DateTime.UtcNow
        };
}
