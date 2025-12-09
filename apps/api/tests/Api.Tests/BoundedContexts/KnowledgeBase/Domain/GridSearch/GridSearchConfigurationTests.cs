using Api.BoundedContexts.KnowledgeBase.Domain.GridSearch;
using Xunit;
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
        Assert.Equal(12, configs.Count);
    }

    [Fact]
    public void GetAllConfigurations_HasUniqueConfigurationIds()
    {
        // Act
        var configs = GridSearchConfiguration.GetAllConfigurations();
        var configIds = configs.Select(c => c.ConfigurationId).ToList();

        // Assert
        Assert.Equal(configs.Count, configIds.Distinct().Count());
    }

    [Fact]
    public void GetQuickConfigurations_Returns3Configurations()
    {
        // Act
        var configs = GridSearchConfiguration.GetQuickConfigurations();

        // Assert
        Assert.Equal(3, configs.Count);
    }

    [Fact]
    public void GetQuickConfigurations_ContainsExpectedConfigurations()
    {
        // Act
        var configs = GridSearchConfiguration.GetQuickConfigurations();
        var configIds = configs.Select(c => c.ConfigurationId).ToList();

        // Assert
        Assert.Contains("baseline_none_no_rerank", configIds);
        Assert.Contains("baseline_none_bge_rerank", configIds);
        Assert.Contains("dense_scalar_int8_bge_rerank", configIds);
    }

    [Fact]
    public void GetAllConfigurations_ContainsAllChunkingVariants()
    {
        // Act
        var configs = GridSearchConfiguration.GetAllConfigurations();

        // Assert - Should contain dense, baseline, and sparse chunking
        Assert.True(configs.Any(c => c.Chunking.Name == "dense"));
        Assert.True(configs.Any(c => c.Chunking.Name == "baseline"));
        Assert.True(configs.Any(c => c.Chunking.Name == "sparse"));
    }

    [Fact]
    public void GetAllConfigurations_ContainsAllQuantizationVariants()
    {
        // Act
        var configs = GridSearchConfiguration.GetAllConfigurations();

        // Assert - Should contain none and scalar_int8
        Assert.True(configs.Any(c => c.Quantization.Type == QuantizationType.None));
        Assert.True(configs.Any(c => c.Quantization.Type == QuantizationType.ScalarInt8));
    }

    [Fact]
    public void GetAllConfigurations_ContainsAllRerankingVariants()
    {
        // Act
        var configs = GridSearchConfiguration.GetAllConfigurations();

        // Assert - Should contain no_rerank and bge_rerank
        Assert.True(configs.Any(c => !c.Reranking.Enabled));
        Assert.True(configs.Any(c => c.Reranking.Enabled));
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
        Assert.Equal(3, configs.Count);
    }

    [Fact]
    public void DenseConfig_HasCorrectValues()
    {
        // Act
        var configs = ChunkingConfig.GetAll();
        var dense = configs.First(c => c.Name == "dense");

        // Assert
        Assert.Equal("Dense (200/20%)", dense.DisplayName);
        Assert.Equal(200, dense.SizeTokens);
        Assert.Equal(0.20, dense.OverlapPercent);
    }

    [Fact]
    public void BaselineConfig_HasCorrectValues()
    {
        // Act
        var configs = ChunkingConfig.GetAll();
        var baseline = configs.First(c => c.Name == "baseline");

        // Assert
        Assert.Equal("Baseline (350/15%)", baseline.DisplayName);
        Assert.Equal(350, baseline.SizeTokens);
        Assert.Equal(0.15, baseline.OverlapPercent);
    }

    [Fact]
    public void SparseConfig_HasCorrectValues()
    {
        // Act
        var configs = ChunkingConfig.GetAll();
        var sparse = configs.First(c => c.Name == "sparse");

        // Assert
        Assert.Equal("Sparse (500/10%)", sparse.DisplayName);
        Assert.Equal(500, sparse.SizeTokens);
        Assert.Equal(0.10, sparse.OverlapPercent);
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
        Assert.Equal(2, configs.Count);
    }

    [Fact]
    public void NoneConfig_HasCorrectValues()
    {
        // Act
        var configs = QuantizationConfig.GetAll();
        var none = configs.First(c => c.Name == "none");

        // Assert
        Assert.Equal("Full Precision", none.DisplayName);
        Assert.Equal(QuantizationType.None, none.Type);
    }

    [Fact]
    public void ScalarInt8Config_HasCorrectValues()
    {
        // Act
        var configs = QuantizationConfig.GetAll();
        var scalar = configs.First(c => c.Name == "scalar_int8");

        // Assert
        Assert.Equal("Scalar INT8", scalar.DisplayName);
        Assert.Equal(QuantizationType.ScalarInt8, scalar.Type);
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
        Assert.Equal(2, configs.Count);
    }

    [Fact]
    public void NoRerankConfig_HasCorrectValues()
    {
        // Act
        var configs = RerankingConfig.GetAll();
        var noRerank = configs.First(c => c.Name == "no_rerank");

        // Assert
        Assert.Equal("No Reranking", noRerank.DisplayName);
        Assert.False(noRerank.Enabled);
        Assert.Null(noRerank.ModelName);
    }

    [Fact]
    public void BgeRerankConfig_HasCorrectValues()
    {
        // Act
        var configs = RerankingConfig.GetAll();
        var bge = configs.First(c => c.Name == "bge_rerank");

        // Assert
        Assert.Equal("BGE Reranker v2-m3", bge.DisplayName);
        Assert.True(bge.Enabled);
        Assert.Equal("BAAI/bge-reranker-v2-m3", bge.ModelName);
    }
}
