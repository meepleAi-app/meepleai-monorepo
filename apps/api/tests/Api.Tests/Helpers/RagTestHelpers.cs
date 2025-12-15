using Api.Services.Rag;
using Moq;

namespace Api.Tests.Helpers;

/// <summary>
/// Shared test helpers for RAG-related tests.
/// Provides consistent mock configuration across integration and performance tests.
/// </summary>
internal static class RagTestHelpers
{
    /// <summary>
    /// Known RAG configuration keys used by RagService.
    /// These correspond to the configuration values validated in RagConfigurationProvider.
    /// </summary>
    public static class ConfigKeys
    {
        /// <summary>TopK: Number of results to retrieve (valid range: 1-50)</summary>
        public const string TopK = "TopK";

        /// <summary>MinScore: Minimum relevance score threshold (valid range: 0.0-1.0)</summary>
        public const string MinScore = "MinScore";

        /// <summary>RrfK: Reciprocal Rank Fusion parameter (valid range: 1-100)</summary>
        public const string RrfK = "RrfK";

        /// <summary>MaxQueryVariations: Maximum query variations for expansion (valid range: 1-10)</summary>
        public const string MaxQueryVariations = "MaxQueryVariations";
    }

    /// <summary>
    /// Creates a default IRagConfigurationProvider mock that returns default values.
    /// This mock simulates the behavior where all configuration requests return their default values,
    /// maintaining consistent test behavior across different test suites.
    /// </summary>
    /// <returns>Mock of IRagConfigurationProvider configured with default behavior</returns>
    public static Mock<IRagConfigurationProvider> CreateDefaultConfigProvider()
    {
        var mock = new Mock<IRagConfigurationProvider>();

        // Setup default RAG config values for int parameters (e.g., TopK, MaxQueryVariations)
        mock.Setup(c => c.GetRagConfigAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync((string key, int defaultValue) => defaultValue);

        // Setup default RAG config values for double parameters (e.g., MinScore, RrfK)
        mock.Setup(c => c.GetRagConfigAsync(It.IsAny<string>(), It.IsAny<double>()))
            .ReturnsAsync((string key, double defaultValue) => defaultValue);

        // Setup default RAG config values for float parameters
        mock.Setup(c => c.GetRagConfigAsync(It.IsAny<string>(), It.IsAny<float>()))
            .ReturnsAsync((string key, float defaultValue) => defaultValue);

        return mock;
    }

    /// <summary>
    /// Creates a custom IRagConfigurationProvider mock with specific configuration values.
    /// Allows tests to verify behavior with different configuration settings.
    /// Uses switch expressions to ensure correct mock setup precedence.
    /// </summary>
    /// <param name="topK">Number of results to retrieve (default: 5, valid range: 1-50)</param>
    /// <param name="minScore">Minimum relevance score threshold (default: 0.7, valid range: 0.0-1.0)</param>
    /// <param name="rrfK">Reciprocal Rank Fusion parameter (default: 60, valid range: 1-100)</param>
    /// <param name="maxQueryVariations">Maximum query variations for expansion (default: 3, valid range: 1-10)</param>
    /// <returns>Mock of IRagConfigurationProvider configured with specified values</returns>
    public static Mock<IRagConfigurationProvider> CreateCustomConfigProvider(
        int? topK = null,
        double? minScore = null,
        int? rrfK = null,
        int? maxQueryVariations = null)
    {
        var mock = new Mock<IRagConfigurationProvider>();

        // Set default values if not specified
        var topKValue = topK ?? 5;
        var minScoreValue = minScore ?? 0.7;
        var rrfKValue = rrfK ?? 60;
        var maxVariationsValue = maxQueryVariations ?? 3;

        // Setup int parameters with switch expression to handle all cases
        mock.Setup(c => c.GetRagConfigAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync((string key, int defaultValue) => key switch
            {
                ConfigKeys.TopK => topKValue,
                ConfigKeys.RrfK => rrfKValue,
                ConfigKeys.MaxQueryVariations => maxVariationsValue,
                _ => defaultValue // Fallback for unknown keys
            });

        // Setup double parameters with switch expression
        mock.Setup(c => c.GetRagConfigAsync(It.IsAny<string>(), It.IsAny<double>()))
            .ReturnsAsync((string key, double defaultValue) => key switch
            {
                ConfigKeys.MinScore => minScoreValue,
                _ => defaultValue // Fallback for unknown keys
            });

        // Setup float parameters (currently no float configs, but keep for future)
        mock.Setup(c => c.GetRagConfigAsync(It.IsAny<string>(), It.IsAny<float>()))
            .ReturnsAsync((string key, float defaultValue) => defaultValue);

        return mock;
    }
}

