namespace Api.BoundedContexts.SystemConfiguration.Domain.Enums;

/// <summary>
/// Environment type for LLM model configuration.
/// Issue #2596: Enables separate model configurations for test and production environments.
/// </summary>
/// <remarks>
/// This separation allows:
/// - Using cheaper/faster models in test environments (CI/CD)
/// - Using production-grade models only when needed
/// - A/B testing models without affecting production
/// - Cost control during development and testing
/// </remarks>
public enum LlmEnvironmentType
{
    /// <summary>
    /// Production environment - uses primary configured models.
    /// </summary>
    Production = 0,

    /// <summary>
    /// Test environment - can use alternative models for cost savings.
    /// Automatically detected via IHostEnvironment.IsProduction().
    /// </summary>
    Test = 1
}
