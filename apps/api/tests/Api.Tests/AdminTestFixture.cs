using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Api.Tests.Fixtures;

namespace Api.Tests;

/// <summary>
/// Base class for admin endpoint integration tests providing shared helpers and utilities.
/// Implements common authentication, seeding, and HTTP client management functionality.
/// Uses IAsyncLifetime for proper test isolation and resource cleanup.
/// </summary>
public abstract class AdminTestFixture : IClassFixture<WebApplicationFactoryFixture>, IAsyncLifetime
{
    protected static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    protected readonly WebApplicationFactoryFixture Factory;
    private readonly List<string> _testUserIds = new();
    private readonly List<string> _testConfigIds = new();

    protected AdminTestFixture(PostgresCollectionFixture postgresFixture, WebApplicationFactoryFixture factory)
    {
        factory.PostgresConnectionString = postgresFixture.ConnectionString;
        Factory = factory;
    }

    /// <summary>
    /// Initialize test resources before each test.
    /// Override in derived classes for custom setup.
    /// </summary>
    public virtual ValueTask InitializeAsync()
    {
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Cleanup test resources after each test.
    /// Automatically cleans up test users, configs, logs, and feedback.
    /// </summary>
    public virtual async ValueTask DisposeAsync()
    {
        try
        {
            using var scope = Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

            // Cleanup test data in reverse dependency order
            if (_testUserIds.Count > 0)
            {
                var userIdSet = _testUserIds.ToHashSet();

                // Remove audit and credential artifacts created by test users
                var auditLogs = await db.AuditLogs
                    .Where(l => l.UserId != null && userIdSet.Contains(l.UserId))
                    .ToListAsync();
                db.AuditLogs.RemoveRange(auditLogs);

                var apiKeys = await db.ApiKeys
                    .Where(k => userIdSet.Contains(k.UserId))
                    .ToListAsync();
                db.ApiKeys.RemoveRange(apiKeys);

                var oauthAccounts = await db.OAuthAccounts
                    .Where(a => userIdSet.Contains(a.UserId))
                    .ToListAsync();
                db.OAuthAccounts.RemoveRange(oauthAccounts);

                var passwordResetTokens = await db.PasswordResetTokens
                    .Where(t => userIdSet.Contains(t.UserId))
                    .ToListAsync();
                db.PasswordResetTokens.RemoveRange(passwordResetTokens);

                var backupCodes = await db.UserBackupCodes
                    .Where(c => userIdSet.Contains(c.UserId))
                    .ToListAsync();
                db.UserBackupCodes.RemoveRange(backupCodes);

                var tempSessions = await db.TempSessions
                    .Where(t => userIdSet.Contains(t.UserId))
                    .ToListAsync();
                db.TempSessions.RemoveRange(tempSessions);

                // Remove chat data created during tests
                var chatIds = await db.Chats
                    .Where(c => userIdSet.Contains(c.UserId))
                    .Select(c => c.Id)
                    .ToListAsync();

                if (chatIds.Count > 0)
                {
                    var chatLogs = await db.ChatLogs
                        .Where(l =>
                            chatIds.Contains(l.ChatId) ||
                            (l.UserId != null && userIdSet.Contains(l.UserId)) ||
                            (l.DeletedByUserId != null && userIdSet.Contains(l.DeletedByUserId)))
                        .ToListAsync();
                    db.ChatLogs.RemoveRange(chatLogs);

                    var chats = await db.Chats
                        .Where(c => chatIds.Contains(c.Id))
                        .ToListAsync();
                    db.Chats.RemoveRange(chats);
                }
                else
                {
                    var standaloneChatLogs = await db.ChatLogs
                        .Where(l =>
                            (l.UserId != null && userIdSet.Contains(l.UserId)) ||
                            (l.DeletedByUserId != null && userIdSet.Contains(l.DeletedByUserId)))
                        .ToListAsync();
                    db.ChatLogs.RemoveRange(standaloneChatLogs);
                }

                // Remove rule comments and threaded replies linked to test users
                var commentIds = await db.RuleSpecComments
                    .Where(c => userIdSet.Contains(c.UserId) || (c.ResolvedByUserId != null && userIdSet.Contains(c.ResolvedByUserId)))
                    .Select(c => c.Id)
                    .ToListAsync();

                if (commentIds.Count > 0)
                {
                    var cascadeCommentIds = new HashSet<Guid>(commentIds);
                    bool added;
                    do
                    {
                        var childIds = await db.RuleSpecComments
                            .Where(c => c.ParentCommentId != null && cascadeCommentIds.Contains(c.ParentCommentId.Value))
                            .Select(c => c.Id)
                            .ToListAsync();

                        added = false;
                        foreach (var id in childIds)
                        {
                            if (cascadeCommentIds.Add(id))
                            {
                                added = true;
                            }
                        }
                    } while (added);

                    var comments = await db.RuleSpecComments
                        .Where(c => cascadeCommentIds.Contains(c.Id))
                        .ToListAsync();
                    db.RuleSpecComments.RemoveRange(comments);
                }

                // Remove PDF artifacts uploaded by test users
                var pdfIds = await db.PdfDocuments
                    .Where(p => userIdSet.Contains(p.UploadedByUserId))
                    .Select(p => p.Id)
                    .ToListAsync();

                if (pdfIds.Count > 0)
                {
                    var textChunks = await db.TextChunks
                        .Where(tc => pdfIds.Contains(tc.PdfDocumentId))
                        .ToListAsync();
                    db.TextChunks.RemoveRange(textChunks);

                    var vectorDocs = await db.VectorDocuments
                        .Where(v => pdfIds.Contains(v.PdfDocumentId))
                        .ToListAsync();
                    db.VectorDocuments.RemoveRange(vectorDocs);

                    var pdfs = await db.PdfDocuments
                        .Where(p => pdfIds.Contains(p.Id))
                        .ToListAsync();
                    db.PdfDocuments.RemoveRange(pdfs);
                }

                // Remove prompt assets owned by test users
                var promptAuditLogs = await db.PromptAuditLogs
                    .Where(p => userIdSet.Contains(p.ChangedByUserId))
                    .ToListAsync();
                db.PromptAuditLogs.RemoveRange(promptAuditLogs);

                var promptVersions = await db.PromptVersions
                    .Where(p => userIdSet.Contains(p.CreatedByUserId))
                    .ToListAsync();
                db.PromptVersions.RemoveRange(promptVersions);

                var promptTemplates = await db.PromptTemplates
                    .Where(p => userIdSet.Contains(p.CreatedByUserId))
                    .ToListAsync();
                db.PromptTemplates.RemoveRange(promptTemplates);

                // Remove user sessions
                var sessions = await db.UserSessions
                    .Where(s => s.UserId != null && _testUserIds.Contains(s.UserId))
                    .ToListAsync();
                db.UserSessions.RemoveRange(sessions);

                // Remove AI request logs
                var logs = await db.AiRequestLogs
                    .Where(l => l.UserId != null && _testUserIds.Contains(l.UserId))
                    .ToListAsync();
                db.AiRequestLogs.RemoveRange(logs);

                // Remove agent feedback
                var feedback = await db.AgentFeedbacks
                    .Where(f => f.UserId != null && _testUserIds.Contains(f.UserId))
                    .ToListAsync();
                db.AgentFeedbacks.RemoveRange(feedback);

                // Remove system configurations created or updated by test users (DeleteBehavior.Restrict)
                var systemConfigs = await db.SystemConfigurations
                    .Where(c =>
                        _testUserIds.Contains(c.CreatedByUserId) ||
                        (c.UpdatedByUserId != null && _testUserIds.Contains(c.UpdatedByUserId)))
                    .ToListAsync();
                db.SystemConfigurations.RemoveRange(systemConfigs);

                // Remove users
                var users = await db.Users
                    .Where(u => _testUserIds.Contains(u.Id))
                    .ToListAsync();
                db.Users.RemoveRange(users);
            }

            // Cleanup N8n configs
            if (_testConfigIds.Count > 0)
            {
                var configs = await db.N8nConfigs
                    .Where(c => _testConfigIds.Contains(c.Id))
                    .ToListAsync();
                db.N8nConfigs.RemoveRange(configs);
            }

            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Log cleanup failure but don't fail the test
            Console.WriteLine($"Test cleanup warning: {ex.Message}");
        }
    }

    // ===== HTTP Client Helpers =====

    protected HttpClient CreateClientWithoutCookies()
    {
        return Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });
    }

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

    protected static List<string> ExtractCookies(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var values))
        {
            return new List<string>();
        }

        return values
            .Select(value => value.Split(';')[0])
            .ToList();
    }

    // ===== Authentication Helpers =====

    protected async Task<List<string>> RegisterAndAuthenticateAsync(HttpClient client, string email, string role)
    {
        var payload = new RegisterPayload(Email: email, Password: "Password123!", DisplayName: "Integration Tester", Role: null);
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", payload);
        response.EnsureSuccessStatusCode();

        if (!string.Equals(role, UserRole.User.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            var parsedRole = Enum.Parse<UserRole>(role, true);
            await PromoteUserAsync(email, parsedRole);
        }

        // Track user ID for cleanup
        var userId = await GetUserIdByEmailAsync(email);
        _testUserIds.Add(userId);

        return ExtractCookies(response);
    }

    protected async Task<string> GetUserIdByEmailAsync(string email)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == email);
        return user.Id;
    }

    protected async Task PromoteUserAsync(string email, UserRole role)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == email);
        user.Role = role;
        await db.SaveChangesAsync();
    }

    // ===== Dashboard/Stats Data Seeding =====

    protected async Task<DashboardSeedContext> SeedDashboardDataAsync(string adminUserId, string otherUserId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        db.AiRequestLogs.RemoveRange(db.AiRequestLogs);
        db.AgentFeedbacks.RemoveRange(db.AgentFeedbacks);
        await db.SaveChangesAsync();

        var baseTime = DateTime.UtcNow;

        var adminSuccess = new AiRequestLogEntity
        {
            UserId = adminUserId,
            GameId = "game-1",
            Endpoint = "qa",
            Query = "How to score points?",
            ResponseSnippet = "Prioritize engine building.",
            LatencyMs = 120,
            TokenCount = 50,
            PromptTokens = 30,
            CompletionTokens = 20,
            Confidence = 0.9,
            Status = "Success",
            IpAddress = "127.0.0.1",
            UserAgent = "integration-test/1.0",
            Model = "gpt-4",
            FinishReason = "stop",
            CreatedAt = baseTime.AddMinutes(-30)
        };

        var adminError = new AiRequestLogEntity
        {
            UserId = adminUserId,
            GameId = "game-1",
            Endpoint = "setup",
            Query = "How do I set up?",
            ResponseSnippet = "Unable to answer.",
            LatencyMs = 80,
            TokenCount = 30,
            PromptTokens = 18,
            CompletionTokens = 12,
            Confidence = 0.5,
            Status = "Error",
            ErrorMessage = "timeout",
            IpAddress = "127.0.0.1",
            UserAgent = "integration-test/2.0",
            Model = "gpt-4",
            FinishReason = "length",
            CreatedAt = baseTime.AddMinutes(-20)
        };

        var otherLog = new AiRequestLogEntity
        {
            UserId = otherUserId,
            GameId = "game-2",
            Endpoint = "qa",
            Query = "Unrelated question",
            ResponseSnippet = "Different game response.",
            LatencyMs = 60,
            TokenCount = 20,
            PromptTokens = 10,
            CompletionTokens = 10,
            Confidence = 0.8,
            Status = "Success",
            IpAddress = "192.168.1.10",
            UserAgent = "integration-test/3.0",
            Model = "gpt-3.5",
            FinishReason = "stop",
            CreatedAt = baseTime.AddMinutes(-10)
        };

        db.AiRequestLogs.AddRange(adminSuccess, adminError, otherLog);

        var helpfulFeedback = new AgentFeedbackEntity
        {
            MessageId = "msg-helpful",
            Endpoint = "qa",
            GameId = "game-1",
            UserId = adminUserId,
            Outcome = "helpful",
            CreatedAt = baseTime.AddMinutes(-25),
            UpdatedAt = baseTime.AddMinutes(-25)
        };

        var notHelpfulFeedback = new AgentFeedbackEntity
        {
            MessageId = "msg-not-helpful",
            Endpoint = "setup",
            GameId = "game-1",
            UserId = adminUserId,
            Outcome = "not-helpful",
            CreatedAt = baseTime.AddMinutes(-15),
            UpdatedAt = baseTime.AddMinutes(-15)
        };

        var otherFeedback = new AgentFeedbackEntity
        {
            MessageId = "msg-other",
            Endpoint = "qa",
            GameId = "game-2",
            UserId = otherUserId,
            Outcome = "helpful",
            CreatedAt = baseTime.AddMinutes(-5),
            UpdatedAt = baseTime.AddMinutes(-5)
        };

        db.AgentFeedbacks.AddRange(helpfulFeedback, notHelpfulFeedback, otherFeedback);

        await db.SaveChangesAsync();

        return new DashboardSeedContext(
            baseTime.AddMinutes(-35),
            baseTime.AddMinutes(-15));
    }

    protected sealed record DashboardSeedContext(DateTime StartDate, DateTime EndDate);

    // ===== N8n Configuration Helpers =====

    protected async Task ClearN8nConfigsAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        db.N8nConfigs.RemoveRange(db.N8nConfigs);
        await db.SaveChangesAsync();
    }

    protected async Task<N8nConfigDto> CreateN8nConfigAsync(string userId, string name)
    {
        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<N8nConfigService>();
        var config = await service.CreateConfigAsync(
            userId,
            new CreateN8nConfigRequest(
                name,
                "https://n8n.seed/",
                "seed-api-key",
                "https://n8n.seed/webhook"),
            default);

        // Track config ID for cleanup
        _testConfigIds.Add(config.Id);

        return config;
    }
}
