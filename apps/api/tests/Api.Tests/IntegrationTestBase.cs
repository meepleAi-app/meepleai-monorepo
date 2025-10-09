using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests;

/// <summary>
/// Base class for integration tests providing automatic test isolation and cleanup.
/// Implements IAsyncLifetime pattern for guaranteed setup/cleanup of test data.
///
/// BDD Scenarios Covered:
/// - Scenario: Test creates data and automatic cleanup happens
/// - Scenario: Test runs independently without data leakage from other tests
/// - Scenario: Test fails but cleanup still executes
/// - Scenario: Tests run in parallel without conflicts
///
/// Usage:
/// <code>
/// public class MyEndpointTests : IntegrationTestBase
/// {
///     public MyEndpointTests(WebApplicationFactoryFixture factory) : base(factory) { }
///
///     [Fact]
///     public async Task Should_CreateGame_WhenValidRequest()
///     {
///         // Given: User is authenticated
///         var user = await CreateTestUserAsync("test-user");
///         var cookies = await AuthenticateUserAsync(user.Email);
///
///         // When: Creating game
///         var game = await CreateTestGameAsync("Catan", user.Id);
///
///         // Then: Game is created
///         Assert.NotNull(game);
///         // Cleanup happens automatically via DisposeAsync
///     }
/// }
/// </code>
/// </summary>
public abstract class IntegrationTestBase : IClassFixture<WebApplicationFactoryFixture>, IAsyncLifetime
{
    protected readonly WebApplicationFactoryFixture Factory;
    protected readonly string TestRunId;

    // Tracked entities for automatic cleanup
    private readonly List<string> _testUserIds = new();
    private readonly List<string> _testGameIds = new();
    private readonly List<string> _testRuleSpecIds = new();
    private readonly List<string> _testPdfDocumentIds = new();
    private readonly List<string> _testChatIds = new();
    private readonly List<string> _testAgentIds = new();
    private readonly List<string> _testN8nConfigIds = new();

    protected IntegrationTestBase(WebApplicationFactoryFixture factory)
    {
        Factory = factory;
        TestRunId = Guid.NewGuid().ToString("N")[..8]; // Short unique ID for test run
    }

    /// <summary>
    /// Initialize test resources before each test.
    /// Override in derived classes for custom setup.
    ///
    /// BDD: Given test is starting
    /// </summary>
    public virtual Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Cleanup test resources after each test.
    /// Automatically removes all tracked entities in correct dependency order.
    ///
    /// BDD Scenarios:
    /// - When test completes (passed or failed)
    /// - Then all created test data is removed
    /// - And database returns to clean state
    /// </summary>
    public virtual async Task DisposeAsync()
    {
        try
        {
            using var scope = Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

            // Cleanup in reverse dependency order to avoid FK constraint violations

            // 1. Remove chats (depends on users and games)
            if (_testChatIds.Count > 0)
            {
                var chatGuids = _testChatIds.Select(Guid.Parse).ToList();

                var chatLogs = await db.ChatLogs
                    .Where(cl => chatGuids.Contains(cl.ChatId))
                    .ToListAsync();
                db.ChatLogs.RemoveRange(chatLogs);

                var chats = await db.Chats
                    .Where(c => chatGuids.Contains(c.Id))
                    .ToListAsync();
                db.Chats.RemoveRange(chats);
            }

            // 2. Remove AI request logs and feedback (depends on users)
            if (_testUserIds.Count > 0)
            {
                var logs = await db.AiRequestLogs
                    .Where(l => l.UserId != null && _testUserIds.Contains(l.UserId))
                    .ToListAsync();
                db.AiRequestLogs.RemoveRange(logs);

                var feedback = await db.AgentFeedbacks
                    .Where(f => f.UserId != null && _testUserIds.Contains(f.UserId))
                    .ToListAsync();
                db.AgentFeedbacks.RemoveRange(feedback);
            }

            // 3. Remove PDF documents (depends on games and users)
            if (_testPdfDocumentIds.Count > 0)
            {
                var vectorDocs = await db.VectorDocuments
                    .Where(v => v.PdfDocumentId != null && _testPdfDocumentIds.Contains(v.PdfDocumentId))
                    .ToListAsync();
                db.VectorDocuments.RemoveRange(vectorDocs);

                var pdfDocs = await db.PdfDocuments
                    .Where(p => _testPdfDocumentIds.Contains(p.Id))
                    .ToListAsync();
                db.PdfDocuments.RemoveRange(pdfDocs);
            }

            // 4. Remove RuleSpecs (depends on games)
            if (_testRuleSpecIds.Count > 0)
            {
                var ruleSpecGuids = _testRuleSpecIds.Select(Guid.Parse).ToList();

                var ruleSpecs = await db.RuleSpecs
                    .Where(r => ruleSpecGuids.Contains(r.Id))
                    .ToListAsync();
                db.RuleSpecs.RemoveRange(ruleSpecs);
            }

            // 5. Remove games
            if (_testGameIds.Count > 0)
            {
                var games = await db.Games
                    .Where(g => _testGameIds.Contains(g.Id))
                    .ToListAsync();
                db.Games.RemoveRange(games);
            }

            // 6. Remove agents and N8n configs (independent)
            if (_testAgentIds.Count > 0)
            {
                var agents = await db.Agents
                    .Where(a => _testAgentIds.Contains(a.Id))
                    .ToListAsync();
                db.Agents.RemoveRange(agents);
            }

            if (_testN8nConfigIds.Count > 0)
            {
                var configs = await db.N8nConfigs
                    .Where(c => _testN8nConfigIds.Contains(c.Id))
                    .ToListAsync();
                db.N8nConfigs.RemoveRange(configs);
            }

            // 7. Remove users and sessions (at the end)
            if (_testUserIds.Count > 0)
            {
                var sessions = await db.UserSessions
                    .Where(s => s.UserId != null && _testUserIds.Contains(s.UserId))
                    .ToListAsync();
                db.UserSessions.RemoveRange(sessions);

                var auditLogs = await db.AuditLogs
                    .Where(a => a.UserId != null && _testUserIds.Contains(a.UserId))
                    .ToListAsync();
                db.AuditLogs.RemoveRange(auditLogs);

                var users = await db.Users
                    .Where(u => _testUserIds.Contains(u.Id))
                    .ToListAsync();
                db.Users.RemoveRange(users);
            }

            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Log cleanup failure but don't fail the test
            // BDD: Even if cleanup fails, test result should not change
            Console.WriteLine($"Test cleanup warning: {ex.Message}");
        }
    }

    // ===== Manual Tracking Methods =====

    /// <summary>
    /// Manually tracks a user ID for cleanup.
    /// Use this when creating users via API instead of helper methods.
    ///
    /// BDD: And user ID is tracked for automatic cleanup
    /// </summary>
    protected void TrackUserId(string userId)
    {
        if (!_testUserIds.Contains(userId))
        {
            _testUserIds.Add(userId);
        }
    }

    /// <summary>
    /// Manually tracks a game ID for cleanup.
    /// Use this when creating games via API instead of helper methods.
    ///
    /// BDD: And game ID is tracked for automatic cleanup
    /// </summary>
    protected void TrackGameId(string gameId)
    {
        if (!_testGameIds.Contains(gameId))
        {
            _testGameIds.Add(gameId);
        }
    }

    /// <summary>
    /// Manually tracks a RuleSpec ID for cleanup.
    /// </summary>
    protected void TrackRuleSpecId(string ruleSpecId)
    {
        if (!_testRuleSpecIds.Contains(ruleSpecId))
        {
            _testRuleSpecIds.Add(ruleSpecId);
        }
    }

    /// <summary>
    /// Manually tracks a PDF document ID for cleanup.
    /// </summary>
    protected void TrackPdfDocumentId(string pdfDocumentId)
    {
        if (!_testPdfDocumentIds.Contains(pdfDocumentId))
        {
            _testPdfDocumentIds.Add(pdfDocumentId);
        }
    }

    // ===== Helper Methods for Test Data Creation =====

    /// <summary>
    /// Creates a test user with unique email and tracks it for cleanup.
    ///
    /// BDD: Given a test user with unique identifier
    /// </summary>
    protected async Task<UserEntity> CreateTestUserAsync(
        string username,
        UserRole role = UserRole.User,
        string? password = null)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var user = new UserEntity
        {
            Id = Guid.NewGuid().ToString(),
            Email = $"{username}-{TestRunId}@test.local",
            PasswordHash = HashPassword(password ?? "TestPassword123!"),
            DisplayName = $"Test {username}",
            Role = role,
            CreatedAt = DateTime.UtcNow
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        _testUserIds.Add(user.Id);
        return user;
    }

    /// <summary>
    /// Hashes a password using PBKDF2 (matches AuthService implementation).
    /// </summary>
    private static string HashPassword(string password)
    {
        const int iterations = 210_000;
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, 32);
        return $"v1.{iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    /// <summary>
    /// Creates a test game with unique ID and tracks it for cleanup.
    ///
    /// BDD: Given a test game with unique identifier
    /// </summary>
    protected async Task<GameEntity> CreateTestGameAsync(string gameName)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var game = new GameEntity
        {
            Id = $"{gameName.ToLowerInvariant()}-{TestRunId}",
            Name = gameName,
            CreatedAt = DateTime.UtcNow
        };

        db.Games.Add(game);
        await db.SaveChangesAsync();

        _testGameIds.Add(game.Id);
        return game;
    }

    /// <summary>
    /// Creates a test RuleSpec and tracks it for cleanup.
    ///
    /// BDD: Given a test rule specification with unique identifier
    /// </summary>
    protected async Task<RuleSpecEntity> CreateTestRuleSpecAsync(
        string gameId,
        string createdByUserId,
        string version = "1.0.0")
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var ruleSpec = new RuleSpecEntity
        {
            GameId = gameId,
            Version = version,
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTime.UtcNow
        };

        db.RuleSpecs.Add(ruleSpec);
        await db.SaveChangesAsync();

        _testRuleSpecIds.Add(ruleSpec.Id.ToString());
        return ruleSpec;
    }

    /// <summary>
    /// Creates a test PDF document and tracks it for cleanup.
    ///
    /// BDD: Given a test PDF document with unique identifier
    /// </summary>
    protected async Task<PdfDocumentEntity> CreateTestPdfDocumentAsync(
        string gameId,
        string uploadedByUserId,
        string filename = "test-rules.pdf")
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var pdf = new PdfDocumentEntity
        {
            Id = $"pdf-{TestRunId}-{Guid.NewGuid():N}",
            GameId = gameId,
            FileName = $"{filename}-{TestRunId}.pdf",
            FilePath = $"/test/{TestRunId}/{filename}",
            FileSizeBytes = 1024,
            UploadedByUserId = uploadedByUserId,
            UploadedAt = DateTime.UtcNow
        };

        db.PdfDocuments.Add(pdf);
        await db.SaveChangesAsync();

        _testPdfDocumentIds.Add(pdf.Id);
        return pdf;
    }

    /// <summary>
    /// Creates a test chat and tracks it for cleanup.
    ///
    /// BDD: Given a test chat session with unique identifier
    /// </summary>
    protected async Task<ChatEntity> CreateTestChatAsync(
        string gameId,
        string agentId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var chat = new ChatEntity
        {
            GameId = gameId,
            AgentId = agentId,
            StartedAt = DateTime.UtcNow
        };

        db.Chats.Add(chat);
        await db.SaveChangesAsync();

        _testChatIds.Add(chat.Id.ToString());
        return chat;
    }

    /// <summary>
    /// Authenticates a user via the API and returns cookies.
    ///
    /// BDD: When user authenticates with valid credentials
    /// </summary>
    protected async Task<List<string>> AuthenticateUserAsync(string email, string password = "TestPassword123!")
    {
        var client = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });

        var loginPayload = new { email, password };
        var response = await client.PostAsJsonAsync("/auth/login", loginPayload);
        response.EnsureSuccessStatusCode();

        if (!response.Headers.TryGetValues("Set-Cookie", out var values))
        {
            return new List<string>();
        }

        return values
            .Select(value => value.Split(';')[0])
            .ToList();
    }

    /// <summary>
    /// Creates an HTTP client without cookie handling.
    /// </summary>
    protected HttpClient CreateClientWithoutCookies()
    {
        return Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });
    }

    /// <summary>
    /// Adds cookies to an HTTP request.
    /// </summary>
    protected static void AddCookies(HttpRequestMessage request, IEnumerable<string> cookies)
    {
        var cookieList = cookies
            .Where(cookie => !string.IsNullOrWhiteSpace(cookie))
            .ToList();

        if (cookieList.Count == 0)
        {
            return;
        }

        request.Headers.Remove("Cookie");
        request.Headers.TryAddWithoutValidation("Cookie", string.Join("; ", cookieList));
    }
}
