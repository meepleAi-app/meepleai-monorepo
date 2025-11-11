namespace Api.Constants;

/// <summary>
/// Centralized timeout and duration constants used across the application.
/// All values are in seconds unless otherwise specified.
/// </summary>
public static class TimeoutConstants
{
    // ========================================
    // AI/Embedding Service Timeouts
    // ========================================

    /// <summary>
    /// Default timeout for embedding API calls (30 seconds)
    /// Used in EmbeddingService for standard embedding requests
    /// </summary>
    public const int EmbeddingDefaultTimeoutSeconds = 30;

    /// <summary>
    /// Extended timeout for large embedding batches (60 seconds)
    /// Used in EmbeddingService for batch operations
    /// </summary>
    public const int EmbeddingBatchTimeoutSeconds = 60;

    // ========================================
    // Authentication & Session Timeouts
    // ========================================

    /// <summary>
    /// OAuth state parameter lifetime (10 minutes)
    /// CSRF protection for OAuth flows
    /// </summary>
    public const int OAuthStateLifetimeMinutes = 10;

    /// <summary>
    /// Session lifetime (7 days)
    /// Default lifetime for user sessions
    /// </summary>
    public const int SessionLifetimeDays = 7;

    /// <summary>
    /// Temporary session lifetime for 2FA flows (5 minutes)
    /// Short-lived session during 2FA verification
    /// </summary>
    public const int TempSessionLifetimeMinutes = 5;

    /// <summary>
    /// Password reset token expiry (30 minutes)
    /// </summary>
    public const int PasswordResetTokenExpiryMinutes = 30;

    // ========================================
    // Cache Expiry Timeouts
    // ========================================

    /// <summary>
    /// Admin statistics cache expiry (5 minutes)
    /// Used in AdminStatsService for dashboard data
    /// </summary>
    public const int AdminStatsCacheMinutes = 5;

    /// <summary>
    /// BGG API response cache (1 hour)
    /// Used in BggApiService for BoardGameGeek data
    /// </summary>
    public const int BggApiCacheHours = 1;

    /// <summary>
    /// Hybrid cache L1 + L2 expiry (5 minutes)
    /// Default TTL for HybridCache entries
    /// </summary>
    public const int HybridCacheExpiryMinutes = 5;

    // ========================================
    // Search & RAG Timeouts
    // ========================================

    /// <summary>
    /// Default RRF (Reciprocal Rank Fusion) constant (60)
    /// Used in HybridSearchService for search result reranking
    /// </summary>
    public const int RrfDefaultConstant = 60;

    /// <summary>
    /// Streaming QA polling interval (10 milliseconds)
    /// Used in StreamingQaService for SSE token streaming
    /// </summary>
    public const int StreamingQaPollingIntervalMs = 10;

    // ========================================
    // Rate Limiting Timeouts
    // ========================================

    /// <summary>
    /// Password reset rate limit window (1 hour)
    /// Maximum 3 requests per hour per email
    /// </summary>
    public const int PasswordResetRateLimitHours = 1;

    /// <summary>
    /// 2FA verification rate limit window (1 minute)
    /// Maximum 3 attempts per minute
    /// </summary>
    public const int TwoFactorRateLimitMinutes = 1;
}
