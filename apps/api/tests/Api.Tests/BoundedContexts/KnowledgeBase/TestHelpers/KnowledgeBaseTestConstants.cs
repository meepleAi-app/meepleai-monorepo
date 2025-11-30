namespace Api.Tests.BoundedContexts.KnowledgeBase.TestHelpers;

/// <summary>
/// KnowledgeBase-specific test constants.
/// Prevents magic numbers in RAG, AI, and chat-related tests.
/// </summary>
public static class KnowledgeBaseTestConstants
{
    /// <summary>
    /// RAG validation thresholds and scores.
    /// </summary>
    public static class RagValidation
    {
        /// <summary>
        /// Minimum confidence score for RAG responses (0.7)
        /// </summary>
        public const double MinConfidence = 0.7;

        /// <summary>
        /// High confidence threshold (0.85)
        /// </summary>
        public const double HighConfidence = 0.85;

        /// <summary>
        /// Very high confidence threshold (0.95)
        /// </summary>
        public const double VeryHighConfidence = 0.95;

        /// <summary>
        /// Low confidence threshold for testing edge cases (0.5)
        /// </summary>
        public const double LowConfidence = 0.5;

        /// <summary>
        /// Below minimum confidence (0.65)
        /// </summary>
        public const double BelowMinConfidence = 0.65;

        /// <summary>
        /// Default Top-K for vector search (5)
        /// </summary>
        public const int DefaultTopK = 5;

        /// <summary>
        /// Large Top-K for testing (20)
        /// </summary>
        public const int LargeTopK = 20;
    }

    /// <summary>
    /// Streaming and timeout constants for AI operations.
    /// </summary>
    public static class Streaming
    {
        /// <summary>
        /// Timeout for streaming operations (30 seconds)
        /// </summary>
        public static readonly TimeSpan StreamTimeout = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Long timeout for complex AI operations (60 seconds)
        /// </summary>
        public static readonly TimeSpan LongStreamTimeout = TimeSpan.FromSeconds(60);

        /// <summary>
        /// Delay between stream chunks (100ms)
        /// </summary>
        public static readonly TimeSpan ChunkDelay = TimeSpan.FromMilliseconds(100);

        /// <summary>
        /// Number of chunks to expect in streaming tests
        /// </summary>
        public const int ExpectedChunkCount = 5;
    }

    /// <summary>
    /// Chat thread and message constants.
    /// </summary>
    public static class ChatThreads
    {
        /// <summary>
        /// Maximum messages per thread for testing
        /// </summary>
        public const int MaxMessagesPerThread = 100;

        /// <summary>
        /// Typical message count for standard tests
        /// </summary>
        public const int TypicalMessageCount = 10;

        /// <summary>
        /// Thread inactivity timeout (24 hours)
        /// </summary>
        public static readonly TimeSpan InactivityTimeout = TimeSpan.FromHours(24);
    }

    /// <summary>
    /// Agent-specific constants.
    /// </summary>
    public static class Agents
    {
        /// <summary>
        /// Default agent response timeout (10 seconds)
        /// </summary>
        public static readonly TimeSpan ResponseTimeout = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Agent health check timeout (5 seconds)
        /// </summary>
        public static readonly TimeSpan HealthCheckTimeout = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Long-running operation timeout (5 seconds in milliseconds)
        /// </summary>
        public static readonly TimeSpan LongRunningTimeout = TimeSpan.FromMilliseconds(5000);

        /// <summary>
        /// Number of agents in test configuration
        /// </summary>
        public const int TestAgentCount = 3;
    }

    /// <summary>
    /// Provider health check specific timeouts.
    /// </summary>
    public static class ProviderHealthCheck
    {
        /// <summary>
        /// Warmup period + first check wait time (11 seconds in milliseconds)
        /// </summary>
        public static readonly TimeSpan WarmupAndFirstCheck = TimeSpan.FromMilliseconds(11000);

        /// <summary>
        /// Warmup + first check with timeout buffer (16 seconds in milliseconds)
        /// </summary>
        public static readonly TimeSpan WarmupWithTimeoutBuffer = TimeSpan.FromMilliseconds(16000);

        /// <summary>
        /// Slow response simulation timeout (10 seconds in milliseconds)
        /// </summary>
        public static readonly TimeSpan SlowResponseTimeout = TimeSpan.FromMilliseconds(10000);
    }

    /// <summary>
    /// Cosine similarity and validation constants.
    /// </summary>
    public static class Similarity
    {
        /// <summary>
        /// High similarity threshold (0.9)
        /// </summary>
        public const double HighSimilarity = 0.9;

        /// <summary>
        /// Medium similarity threshold (0.7)
        /// </summary>
        public const double MediumSimilarity = 0.7;

        /// <summary>
        /// Low similarity threshold (0.5)
        /// </summary>
        public const double LowSimilarity = 0.5;

        /// <summary>
        /// Tolerance for similarity comparisons (0.01)
        /// </summary>
        public const double Tolerance = 0.01;
    }

    /// <summary>
    /// LLM cost tracking constants.
    /// </summary>
    public static class CostTracking
    {
        /// <summary>
        /// Cost per token for GPT-4 (example: $0.00003)
        /// </summary>
        public const decimal Gpt4CostPerToken = 0.00003m;

        /// <summary>
        /// Cost per token for GPT-3.5 (example: $0.000002)
        /// </summary>
        public const decimal Gpt35CostPerToken = 0.000002m;

        /// <summary>
        /// Typical token count for test queries
        /// </summary>
        public const int TypicalTokenCount = 100;
    }

    /// <summary>
    /// Test entity IDs for knowledge base tests.
    /// </summary>
    public static class TestEntityIds
    {
        public static readonly Guid Thread1 = Guid.Parse("10000000-0000-0000-0000-000000000001");
        public static readonly Guid Thread2 = Guid.Parse("10000000-0000-0000-0000-000000000002");
        public static readonly Guid Message1 = Guid.Parse("20000000-0000-0000-0000-000000000001");
        public static readonly Guid Message2 = Guid.Parse("20000000-0000-0000-0000-000000000002");
        public static readonly Guid Agent1 = Guid.Parse("30000000-0000-0000-0000-000000000001");
    }
}
