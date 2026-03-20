namespace Api.Tests.BoundedContexts.Administration.TestHelpers;

/// <summary>
/// Administration-specific test constants.
/// Prevents magic numbers in admin, alerting, and audit tests.
/// </summary>
public static class AdministrationTestConstants
{
    /// <summary>
    /// Alert and monitoring timeouts.
    /// </summary>
    public static class AlertTimeouts
    {
        /// <summary>
        /// Alert acknowledgment timeout (5 minutes)
        /// </summary>
        public static readonly TimeSpan AcknowledgmentTimeout = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Alert auto-resolution timeout (24 hours)
        /// </summary>
        public static readonly TimeSpan AutoResolutionTimeout = TimeSpan.FromHours(24);

        /// <summary>
        /// Critical alert escalation time (15 minutes)
        /// </summary>
        public static readonly TimeSpan CriticalEscalation = TimeSpan.FromMinutes(15);
    }

    /// <summary>
    /// Alert severity thresholds and counts.
    /// </summary>
    public static class AlertSeverity
    {
        /// <summary>
        /// Maximum active critical alerts before escalation
        /// </summary>
        public const int MaxCriticalAlerts = 5;

        /// <summary>
        /// Warning threshold for alert count
        /// </summary>
        public const int WarningThreshold = 10;

        /// <summary>
        /// Alert history retention days
        /// </summary>
        public const int HistoryRetentionDays = 90;
    }

    /// <summary>
    /// Audit log and tracking constants.
    /// </summary>
    public static class AuditLog
    {
        /// <summary>
        /// Audit log retention period (1 year)
        /// </summary>
        public static readonly TimeSpan RetentionPeriod = TimeSpan.FromDays(365);

        /// <summary>
        /// Maximum audit entries per query
        /// </summary>
        public const int MaxEntriesPerQuery = 100;

        /// <summary>
        /// Typical batch size for audit processing
        /// </summary>
        public const int TypicalBatchSize = 50;
    }

    /// <summary>
    /// User management and statistics constants.
    /// </summary>
    public static class UserManagement
    {
        /// <summary>
        /// Password reset token expiry (24 hours)
        /// </summary>
        public static readonly TimeSpan PasswordResetExpiry = TimeSpan.FromHours(24);

        /// <summary>
        /// Account lockout duration (30 minutes)
        /// </summary>
        public static readonly TimeSpan AccountLockoutDuration = TimeSpan.FromMinutes(30);

        /// <summary>
        /// Maximum failed login attempts before lockout
        /// </summary>
        public const int MaxFailedLoginAttempts = 5;

        /// <summary>
        /// User inactivity period before flagging (90 days)
        /// </summary>
        public static readonly TimeSpan InactivityThreshold = TimeSpan.FromDays(90);
    }

    /// <summary>
    /// Weekly evaluation and reporting constants.
    /// </summary>
    public static class WeeklyEvaluation
    {
        /// <summary>
        /// Evaluation processing timeout (10 minutes)
        /// </summary>
        public static readonly TimeSpan ProcessingTimeout = TimeSpan.FromMinutes(10);

        /// <summary>
        /// Minimum evaluation samples required
        /// </summary>
        public const int MinSamplesRequired = 10;

        /// <summary>
        /// Report generation timeout (5 minutes)
        /// </summary>
        public static readonly TimeSpan ReportTimeout = TimeSpan.FromMinutes(5);
    }

    /// <summary>
    /// LLM health monitoring constants.
    /// </summary>
    public static class LlmHealth
    {
        /// <summary>
        /// Health check interval (1 minute)
        /// </summary>
        public static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Health check timeout (5 seconds)
        /// </summary>
        public static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Maximum consecutive failures before alert
        /// </summary>
        public const int MaxConsecutiveFailures = 3;
    }

    /// <summary>
    /// Test entity IDs for administration tests.
    /// </summary>
    public static class TestEntityIds
    {
        public static readonly Guid Alert1 = Guid.Parse("70000000-0000-0000-0000-000000000001");
        public static readonly Guid AuditLog1 = Guid.Parse("80000000-0000-0000-0000-000000000001");
    }
}