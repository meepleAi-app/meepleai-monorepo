using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Unit tests for GetPublicRagStrategiesQueryHandler.
/// Issue #8: Public RAG strategies endpoint for user/editor wizards.
/// </summary>
public class GetPublicRagStrategiesQueryHandlerTests
{
    private readonly GetPublicRagStrategiesQueryHandler _handler;

    public GetPublicRagStrategiesQueryHandlerTests()
    {
        _handler = new GetPublicRagStrategiesQueryHandler();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_ReturnsAllStrategies()
    {
        // Arrange
        var query = new GetPublicRagStrategiesQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(12); // 12 strategies in RagStrategy enum
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_StrategiesOrderedByComplexity()
    {
        // Arrange
        var query = new GetPublicRagStrategiesQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeInAscendingOrder(s => s.Complexity);
        result.First().Name.Should().Be("Fast"); // Complexity 0
        result.First().Complexity.Should().Be(0);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_IncludesAllRequiredFields()
    {
        // Arrange
        var query = new GetPublicRagStrategiesQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().AllSatisfy(strategy =>
        {
            strategy.Name.Should().NotBeNullOrEmpty();
            strategy.DisplayName.Should().NotBeNullOrEmpty();
            strategy.Description.Should().NotBeNullOrEmpty();
            strategy.Complexity.Should().BeGreaterThanOrEqualTo(0);
            strategy.EstimatedTokens.Should().BeGreaterThanOrEqualTo(0);
            strategy.UseCase.Should().NotBeNullOrEmpty();
        });
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_CustomStrategyRequiresAdmin()
    {
        // Arrange
        var query = new GetPublicRagStrategiesQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        var customStrategy = result.Single(s => s.Name == "Custom");
        customStrategy.RequiresAdmin.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_NonCustomStrategiesDoNotRequireAdmin()
    {
        // Arrange
        var query = new GetPublicRagStrategiesQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        var nonCustomStrategies = result.Where(s => s.Name != "Custom");
        nonCustomStrategies.Should().AllSatisfy(s => s.RequiresAdmin.Should().BeFalse());
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("Fast", "FAST", 1500)]
    [InlineData("Balanced", "BALANCED", 2800)]
    [InlineData("Precise", "PRECISE", 22400)]
    [InlineData("MultiAgent", "MULTI_AGENT", 12900)]
    public async Task Handle_StrategyMappingCorrect(string name, string displayName, int estimatedTokens)
    {
        // Arrange
        var query = new GetPublicRagStrategiesQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        var strategy = result.Single(s => s.Name == name);
        strategy.DisplayName.Should().Be(displayName);
        strategy.EstimatedTokens.Should().Be(estimatedTokens);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_ReturnsConsistentResults()
    {
        // Arrange
        var query = new GetPublicRagStrategiesQuery();

        // Act
        var result1 = await _handler.Handle(query, CancellationToken.None);
        var result2 = await _handler.Handle(query, CancellationToken.None);

        // Assert - Results should be identical (deterministic)
        result1.Should().HaveCount(result2.Count);
        for (int i = 0; i < result1.Count; i++)
        {
            result1[i].Name.Should().Be(result2[i].Name);
            result1[i].Complexity.Should().Be(result2[i].Complexity);
        }
    }
}
