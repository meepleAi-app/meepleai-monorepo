namespace Api.Tests;

/// <summary>
/// Centralized test constants used across all bounded context tests.
/// Prevents magic strings and ensures consistency in test data.
/// </summary>
public static class TestConstants
{
    /// <summary>
    /// Standard test user data.
    /// </summary>
    public static class Users
    {
        public const string DefaultEmail = "test@example.com";
        public const string DefaultPassword = "TestPassword123!";
        public const string DefaultDisplayName = "Test User";

        public const string AdminEmail = "admin@meepleai.dev";
        public const string EditorEmail = "editor@meepleai.dev";
        public const string UserEmail = "user@meepleai.dev";
    }

    /// <summary>
    /// Standard test game data.
    /// </summary>
    public static class Games
    {
        public const string DefaultTitle = "Catan";
        public const string DefaultPublisher = "KOSMOS";
        public const int DefaultYear = 1995;
        public const int DefaultMinPlayers = 3;
        public const int DefaultMaxPlayers = 4;
        public const int DefaultMinPlayTime = 60;
        public const int DefaultMaxPlayTime = 120;

        public const string TicketToRide = "Ticket to Ride";
        public const string Pandemic = "Pandemic";
        public const string Azul = "Azul";
    }

    /// <summary>
    /// Standard test AI/Agent data.
    /// </summary>
    public static class Agents
    {
        public const string DefaultAgentName = "TestAgent";
        public const string QaAgentName = "QA Agent";
        public const string ExplainAgentName = "Explain Agent";
        public const string SetupAgentName = "Setup Agent";
    }

    /// <summary>
    /// Standard test session/player data.
    /// </summary>
    public static class Sessions
    {
        public const string Player1 = "Player 1";
        public const string Player2 = "Player 2";
        public const string Player3 = "Player 3";
        public const string Player4 = "Player 4";

        public static readonly string[] TwoPlayers = { Player1, Player2 };
        public static readonly string[] FourPlayers = { Player1, Player2, Player3, Player4 };
    }

    /// <summary>
    /// Standard test PDF/document data.
    /// </summary>
    public static class Documents
    {
        public const string DefaultPdfFileName = "test-rulebook.pdf";
        public const string DefaultGameName = "Test Game";
        public const int DefaultPageCount = 10;
        public const double DefaultQualityScore = 0.85;
    }

    /// <summary>
    /// Standard test API/OAuth data.
    /// </summary>
    public static class Auth
    {
        public const string DefaultApiKeyName = "Test API Key";
        public const string DefaultSessionToken = "test_session_token_123";
        public const string DefaultTotpSecret = "test_totp_secret_encrypted";

        public const string GoogleProvider = "google";
        public const string DiscordProvider = "discord";
        public const string GitHubProvider = "github";
    }

    /// <summary>
    /// Standard test time/date values.
    /// </summary>
    public static class Timing
    {
        // Timeouts
        public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);
        public static readonly TimeSpan ShortTimeout = TimeSpan.FromSeconds(5);
        public static readonly TimeSpan LongTimeout = TimeSpan.FromMinutes(5);

        // Short delays for async operations
        public static readonly TimeSpan MinimalDelay = TimeSpan.FromMilliseconds(1);
        public static readonly TimeSpan TinyDelay = TimeSpan.FromMilliseconds(10);
        public static readonly TimeSpan SmallDelay = TimeSpan.FromMilliseconds(100);
        public static readonly TimeSpan MediumDelay = TimeSpan.FromMilliseconds(150);
        public static readonly TimeSpan LargeDelay = TimeSpan.FromMilliseconds(300);
        public static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(500);
        public static readonly TimeSpan VeryShortTimeout = TimeSpan.FromSeconds(1);
        public static readonly TimeSpan MediumTimeout = TimeSpan.FromSeconds(2);

        // Assertion tolerances
        public static readonly TimeSpan AssertionTolerance = TimeSpan.FromSeconds(10);
        public static readonly TimeSpan StrictAssertionTolerance = TimeSpan.FromSeconds(2);

        public static readonly DateTime TestDate = new(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    }

    /// <summary>
    /// Standard test query/search data.
    /// </summary>
    public static class Queries
    {
        public const string SimpleQuestion = "How do I set up the game?";
        public const string ComplexQuestion = "What happens when two players tie for victory points at the end of the game?";
        public const string InvalidQuestion = "";

        public const int DefaultTopK = 5;
        public const double DefaultMinConfidence = 0.7;
    }
}

