using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.Models;
using Api.Tests.BoundedContexts.Authentication.TestHelpers;
using Api.Tests.BoundedContexts.DocumentProcessing.TestHelpers;

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
/// // Multiple users with different roles
/// var user = TestDataFactory.CreateUser();
/// var admin = TestDataFactory.CreateAdmin();
///
/// // Complete scenarios
/// var (user, session) = TestDataFactory.CreateUserWithSession();
/// </code>
/// </remarks>
internal static class TestDataFactory
{
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

}

