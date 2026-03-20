namespace Api.Tests.BoundedContexts.Authentication.TestHelpers;

/// <summary>
/// Authentication-specific test constants.
/// Prevents magic numbers in authentication-related tests.
/// </summary>
public static class AuthenticationTestConstants
{
    /// <summary>
    /// Session and token expiration timeouts.
    /// </summary>
    public static class SessionExpiry
    {
        /// <summary>
        /// Very short session expiry for testing expiration logic (1 second)
        /// </summary>
        public static readonly TimeSpan VeryShort = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Short session expiry for testing (5 seconds)
        /// </summary>
        public static readonly TimeSpan Short = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Standard test session expiry (30 seconds)
        /// </summary>
        public static readonly TimeSpan Standard = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Long session expiry for integration tests (5 minutes)
        /// </summary>
        public static readonly TimeSpan Long = TimeSpan.FromMinutes(5);
    }

    /// <summary>
    /// Time advances for testing session/token expiration.
    /// </summary>
    public static class TimeAdvance
    {
        /// <summary>
        /// Time advance to trigger 1-second expiration (2 seconds for buffer)
        /// </summary>
        public static readonly TimeSpan PastVeryShortExpiry = TimeSpan.FromSeconds(2);

        /// <summary>
        /// Time advance to trigger 5-second expiration (6 seconds for buffer)
        /// </summary>
        public static readonly TimeSpan PastShortExpiry = TimeSpan.FromSeconds(6);
    }

    /// <summary>
    /// TOTP and 2FA-specific constants.
    /// </summary>
    public static class TwoFactor
    {
        /// <summary>
        /// Standard TOTP time window (30 seconds)
        /// </summary>
        public static readonly TimeSpan TotpWindow = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Time advance for TOTP replay prevention tests (31 seconds, past window)
        /// </summary>
        public static readonly TimeSpan PastTotpWindow = TimeSpan.FromSeconds(31);

        /// <summary>
        /// Number of backup codes generated per user
        /// </summary>
        public const int BackupCodeCount = 10;

        /// <summary>
        /// TOTP code length
        /// </summary>
        public const int TotpCodeLength = 6;
    }

    /// <summary>
    /// API Key-specific constants.
    /// </summary>
    public static class ApiKey
    {
        /// <summary>
        /// API key expiry for short tests (1 day)
        /// </summary>
        public static readonly TimeSpan ShortExpiry = TimeSpan.FromDays(1);

        /// <summary>
        /// Standard API key expiry for tests (30 days)
        /// </summary>
        public static readonly TimeSpan StandardExpiry = TimeSpan.FromDays(30);

        /// <summary>
        /// Long API key expiry for tests (90 days)
        /// </summary>
        public static readonly TimeSpan LongExpiry = TimeSpan.FromDays(90);
    }

    /// <summary>
    /// OAuth-specific constants.
    /// </summary>
    public static class OAuth
    {
        /// <summary>
        /// OAuth state token expiry (10 minutes)
        /// </summary>
        public static readonly TimeSpan StateExpiry = TimeSpan.FromMinutes(10);

        /// <summary>
        /// OAuth code expiry (5 minutes)
        /// </summary>
        public static readonly TimeSpan CodeExpiry = TimeSpan.FromMinutes(5);
    }

    /// <summary>
    /// Test user IDs for authentication tests.
    /// </summary>
    public static class TestUserIds
    {
        public static readonly Guid User1 = Guid.Parse("00000000-0000-0000-0000-000000000001");
        public static readonly Guid User2 = Guid.Parse("00000000-0000-0000-0000-000000000002");
        public static readonly Guid User3 = Guid.Parse("00000000-0000-0000-0000-000000000003");
        public static readonly Guid AdminUser = Guid.Parse("00000000-0000-0000-0000-000000000099");
    }
}