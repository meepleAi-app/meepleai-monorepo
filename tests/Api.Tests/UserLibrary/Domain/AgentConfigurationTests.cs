using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.UserLibrary.Domain;

/// <summary>
/// Unit tests for AgentConfiguration value object.
/// </summary>
public class AgentConfigurationTests
{
    [Fact]
    public void Create_WithValidParameters_ShouldCreateAgentConfiguration()
    {
        // Act
        var config = AgentConfiguration.Create(
            llmModel: "google/gemini-pro",
            temperature: 0.7,
            maxTokens: 4096,
            personality: "Amichevole",
            detailLevel: "Normale",
            personalNotes: "Preferisco risposte brevi"
        );

        // Assert
        config.Should().NotBeNull();
        config.LlmModel.Should().Be("google/gemini-pro");
        config.Temperature.Should().Be(0.7);
        config.MaxTokens.Should().Be(4096);
        config.Personality.Should().Be("Amichevole");
        config.DetailLevel.Should().Be("Normale");
        config.PersonalNotes.Should().Be("Preferisco risposte brevi");
    }

    [Fact]
    public void CreateDefault_ShouldCreateDefaultConfiguration()
    {
        // Act
        var config = AgentConfiguration.CreateDefault();

        // Assert
        config.LlmModel.Should().Be("google/gemini-pro");
        config.Temperature.Should().Be(0.7);
        config.MaxTokens.Should().Be(4096);
        config.Personality.Should().Be("Amichevole");
        config.DetailLevel.Should().Be("Normale");
        config.PersonalNotes.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Create_WithInvalidLlmModel_ShouldThrowArgumentException(string? llmModel)
    {
        // Act & Assert
        var act = () => AgentConfiguration.Create(
            llmModel: llmModel!,
            temperature: 0.7,
            maxTokens: 4096,
            personality: "Amichevole",
            detailLevel: "Normale"
        );

        act.Should().Throw<ArgumentException>()
            .WithMessage("*LLM model cannot be empty*");
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(2.1)]
    [InlineData(100.0)]
    public void Create_WithInvalidTemperature_ShouldThrowArgumentOutOfRangeException(double temperature)
    {
        // Act & Assert
        var act = () => AgentConfiguration.Create(
            llmModel: "google/gemini-pro",
            temperature: temperature,
            maxTokens: 4096,
            personality: "Amichevole",
            detailLevel: "Normale"
        );

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*Temperature must be between 0 and 2*");
    }

    [Theory]
    [InlineData(50)]
    [InlineData(40000)]
    public void Create_WithInvalidMaxTokens_ShouldThrowArgumentOutOfRangeException(int maxTokens)
    {
        // Act & Assert
        var act = () => AgentConfiguration.Create(
            llmModel: "google/gemini-pro",
            temperature: 0.7,
            maxTokens: maxTokens,
            personality: "Amichevole",
            detailLevel: "Normale"
        );

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*MaxTokens must be between 100 and 32000*");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Create_WithInvalidPersonality_ShouldThrowArgumentException(string? personality)
    {
        // Act & Assert
        var act = () => AgentConfiguration.Create(
            llmModel: "google/gemini-pro",
            temperature: 0.7,
            maxTokens: 4096,
            personality: personality!,
            detailLevel: "Normale"
        );

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Personality cannot be empty*");
    }

    [Fact]
    public void Equality_WithSameValues_ShouldBeEqual()
    {
        // Arrange
        var config1 = AgentConfiguration.Create("model", 0.7, 4096, "Friendly", "Normal");
        var config2 = AgentConfiguration.Create("model", 0.7, 4096, "Friendly", "Normal");

        // Assert
        config1.Should().Be(config2);
        (config1 == config2).Should().BeTrue();
    }

    [Fact]
    public void Equality_WithDifferentValues_ShouldNotBeEqual()
    {
        // Arrange
        var config1 = AgentConfiguration.Create("model", 0.7, 4096, "Friendly", "Normal");
        var config2 = AgentConfiguration.Create("model", 0.8, 4096, "Friendly", "Normal");

        // Assert
        config1.Should().NotBe(config2);
        (config1 == config2).Should().BeFalse();
    }
}
