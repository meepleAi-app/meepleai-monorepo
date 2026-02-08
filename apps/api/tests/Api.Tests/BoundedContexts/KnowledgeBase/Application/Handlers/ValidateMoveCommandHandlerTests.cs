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
/// ISSUE-3759: Unit tests for ValidateMoveCommandHandler.
/// Tests CQRS handler calls orchestration service for Arbitro agent.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "3759")]
public class ValidateMoveCommandHandlerTests
{
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly Mock<ILogger<ValidateMoveCommandHandler>> _mockLogger;
    private readonly ValidateMoveCommandHandler _handler;

    public ValidateMoveCommandHandlerTests()
    {
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        _mockLogger = new Mock<ILogger<ValidateMoveCommandHandler>>();

        var httpClient = new HttpClient(_mockHttpMessageHandler.Object)
        {
            BaseAddress = new Uri("http://orchestration-service:8004")
        };

        _mockHttpClientFactory
            .Setup(x => x.CreateClient("OrchestrationService"))
            .Returns(httpClient);

        _handler = new ValidateMoveCommandHandler(_mockHttpClientFactory.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_ValidMove_ReturnsValidResult()
    {
        // Arrange
        var command = new ValidateMoveCommand(
            GameId: Guid.NewGuid(),
            SessionId: Guid.NewGuid(),
            Move: "Nf3",
            GameState: "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1"
        );

        var ruleId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var orchestrationResponse = new
        {
            is_valid = true,
            reason = "Knight moves in L-shape, position is legal",
            applied_rule_ids = new List<Guid> { ruleId },
            confidence = 0.95,
            citations = new List<string> { "chess_rules.pdf#L45" },
            execution_time_ms = 42.3,
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
        result.IsValid.Should().BeTrue();
        result.Reason.Should().Contain("Knight");
        result.Confidence.Should().Be(0.95);
        result.ExecutionTimeMs.Should().Be(42.3);
        result.AppliedRuleIds.Should().ContainSingle();
        result.Citations.Should().ContainSingle();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task Handle_InvalidMove_ReturnsInvalidResult()
    {
        // Arrange
        var command = new ValidateMoveCommand(
            GameId: Guid.NewGuid(),
            SessionId: Guid.NewGuid(),
            Move: "Ke9", // Invalid - king can't move off board
            GameState: "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1"
        );

        var ruleId = Guid.Parse("00000000-0000-0000-0000-000000000003");
        var orchestrationResponse = new
        {
            is_valid = false,
            reason = "Move violates board boundaries",
            applied_rule_ids = new List<Guid> { ruleId },
            confidence = 0.98,
            citations = new List<string>(),
            execution_time_ms = 38.5,
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
        result.IsValid.Should().BeFalse();
        result.Reason.Should().Contain("violates");
        result.Confidence.Should().Be(0.98);
    }

    [Fact]
    public async Task Handle_OrchestrationServiceUnavailable_ThrowsInvalidOperationException()
    {
        // Arrange
        var command = new ValidateMoveCommand(
            GameId: Guid.NewGuid(),
            SessionId: Guid.NewGuid(),
            Move: "e4",
            GameState: "start"
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

    [Fact]
    public async Task Handle_PerformanceTarget_CompletesUnder100ms()
    {
        // Arrange
        var command = new ValidateMoveCommand(
            GameId: Guid.NewGuid(),
            SessionId: Guid.NewGuid(),
            Move: "e4",
            GameState: "start"
        );

        var orchestrationResponse = new
        {
            is_valid = true,
            reason = "Valid pawn move",
            applied_rule_ids = new List<Guid> { Guid.NewGuid() },
            confidence = 0.95,
            citations = new List<string>(),
            execution_time_ms = 85.0, // Under 100ms target
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
        result.ExecutionTimeMs.Should().BeLessThan(100);
    }
}
