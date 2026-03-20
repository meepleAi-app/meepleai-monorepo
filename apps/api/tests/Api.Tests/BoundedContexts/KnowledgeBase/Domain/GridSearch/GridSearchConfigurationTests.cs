using Api.BoundedContexts.KnowledgeBase.Domain.GridSearch;
using Xunit;
using FluentAssertions;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.GridSearch;

/// <summary>
/// Unit tests for GridSearchConfiguration.
/// ADR-016 Phase 5: Validates configuration creation and grid search setup.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class GridSearchConfigurationTests
{
    [Fact]
    public void GetAllConfigurations_Returns12Configurations()
    {
        // Act
        var configs = GridSearchConfiguration.GetAllConfigurations();

        // Assert
        // 3 chunking x 2 quantization x 2 reranking = 12
        configs.Count.Should().Be(12);
    }

    [Fact]
    public void GetAllConfigurations_HasUniqueConfigurationIds()
    {
        // Act
        var configs = GridSearchConfiguration.GetAllConfigurations();
        var configIds = configs.Select(c => c.ConfigurationId).ToList();

        // Assert
        configIds.Distinct().Count().Should().Be(configs.Count);
    }

    [Fact]
    public void GetQuickConfigurations_Returns3Configurations()
    {
        // Act
        var configs = GridSearchConfiguration.GetQuickConfigurations();

        // Assert
        configs.Count.Should().Be(3);
    }

    [Fact]
    public void GetQuickConfigurations_ContainsExpectedConfigurations()
    {
        // Act
        var configs = GridSearchConfiguration.GetQuickConfigurations();
        var configIds = configs.Select(c => c.ConfigurationId).ToList();

        // Assert
        configIds.Should().Contain("baseline_none_no_rerank");
        configIds.Should().Contain("baseline_none_bge_rerank");
        configIds.Should().Contain("dense_scalar_int8_bge_rerank");
    }

    [Fact]
    public void GetAllConfigurations_ContainsAllChunkingVariants()
    {
        // Act
        var configs = GridSearchConfiguration.GetAllConfigurations();

        // Assert - Should contain dense, baseline, and sparse chunking
        configs.Any(c => c.Chunking.Name == "dense").Should().BeTrue();
        configs.Any(c => c.Chunking.Name == "baseline").Should().BeTrue();
        configs.Any(c => c.Chunking.Name == "sparse").Should().BeTrue();
    }

    [Fact]
    public void GetAllConfigurations_ContainsAllQuantizationVariants()
    {
        // Act
        var configs = GridSearchConfiguration.GetAllConfigurations();

        // Assert - Should contain none and scalar_int8
        configs.Any(c => c.Quantization.Type == QuantizationType.None).Should().BeTrue();
        configs.Any(c => c.Quantization.Type == QuantizationType.ScalarInt8).Should().BeTrue();
    }

    [Fact]
    public void GetAllConfigurations_ContainsAllRerankingVariants()
    {
        // Act
        var configs = GridSearchConfiguration.GetAllConfigurations();

        // Assert - Should contain no_rerank and bge_rerank
        configs.Any(c => !c.Reranking.Enabled).Should().BeTrue();
        configs.Any(c => c.Reranking.Enabled).Should().BeTrue();
    }
}

/// <summary>
/// Unit tests for ChunkingConfig.
/// </summary>
public class ChunkingConfigTests
{
    [Fact]
    public void GetAll_Returns3Configurations()
    {
        // Act
        var configs = ChunkingConfig.GetAll();

        // Assert
        configs.Count.Should().Be(3);
    }

    [Fact]
    public void DenseConfig_HasCorrectValues()
    {
        // Act
        var configs = ChunkingConfig.GetAll();
        var dense = configs.First(c => c.Name == "dense");

        // Assert
        dense.DisplayName.Should().Be("Dense (200/20%)");
        dense.SizeTokens.Should().Be(200);
        dense.OverlapPercent.Should().Be(0.20);
    }

    [Fact]
    public void BaselineConfig_HasCorrectValues()
    {
        // Act
        var configs = ChunkingConfig.GetAll();
        var baseline = configs.First(c => c.Name == "baseline");

        // Assert
        baseline.DisplayName.Should().Be("Baseline (350/15%)");
        baseline.SizeTokens.Should().Be(350);
        baseline.OverlapPercent.Should().Be(0.15);
    }

    [Fact]
    public void SparseConfig_HasCorrectValues()
    {
        // Act
        var configs = ChunkingConfig.GetAll();
        var sparse = configs.First(c => c.Name == "sparse");

        // Assert
        sparse.DisplayName.Should().Be("Sparse (500/10%)");
        sparse.SizeTokens.Should().Be(500);
        sparse.OverlapPercent.Should().Be(0.10);
    }
}

/// <summary>
/// Unit tests for QuantizationConfig.
/// </summary>
public class QuantizationConfigTests
{
    [Fact]
    public void GetAll_Returns2Configurations()
    {
        // Act
        var configs = QuantizationConfig.GetAll();

        // Assert
        configs.Count.Should().Be(2);
    }

    [Fact]
    public void NoneConfig_HasCorrectValues()
    {
        // Act
        var configs = QuantizationConfig.GetAll();
        var none = configs.First(c => c.Name == "none");

        // Assert
        none.DisplayName.Should().Be("Full Precision");
        none.Type.Should().Be(QuantizationType.None);
    }

    [Fact]
    public void ScalarInt8Config_HasCorrectValues()
    {
        // Act
        var configs = QuantizationConfig.GetAll();
        var scalar = configs.First(c => c.Name == "scalar_int8");

        // Assert
        scalar.DisplayName.Should().Be("Scalar INT8");
        scalar.Type.Should().Be(QuantizationType.ScalarInt8);
    }
}

/// <summary>
/// Unit tests for RerankingConfig.
/// </summary>
public class RerankingConfigTests
{
    [Fact]
    public void GetAll_Returns2Configurations()
    {
        // Act
        var configs = RerankingConfig.GetAll();

        // Assert
        configs.Count.Should().Be(2);
    }

    [Fact]
    public void NoRerankConfig_HasCorrectValues()
    {
        // Act
        var configs = RerankingConfig.GetAll();
        var noRerank = configs.First(c => c.Name == "no_rerank");

        // Assert
        noRerank.DisplayName.Should().Be("No Reranking");
        noRerank.Enabled.Should().BeFalse();
        noRerank.ModelName.Should().BeNull();
    }

    [Fact]
    public void BgeRerankConfig_HasCorrectValues()
    {
        // Act
        var configs = RerankingConfig.GetAll();
        var bge = configs.First(c => c.Name == "bge_rerank");

        // Assert
        bge.DisplayName.Should().Be("BGE Reranker v2-m3");
        bge.Enabled.Should().BeTrue();
        bge.ModelName.Should().Be("BAAI/bge-reranker-v2-m3");
    }
}
