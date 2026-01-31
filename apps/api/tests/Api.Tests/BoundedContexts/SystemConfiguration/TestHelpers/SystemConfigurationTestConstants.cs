namespace Api.Tests.BoundedContexts.SystemConfiguration.TestHelpers;

/// <summary>
/// SystemConfiguration-specific test constants.
/// Prevents magic numbers in configuration and feature flag tests.
/// </summary>
public static class SystemConfigurationTestConstants
{
    /// <summary>
    /// Configuration caching and refresh timeouts.
    /// </summary>
    public static class CacheTimeouts
    {
        /// <summary>
        /// Configuration cache expiry (5 minutes)
        /// </summary>
        public static readonly TimeSpan CacheExpiry = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Configuration refresh interval (1 minute)
        /// </summary>
        public static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Hot reload delay (500ms)
        /// </summary>
        public static readonly TimeSpan HotReloadDelay = TimeSpan.FromMilliseconds(500);
    }

    /// <summary>
    /// Configuration validation constants.
    /// </summary>
    public static class Validation
    {
        /// <summary>
        /// Maximum configuration value length
        /// </summary>
        public const int MaxValueLength = 1000;

        /// <summary>
        /// Maximum configuration key length
        /// </summary>
        public const int MaxKeyLength = 100;

        /// <summary>
        /// Configuration history retention days
        /// </summary>
        public const int HistoryRetentionDays = 90;
    }

    /// <summary>
    /// Feature flag constants.
    /// </summary>
    public static class FeatureFlags
    {
        /// <summary>
        /// Feature flag evaluation timeout (1 second)
        /// </summary>
        public static readonly TimeSpan EvaluationTimeout = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Maximum feature flags per environment
        /// </summary>
        public const int MaxFlagsPerEnvironment = 100;
    }

    /// <summary>
    /// Test entity IDs for system configuration tests.
    /// </summary>
    public static class TestEntityIds
    {
        public static readonly Guid Config1 = Guid.Parse("92000000-0000-0000-0000-000000000001");
        public static readonly Guid Config2 = Guid.Parse("92000000-0000-0000-0000-000000000002");
    }
}
