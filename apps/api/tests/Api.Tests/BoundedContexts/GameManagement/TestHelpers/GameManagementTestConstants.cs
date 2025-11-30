namespace Api.Tests.BoundedContexts.GameManagement.TestHelpers;

/// <summary>
/// GameManagement-specific test constants.
/// Prevents magic numbers in game session and rule comment tests.
/// </summary>
public static class GameManagementTestConstants
{
    /// <summary>
    /// Game session timeout and duration constants.
    /// </summary>
    public static class SessionTimeouts
    {
        /// <summary>
        /// Short session timeout for testing (5 minutes)
        /// </summary>
        public static readonly TimeSpan Short = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Standard session timeout (30 minutes)
        /// </summary>
        public static readonly TimeSpan Standard = TimeSpan.FromMinutes(30);

        /// <summary>
        /// Long session timeout for complex games (2 hours)
        /// </summary>
        public static readonly TimeSpan Long = TimeSpan.FromHours(2);

        /// <summary>
        /// Session inactivity timeout (15 minutes)
        /// </summary>
        public static readonly TimeSpan Inactivity = TimeSpan.FromMinutes(15);
    }

    /// <summary>
    /// Player count constants for testing.
    /// </summary>
    public static class PlayerCounts
    {
        public const int MinPlayers = 2;
        public const int MaxPlayers = 6;
        public const int TypicalPlayers = 4;
    }

    /// <summary>
    /// Game duration constants (in minutes).
    /// </summary>
    public static class GameDurations
    {
        public const int ShortGame = 15;
        public const int MediumGame = 60;
        public const int LongGame = 120;
        public const int MinPlayTime = 30;
        public const int MaxPlayTime = 180;
    }

    /// <summary>
    /// Rule comment and versioning constants.
    /// </summary>
    public static class RuleComments
    {
        /// <summary>
        /// Maximum comment length for testing
        /// </summary>
        public const int MaxCommentLength = 1000;

        /// <summary>
        /// Typical comment length for standard tests
        /// </summary>
        public const int TypicalCommentLength = 100;

        /// <summary>
        /// Maximum nested reply depth
        /// </summary>
        public const int MaxReplyDepth = 3;
    }

    /// <summary>
    /// Test entity IDs for game management tests.
    /// </summary>
    public static class TestEntityIds
    {
        public static readonly Guid Game1 = Guid.Parse("40000000-0000-0000-0000-000000000001");
        public static readonly Guid Game2 = Guid.Parse("40000000-0000-0000-0000-000000000002");
        public static readonly Guid Session1 = Guid.Parse("50000000-0000-0000-0000-000000000001");
        public static readonly Guid Session2 = Guid.Parse("50000000-0000-0000-0000-000000000002");
        public static readonly Guid RuleSpec1 = Guid.Parse("60000000-0000-0000-0000-000000000001");
    }
}
