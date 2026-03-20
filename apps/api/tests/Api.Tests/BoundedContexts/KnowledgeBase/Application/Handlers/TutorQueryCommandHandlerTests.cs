using Api.Infrastructure.Entities;
using System.Net;
using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Unit tests for TutorQueryCommandHandler.
/// Issue #3490: Multi-Agent System - Test coverage for Tutor Agent.
/// </summary>
[Trait("Category", "Unit")]
public class TutorQueryCommandHandlerTests
{
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<ILogger<TutorQueryCommandHandler>> _loggerMock;
    private readonly TutorQueryCommandHandler _handler;
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;

    public TutorQueryCommandHandlerTests()
    {
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _loggerMock = new Mock<ILogger<TutorQueryCommandHandler>>();
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();

        var httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("http://localhost:8090")
        };

        _httpClientFactoryMock
            .Setup(f => f.CreateClient("OrchestrationService"))
            .Returns(httpClient);

        _handler = new TutorQueryCommandHandler(_httpClientFactoryMock.Object, CreatePermissiveRagAccessServiceMock(), _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidRequest_ReturnsSuccessfulResponse()
    {
        // Arrange
        var command = new TutorQueryCommand(
            GameId: Guid.NewGuid(),
            SessionId: Guid.NewGuid(),
            Query: "How do I set up the game?"
        );

        var orchestrationResponse = new
        {
            agent_type = "tutor",
            response = "To set up the game, follow these steps...",
            confidence = 0.95,
            citations = new List<string> { "setup_guide.pdf:p1" },
            execution_time_ms = 250.0,
            error = (string?)null
        };

        var responseJson = JsonSerializer.Serialize(orchestrationResponse);
        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json")
        };

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri!.ToString().Contains("/execute")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        result.AgentType.Should().Be("tutor");
        Assert.Equal("To set up the game, follow these steps...", result.Response);
        result.Confidence.Should().Be(0.95);
        result.Citations.Should().ContainSingle();
        result.Citations[0].Should().Be("setup_guide.pdf:p1");
        result.ExecutionTimeMs.Should().Be(250.0);
    }

    [Fact]
    public async Task Handle_WithOrchestrationServiceUnavailable_ThrowsInvalidOperationException()
    {
        // Arrange
        var command = new TutorQueryCommand(
            GameId: Guid.NewGuid(),
            SessionId: Guid.NewGuid(),
            Query: "Test query"
        );

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Service unavailable"));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, CancellationToken.None));

        exception.Message.Should().Be("Tutor agent service unavailable");
        Assert.IsType<HttpRequestException>(exception.InnerException);
    }

    [Fact]
    public async Task Handle_WithInvalidJsonResponse_ThrowsException()
    {
        // Arrange
        var command = new TutorQueryCommand(
            GameId: Guid.NewGuid(),
            SessionId: Guid.NewGuid(),
            Query: "Test query"
        );

        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("invalid json", System.Text.Encoding.UTF8, "application/json")
        };

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act & Assert - JsonSerializer.Deserialize throws JsonException
        // which is caught and re-thrown by handler's catch block
        await Assert.ThrowsAnyAsync<Exception>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WithHttpErrorStatus_ThrowsInvalidOperationException()
    {
        // Arrange
        var command = new TutorQueryCommand(
            GameId: Guid.NewGuid(),
            SessionId: Guid.NewGuid(),
            Query: "Test query"
        );

        var httpResponse = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("Internal server error")
        };

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act & Assert - EnsureSuccessStatusCode throws HttpRequestException
        // which is caught and wrapped in InvalidOperationException by handler
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, CancellationToken.None));

        exception.Message.Should().Be("Tutor agent service unavailable");
        Assert.IsType<HttpRequestException>(exception.InnerException);
    }

    [Theory]
    [InlineData(0.95, "High confidence response")]
    [InlineData(0.75, "Medium confidence response")]
    [InlineData(0.55, "Low confidence response")]
    public async Task Handle_WithDifferentConfidenceLevels_ReturnsCorrectConfidence(
        double confidence,
        string response)
    {
        // Arrange
        var command = new TutorQueryCommand(
            GameId: Guid.NewGuid(),
            SessionId: Guid.NewGuid(),
            Query: "Test query"
        );

        var orchestrationResponse = new
        {
            agent_type = "tutor",
            response,
            confidence,
            citations = new List<string>(),
            execution_time_ms = 200.0,
            error = (string?)null
        };

        var responseJson = JsonSerializer.Serialize(orchestrationResponse);
        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json")
        };

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Confidence.Should().Be(confidence);
        result.Response.Should().Be(response);
    }

    [Fact]
    public async Task Handle_WithEmptyCitations_ReturnsEmptyList()
    {
        // Arrange
        var command = new TutorQueryCommand(
            GameId: Guid.NewGuid(),
            SessionId: Guid.NewGuid(),
            Query: "General question"
        );

        var orchestrationResponse = new
        {
            agent_type = "tutor",
            response = "General answer without specific sources",
            confidence = 0.80,
            citations = new List<string>(),
            execution_time_ms = 180.0,
            error = (string?)null
        };

        var responseJson = JsonSerializer.Serialize(orchestrationResponse);
        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json")
        };

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Empty(result.Citations);
    }

    [Fact]
    public async Task Handle_WithMultipleCitations_ReturnsAllCitations()
    {
        // Arrange
        var command = new TutorQueryCommand(
            GameId: Guid.NewGuid(),
            SessionId: Guid.NewGuid(),
            Query: "Detailed rules question"
        );

        var citations = new List<string>
        {
            "rulebook.pdf:p12",
            "faq.pdf:p3",
            "errata.pdf:p1"
        };

        var orchestrationResponse = new
        {
            agent_type = "tutor",
            response = "According to the rules...",
            confidence = 0.92,
            citations,
            execution_time_ms = 320.0,
            error = (string?)null
        };

        var responseJson = JsonSerializer.Serialize(orchestrationResponse);
        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json")
        };

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Citations.Count.Should().Be(3);
        result.Citations.Should().Contain("rulebook.pdf:p12");
        result.Citations.Should().Contain("faq.pdf:p3");
        result.Citations.Should().Contain("errata.pdf:p1");
    }

    [Fact]
    public async Task Handle_LogsInformationMessages()
    {
        // Arrange
        var command = new TutorQueryCommand(
            GameId: Guid.NewGuid(),
            SessionId: Guid.NewGuid(),
            Query: "Test logging"
        );

        var orchestrationResponse = new
        {
            agent_type = "tutor",
            response = "Test response",
            confidence = 0.85,
            citations = new List<string>(),
            execution_time_ms = 200.0,
            error = (string?)null
        };

        var responseJson = JsonSerializer.Serialize(orchestrationResponse);
        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json")
        };

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert - Verify logging occurred (check mock was invoked)
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Executing tutor query")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Tutor query completed")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
    private static IRagAccessService CreatePermissiveRagAccessServiceMock()
    {
        var mock = new Mock<IRagAccessService>();
        mock.Setup(s => s.CanAccessRagAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<UserRole>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        return mock.Object;
    }
}
