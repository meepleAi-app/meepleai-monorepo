using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

[Trait("Category", TestCategories.Unit)]
public class AgentStrategyTests
{
    [Fact]
    public void HybridSearch_WithDefaultParameters_CreatesStrategy()
    {
        // Act
        var strategy = AgentStrategy.HybridSearch();

        // Assert
        strategy.Name.Should().Be("HybridSearch");
        strategy.GetParameter<double>("VectorWeight").Should().BeApproximately(0.7, 0.0001);
        strategy.GetParameter<double>("KeywordWeight").Should().BeApproximately(0.3, 0.0001);
        strategy.GetParameter<int>("TopK").Should().Be(10);
        strategy.GetParameter<double>("MinScore").Should().BeApproximately(0.55, 0.0001);
    }

    [Fact]
    public void HybridSearch_WithCustomParameters_CreatesStrategy()
    {
        // Act
        var strategy = AgentStrategy.HybridSearch(vectorWeight: 0.8, topK: 15, minScore: 0.65);

        // Assert
        strategy.Name.Should().Be("HybridSearch");
        strategy.GetParameter<double>("VectorWeight").Should().BeApproximately(0.8, 0.0001);
        strategy.GetParameter<double>("KeywordWeight").Should().BeApproximately(0.2, 0.0001);
        strategy.GetParameter<int>("TopK").Should().Be(15);
        strategy.GetParameter<double>("MinScore").Should().BeApproximately(0.65, 0.0001);
    }

    [Fact]
    public void VectorOnly_WithDefaultParameters_CreatesStrategy()
    {
        // Act
        var strategy = AgentStrategy.VectorOnly();

        // Assert
        strategy.Name.Should().Be("VectorOnly");
        strategy.GetParameter<int>("TopK").Should().Be(10);
        strategy.GetParameter<double>("MinScore").Should().Be(0.80);
    }

    [Fact]
    public void VectorOnly_WithCustomParameters_CreatesStrategy()
    {
        // Act
        var strategy = AgentStrategy.VectorOnly(topK: 20, minScore: 0.90);

        // Assert
        strategy.Name.Should().Be("VectorOnly");
        strategy.GetParameter<int>("TopK").Should().Be(20);
        strategy.GetParameter<double>("MinScore").Should().Be(0.90);
    }

    [Fact]
    public void MultiModelConsensus_WithDefaultParameters_CreatesStrategy()
    {
        // Act
        var strategy = AgentStrategy.MultiModelConsensus();

        // Assert
        strategy.Name.Should().Be("MultiModelConsensus");
        strategy.GetParameter<string[]>("Models").Should().ContainInOrder("gpt-4", "claude-3-opus");
        strategy.GetParameter<double>("ConsensusThreshold").Should().Be(0.8);
    }

    [Fact]
    public void MultiModelConsensus_WithCustomModels_CreatesStrategy()
    {
        // Arrange
        var customModels = new[] { "gpt-3.5-turbo", "claude-2" };

        // Act
        var strategy = AgentStrategy.MultiModelConsensus(models: customModels, consensusThreshold: 0.9);

        // Assert
        strategy.Name.Should().Be("MultiModelConsensus");
        strategy.GetParameter<string[]>("Models").Should().BeEquivalentTo(customModels);
        strategy.GetParameter<double>("ConsensusThreshold").Should().Be(0.9);
    }

    [Fact]
    public void CitationValidation_WithDefaultParameters_CreatesStrategy()
    {
        // Act
        var strategy = AgentStrategy.CitationValidation();

        // Assert
        strategy.Name.Should().Be("CitationValidation");
        strategy.GetParameter<bool>("RequireExactMatch").Should().BeTrue();
        strategy.GetParameter<int>("MaxDistanceWords").Should().Be(50);
    }

    [Fact]
    public void CitationValidation_WithCustomParameters_CreatesStrategy()
    {
        // Act
        var strategy = AgentStrategy.CitationValidation(requireExactMatch: false, maxDistanceWords: 100);

        // Assert
        strategy.Name.Should().Be("CitationValidation");
        strategy.GetParameter<bool>("RequireExactMatch").Should().BeFalse();
        strategy.GetParameter<int>("MaxDistanceWords").Should().Be(100);
    }

    [Fact]
    public void ConfidenceScoring_WithDefaultParameters_CreatesStrategy()
    {
        // Act
        var strategy = AgentStrategy.ConfidenceScoring();

        // Assert
        strategy.Name.Should().Be("ConfidenceScoring");
        strategy.GetParameter<double>("MinConfidence").Should().Be(0.70);
        strategy.GetParameter<bool>("EnableMultiLayer").Should().BeTrue();
        strategy.GetParameter<string[]>("Layers").Should().HaveCount(5);
        strategy.GetParameter<string[]>("Layers").Should().Contain("RetrievalScore");
    }

    [Fact]
    public void Custom_WithValidParameters_CreatesStrategy()
    {
        // Arrange
        var parameters = new Dictionary<string, object>
        {
            ["Param1"] = 42,
            ["Param2"] = "value"
        };

        // Act
        var strategy = AgentStrategy.Custom("CustomStrategy", parameters);

        // Assert
        strategy.Name.Should().Be("CustomStrategy");
        strategy.GetParameter<int>("Param1").Should().Be(42);
        strategy.GetParameter<string>("Param2").Should().Be("value");
    }

    [Fact]
    public void GetParameter_WithIntType_ReturnsInt()
    {
        // Arrange
        var strategy = AgentStrategy.HybridSearch(topK: 15);

        // Act
        var topK = strategy.GetParameter<int>("TopK");

        // Assert
        topK.Should().Be(15);
    }

    [Fact]
    public void GetParameter_WithDoubleType_ReturnsDouble()
    {
        // Arrange
        var strategy = AgentStrategy.VectorOnly(minScore: 0.85);

        // Act
        var minScore = strategy.GetParameter<double>("MinScore");

        // Assert
        minScore.Should().Be(0.85);
    }

    [Fact]
    public void GetParameter_WithStringType_ReturnsString()
    {
        // Arrange
        var parameters = new Dictionary<string, object> { ["Key"] = "TestValue" };
        var strategy = AgentStrategy.Custom("Test", parameters);

        // Act
        var value = strategy.GetParameter<string>("Key");

        // Assert
        value.Should().Be("TestValue");
    }

    [Fact]
    public void GetParameter_WithBoolType_ReturnsBool()
    {
        // Arrange
        var strategy = AgentStrategy.CitationValidation(requireExactMatch: true);

        // Act
        var exactMatch = strategy.GetParameter<bool>("RequireExactMatch");

        // Assert
        exactMatch.Should().BeTrue();
    }

    [Theory]
    [InlineData("NonExistentKey", 0)]
    [InlineData("MissingKey", -1)]
    public void GetParameter_WithMissingKey_ReturnsDefaultValue(string key, int defaultValue)
    {
        // Arrange
        var strategy = AgentStrategy.VectorOnly();

        // Act
        var value = strategy.GetParameter(key, defaultValue);

        // Assert
        value.Should().Be(defaultValue);
    }

    [Fact]
    public void GetParameter_WithUnsupportedTypeConversion_ReturnsDefault()
    {
        // Arrange
        var parameters = new Dictionary<string, object> { ["Key"] = "not-a-guid" };
        var strategy = AgentStrategy.Custom("Test", parameters);

        // Act
        var value = strategy.GetParameter<Guid>("Key");

        // Assert
        value.Should().Be(Guid.Empty);
    }

    [Theory]
    [InlineData("TopK", true)]
    [InlineData("MinScore", true)]
    [InlineData("NonExistentKey", false)]
    public void HasParameter_ChecksParameterExistence(string key, bool expectedExists)
    {
        // Arrange
        var strategy = AgentStrategy.VectorOnly();

        // Act
        var exists = strategy.HasParameter(key);

        // Assert
        exists.Should().Be(expectedExists);
    }

    [Fact]
    public void ToString_ReturnsFormattedString()
    {
        // Arrange
        var strategy = AgentStrategy.VectorOnly();

        // Act
        var result = strategy.ToString();

        // Assert
        result.Should().Be("VectorOnly (2 parameters)");
    }

    [Fact]
    public void Custom_WithNullName_ThrowsArgumentException()
    {
        // Arrange
        var parameters = new Dictionary<string, object>();

        // Act
        var act = () => AgentStrategy.Custom(null!, parameters);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Strategy name cannot be empty*");
    }

    [Fact]
    public void Custom_WithEmptyName_ThrowsArgumentException()
    {
        // Arrange
        var parameters = new Dictionary<string, object>();

        // Act
        var act = () => AgentStrategy.Custom(string.Empty, parameters);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Strategy name cannot be empty*");
    }
}
