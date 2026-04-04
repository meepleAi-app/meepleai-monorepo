using Api.BoundedContexts.Administration.Application.Commands.PromptEvaluation;
using Api.Models;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application.Handlers.PromptHandlers;

[Trait("Category", TestCategories.Unit)]
public class EvaluatePromptCommandHandlerTests
{
    private readonly Mock<IPromptEvaluationService> _mockEvaluationService;
    private readonly Mock<ILogger<EvaluatePromptCommandHandler>> _mockLogger;
    private readonly EvaluatePromptCommandHandler _handler;

    public EvaluatePromptCommandHandlerTests()
    {
        _mockEvaluationService = new Mock<IPromptEvaluationService>();
        _mockLogger = new Mock<ILogger<EvaluatePromptCommandHandler>>();
        _handler = new EvaluatePromptCommandHandler(
            _mockEvaluationService.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldEvaluateAndStoreResults()
    {
        // Arrange
        var command = new EvaluatePromptCommand
        {
            TemplateId = "template-123",
            VersionId = "version-456",
            DatasetPath = "/datasets/qa-test-dataset.json",
            StoreResults = true
        };

        var evaluationResult = new PromptEvaluationResult
        {
            EvaluationId = "eval-789",
            TemplateId = "template-123",
            VersionId = "version-456",
            DatasetId = "qa-test-dataset",
            ExecutedAt = DateTime.UtcNow,
            TotalQueries = 10,
            Passed = true,
            Metrics = new EvaluationMetrics
            {
                Accuracy = 85.5,
                Relevance = 90.0,
                Completeness = 80.0,
                Clarity = 88.0,
                CitationQuality = 92.0
            },
            QueryResults = new List<QueryEvaluationResult>(),
            Summary = "Evaluation passed all thresholds"
        };

        _mockEvaluationService
            .Setup(s => s.EvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Action<int, int>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(evaluationResult);

        _mockEvaluationService
            .Setup(s => s.StoreResultsAsync(It.IsAny<PromptEvaluationResult>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.EvaluationId.Should().Be("eval-789");
        result.TemplateId.Should().Be("template-123");
        result.VersionId.Should().Be("version-456");
        result.Passed.Should().BeTrue();
        result.Metrics.Accuracy.Should().Be(85.5);
        result.Metrics.Relevance.Should().Be(90.0);

        _mockEvaluationService.Verify(s => s.EvaluateAsync(
            "template-123",
            "version-456",
            "/datasets/qa-test-dataset.json",
            null,
            It.IsAny<CancellationToken>()),
            Times.Once);

        _mockEvaluationService.Verify(s => s.StoreResultsAsync(
            It.Is<PromptEvaluationResult>(r => r.EvaluationId == "eval-789"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithStoreResultsFalse_ShouldNotStoreResults()
    {
        // Arrange
        var command = new EvaluatePromptCommand
        {
            TemplateId = "template-123",
            VersionId = "version-456",
            DatasetPath = "/datasets/qa-test-dataset.json",
            StoreResults = false
        };

        var evaluationResult = new PromptEvaluationResult
        {
            EvaluationId = "eval-789",
            TemplateId = "template-123",
            VersionId = "version-456",
            DatasetId = "qa-test-dataset",
            ExecutedAt = DateTime.UtcNow,
            TotalQueries = 10,
            Passed = true,
            Metrics = new EvaluationMetrics
            {
                Accuracy = 85.5,
                Relevance = 90.0,
                Completeness = 80.0,
                Clarity = 88.0,
                CitationQuality = 92.0
            },
            QueryResults = new List<QueryEvaluationResult>(),
            Summary = "Evaluation passed"
        };

        _mockEvaluationService
            .Setup(s => s.EvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Action<int, int>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(evaluationResult);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.EvaluationId.Should().Be("eval-789");

        _mockEvaluationService.Verify(s => s.EvaluateAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<Action<int, int>?>(),
            It.IsAny<CancellationToken>()),
            Times.Once);

        _mockEvaluationService.Verify(s => s.StoreResultsAsync(
            It.IsAny<PromptEvaluationResult>(),
            It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithFailedEvaluation_ShouldReturnFailedResult()
    {
        // Arrange
        var command = new EvaluatePromptCommand
        {
            TemplateId = "template-123",
            VersionId = "version-456",
            DatasetPath = "/datasets/qa-test-dataset.json",
            StoreResults = true
        };

        var evaluationResult = new PromptEvaluationResult
        {
            EvaluationId = "eval-789",
            TemplateId = "template-123",
            VersionId = "version-456",
            DatasetId = "qa-test-dataset",
            ExecutedAt = DateTime.UtcNow,
            TotalQueries = 10,
            Passed = false,
            Metrics = new EvaluationMetrics
            {
                Accuracy = 65.0,
                Relevance = 70.0,
                Completeness = 60.0,
                Clarity = 68.0,
                CitationQuality = 72.0
            },
            QueryResults = new List<QueryEvaluationResult>(),
            Summary = "Failed: Accuracy below threshold (65% < 80%)"
        };

        _mockEvaluationService
            .Setup(s => s.EvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Action<int, int>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(evaluationResult);

        _mockEvaluationService
            .Setup(s => s.StoreResultsAsync(It.IsAny<PromptEvaluationResult>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Passed.Should().BeFalse();
        result.Metrics.Accuracy.Should().BeLessThan(80.0);
        result.Summary.Should().Contain("Failed");

        _mockEvaluationService.Verify(s => s.StoreResultsAsync(
            It.Is<PromptEvaluationResult>(r => !r.Passed),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithNullCommand_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var act = () =>
            _handler.Handle(null!, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullEvaluationService_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var act = () =>
            new EvaluatePromptCommandHandler(null!, _mockLogger.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var act = () =>
            new EvaluatePromptCommandHandler(_mockEvaluationService.Object, null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
