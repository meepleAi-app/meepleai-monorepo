using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain;

/// <summary>
/// Unit tests for AgentDefinitionConfig value object (Issue #3808)
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class AgentDefinitionConfigTests
{
    [Fact]
    public void Create_WithValidData_ShouldCreateConfig()
    {
        // Act
        var config = AgentDefinitionConfig.Create("gpt-4", 2048, 0.7f);

        // Assert
        config.Should().NotBeNull();
        config.Model.Should().Be("gpt-4");
        config.MaxTokens.Should().Be(2048);
        config.Temperature.Should().Be(0.7f);
    }

    [Theory]
    [InlineData("", 2048, 0.7f)]
    [InlineData(null, 2048, 0.7f)]
    [InlineData("   ", 2048, 0.7f)]
    public void Create_WithInvalidModel_ShouldThrowArgumentException(string model, int maxTokens, float temperature)
    {
        // Act
        var act = () => AgentDefinitionConfig.Create(model!, maxTokens, temperature);

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("model");
    }

    [Theory]
    [InlineData(99)]
    [InlineData(32001)]
    public void Create_WithInvalidMaxTokens_ShouldThrowArgumentException(int maxTokens)
    {
        // Act
        var act = () => AgentDefinitionConfig.Create("gpt-4", maxTokens, 0.7f);

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("maxTokens");
    }

    [Theory]
    [InlineData(-0.1f)]
    [InlineData(2.1f)]
    public void Create_WithInvalidTemperature_ShouldThrowArgumentException(float temperature)
    {
        // Act
        var act = () => AgentDefinitionConfig.Create("gpt-4", 2048, temperature);

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("temperature");
    }

    [Fact]
    public void Default_ShouldCreateValidConfig()
    {
        // Act
        var config = AgentDefinitionConfig.Default();

        // Assert
        config.Should().NotBeNull();
        config.Model.Should().Be("gpt-4");
        config.MaxTokens.Should().Be(2048);
        config.Temperature.Should().Be(0.7f);
    }
}
