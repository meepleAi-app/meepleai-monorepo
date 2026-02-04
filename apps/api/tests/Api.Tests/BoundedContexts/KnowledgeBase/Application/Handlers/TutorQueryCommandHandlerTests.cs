using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Handlers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text.Json;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// ISSUE-3499: Unit tests for TutorQueryCommandHandler.
/// Tests CQRS handler calls orchestration service correctly.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "3499")]
public class TutorQueryCommandHandlerTests
{
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly Mock<ILogger<TutorQueryCommandHandler>> _mockLogger;
    private readonly TutorQueryCommandHandler _handler;

    public TutorQueryCommandHandlerTests()
    {
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        _mockLogger = new Mock<ILogger<TutorQueryCommandHandler>>();

        var httpClient = new HttpClient(_mockHttpMessageHandler.Object)
        {
            BaseAddress = new Uri("http://orchestration-service:8004")
        };

        _mockHttpClientFactory
            .Setup(x => x.CreateClient("OrchestrationService"))
            .Returns(httpClient);

        _handler = new TutorQueryCommandHandler(_mockHttpClientFactory.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_ValidCommand_CallsOrchestrationService()
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
            response = "Setup instructions...",
            confidence = 0.92,
            citations = new List<string>(),
            execution_time_ms = 245.0,
            error = (string?)null
        };

        var responseJson = JsonSerializer.Serialize(orchestrationResponse);

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseJson)
            });

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Response.Should().Be("Setup instructions...");
        result.AgentType.Should().Be("tutor");
        result.Confidence.Should().Be(0.92);
        result.ExecutionTimeMs.Should().Be(245.0);
    }

    [Fact]
    public async Task Handle_OrchestrationServiceUnavailable_ThrowsInvalidOperationException()
    {
        // Arrange
        var command = new TutorQueryCommand(
            GameId: Guid.NewGuid(),
            SessionId: Guid.NewGuid(),
            Query: "Test query"
        );

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Service unavailable"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(command, CancellationToken.None));
    }
}
