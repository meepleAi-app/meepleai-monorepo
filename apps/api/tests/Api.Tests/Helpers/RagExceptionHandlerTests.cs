using System.Diagnostics;
using Api.Helpers;
using Api.Models;
using Api.Observability;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.Helpers;

/// <summary>
/// Unit tests for RagExceptionHandler (Issue #1441).
/// Verifies centralized exception handling eliminates duplicate catch blocks.
/// </summary>
[Trait("Category", TestCategories.Unit)]
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
        var expectedResponse = new QaResponse("Network error", Array.Empty<Snippet>());
        var errorFactories = new Dictionary<string, Func<QaResponse>>
        {
            ["HttpRequestException"] = () => expectedResponse,
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
        Assert.Equal(expectedResponse.answer, response.answer);
        Assert.Empty(response.snippets);
    }

    [Fact]
    public void HandleExceptionDispatch_TaskCanceledException_ReturnsTimeoutErrorResponse()
    {
        // Arrange
        var exception = new TaskCanceledException("Operation timed out");
        var expectedResponse = new QaResponse("Request timed out", Array.Empty<Snippet>());
        var errorFactories = new Dictionary<string, Func<QaResponse>>
        {
            ["TaskCanceledException"] = () => expectedResponse,
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
        Assert.Equal(expectedResponse.answer, response.answer);
    }

    [Fact]
    public void HandleExceptionDispatch_InvalidOperationException_ReturnsConfigErrorResponse()
    {
        // Arrange
        var exception = new InvalidOperationException("Invalid configuration");
        var expectedResponse = new QaResponse("Configuration error", Array.Empty<Snippet>());
        var errorFactories = new Dictionary<string, Func<QaResponse>>
        {
            ["InvalidOperationException"] = () => expectedResponse,
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
        Assert.Contains(expectedResponse.answer, response.answer);
    }

    [Fact]
    public void HandleExceptionDispatch_DbUpdateException_ReturnsDatabaseErrorResponse()
    {
        // Arrange
        var exception = new DbUpdateException("Database error");
        var expectedResponse = new QaResponse("Database error", Array.Empty<Snippet>());
        var errorFactories = new Dictionary<string, Func<QaResponse>>
        {
            ["DbUpdateException"] = () => expectedResponse,
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
        Assert.Contains(expectedResponse.answer, response.answer);
    }

    [Fact]
    public void HandleExceptionDispatch_UnknownException_FallsBackToGenericError()
    {
        // Arrange
        var exception = new ArgumentException("Invalid argument");
        var expectedResponse = new QaResponse("Unexpected error", Array.Empty<Snippet>());
        var errorFactories = new Dictionary<string, Func<QaResponse>>
        {
            ["HttpRequestException"] = () => new QaResponse("Network error", Array.Empty<Snippet>()),
            ["Exception"] = () => expectedResponse
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
        Assert.Equal(expectedResponse.answer, response.answer);
    }

    [Fact]
    public void HandleExceptionDispatch_WithExplainResponse_ReturnsExplainError()
    {
        // Arrange
        var exception = new HttpRequestException("Network error");
        var expectedResponse = new ExplainResponse(
            new ExplainOutline("", new List<string>()),
            "Network error during explanation",
            Array.Empty<Snippet>(),
            0);
        var errorFactories = new Dictionary<string, Func<ExplainResponse>>
        {
            ["HttpRequestException"] = () => expectedResponse,
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
        Assert.Contains(expectedResponse.script, response.script);
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
        if (activity != null)
        {
            Assert.Equal(ActivityStatusCode.Error, activity.Status);
            Assert.Contains("Test error", activity.StatusDescription ?? "");
        }
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
        var expectedResponse = new QaResponse("Network error", Array.Empty<Snippet>());
        var errorFactories = new Dictionary<string, Func<QaResponse>>
        {
            ["HttpRequestException"] = () => expectedResponse,
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
        Assert.Equal(expectedResponse.answer, response.answer);
    }

    // Issue #1444: Tests for HandleServiceException method

    [Fact]
    public void HandleServiceException_HttpRequestException_ReturnsFailureResult()
    {
        // Arrange
        var exception = new HttpRequestException("Connection refused");
        Func<string, TestResult> failureFactory = errorMessage => TestResult.CreateFailure(errorMessage);

        // Act
        var result = RagExceptionHandler.HandleServiceException(
            exception,
            _mockLogger.Object,
            "vector search",
            failureFactory);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Network error", result.ErrorMessage ?? "");
    }

    [Fact]
    public void HandleServiceException_TaskCanceledException_ReturnsTimeoutMessage()
    {
        // Arrange
        var exception = new TaskCanceledException("Request timed out");
        Func<string, TestResult> failureFactory = errorMessage => TestResult.CreateFailure(errorMessage);

        // Act
        var result = RagExceptionHandler.HandleServiceException(
            exception,
            _mockLogger.Object,
            "embedding generation",
            failureFactory);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("timed out", result.ErrorMessage ?? "");
    }

    [Fact]
    public void HandleServiceException_WithActivity_SetsErrorStatus()
    {
        // Arrange
        using var activity = MeepleAiActivitySources.Rag.StartActivity("Test");
        var exception = new InvalidOperationException("Configuration error");
        Func<string, TestResult> failureFactory = errorMessage => TestResult.CreateFailure(errorMessage);

        // Act
        var result = RagExceptionHandler.HandleServiceException(
            exception,
            _mockLogger.Object,
            "service operation",
            failureFactory,
            activity);

        // Assert
        if (activity != null)
        {
            Assert.Equal(ActivityStatusCode.Error, activity.Status);
            Assert.Contains("Configuration error", activity.StatusDescription ?? "");
        }
    }

    [Fact]
    public void HandleServiceException_WithoutUserFriendlyMessage_ReturnsTechnicalMessage()
    {
        // Arrange
        var exception = new ArgumentException("Invalid parameter");
        Func<string, TestResult> failureFactory = errorMessage => TestResult.CreateFailure(errorMessage);

        // Act
        var result = RagExceptionHandler.HandleServiceException(
            exception,
            _mockLogger.Object,
            "validation",
            failureFactory,
            useUserFriendlyMessage: false);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("validation failed", result.ErrorMessage ?? "");
    }

    [Fact]
    public void HandleServiceException_LogsException()
    {
        // Arrange
        var exception = new HttpRequestException("Test error");
        Func<string, TestResult> failureFactory = errorMessage => TestResult.CreateFailure(errorMessage);

        // Act
        RagExceptionHandler.HandleServiceException(
            exception,
            _mockLogger.Object,
            "test operation",
            failureFactory);

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

    // Issue #1444: Tests for LogAndRethrow method

    [Fact]
    public void LogAndRethrow_LogsExceptionAndRethrows()
    {
        // Arrange
        var exception = new InvalidOperationException("Test error");

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            RagExceptionHandler.LogAndRethrow(
                exception,
                _mockLogger.Object,
                "test operation"));

        Assert.Equal(exception, ex);
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
    public void LogAndRethrow_WithAdditionalContext_IncludesContextInLog()
    {
        // Arrange
        var exception = new HttpRequestException("Connection failed");
        var additionalContext = new object[] { "gameId", "123", "query", "test" };

        // Act & Assert
        var ex = Assert.Throws<HttpRequestException>(() =>
            RagExceptionHandler.LogAndRethrow(
                exception,
                _mockLogger.Object,
                "search operation",
                additionalContext));

        Assert.Equal(exception, ex);
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    // Issue #1444: Tests for GetUserFriendlyMessage method

    [Fact]
    public void GetUserFriendlyMessage_TaskCanceledException_ReturnsTimeoutMessage()
    {
        // Arrange
        var exception = new TaskCanceledException("Operation timed out");

        // Act
        var message = RagExceptionHandler.GetUserFriendlyMessage(exception, "Default message");

        // Assert
        Assert.Contains("timed out", message);
        Assert.Contains("try again", message);
    }

    [Fact]
    public void GetUserFriendlyMessage_TimeoutException_ReturnsTimeoutMessage()
    {
        // Arrange
        var exception = new TimeoutException("Request timeout");

        // Act
        var message = RagExceptionHandler.GetUserFriendlyMessage(exception, "Default message");

        // Assert
        Assert.Contains("timed out", message);
    }

    [Fact]
    public void GetUserFriendlyMessage_HttpRequestException_ReturnsNetworkError()
    {
        // Arrange
        var exception = new HttpRequestException("Connection failed", null, System.Net.HttpStatusCode.BadGateway);

        // Act
        var message = RagExceptionHandler.GetUserFriendlyMessage(exception, "Default message");

        // Assert
        Assert.Contains("Network error", message);
        Assert.Contains("502", message);
    }

    [Fact]
    public void GetUserFriendlyMessage_UnknownException_ReturnsDefaultMessage()
    {
        // Arrange
        var exception = new NotImplementedException("Feature not implemented");
        var defaultMessage = "This is the default error message";

        // Act
        var message = RagExceptionHandler.GetUserFriendlyMessage(exception, defaultMessage);

        // Assert
        Assert.Equal(defaultMessage, message);
    }

    [Fact]
    public void GetUserFriendlyMessage_InvalidOperationExceptionWithConfiguration_ReturnsConfigError()
    {
        // Arrange
        var exception = new InvalidOperationException("Configuration is invalid");

        // Act
        var message = RagExceptionHandler.GetUserFriendlyMessage(exception, "Default message");

        // Assert
        Assert.Contains("Configuration error", message);
        Assert.Contains("contact support", message);
    }

    [Fact]
    public void GetUserFriendlyMessage_ArgumentException_ReturnsInvalidParametersMessage()
    {
        // Arrange
        var exception = new ArgumentException("Invalid argument provided");

        // Act
        var message = RagExceptionHandler.GetUserFriendlyMessage(exception, "Default message");

        // Assert
        Assert.Contains("Invalid request parameters", message);
    }

    // Helper class for testing Result pattern
    private record TestResult
    {
        public bool Success { get; init; }
        public string? ErrorMessage { get; init; }

        public static TestResult CreateFailure(string error) => new() { Success = false, ErrorMessage = error };
    }
}

