using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers.RagDashboard;

// Issue #3305: RAG Dashboard Test Suite

/// <summary>
/// Unit tests for GetRagDashboardOptionsQueryHandler.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Feature", "RagDashboard")]
public class GetRagDashboardOptionsQueryHandlerTests
{
    private readonly Mock<ILogger<GetRagDashboardOptionsQueryHandler>> _mockLogger;
    private readonly GetRagDashboardOptionsQueryHandler _handler;

    public GetRagDashboardOptionsQueryHandlerTests()
    {
        _mockLogger = new Mock<ILogger<GetRagDashboardOptionsQueryHandler>>();
        _handler = new GetRagDashboardOptionsQueryHandler(_mockLogger.Object);
    }

    [Fact]
    public async Task Handle_ReturnsNonNullOptions()
    {
        // Arrange
        var query = new GetRagDashboardOptionsQuery();

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.LlmModels.Should().NotBeNull();
        result.RerankerModels.Should().NotBeNull();
        result.Strategies.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_ReturnsAtLeastOneLlmModel()
    {
        // Arrange
        var query = new GetRagDashboardOptionsQuery();

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.LlmModels.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Handle_ReturnsAtLeastOneReranker()
    {
        // Arrange
        var query = new GetRagDashboardOptionsQuery();

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.RerankerModels.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Handle_ReturnsAtLeastOneStrategy()
    {
        // Arrange
        var query = new GetRagDashboardOptionsQuery();

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Strategies.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("gpt-4o-mini")]
    [InlineData("gpt-4o")]
    [InlineData("claude-3-5-sonnet")]
    public async Task Handle_ContainsExpectedLlmModels(string modelId)
    {
        // Arrange
        var query = new GetRagDashboardOptionsQuery();

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.LlmModels.Should().Contain(m => m.Id == modelId);
    }

    [Theory]
    [InlineData("Hybrid")]
    [InlineData("Semantic")]
    [InlineData("Keyword")]
    [InlineData("Contextual")]
    [InlineData("MultiQuery")]
    [InlineData("Agentic")]
    public async Task Handle_ContainsExpectedStrategies(string strategyId)
    {
        // Arrange
        var query = new GetRagDashboardOptionsQuery();

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Strategies.Should().Contain(s => s.Id == strategyId);
    }

    [Fact]
    public async Task Handle_LlmModelsHaveValidProperties()
    {
        // Arrange
        var query = new GetRagDashboardOptionsQuery();

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        foreach (var model in result.LlmModels)
        {
            model.Id.Should().NotBeNull();
            model.Name.Should().NotBeNull();
            model.Provider.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task Handle_StrategiesHaveValidProperties()
    {
        // Arrange
        var query = new GetRagDashboardOptionsQuery();

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        foreach (var strategy in result.Strategies)
        {
            strategy.Id.Should().NotBeNull();
            strategy.Name.Should().NotBeNull();
            strategy.Description.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task Handle_RerankersHaveValidProperties()
    {
        // Arrange
        var query = new GetRagDashboardOptionsQuery();

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        foreach (var reranker in result.RerankerModels)
        {
            reranker.Id.Should().NotBeNull();
            reranker.Name.Should().NotBeNull();
        }
    }
}
