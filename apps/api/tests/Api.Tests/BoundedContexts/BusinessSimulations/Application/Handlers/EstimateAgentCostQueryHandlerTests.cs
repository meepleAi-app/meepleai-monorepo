using Api.BoundedContexts.BusinessSimulations.Application.Handlers;
using Api.BoundedContexts.BusinessSimulations.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.BusinessSimulations.Application.Handlers;

/// <summary>
/// Unit tests for EstimateAgentCostQueryHandler (Issue #3725)
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "BusinessSimulations")]
public sealed class EstimateAgentCostQueryHandlerTests
{
    private readonly Mock<ILlmCostCalculator> _costCalculatorMock;
    private readonly Mock<ILogger<EstimateAgentCostQueryHandler>> _loggerMock;
    private readonly EstimateAgentCostQueryHandler _handler;

    public EstimateAgentCostQueryHandlerTests()
    {
        _costCalculatorMock = new Mock<ILlmCostCalculator>();
        _loggerMock = new Mock<ILogger<EstimateAgentCostQueryHandler>>();
        _handler = new EstimateAgentCostQueryHandler(
            _costCalculatorMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithKnownModel_ShouldReturnAccurateCostEstimate()
    {
        // Arrange
        _costCalculatorMock
            .Setup(c => c.GetModelPricing("deepseek/deepseek-chat"))
            .Returns(new LlmModelPricing
            {
                ModelId = "deepseek/deepseek-chat",
                Provider = "OpenRouter",
                InputCostPer1M = 0.27m,
                OutputCostPer1M = 1.10m
            });

        var query = new EstimateAgentCostQuery(
            Strategy: "Balanced",
            ModelId: "deepseek/deepseek-chat",
            MessagesPerDay: 1000,
            ActiveUsers: 10,
            AvgTokensPerRequest: 1000);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Strategy.Should().Be("Balanced");
        result.ModelId.Should().Be("deepseek/deepseek-chat");
        result.Provider.Should().Be("OpenRouter");
        result.InputCostPer1MTokens.Should().Be(0.27m);
        result.OutputCostPer1MTokens.Should().Be(1.10m);
        result.CostPerRequest.Should().BeGreaterThan(0);
        result.TotalDailyRequests.Should().Be(10000);
        result.DailyProjection.Should().BeGreaterThan(0);
        result.MonthlyProjection.Should().Be(result.DailyProjection * 30);
        result.AvgTokensPerRequest.Should().Be(1000);
    }

    [Fact]
    public async Task Handle_WithFreeModel_ShouldReturnZeroCostAndWarning()
    {
        // Arrange
        _costCalculatorMock
            .Setup(c => c.GetModelPricing("meta-llama/llama-3.3-70b-instruct:free"))
            .Returns(new LlmModelPricing
            {
                ModelId = "meta-llama/llama-3.3-70b-instruct:free",
                Provider = "OpenRouter",
                InputCostPer1M = 0m,
                OutputCostPer1M = 0m
            });

        var query = new EstimateAgentCostQuery(
            Strategy: "Fast",
            ModelId: "meta-llama/llama-3.3-70b-instruct:free",
            MessagesPerDay: 5000,
            ActiveUsers: 100,
            AvgTokensPerRequest: 800);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.CostPerRequest.Should().Be(0);
        result.DailyProjection.Should().Be(0);
        result.MonthlyProjection.Should().Be(0);
        result.Warnings.Should().Contain(w => w.Contains("free tier"));
    }

    [Fact]
    public async Task Handle_WithUnknownModel_ShouldFallbackToStrategyEstimate()
    {
        // Arrange - no model pricing found
        _costCalculatorMock
            .Setup(c => c.GetModelPricing(It.IsAny<string>()))
            .Returns((LlmModelPricing?)null);

        var query = new EstimateAgentCostQuery(
            Strategy: "Balanced",
            ModelId: "unknown-model/v1",
            MessagesPerDay: 100,
            ActiveUsers: 10,
            AvgTokensPerRequest: 1000);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.CostPerRequest.Should().BeGreaterThan(0); // Strategy has cost 0.01/1K tokens
        result.Warnings.Should().Contain(w => w.Contains("not found in pricing database"));
    }

    [Fact]
    public async Task Handle_WithHighMonthlyCost_ShouldAddCostWarning()
    {
        // Arrange
        _costCalculatorMock
            .Setup(c => c.GetModelPricing("deepseek/deepseek-chat"))
            .Returns(new LlmModelPricing
            {
                ModelId = "deepseek/deepseek-chat",
                Provider = "OpenRouter",
                InputCostPer1M = 0.27m,
                OutputCostPer1M = 1.10m
            });

        var query = new EstimateAgentCostQuery(
            Strategy: "Balanced",
            ModelId: "deepseek/deepseek-chat",
            MessagesPerDay: 50000,
            ActiveUsers: 1000,
            AvgTokensPerRequest: 2000);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.MonthlyProjection.Should().BeGreaterThan(1000);
        result.Warnings.Should().Contain(w => w.Contains("Monthly projection exceeds"));
    }

    [Fact]
    public async Task Handle_WithMultiAgentStrategy_ShouldAddMultiAgentWarning()
    {
        // Arrange
        _costCalculatorMock
            .Setup(c => c.GetModelPricing(It.IsAny<string>()))
            .Returns((LlmModelPricing?)null);

        var query = new EstimateAgentCostQuery(
            Strategy: "MultiAgent",
            ModelId: "anthropic/claude-sonnet-4.5",
            MessagesPerDay: 100,
            ActiveUsers: 10,
            AvgTokensPerRequest: 1000);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Warnings.Should().Contain(w => w.Contains("MultiAgent"));
    }

    [Fact]
    public async Task Handle_WithInvalidStrategy_ShouldReturnFallbackResult()
    {
        // Arrange
        var query = new EstimateAgentCostQuery(
            Strategy: "NonExistent",
            ModelId: "some-model",
            MessagesPerDay: 100,
            ActiveUsers: 10,
            AvgTokensPerRequest: 1000);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.CostPerRequest.Should().Be(0);
        result.Provider.Should().Be("Unknown");
        result.Warnings.Should().Contain(w => w.Contains("Unknown strategy"));
    }

    [Fact]
    public async Task Handle_WithZeroMessagesPerDay_ShouldReturnZeroDailyCost()
    {
        // Arrange
        _costCalculatorMock
            .Setup(c => c.GetModelPricing("deepseek/deepseek-chat"))
            .Returns(new LlmModelPricing
            {
                ModelId = "deepseek/deepseek-chat",
                Provider = "OpenRouter",
                InputCostPer1M = 0.27m,
                OutputCostPer1M = 1.10m
            });

        var query = new EstimateAgentCostQuery(
            Strategy: "Balanced",
            ModelId: "deepseek/deepseek-chat",
            MessagesPerDay: 0,
            ActiveUsers: 100,
            AvgTokensPerRequest: 1000);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.CostPerRequest.Should().BeGreaterThan(0);
        result.DailyProjection.Should().Be(0);
        result.MonthlyProjection.Should().Be(0);
        result.TotalDailyRequests.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithHighRequestVolume_ShouldAddVolumeWarning()
    {
        // Arrange
        _costCalculatorMock
            .Setup(c => c.GetModelPricing(It.IsAny<string>()))
            .Returns(new LlmModelPricing
            {
                ModelId = "deepseek/deepseek-chat",
                Provider = "OpenRouter",
                InputCostPer1M = 0.27m,
                OutputCostPer1M = 1.10m
            });

        var query = new EstimateAgentCostQuery(
            Strategy: "Balanced",
            ModelId: "deepseek/deepseek-chat",
            MessagesPerDay: 10000,
            ActiveUsers: 100,
            AvgTokensPerRequest: 1000);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.TotalDailyRequests.Should().Be(1_000_000);
        result.Warnings.Should().Contain(w => w.Contains("High request volume"));
    }

    [Fact]
    public async Task Handle_WithCustomStrategy_ShouldAddCustomWarning()
    {
        // Arrange - Custom has null EstimatedCostPer1KTokens
        _costCalculatorMock
            .Setup(c => c.GetModelPricing(It.IsAny<string>()))
            .Returns((LlmModelPricing?)null);

        var query = new EstimateAgentCostQuery(
            Strategy: "Custom",
            ModelId: "anthropic/claude-haiku-4.5",
            MessagesPerDay: 100,
            ActiveUsers: 10,
            AvgTokensPerRequest: 1000);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Warnings.Should().Contain(w => w.Contains("variable cost"));
    }
}
