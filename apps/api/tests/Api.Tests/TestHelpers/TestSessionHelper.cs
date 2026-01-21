using System.Net.Http.Json;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Tests.TestHelpers;

/// <summary>
/// Helper for creating test sessions in integration tests.
/// Provides admin and user session authentication for HTTP endpoint testing.
/// Issue #2688: Test authentication infrastructure for admin endpoint tests.
///
/// IMPORTANT: Tests using this helper must also mock IHybridCacheService in their
/// WebApplicationFactory setup to enable session validation. Example:
/// <code>
/// services.RemoveAll(typeof(Api.Services.IHybridCacheService));
/// services.AddScoped&lt;Api.Services.IHybridCacheService&gt;(_ => Mock.Of&lt;Api.Services.IHybridCacheService&gt;());
/// </code>
/// </summary>
internal static class TestSessionHelper
{
    /// <summary>
    /// Default session cookie name (matches CookieHelpers.GetSessionCookieName default).
    /// </summary>
    public const string SessionCookieName = "meepleai_session";

    /// <summary>
    /// Pre-computed password hash for test users.
    /// Using a static hash avoids expensive PBKDF2 computation in tests.
    /// Password: "TestPassword123!" hashed with v1.210000 iterations.
    /// </summary>
    private const string TestPasswordHash = "v1.210000.AAAAAAAAAAAAAAAAAAAAAA==.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";

    /// <summary>
    /// Creates an admin user and session in the database, returning the raw token for HTTP requests.
    /// </summary>
    /// <param name="dbContext">The database context to use</param>
    /// <param name="userId">Optional user ID (generates new if not provided)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tuple of (UserId, RawSessionToken) for use in HTTP cookie header</returns>
    public static async Task<(Guid UserId, string RawToken)> CreateAdminSessionAsync(
        MeepleAiDbContext dbContext,
        Guid? userId = null,
        CancellationToken cancellationToken = default)
    {
        return await CreateSessionAsync(dbContext, "Admin", userId, cancellationToken);
    }

    /// <summary>
    /// Creates a regular user and session in the database, returning the raw token for HTTP requests.
    /// </summary>
    /// <param name="dbContext">The database context to use</param>
    /// <param name="userId">Optional user ID (generates new if not provided)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tuple of (UserId, RawSessionToken) for use in HTTP cookie header</returns>
    public static async Task<(Guid UserId, string RawToken)> CreateUserSessionAsync(
        MeepleAiDbContext dbContext,
        Guid? userId = null,
        CancellationToken cancellationToken = default)
    {
        return await CreateSessionAsync(dbContext, "User", userId, cancellationToken);
    }

    /// <summary>
    /// Creates an editor user and session in the database, returning the raw token for HTTP requests.
    /// </summary>
    /// <param name="dbContext">The database context to use</param>
    /// <param name="userId">Optional user ID (generates new if not provided)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tuple of (UserId, RawSessionToken) for use in HTTP cookie header</returns>
    public static async Task<(Guid UserId, string RawToken)> CreateEditorSessionAsync(
        MeepleAiDbContext dbContext,
        Guid? userId = null,
        CancellationToken cancellationToken = default)
    {
        return await CreateSessionAsync(dbContext, "Editor", userId, cancellationToken);
    }

    /// <summary>
    /// Creates a user and session in the database with the specified role.
    /// </summary>
    private static async Task<(Guid UserId, string RawToken)> CreateSessionAsync(
        MeepleAiDbContext dbContext,
        string role,
        Guid? userId = null,
        CancellationToken cancellationToken = default)
    {
        var actualUserId = userId ?? Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        // Generate the session token
        var token = SessionToken.Generate();
        var tokenHash = token.ComputeHash();

        // Check if user already exists
        var existingUser = await dbContext.Set<UserEntity>()
            .FirstOrDefaultAsync(u => u.Id == actualUserId, cancellationToken);

        if (existingUser == null)
        {
            // Create user entity with required fields for UserRepository.MapToDomain
            var user = new UserEntity
            {
                Id = actualUserId,
                Email = $"test-{role.ToLowerInvariant()}-{Guid.NewGuid():N}@test.com",
                DisplayName = $"Test {role}",
                Role = role.ToLowerInvariant(), // DB stores lowercase
                PasswordHash = TestPasswordHash, // Required for domain mapping
                Tier = "free", // Default tier
                CreatedAt = DateTime.UtcNow
            };
            dbContext.Set<UserEntity>().Add(user);
        }
        else
        {
            // Update role if existing user
            existingUser.Role = role.ToLowerInvariant();
        }

        // Create session entity
        var session = new UserSessionEntity
        {
            Id = sessionId,
            UserId = actualUserId,
            TokenHash = tokenHash,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            LastSeenAt = DateTime.UtcNow,
            User = null! // Navigation property, not needed for insert
        };
        dbContext.Set<UserSessionEntity>().Add(session);

        await dbContext.SaveChangesAsync(cancellationToken);

        return (actualUserId, token.Value);
    }

    /// <summary>
    /// Creates an HttpRequestMessage with the session cookie header.
    /// Use this method when making individual authenticated requests.
    /// </summary>
    /// <param name="method">HTTP method</param>
    /// <param name="requestUri">Request URI</param>
    /// <param name="sessionToken">The raw session token</param>
    /// <returns>Configured HttpRequestMessage with authentication</returns>
    public static HttpRequestMessage CreateAuthenticatedRequest(
        HttpMethod method,
        string requestUri,
        string sessionToken)
    {
        var request = new HttpRequestMessage(method, requestUri);
        request.Headers.Add("Cookie", $"{SessionCookieName}={sessionToken}");
        return request;
    }

    /// <summary>
    /// Creates an HttpRequestMessage with JSON content and session cookie header.
    /// Use this method when making authenticated requests with a body.
    /// </summary>
    /// <typeparam name="T">Type of the content</typeparam>
    /// <param name="method">HTTP method</param>
    /// <param name="requestUri">Request URI</param>
    /// <param name="sessionToken">The raw session token</param>
    /// <param name="content">Content to serialize as JSON</param>
    /// <returns>Configured HttpRequestMessage with authentication and content</returns>
    public static HttpRequestMessage CreateAuthenticatedRequest<T>(
        HttpMethod method,
        string requestUri,
        string sessionToken,
        T content)
    {
        var request = CreateAuthenticatedRequest(method, requestUri, sessionToken);
        request.Content = JsonContent.Create(content);
        return request;
    }

}
