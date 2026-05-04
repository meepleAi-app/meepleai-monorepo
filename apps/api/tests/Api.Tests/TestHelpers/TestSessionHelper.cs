using System.Net.Http.Json;
using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
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
    /// Creates a super admin user and session in the database, returning the raw token for HTTP requests.
    /// </summary>
    /// <param name="dbContext">The database context to use</param>
    /// <param name="userId">Optional user ID (generates new if not provided)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tuple of (UserId, RawSessionToken) for use in HTTP cookie header</returns>
    public static async Task<(Guid UserId, string RawToken)> CreateSuperAdminSessionAsync(
        MeepleAiDbContext dbContext,
        Guid? userId = null,
        CancellationToken cancellationToken = default)
    {
        return await CreateSessionAsync(dbContext, "SuperAdmin", userId, cancellationToken);
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
                EmailVerified = true, // Test users are pre-verified to bypass EmailVerificationMiddleware
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

    /// <summary>
    /// Seeds AgentDefinition aggregates into the test database.
    /// Issue #641 (Wave B.2 hotfix): used by AgentsEndpoints integration tests to verify activeOnly filter.
    /// </summary>
    /// <param name="dbContext">Test database context.</param>
    /// <param name="activeCount">Number of active agents to seed (calls Activate() after Create()).</param>
    /// <param name="inactiveCount">Number of inactive agents to seed (default Draft state from Create()).</param>
    /// <param name="gameId">
    /// Optional GameId association for all seeded agents. Default null (system-wide agents).
    /// Required parameter for Phase 6 future drift fix coexistence (AgentDto.GameName).
    /// </param>
    public static async Task SeedAgentDefinitionsAsync(
        MeepleAiDbContext dbContext,
        int activeCount,
        int inactiveCount,
        Guid? gameId = null)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        for (var i = 0; i < activeCount; i++)
        {
            var agent = AgentDefinition.Create(
                name: $"Active Agent {i}",
                description: $"Test active agent #{i}",
                type: AgentType.RagAgent,
                config: AgentDefinitionConfig.Create("gpt-4", 1000, 0.7f));
            agent.Activate();

            if (gameId.HasValue)
            {
                // Issue #660: Use SetGameId domain method to associate seeded agents with a SharedGame.
                agent.SetGameId(gameId.Value);
            }

            dbContext.AgentDefinitions.Add(agent);
        }

        for (var i = 0; i < inactiveCount; i++)
        {
            var agent = AgentDefinition.Create(
                name: $"Inactive Agent {i}",
                description: $"Test inactive agent #{i}",
                type: AgentType.RulesInterpreter,
                config: AgentDefinitionConfig.Create("gpt-4", 1000, 0.7f));
            // Default state from Create() is IsActive=false / Status=Draft

            if (gameId.HasValue)
            {
                agent.SetGameId(gameId.Value);
            }

            dbContext.AgentDefinitions.Add(agent);
        }

        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Seeds AgentDefinition aggregates by Status for integration tests.
    /// Issue #649: used by AgentTypologiesEndpoints integration tests to verify PublishedOnly filter.
    /// Published path: Create() → StartTesting() → Publish() (auto-sets IsActive=true).
    /// Draft path: Create() only (default state IsActive=false / Status=Draft).
    /// </summary>
    /// <param name="dbContext">Test database context.</param>
    /// <param name="publishedCount">Number of Published agents to seed.</param>
    /// <param name="draftCount">Number of Draft agents to seed.</param>
    public static async Task SeedAgentDefinitionsByStatusAsync(
        MeepleAiDbContext dbContext,
        int publishedCount,
        int draftCount)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        for (var i = 0; i < publishedCount; i++)
        {
            var agent = AgentDefinition.Create(
                name: $"Published Agent {i}",
                description: $"Test published agent #{i}",
                type: AgentType.RagAgent,
                config: AgentDefinitionConfig.Create("gpt-4", 1000, 0.7f));
            agent.StartTesting();
            agent.Publish();

            dbContext.AgentDefinitions.Add(agent);
        }

        for (var i = 0; i < draftCount; i++)
        {
            var agent = AgentDefinition.Create(
                name: $"Draft Agent {i}",
                description: $"Test draft agent #{i}",
                type: AgentType.RulesInterpreter,
                config: AgentDefinitionConfig.Create("gpt-4", 1000, 0.7f));
            // Default state from Create() is IsActive=false / Status=Draft

            dbContext.AgentDefinitions.Add(agent);
        }

        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Seeds a SharedGame entity with the given title for integration tests.
    /// Issue #660: Used by AgentsEndpointsIntegrationTests to assert AgentDto.GameName population
    /// when an agent definition is linked to a game in the SharedGame catalog.
    /// </summary>
    /// <param name="dbContext">Test database context.</param>
    /// <param name="title">SharedGame title (e.g. "Catan").</param>
    /// <returns>The newly seeded SharedGame ID.</returns>
    public static async Task<Guid> SeedSharedGameAsync(
        MeepleAiDbContext dbContext,
        string title)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title cannot be empty", nameof(title));

        var sharedGameId = Guid.NewGuid();
        dbContext.SharedGames.Add(new SharedGameEntity
        {
            Id = sharedGameId,
            Title = title,
            Description = "Integration test game",
            ImageUrl = string.Empty,
            ThumbnailUrl = string.Empty,
            YearPublished = 2024,
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 45,
            MinAge = 8,
            Status = 1, // Approved — queryable in default ISharedGameRepository.GetByIds/Names paths
            CreatedBy = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
        });
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        return sharedGameId;
    }
}
