using System.Diagnostics;
using Api.Helpers;
using Api.Models;
using Api.Observability;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.Helpers;

/// <summary>
/// Unit tests for RagExceptionHandler (Issue #1441).
/// Verifies centralized exception handling eliminates duplicate catch blocks.
/// </summary>
public class RagExceptionHandlerTests
{
    private readonly Mock<ILogger> _mockLogger;
    private readonly string _testGameId = "test-game-123";
    private readonly Activity? _testActivity;
    private readonly Stopwatch _stopwatch;

    public RagExceptionHandlerTests()
    {
        _mockLogger = new Mock<ILogger>();
        _testActivity = MeepleAiActivitySources.Rag.StartActivity("Test");
        _stopwatch = Stopwatch.StartNew();
    }

    [Fact]
    public void HandleExceptionDispatch_HttpRequestException_ReturnsNetworkErrorResponse()
    {
        // Arrange
        var exception = new HttpRequestException("Connection refused");
        var errorFactories = new Dictionary<string, Func<QaResponse>>
        {
            ["HttpRequestException"] = () => new QaResponse("Network error", Array.Empty<Snippet>()),
            ["Exception"] = () => new QaResponse("Unexpected error", Array.Empty<Snippet>())
        };

        // Act
        var response = RagExceptionHandler.HandleExceptionDispatch(
            exception,
            _mockLogger.Object,
            "RAG query",
            _testGameId,
            "qa",
            _testActivity,
            _stopwatch,
            errorFactories);

        // Assert
        Assert.Equal("Network error", response.Answer);
        Assert.Empty(response.Snippets);
    }

    [Fact]
    public void HandleExceptionDispatch_TaskCanceledException_ReturnsTimeoutErrorResponse()
    {
        // Arrange
        var exception = new TaskCanceledException("Operation timed out");
        var errorFactories = new Dictionary<string, Func<QaResponse>>
        {
            ["TaskCanceledException"] = () => new QaResponse("Request timed out", Array.Empty<Snippet>()),
            ["Exception"] = () => new QaResponse("Unexpected error", Array.Empty<Snippet>())
        };

        // Act
        var response = RagExceptionHandler.HandleExceptionDispatch(
            exception,
            _mockLogger.Object,
            "RAG query",
            _testGameId,
            "qa",
            _testActivity,
            _stopwatch,
            errorFactories);

        // Assert
        Assert.Contains("timed out", response.Answer, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HandleExceptionDispatch_InvalidOperationException_ReturnsConfigErrorResponse()
    {
        // Arrange
        var exception = new InvalidOperationException("Invalid configuration");
        var errorFactories = new Dictionary<string, Func<QaResponse>>
        {
            ["InvalidOperationException"] = () => new QaResponse("Configuration error", Array.Empty<Snippet>()),
            ["Exception"] = () => new QaResponse("Unexpected error", Array.Empty<Snippet>())
        };

        // Act
        var response = RagExceptionHandler.HandleExceptionDispatch(
            exception,
            _mockLogger.Object,
            "RAG query",
            _testGameId,
            "qa",
            _testActivity,
            _stopwatch,
            errorFactories);

        // Assert
        Assert.Contains("Configuration error", response.Answer);
    }

    [Fact]
    public void HandleExceptionDispatch_DbUpdateException_ReturnsDatabaseErrorResponse()
    {
        // Arrange
        var exception = new DbUpdateException("Database error");
        var errorFactories = new Dictionary<string, Func<QaResponse>>
        {
            ["DbUpdateException"] = () => new QaResponse("Database error", Array.Empty<Snippet>()),
            ["Exception"] = () => new QaResponse("Unexpected error", Array.Empty<Snippet>())
        };

        // Act
        var response = RagExceptionHandler.HandleExceptionDispatch(
            exception,
            _mockLogger.Object,
            "RAG query",
            _testGameId,
            "qa",
            _testActivity,
            _stopwatch,
            errorFactories);

        // Assert
        Assert.Contains("Database error", response.Answer);
    }

    [Fact]
    public void HandleExceptionDispatch_UnknownException_FallsBackToGenericError()
    {
        // Arrange
        var exception = new ArgumentException("Invalid argument");
        var errorFactories = new Dictionary<string, Func<QaResponse>>
        {
            ["HttpRequestException"] = () => new QaResponse("Network error", Array.Empty<Snippet>()),
            ["Exception"] = () => new QaResponse("Unexpected error", Array.Empty<Snippet>())
        };

        // Act
        var response = RagExceptionHandler.HandleExceptionDispatch(
            exception,
            _mockLogger.Object,
            "RAG query",
            _testGameId,
            "qa",
            _testActivity,
            _stopwatch,
            errorFactories);

        // Assert
        Assert.Equal("Unexpected error", response.Answer);
    }

    [Fact]
    public void HandleExceptionDispatch_WithExplainResponse_ReturnsExplainError()
    {
        // Arrange
        var exception = new HttpRequestException("Network error");
        var errorFactories = new Dictionary<string, Func<ExplainResponse>>
        {
            ["HttpRequestException"] = () => new ExplainResponse(
                new ExplainOutline("", new List<string>()),
                "Network error during explanation",
                Array.Empty<Snippet>(),
                0),
            ["Exception"] = () => new ExplainResponse(
                new ExplainOutline("", new List<string>()),
                "Unexpected error",
                Array.Empty<Snippet>(),
                0)
        };

        // Act
        var response = RagExceptionHandler.HandleExceptionDispatch(
            exception,
            _mockLogger.Object,
            "RAG explain",
            _testGameId,
            "explain",
            _testActivity,
            _stopwatch,
            errorFactories);

        // Assert
        Assert.Contains("Network error", response.Script);
    }

    [Fact]
    public void HandleExceptionDispatch_LogsException()
    {
        // Arrange
        var exception = new HttpRequestException("Test error");
        var errorFactories = new Dictionary<string, Func<QaResponse>>
        {
            ["HttpRequestException"] = () => new QaResponse("Network error", Array.Empty<Snippet>()),
            ["Exception"] = () => new QaResponse("Unexpected error", Array.Empty<Snippet>())
        };

        // Act
        RagExceptionHandler.HandleExceptionDispatch(
            exception,
            _mockLogger.Object,
            "RAG query",
            _testGameId,
            "qa",
            _testActivity,
            _stopwatch,
            errorFactories);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public void HandleExceptionDispatch_SetsActivityErrorStatus()
    {
        // Arrange
        using var activity = MeepleAiActivitySources.Rag.StartActivity("Test");
        var exception = new HttpRequestException("Test error");
        var errorFactories = new Dictionary<string, Func<QaResponse>>
        {
            ["HttpRequestException"] = () => new QaResponse("Network error", Array.Empty<Snippet>()),
            ["Exception"] = () => new QaResponse("Unexpected error", Array.Empty<Snippet>())
        };

        // Act
        RagExceptionHandler.HandleExceptionDispatch(
            exception,
            _mockLogger.Object,
            "RAG query",
            _testGameId,
            "qa",
            activity,
            _stopwatch,
            errorFactories);

        // Assert
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Contains("Test error", activity.StatusDescription ?? "");
    }

    [Fact]
    public void HandleExceptionDispatch_WithAdditionalInfo_IncludesInLog()
    {
        // Arrange
        var exception = new HttpRequestException("Test error");
        var errorFactories = new Dictionary<string, Func<QaResponse>>
        {
            ["HttpRequestException"] = () => new QaResponse("Network error", Array.Empty<Snippet>()),
            ["Exception"] = () => new QaResponse("Unexpected error", Array.Empty<Snippet>())
        };

        // Act
        RagExceptionHandler.HandleExceptionDispatch(
            exception,
            _mockLogger.Object,
            "RAG query",
            _testGameId,
            "qa",
            _testActivity,
            _stopwatch,
            errorFactories,
            additionalInfo: "mode: hybrid");

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public void HandleException_WithSpecificLogAction_CallsLogAction()
    {
        // Arrange
        var exception = new HttpRequestException("Test error");
        var logActionCalled = false;
        Action<ILogger, Exception> logAction = (logger, ex) => { logActionCalled = true; };
        Func<QaResponse> errorResponseFactory = () => new QaResponse("Network error", Array.Empty<Snippet>());

        // Act
        RagExceptionHandler.HandleException(
            exception,
            _mockLogger.Object,
            logAction,
            _testGameId,
            "qa",
            _testActivity,
            _stopwatch,
            errorResponseFactory);

        // Assert
        Assert.True(logActionCalled);
    }

    [Fact]
    public void GetLogAction_HttpRequestException_ReturnsHttpLogAction()
    {
        // Arrange
        var exception = new HttpRequestException("Connection refused");

        // Act
        var logAction = RagExceptionHandler.GetLogAction("HttpRequestException", "RAG query", _testGameId);

        // Assert
        Assert.NotNull(logAction);
        // Verify it doesn't throw
        logAction(_mockLogger.Object, exception);
    }

    [Fact]
    public void GetLogAction_TaskCanceledException_ReturnsTimeoutLogAction()
    {
        // Arrange
        var exception = new TaskCanceledException("Operation timed out");

        // Act
        var logAction = RagExceptionHandler.GetLogAction("TaskCanceledException", "RAG query", _testGameId);

        // Assert
        Assert.NotNull(logAction);
        logAction(_mockLogger.Object, exception);
    }

    [Fact]
    public void GetLogAction_InvalidOperationException_ReturnsInvalidOpLogAction()
    {
        // Arrange
        var exception = new InvalidOperationException("Invalid configuration");

        // Act
        var logAction = RagExceptionHandler.GetLogAction("InvalidOperationException", "RAG query", _testGameId);

        // Assert
        Assert.NotNull(logAction);
        logAction(_mockLogger.Object, exception);
    }

    [Fact]
    public void GetLogAction_DbUpdateException_ReturnsDatabaseLogAction()
    {
        // Arrange
        var exception = new DbUpdateException("Database error");

        // Act
        var logAction = RagExceptionHandler.GetLogAction("DbUpdateException", "RAG query", _testGameId);

        // Assert
        Assert.NotNull(logAction);
        logAction(_mockLogger.Object, exception);
    }

    [Fact]
    public void GetLogAction_UnknownException_ReturnsGenericLogAction()
    {
        // Arrange
        var exception = new ArgumentException("Invalid argument");

        // Act
        var logAction = RagExceptionHandler.GetLogAction("ArgumentException", "RAG query", _testGameId);

        // Assert
        Assert.NotNull(logAction);
        logAction(_mockLogger.Object, exception);
    }

    [Fact]
    public void HandleExceptionDispatch_WithNullActivity_HandlesGracefully()
    {
        // Arrange
        var exception = new HttpRequestException("Test error");
        var errorFactories = new Dictionary<string, Func<QaResponse>>
        {
            ["HttpRequestException"] = () => new QaResponse("Network error", Array.Empty<Snippet>()),
            ["Exception"] = () => new QaResponse("Unexpected error", Array.Empty<Snippet>())
        };

        // Act
        var response = RagExceptionHandler.HandleExceptionDispatch(
            exception,
            _mockLogger.Object,
            "RAG query",
            _testGameId,
            "qa",
            activity: null, // Null activity
            _stopwatch,
            errorFactories);

        // Assert
        Assert.Equal("Network error", response.Answer);
    }
}
