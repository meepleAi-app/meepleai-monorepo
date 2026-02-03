using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Domain.ValueObjects;

/// <summary>
/// Tests for the AgentConfiguration value object.
/// Issue #3025: Backend 90% Coverage Target - Phase 20
/// </summary>
[Trait("Category", "Unit")]
public sealed class AgentConfigurationTests
{
    #region Create Factory Tests

    [Fact]
    public void Create_WithValidParameters_CreatesConfiguration()
    {
        // Act
        var config = AgentConfiguration.Create(
            llmModel: "google/gemini-pro",
            temperature: 0.7,
            maxTokens: 4096,
            personality: "Amichevole",
            detailLevel: "Normale",
            personalNotes: "Test notes");

        // Assert
        config.LlmModel.Should().Be("google/gemini-pro");
        config.Temperature.Should().Be(0.7);
        config.MaxTokens.Should().Be(4096);
        config.Personality.Should().Be("Amichevole");
        config.DetailLevel.Should().Be("Normale");
        config.PersonalNotes.Should().Be("Test notes");
    }

    [Fact]
    public void Create_WithoutPersonalNotes_CreatesConfigWithNullNotes()
    {
        // Act
        var config = AgentConfiguration.Create(
            llmModel: "google/gemini-pro",
            temperature: 0.7,
            maxTokens: 4096,
            personality: "Amichevole",
            detailLevel: "Normale");

        // Assert
        config.PersonalNotes.Should().BeNull();
    }

    [Fact]
    public void Create_WithEmptyLlmModel_ThrowsArgumentException()
    {
        // Act
        var action = () => AgentConfiguration.Create(
            llmModel: "",
            temperature: 0.7,
            maxTokens: 4096,
            personality: "Amichevole",
            detailLevel: "Normale");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("llmModel")
            .WithMessage("*LLM model cannot be empty*");
    }

    [Fact]
    public void Create_WithWhitespaceLlmModel_ThrowsArgumentException()
    {
        // Act
        var action = () => AgentConfiguration.Create(
            llmModel: "   ",
            temperature: 0.7,
            maxTokens: 4096,
            personality: "Amichevole",
            detailLevel: "Normale");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*LLM model cannot be empty*");
    }

    [Fact]
    public void Create_WithTemperatureBelowZero_ThrowsArgumentOutOfRangeException()
    {
        // Act
        var action = () => AgentConfiguration.Create(
            llmModel: "google/gemini-pro",
            temperature: -0.1,
            maxTokens: 4096,
            personality: "Amichevole",
            detailLevel: "Normale");

        // Assert
        action.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("temperature")
            .WithMessage("*Temperature must be between 0 and 2*");
    }

    [Fact]
    public void Create_WithTemperatureAboveTwo_ThrowsArgumentOutOfRangeException()
    {
        // Act
        var action = () => AgentConfiguration.Create(
            llmModel: "google/gemini-pro",
            temperature: 2.1,
            maxTokens: 4096,
            personality: "Amichevole",
            detailLevel: "Normale");

        // Assert
        action.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*Temperature must be between 0 and 2*");
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(1.0)]
    [InlineData(2.0)]
    public void Create_WithBoundaryTemperatures_Succeeds(double temperature)
    {
        // Act
        var config = AgentConfiguration.Create(
            llmModel: "google/gemini-pro",
            temperature: temperature,
            maxTokens: 4096,
            personality: "Amichevole",
            detailLevel: "Normale");

        // Assert
        config.Temperature.Should().Be(temperature);
    }

    [Fact]
    public void Create_WithMaxTokensBelow100_ThrowsArgumentOutOfRangeException()
    {
        // Act
        var action = () => AgentConfiguration.Create(
            llmModel: "google/gemini-pro",
            temperature: 0.7,
            maxTokens: 99,
            personality: "Amichevole",
            detailLevel: "Normale");

        // Assert
        action.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("maxTokens")
            .WithMessage("*MaxTokens must be between 100 and 32000*");
    }

    [Fact]
    public void Create_WithMaxTokensAbove32000_ThrowsArgumentOutOfRangeException()
    {
        // Act
        var action = () => AgentConfiguration.Create(
            llmModel: "google/gemini-pro",
            temperature: 0.7,
            maxTokens: 32001,
            personality: "Amichevole",
            detailLevel: "Normale");

        // Assert
        action.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*MaxTokens must be between 100 and 32000*");
    }

    [Theory]
    [InlineData(100)]
    [InlineData(4096)]
    [InlineData(32000)]
    public void Create_WithBoundaryMaxTokens_Succeeds(int maxTokens)
    {
        // Act
        var config = AgentConfiguration.Create(
            llmModel: "google/gemini-pro",
            temperature: 0.7,
            maxTokens: maxTokens,
            personality: "Amichevole",
            detailLevel: "Normale");

        // Assert
        config.MaxTokens.Should().Be(maxTokens);
    }

    [Fact]
    public void Create_WithEmptyPersonality_ThrowsArgumentException()
    {
        // Act
        var action = () => AgentConfiguration.Create(
            llmModel: "google/gemini-pro",
            temperature: 0.7,
            maxTokens: 4096,
            personality: "",
            detailLevel: "Normale");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("personality")
            .WithMessage("*Personality cannot be empty*");
    }

    [Fact]
    public void Create_WithEmptyDetailLevel_ThrowsArgumentException()
    {
        // Act
        var action = () => AgentConfiguration.Create(
            llmModel: "google/gemini-pro",
            temperature: 0.7,
            maxTokens: 4096,
            personality: "Amichevole",
            detailLevel: "");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("detailLevel")
            .WithMessage("*DetailLevel cannot be empty*");
    }

    [Fact]
    public void Create_TrimsAllStringFields()
    {
        // Act
        var config = AgentConfiguration.Create(
            llmModel: "  google/gemini-pro  ",
            temperature: 0.7,
            maxTokens: 4096,
            personality: "  Amichevole  ",
            detailLevel: "  Normale  ",
            personalNotes: "  Test notes  ");

        // Assert
        config.LlmModel.Should().Be("google/gemini-pro");
        config.Personality.Should().Be("Amichevole");
        config.DetailLevel.Should().Be("Normale");
        config.PersonalNotes.Should().Be("Test notes");
    }

    #endregion

    #region CreateDefault Tests

    [Fact]
    public void CreateDefault_ReturnsConfigWithDefaultValues()
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

    #endregion

    #region Equality Tests

    [Fact]
    public void Equals_WithSameValues_ReturnsTrue()
    {
        // Arrange
        var config1 = AgentConfiguration.Create(
            "google/gemini-pro", 0.7, 4096, "Amichevole", "Normale", "Notes");
        var config2 = AgentConfiguration.Create(
            "google/gemini-pro", 0.7, 4096, "Amichevole", "Normale", "Notes");

        // Assert
        config1.Should().Be(config2);
    }

    [Fact]
    public void Equals_WithDifferentLlmModel_ReturnsFalse()
    {
        // Arrange
        var config1 = AgentConfiguration.Create(
            "google/gemini-pro", 0.7, 4096, "Amichevole", "Normale");
        var config2 = AgentConfiguration.Create(
            "anthropic/claude-3", 0.7, 4096, "Amichevole", "Normale");

        // Assert
        config1.Should().NotBe(config2);
    }

    [Fact]
    public void Equals_WithDifferentTemperature_ReturnsFalse()
    {
        // Arrange
        var config1 = AgentConfiguration.Create(
            "google/gemini-pro", 0.7, 4096, "Amichevole", "Normale");
        var config2 = AgentConfiguration.Create(
            "google/gemini-pro", 0.8, 4096, "Amichevole", "Normale");

        // Assert
        config1.Should().NotBe(config2);
    }

    [Fact]
    public void GetHashCode_WithSameValues_ReturnsSameHash()
    {
        // Arrange
        var config1 = AgentConfiguration.Create(
            "google/gemini-pro", 0.7, 4096, "Amichevole", "Normale");
        var config2 = AgentConfiguration.Create(
            "google/gemini-pro", 0.7, 4096, "Amichevole", "Normale");

        // Assert
        config1.GetHashCode().Should().Be(config2.GetHashCode());
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_ReturnsFormattedString()
    {
        // Arrange
        var config = AgentConfiguration.Create(
            "google/gemini-pro", 0.7, 4096, "Amichevole", "Normale");

        // Act
        var result = config.ToString();

        // Assert
        result.Should().Contain("Model=google/gemini-pro");
        result.Should().Contain("Temp=0");
        result.Should().Contain("Tokens=4096");
        result.Should().Contain("Personality=Amichevole");
    }

    #endregion
}
