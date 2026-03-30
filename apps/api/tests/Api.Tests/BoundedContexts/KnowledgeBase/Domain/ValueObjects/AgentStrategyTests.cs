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
        var strategy = AgentStrategy.HybridSearch(minScore: 0.85);

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
        var parameters = new Dictionary<string, object> { ["RequireExactMatch"] = true };
        var strategy = AgentStrategy.Custom("Test", parameters);

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
        var strategy = AgentStrategy.HybridSearch();

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
        var strategy = AgentStrategy.HybridSearch();

        // Act
        var exists = strategy.HasParameter(key);

        // Assert
        exists.Should().Be(expectedExists);
    }

    [Fact]
    public void ToString_ReturnsFormattedString()
    {
        // Arrange
        var strategy = AgentStrategy.HybridSearch();

        // Act
        var result = strategy.ToString();

        // Assert
        result.Should().Be("HybridSearch (4 parameters)");
    }

    [Fact]
    public void SentenceWindowRAG_WithDefaults_CreatesStrategy()
    {
        var strategy = AgentStrategy.SentenceWindowRAG();

        strategy.Name.Should().Be("SentenceWindowRAG");
        strategy.GetParameter<int>("WindowSize").Should().Be(3);
        strategy.GetParameter<int>("TopK").Should().Be(5);
    }

    [Fact]
    public void ColBERTReranking_WithDefaults_CreatesStrategy()
    {
        var strategy = AgentStrategy.ColBERTReranking();

        strategy.Name.Should().Be("ColBERTReranking");
        strategy.GetParameter<int>("TopK").Should().Be(5);
        strategy.GetParameter<int>("RerankTopN").Should().Be(20);
    }
}
