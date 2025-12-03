using Api.BoundedContexts.KnowledgeBase.Domain.Chunking;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Chunking;

/// <summary>
/// Unit tests for ChunkingConfiguration value object.
/// </summary>
public class ChunkingConfigurationTests
{
    [Fact]
    public void Baseline_ReturnsCorrectConfiguration()
    {
        // Act
        var config = ChunkingConfiguration.Baseline;

        // Assert
        config.Name.Should().Be("baseline");
        config.ChunkSizeTokens.Should().Be(350);
        config.OverlapPercentage.Should().Be(0.15);
    }

    [Fact]
    public void Dense_ReturnsCorrectConfiguration()
    {
        // Act
        var config = ChunkingConfiguration.Dense;

        // Assert
        config.Name.Should().Be("dense");
        config.ChunkSizeTokens.Should().Be(200);
        config.OverlapPercentage.Should().Be(0.20);
    }

    [Fact]
    public void Sparse_ReturnsCorrectConfiguration()
    {
        // Act
        var config = ChunkingConfiguration.Sparse;

        // Assert
        config.Name.Should().Be("sparse");
        config.ChunkSizeTokens.Should().Be(500);
        config.OverlapPercentage.Should().Be(0.10);
    }

    [Fact]
    public void OverlapTokens_CalculatesCorrectly()
    {
        // Arrange
        var config = ChunkingConfiguration.Baseline;

        // Act
        var overlap = config.OverlapTokens;

        // Assert
        // 350 * 0.15 = 52.5 -> 52 (int truncation)
        overlap.Should().Be(52);
    }

    [Fact]
    public void ChunkSizeChars_CalculatesCorrectly()
    {
        // Arrange
        var config = ChunkingConfiguration.Baseline;

        // Act
        var charsSize = config.ChunkSizeChars;

        // Assert
        // 350 * 4.0 = 1400
        charsSize.Should().Be(1400);
    }

    [Fact]
    public void OverlapChars_CalculatesCorrectly()
    {
        // Arrange
        var config = ChunkingConfiguration.Baseline;

        // Act
        var overlapChars = config.OverlapChars;

        // Assert
        // 52 * 4.0 = 208
        overlapChars.Should().Be(208);
    }

    [Fact]
    public void Validate_WithValidConfig_ReturnsTrue()
    {
        // Arrange
        var config = ChunkingConfiguration.Baseline;

        // Act
        var result = config.Validate();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithZeroChunkSize_ThrowsArgumentException()
    {
        // Arrange
        var config = new ChunkingConfiguration { ChunkSizeTokens = 0 };

        // Act
        var act = () => config.Validate();

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*ChunkSizeTokens must be positive*");
    }

    [Fact]
    public void Validate_WithNegativeChunkSize_ThrowsArgumentException()
    {
        // Arrange
        var config = new ChunkingConfiguration { ChunkSizeTokens = -10 };

        // Act
        var act = () => config.Validate();

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*ChunkSizeTokens must be positive*");
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(0.6)]
    [InlineData(1.0)]
    public void Validate_WithInvalidOverlap_ThrowsArgumentException(double invalidOverlap)
    {
        // Arrange
        var config = new ChunkingConfiguration
        {
            ChunkSizeTokens = 350,
            OverlapPercentage = invalidOverlap
        };

        // Act
        var act = () => config.Validate();

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*OverlapPercentage must be between 0 and 0.5*");
    }

    [Fact]
    public void Validate_WithZeroCharsPerToken_ThrowsArgumentException()
    {
        // Arrange
        var config = new ChunkingConfiguration
        {
            ChunkSizeTokens = 350,
            CharsPerToken = 0
        };

        // Act
        var act = () => config.Validate();

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*CharsPerToken must be positive*");
    }

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var config = new ChunkingConfiguration();

        // Assert
        config.Name.Should().Be("baseline");
        config.ChunkSizeTokens.Should().Be(350);
        config.OverlapPercentage.Should().Be(0.15);
        config.CharsPerToken.Should().Be(4.0);
        config.RespectSentenceBoundaries.Should().BeTrue();
        config.RespectParagraphBoundaries.Should().BeTrue();
    }

    [Fact]
    public void RecordEquality_WorksCorrectly()
    {
        // Arrange
        var config1 = ChunkingConfiguration.Baseline;
        var config2 = ChunkingConfiguration.Baseline;

        // Assert
        config1.Should().Be(config2);
        (config1 == config2).Should().BeTrue();
    }
}
