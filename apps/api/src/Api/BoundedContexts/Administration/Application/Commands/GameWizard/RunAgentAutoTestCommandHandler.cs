using System.Diagnostics;
using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Commands.GameWizard;

/// <summary>
/// Runs 8 standard board game questions against the RAG agent for a game,
/// evaluates responses, and produces a quality report with grading.
/// </summary>
internal sealed class RunAgentAutoTestCommandHandler : ICommandHandler<RunAgentAutoTestCommand, AgentAutoTestResult>
{
    private readonly IMediator _mediator;
    private readonly ISharedGameRepository _gameRepo;
    private readonly ILogger<RunAgentAutoTestCommandHandler> _logger;

    /// <summary>
    /// Standard test questions that apply to any board game.
    /// Designed to test RAG retrieval quality across different rulebook sections.
    /// </summary>
    private static readonly string[] TestQuestions =
    [
        "How do you set up the game?",
        "What are the win conditions?",
        "How does the turn order work?",
        "What actions can a player take on their turn?",
        "How does scoring work?",
        "Are there any special rules or exceptions?",
        "What components are included in the game?",
        "How many players can play and what is the recommended player count?"
    ];

    public RunAgentAutoTestCommandHandler(
        IMediator mediator,
        ISharedGameRepository gameRepo,
        ILogger<RunAgentAutoTestCommandHandler> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _gameRepo = gameRepo ?? throw new ArgumentNullException(nameof(gameRepo));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AgentAutoTestResult> Handle(RunAgentAutoTestCommand request, CancellationToken cancellationToken)
    {
        var game = await _gameRepo.GetByIdAsync(request.GameId, cancellationToken).ConfigureAwait(false);
        if (game == null)
        {
            throw new NotFoundException("Game", request.GameId.ToString());
        }

        _logger.LogInformation(
            "AutoTest: Starting test suite for game {GameId} ({GameTitle}) with {Count} questions",
            request.GameId, game.Title, TestQuestions.Length);

        var testCases = new List<TestCaseResult>();

        for (var i = 0; i < TestQuestions.Length; i++)
        {
            var question = TestQuestions[i];
            var testCase = await RunSingleTest(i, question, request.GameId, cancellationToken).ConfigureAwait(false);
            testCases.Add(testCase);
        }

        var report = BuildAgentQualityReport(testCases);

        _logger.LogInformation(
            "AutoTest: Completed for game {GameId}. Grade={Grade}, Pass={Passed}/{Total}, AvgConfidence={Confidence:F2}",
            request.GameId, report.OverallGrade, report.Passed, report.TotalTests, report.AverageConfidence);

        return new AgentAutoTestResult
        {
            GameId = request.GameId,
            GameTitle = game.Title,
            TestCases = testCases,
            Report = report,
            ExecutedAt = DateTime.UtcNow
        };
    }

    private async Task<TestCaseResult> RunSingleTest(
        int index,
        string question,
        Guid gameId,
        CancellationToken cancellationToken)
    {
        try
        {
            var sw = Stopwatch.StartNew();

            var query = new AskQuestionQuery(
                GameId: gameId,
                Question: question,
                SearchMode: "Hybrid"
            );

            var response = await _mediator.Send(query, cancellationToken).ConfigureAwait(false);
            sw.Stop();

            var hasAnswer = !string.IsNullOrWhiteSpace(response.Answer);
            var hasSources = response.Sources.Count > 0;
            var avgConfidence = hasSources
                ? response.Sources.Average(s => s.RelevanceScore)
                : 0.0;
            var latency = (int)sw.ElapsedMilliseconds;

            // Determine pass/fail
            string? failureReason = null;
            var passed = true;

            if (!hasAnswer)
            {
                passed = false;
                failureReason = "No answer generated";
            }
            else if (!hasSources)
            {
                passed = false;
                failureReason = "No relevant chunks retrieved from vector search";
            }
            else if (avgConfidence < 0.3)
            {
                passed = false;
                failureReason = $"Average chunk confidence too low: {avgConfidence:F2}";
            }
            else if (latency > 30000)
            {
                passed = false;
                failureReason = $"Response too slow: {latency}ms (max 30s)";
            }

            return new TestCaseResult
            {
                Index = index,
                Question = question,
                Answer = response.Answer,
                ConfidenceScore = avgConfidence,
                LatencyMs = latency,
                ChunksRetrieved = response.Sources.Count,
                Passed = passed,
                FailureReason = failureReason
            };
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AutoTest: Question {Index} failed with error: {Message}", index, ex.Message);

            return new TestCaseResult
            {
                Index = index,
                Question = question,
                Answer = null,
                ConfidenceScore = 0,
                LatencyMs = 0,
                ChunksRetrieved = 0,
                Passed = false,
                FailureReason = $"Error: {ex.Message}"
            };
        }
#pragma warning restore CA1031
    }

    private static AgentQualityReport BuildAgentQualityReport(List<TestCaseResult> testCases)
    {
        var total = testCases.Count;
        var passed = testCases.Count(t => t.Passed);
        var failed = total - passed;
        var passRate = total > 0 ? (double)passed / total : 0;
        var avgConfidence = testCases.Count > 0 ? testCases.Average(t => t.ConfidenceScore) : 0;
        var avgLatency = testCases.Count > 0 ? (int)testCases.Average(t => t.LatencyMs) : 0;

        // Grading: A (>80% pass + avg confidence >0.7), B (>60%), C (>40%), F (<40%)
        var grade = passRate switch
        {
            > 0.8 when avgConfidence > 0.7 => "A",
            > 0.8 => "B",
            > 0.6 => "B",
            > 0.4 => "C",
            _ => "F"
        };

        return new AgentQualityReport
        {
            TotalTests = total,
            Passed = passed,
            Failed = failed,
            AverageConfidence = Math.Round(avgConfidence, 3),
            AverageLatencyMs = avgLatency,
            OverallGrade = grade,
            PassRate = Math.Round(passRate, 3)
        };
    }
}
