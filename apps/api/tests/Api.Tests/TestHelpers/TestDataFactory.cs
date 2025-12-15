using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Models;
using Api.Tests.BoundedContexts.GameManagement.TestHelpers;
using Api.Tests.BoundedContexts.Authentication.TestHelpers;
using Api.Tests.BoundedContexts.DocumentProcessing.TestHelpers;
using Api.Tests.BoundedContexts.KnowledgeBase.TestHelpers;

namespace Api.Tests.TestHelpers;

/// <summary>
/// Centralized factory for creating common test data scenarios.
/// Complements existing builders to provide quick access to typical test cases.
/// </summary>
/// <remarks>
/// <para><strong>When to use:</strong></para>
/// <list type="bullet">
///   <item>Use <b>Factories</b> when you need common, pre-configured test scenarios</item>
///   <item>Use <b>Builders</b> when you need fine-grained customization</item>
///   <item>Combine both for best results (factories call builders internally)</item>
/// </list>
///
/// <para><strong>Examples:</strong></para>
/// <code>
/// // Quick game creation
/// var game = TestDataFactory.CreateValidGame();
///
/// // Multiple users with different roles
/// var user = TestDataFactory.CreateUser();
/// var admin = TestDataFactory.CreateAdmin();
///
/// // Complete scenarios
/// var (game, pdf, snippets) = TestDataFactory.CreateRagScenario();
/// var (user, session) = TestDataFactory.CreateUserWithSession();
///
/// // Custom title with factory defaults
/// var game = TestDataFactory.CreateValidGame("Catan");
///
/// // Mix factory with builder for customization
/// var game = new GameBuilder()
///     .WithTitle(TestDataFactory.Defaults.TestGameTitle)
///     .WithPlayerCount(1, 4)
///     .Build();
/// </code>
/// </remarks>
internal static class TestDataFactory
{
    /// <summary>
    /// Creates a valid game with sensible defaults (Catan-like).
    /// </summary>
    public static Game CreateValidGame(string? title = null) =>
        new GameBuilder()
            .WithTitle(title ?? "Test Game")
            .WithPublisher("Test Publisher")
            .WithYearPublished(2000)
            .WithPlayerCount(2, 4)
            .WithPlayTime(45, 90)
            .Build();

    /// <summary>
    /// Creates a game with full BGG metadata.
    /// </summary>
    public static Game CreateGameWithBggData(int bggId = 13, string? metadata = null) =>
        new GameBuilder()
            .WithTitle("Catan")
            .WithPublisher("Catan Studio")
            .WithYearPublished(1995)
            .WithPlayerCount(3, 4)
            .WithPlayTime(60, 120)
            .WithBggLink(bggId, metadata ?? "{\"rating\": 7.2, \"weight\": 2.3}")
            .Build();

    /// <summary>
    /// Creates a game with minimal data (title only).
    /// </summary>
    public static Game CreateMinimalGame(string title = "Minimal Game") =>
        new GameBuilder()
            .WithTitle(title)
            .Build();

    /// <summary>
    /// Creates multiple games for list tests.
    /// </summary>
    public static List<Game> CreateGames(int count)
    {
        var games = new List<Game>();
        for (int i = 1; i <= count; i++)
        {
            games.Add(CreateValidGame($"Game {i}"));
        }
        return games;
    }
    /// <summary>
    /// Creates a standard user with User role.
    /// </summary>
    public static User CreateUser(string? email = null, string? displayName = null) =>
        new UserBuilder()
            .WithEmail(email ?? "user@test.com")
            .WithDisplayName(displayName ?? "Test User")
            .WithRole(Role.User)
            .Build();

    /// <summary>
    /// Creates an admin user.
    /// </summary>
    public static User CreateAdmin(string? email = null) =>
        new UserBuilder()
            .WithEmail(email ?? "admin@test.com")
            .WithDisplayName("Test Admin")
            .WithRole(Role.Admin)
            .Build();

    /// <summary>
    /// Creates an editor user.
    /// </summary>
    public static User CreateEditor(string? email = null) =>
        new UserBuilder()
            .WithEmail(email ?? "editor@test.com")
            .WithDisplayName("Test Editor")
            .WithRole(Role.Editor)
            .Build();

    /// <summary>
    /// Creates a user with 2FA enabled.
    /// </summary>
    public static User CreateUserWith2FA(string? email = null) =>
        new UserBuilder()
            .WithEmail(email ?? "user2fa@test.com")
            .WithDisplayName("Test User with 2FA")
            .With2FA("JBSWY3DPEHPK3PXP")
            .Build();
    /// <summary>
    /// Creates an active session for a user.
    /// </summary>
    public static Session CreateActiveSession(Guid userId, int expiresInMinutes = 60) =>
        new SessionBuilder()
            .ForUser(userId)
            .WithLifetime(TimeSpan.FromMinutes(expiresInMinutes))
            .Build();

    /// <summary>
    /// Creates an expired session.
    /// </summary>
    public static Session CreateExpiredSession(Guid userId) =>
        new SessionBuilder()
            .ForUser(userId)
            .Expired()
            .Build();

    /// <summary>
    /// Creates a temporary 2FA session (5 minutes).
    /// </summary>
    public static Session CreateTemp2FASession(Guid userId) =>
        new SessionBuilder()
            .ForUser(userId)
            .WithLifetime(TimeSpan.FromMinutes(5))
            .Build();
    /// <summary>
    /// Creates a valid PDF document for a game.
    /// </summary>
    public static PdfDocument CreatePdfDocument(
        Guid gameId,
        string? fileName = null,
        int pageCount = 10) =>
        new PdfDocumentBuilder()
            .WithGameId(gameId)
            .WithFileName(fileName ?? "test-rules.pdf")
            .ThatIsCompleted(pageCount)
            .Build();

    /// <summary>
    /// Creates multiple PDFs for a game.
    /// </summary>
    public static List<PdfDocument> CreatePdfDocuments(Guid gameId, int count)
    {
        var pdfs = new List<PdfDocument>();
        for (int i = 1; i <= count; i++)
        {
            pdfs.Add(CreatePdfDocument(gameId, $"document-{i}.pdf", pageCount: 5 + i));
        }
        return pdfs;
    }

    /// <summary>
    /// Creates a PDF document in processing status.
    /// </summary>
    public static PdfDocument CreatePdfInProcessing(Guid gameId, string? fileName = null) =>
        new PdfDocumentBuilder()
            .WithGameId(gameId)
            .WithFileName(fileName ?? "processing-rules.pdf")
            .ThatIsProcessing()
            .Build();

    /// <summary>
    /// Creates a PDF document in failed status with error message.
    /// </summary>
    public static PdfDocument CreateFailedPdf(
        Guid gameId,
        string? fileName = null,
        string? errorMessage = null) =>
        new PdfDocumentBuilder()
            .WithGameId(gameId)
            .WithFileName(fileName ?? "failed-rules.pdf")
            .ThatIsFailed(errorMessage ?? "PDF processing failed")
            .Build();
    /// <summary>
    /// Creates valid snippets for citation testing.
    /// </summary>
    public static List<Snippet> CreateValidSnippets(
        Guid pdfId,
        int count = 3)
    {
        var snippets = new List<Snippet>();
        for (int i = 1; i <= count; i++)
        {
            snippets.Add(new Snippet(
                text: $"Sample text {i}",
                source: $"PDF:{pdfId}",
                page: i,
                line: 0,
                score: 0.9f - (i * 0.05f)
            ));
        }
        return snippets;
    }

    /// <summary>
    /// Creates snippets with invalid page numbers.
    /// </summary>
    public static List<Snippet> CreateSnippetsWithInvalidPages(Guid pdfId) =>
        new()
        {
            new Snippet("text", $"PDF:{pdfId}", page: 0, line: 0, score: 0.9f),
            new Snippet("text", $"PDF:{pdfId}", page: -1, line: 0, score: 0.8f),
            new Snippet("text", $"PDF:{pdfId}", page: 999, line: 0, score: 0.7f),
        };
    /// <summary>
    /// Creates a RAG agent with hybrid search strategy.
    /// </summary>
    public static Agent CreateRagAgent(string? name = null) =>
        new AgentBuilder()
            .WithName(name ?? "Test RAG Agent")
            .WithType(AgentType.RagAgent)
            .WithStrategy(AgentStrategy.HybridSearch())
            .Build();

    /// <summary>
    /// Creates a citation agent.
    /// </summary>
    public static Agent CreateCitationAgent(string? name = null) =>
        new AgentBuilder()
            .WithName(name ?? "Test Citation Agent")
            .WithType(AgentType.RagAgent)
            .WithStrategy(AgentStrategy.VectorOnly())
            .Build();

    /// <summary>
    /// Creates a conversation agent.
    /// </summary>
    public static Agent CreateConversationAgent(string? name = null) =>
        new AgentBuilder()
            .WithName(name ?? "Test Conversation Agent")
            .WithType(AgentType.ConversationAgent)
            .WithStrategy(AgentStrategy.HybridSearch())
            .Build();
    /// <summary>
    /// Creates a complete game setup with user, game, and PDFs.
    /// </summary>
    public static (User user, Game game, List<PdfDocument> pdfs) CreateGameSetup()
    {
        var user = CreateUser();
        var game = CreateValidGame();
        var pdfs = CreatePdfDocuments(game.Id, count: 2);

        return (user, game, pdfs);
    }

    /// <summary>
    /// Creates a complete RAG test scenario.
    /// </summary>
    public static (Game game, PdfDocument pdf, List<Snippet> snippets) CreateRagScenario()
    {
        var game = CreateValidGame();
        var pdf = CreatePdfDocument(game.Id);
        var snippets = CreateValidSnippets(pdf.Id);

        return (game, pdf, snippets);
    }

    /// <summary>
    /// Creates a user with an active session.
    /// </summary>
    public static (User user, Session session) CreateUserWithSession()
    {
        var user = CreateUser();
        var session = CreateActiveSession(user.Id);
        return (user, session);
    }

    /// <summary>
    /// Creates an admin user with an active session.
    /// </summary>
    public static (User admin, Session session) CreateAdminWithSession()
    {
        var admin = CreateAdmin();
        var session = CreateActiveSession(admin.Id);
        return (admin, session);
    }

    /// <summary>
    /// Creates a game with an active play session.
    /// </summary>
    public static (Game game, GameSession gameSession) CreateGameWithSession(int playerCount = 2)
    {
        var game = CreateValidGame();
        var gameSession = new GameSessionBuilder()
            .WithGameId(game.Id)
            .ThatIsStarted()
            .Build();
        return (game, gameSession);
    }
    /// <summary>
    /// Default values for test data.
    /// Provides commonly used constants and deterministic GUIDs for stable test data.
    /// </summary>
    public static class Defaults
    {
        // Authentication & User defaults
        /// <summary>Default test email address.</summary>
        public const string TestEmail = "test@example.com";

        /// <summary>Default test password (for use with UserBuilder).</summary>
        public const string TestPassword = "Test123!";

        /// <summary>Default test user display name.</summary>
        public const string TestDisplayName = "Test User";

        // Game defaults
        /// <summary>Default test game title.</summary>
        public const string TestGameTitle = "Test Game";

        // PDF & Quality defaults
        /// <summary>Default page count for test PDF documents.</summary>
        public const int DefaultPageCount = 10;

        /// <summary>Default quality score for PDF processing (0.0-1.0).</summary>
        public const double DefaultQualityScore = 0.85;

        /// <summary>Default snippet relevance score for RAG (0.0-1.0).</summary>
        public const float DefaultSnippetScore = 0.9f;

        // Deterministic GUIDs for stable test data
        /// <summary>Deterministic GUID for test user (useful for assertions that need stable IDs).</summary>
        public static readonly Guid TestUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");

        /// <summary>Deterministic GUID for test game (useful for assertions that need stable IDs).</summary>
        public static readonly Guid TestGameId = Guid.Parse("00000000-0000-0000-0000-000000000002");

        /// <summary>Deterministic GUID for test PDF document (useful for assertions that need stable IDs).</summary>
        public static readonly Guid TestPdfId = Guid.Parse("00000000-0000-0000-0000-000000000003");
    }
}

