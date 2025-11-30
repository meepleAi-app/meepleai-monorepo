namespace Api.Tests.BoundedContexts.WorkflowIntegration.TestHelpers;

/// <summary>
/// WorkflowIntegration-specific test constants.
/// Prevents magic numbers in n8n workflow and error logging tests.
/// </summary>
public static class WorkflowIntegrationTestConstants
{
    /// <summary>
    /// Workflow execution timeouts.
    /// </summary>
    public static class WorkflowTimeouts
    {
        /// <summary>
        /// Short workflow execution timeout (10 seconds)
        /// </summary>
        public static readonly TimeSpan Short = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Standard workflow execution timeout (30 seconds)
        /// </summary>
        public static readonly TimeSpan Standard = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Long workflow execution timeout for complex workflows (2 minutes)
        /// </summary>
        public static readonly TimeSpan Long = TimeSpan.FromMinutes(2);
    }

    /// <summary>
    /// Workflow retry and error handling constants.
    /// </summary>
    public static class ErrorHandling
    {
        /// <summary>
        /// Maximum retry attempts for failed workflows
        /// </summary>
        public const int MaxRetryAttempts = 3;

        /// <summary>
        /// Retry delay between attempts (5 seconds)
        /// </summary>
        public static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Error log retention period (30 days)
        /// </summary>
        public static readonly TimeSpan ErrorLogRetention = TimeSpan.FromDays(30);
    }

    /// <summary>
    /// n8n configuration constants.
    /// </summary>
    public static class N8nConfig
    {
        /// <summary>
        /// Webhook timeout (15 seconds)
        /// </summary>
        public static readonly TimeSpan WebhookTimeout = TimeSpan.FromSeconds(15);

        /// <summary>
        /// Configuration validation timeout (5 seconds)
        /// </summary>
        public static readonly TimeSpan ValidationTimeout = TimeSpan.FromSeconds(5);
    }

    /// <summary>
    /// Test entity IDs for workflow integration tests.
    /// </summary>
    public static class TestEntityIds
    {
        public static readonly Guid Workflow1 = Guid.Parse("90000000-0000-0000-0000-000000000001");
        public static readonly Guid N8nConfig1 = Guid.Parse("91000000-0000-0000-0000-000000000001");
    }
}
