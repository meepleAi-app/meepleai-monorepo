using Microsoft.Extensions.Configuration;
using Xunit;
using FluentAssertions;
using Xunit;

namespace Api.Tests;

/// <summary>
/// Debug test to verify EmbeddingService dimension detection logic
/// </summary>
public class EmbeddingDimensionsDebugTest
{
    private readonly ITestOutputHelper _output;

    public EmbeddingDimensionsDebugTest(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void DetermineEmbeddingDimensions_WithNullConfig_ShouldReturn768ForNomicEmbed()
    {
        // Arrange: Empty configuration (all lookups return null)
        var config = new ConfigurationBuilder().Build();
        var modelName = "nomic-embed-text";

        // Act: Call the private static method via reflection
        var embeddingServiceType = typeof(Api.Services.EmbeddingService);
        var method = embeddingServiceType.GetMethod(
            "DetermineEmbeddingDimensions",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static
        );

        var result = (int)method!.Invoke(null, new object[] { modelName, config })!;

        // Assert
        _output.WriteLine($"Result: {result}");
        _output.WriteLine($"Model: {modelName}");
        _output.WriteLine($"Config[EMBEDDING_DIMENSIONS]: {config["EMBEDDING_DIMENSIONS"]}");

        result.Should().Be(768);
    }

    [Fact]
    public void DetermineEmbeddingDimensions_WithEmbeddingDimensions0_ShouldFallbackToModel()
    {
        // Arrange: Configuration explicitly sets EMBEDDING_DIMENSIONS=0 (invalid)
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["EMBEDDING_DIMENSIONS"] = "0"
            })
            .Build();
        var modelName = "nomic-embed-text";

        // Act: Call the private static method via reflection
        var embeddingServiceType = typeof(Api.Services.EmbeddingService);
        var method = embeddingServiceType.GetMethod(
            "DetermineEmbeddingDimensions",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static
        );

        var result = (int)method!.Invoke(null, new object[] { modelName, config })!;

        // Assert
        _output.WriteLine($"Result: {result}");
        _output.WriteLine($"Model: {modelName}");
        _output.WriteLine($"Config[EMBEDDING_DIMENSIONS]: {config["EMBEDDING_DIMENSIONS"]}");

        // FIX: Now rejects 0 and falls back to model detection (768)
        result.Should().Be(768);
    }

    [Fact]
    public void DetermineEmbeddingDimensions_WithNestedEmbeddingConfig_ShouldUseNestedValue()
    {
        // Arrange: Configuration uses nested Embedding:Dimensions (like appsettings.json)
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Embedding:Dimensions"] = "768",
                ["Embedding:Model"] = "nomic-embed-text",
                ["Embedding:Provider"] = "ollama"
            })
            .Build();
        var modelName = "nomic-embed-text";

        // Act: Call the private static method via reflection
        var embeddingServiceType = typeof(Api.Services.EmbeddingService);
        var method = embeddingServiceType.GetMethod(
            "DetermineEmbeddingDimensions",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static
        );

        var result = (int)method!.Invoke(null, new object[] { modelName, config })!;

        // Assert
        _output.WriteLine($"Result: {result}");
        _output.WriteLine($"Model: {modelName}");
        _output.WriteLine($"Config[EMBEDDING_DIMENSIONS]: {config["EMBEDDING_DIMENSIONS"]}");
        _output.WriteLine($"Config[Embedding:Dimensions]: {config["Embedding:Dimensions"]}");

        // Current bug: Code looks for EMBEDDING_DIMENSIONS (flat), but config has Embedding:Dimensions (nested)
        // So it falls back to model name detection and returns 768
        result.Should().Be(768);
    }
}
