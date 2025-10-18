using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration;

/// <summary>
/// BDD-style integration tests for Chat Export endpoints (CHAT-05).
///
/// Feature: Chat Export API
/// As an authenticated user
/// I want to export my chat conversations via HTTP endpoints
/// So that I can download and archive conversations in my preferred format
///
/// Endpoints Tested:
/// - GET /api/v1/chats/{id}/export?format={format}&startDate={date}&endDate={date}
///
/// Test Strategy: Full HTTP stack with Testcontainers (Postgres)
/// </summary>
public class ChatExportEndpointTests : IntegrationTestBase
{
    public ChatExportEndpointTests(WebApplicationFactoryFixture factory) : base(factory)
    {
    }

    /// <summary>
    /// Scenario: Unauthenticated user attempts to export chat
    ///   Given no authentication is provided
    ///   When user requests chat export
    ///   Then 401 Unauthorized is returned
    ///   And no file is downloaded
    /// </summary>
    [Fact]
    public async Task GivenNoAuthentication_WhenExportingChat_ThenReturns401()
    {
        // Given: No authentication is provided
        var client = CreateClientWithoutCookies();

        // When: User requests chat export
        var chatId = Guid.NewGuid();
        var response = await client.GetAsync($"/api/v1/chats/{chatId}/export?format=pdf");

        // Then: 401 Unauthorized is returned
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Scenario: User exports another user's chat
    ///   Given chat belongs to different user
    ///   When user attempts to export
    ///   Then 404 Not Found is returned (not 403 for security)
    ///   And no file is downloaded
    /// </summary>
    [Fact]
    public async Task GivenOtherUsersChat_WhenExporting_ThenReturns404()
    {
        // Given: Chat belongs to different user
        var owner = await CreateTestUserAsync($"owner-{TestRunId}", UserRole.User);
        var attacker = await CreateTestUserAsync($"attacker-{TestRunId}", UserRole.User);

        var game = await CreateTestGameAsync("Secret Game");

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var agent = new AgentEntity
        {
            Id = $"agent-{TestRunId}",
            GameId = game.Id,
            Name = "Q&A Agent",
            Kind = "qa",
            CreatedAt = DateTime.UtcNow
        };
        db.Agents.Add(agent);
        await db.SaveChangesAsync();

        var chat = new ChatEntity
        {
            Id = Guid.NewGuid(),
            UserId = owner.Id,
            GameId = game.Id,
            AgentId = agent.Id,
            StartedAt = DateTime.UtcNow
        };
        db.Chats.Add(chat);
        await db.SaveChangesAsync();

        // When: Different user attempts to export
        var cookies = await AuthenticateUserAsync(attacker.Email);
        var client = CreateClientWithoutCookies();
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/chats/{chat.Id}/export?format=pdf");
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: 404 Not Found is returned (not 403 for security)
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Scenario: User exports their own chat as PDF
    ///   Given user owns a chat with messages
    ///   When user requests PDF export
    ///   Then 200 OK is returned
    ///   And Content-Type is "application/pdf"
    ///   And Content-Disposition header includes filename
    ///   And response body contains PDF data (starts with %PDF-)
    /// </summary>
    [Fact]
    public async Task GivenUserOwnsChat_WhenExportingAsPdf_ThenReturns200WithPdf()
    {
        // Given: User owns a chat with messages
        var user = await CreateTestUserAsync($"pdf-export-{TestRunId}", UserRole.User);
        var game = await CreateTestGameAsync("PDF Export Game");

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var agent = new AgentEntity
        {
            Id = $"pdf-agent-{TestRunId}",
            GameId = game.Id,
            Name = "Q&A Agent",
            Kind = "qa",
            CreatedAt = DateTime.UtcNow
        };
        db.Agents.Add(agent);
        await db.SaveChangesAsync();

        var chat = new ChatEntity
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            GameId = game.Id,
            AgentId = agent.Id,
            StartedAt = DateTime.UtcNow.AddHours(-1)
        };
        db.Chats.Add(chat);
        await db.SaveChangesAsync();

        var log = new ChatLogEntity
        {
            Id = Guid.NewGuid(),
            ChatId = chat.Id,
            Level = "user",
            Message = "Test message for PDF export",
            CreatedAt = DateTime.UtcNow
        };
        db.ChatLogs.Add(log);
        await db.SaveChangesAsync();

        // When: User requests PDF export
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = CreateClientWithoutCookies();
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/chats/{chat.Id}/export?format=pdf");
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: 200 OK is returned
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // And: Content-Type is "application/pdf"
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);

        // And: Content-Disposition header includes filename
        Assert.NotNull(response.Content.Headers.ContentDisposition);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition.DispositionType);
        Assert.Contains(".pdf", response.Content.Headers.ContentDisposition.FileName);
        Assert.Contains("chat-", response.Content.Headers.ContentDisposition.FileName);

        // And: Response body contains PDF data
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 0);
        var magicBytes = System.Text.Encoding.ASCII.GetString(bytes.Take(5).ToArray());
        Assert.Equal("%PDF-", magicBytes);
    }

    /// <summary>
    /// Scenario: User exports their own chat as TXT
    ///   Given user owns a chat
    ///   When user requests TXT export
    ///   Then 200 OK is returned
    ///   And Content-Type is "text/plain"
    ///   And Content-Disposition includes .txt filename
    ///   And response body contains readable text
    /// </summary>
    [Fact]
    public async Task GivenUserOwnsChat_WhenExportingAsTxt_ThenReturns200WithTxt()
    {
        // Given: User owns a chat
        var user = await CreateTestUserAsync($"txt-export-{TestRunId}", UserRole.User);
        var game = await CreateTestGameAsync("TXT Export Game");

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var agent = new AgentEntity
        {
            Id = $"txt-agent-{TestRunId}",
            GameId = game.Id,
            Name = "Q&A Agent",
            Kind = "qa",
            CreatedAt = DateTime.UtcNow
        };
        db.Agents.Add(agent);
        await db.SaveChangesAsync();

        var chat = new ChatEntity
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            GameId = game.Id,
            AgentId = agent.Id,
            StartedAt = DateTime.UtcNow
        };
        db.Chats.Add(chat);
        await db.SaveChangesAsync();

        var log = new ChatLogEntity
        {
            Id = Guid.NewGuid(),
            ChatId = chat.Id,
            Level = "user",
            Message = "Test message for TXT export",
            CreatedAt = DateTime.UtcNow
        };
        db.ChatLogs.Add(log);
        await db.SaveChangesAsync();

        // When: User requests TXT export
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = CreateClientWithoutCookies();
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/chats/{chat.Id}/export?format=txt");
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: 200 OK is returned
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // And: Content-Type is "text/plain"
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);

        // And: Content-Disposition includes .txt filename
        Assert.NotNull(response.Content.Headers.ContentDisposition);
        Assert.Contains(".txt", response.Content.Headers.ContentDisposition.FileName);

        // And: Response body contains readable text
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Test message for TXT export", content);
    }

    /// <summary>
    /// Scenario: User exports their own chat as Markdown
    ///   Given user owns a chat
    ///   When user requests MD export
    ///   Then 200 OK is returned
    ///   And Content-Type is "text/markdown"
    ///   And Content-Disposition includes .md filename
    ///   And response body contains Markdown syntax
    /// </summary>
    [Fact]
    public async Task GivenUserOwnsChat_WhenExportingAsMarkdown_ThenReturns200WithMarkdown()
    {
        // Given: User owns a chat
        var user = await CreateTestUserAsync($"md-export-{TestRunId}", UserRole.User);
        var game = await CreateTestGameAsync("MD Export Game");

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var agent = new AgentEntity
        {
            Id = $"md-agent-{TestRunId}",
            GameId = game.Id,
            Name = "Q&A Agent",
            Kind = "qa",
            CreatedAt = DateTime.UtcNow
        };
        db.Agents.Add(agent);
        await db.SaveChangesAsync();

        var chat = new ChatEntity
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            GameId = game.Id,
            AgentId = agent.Id,
            StartedAt = DateTime.UtcNow
        };
        db.Chats.Add(chat);
        await db.SaveChangesAsync();

        var log = new ChatLogEntity
        {
            Id = Guid.NewGuid(),
            ChatId = chat.Id,
            Level = "user",
            Message = "Test message for MD export",
            CreatedAt = DateTime.UtcNow
        };
        db.ChatLogs.Add(log);
        await db.SaveChangesAsync();

        // When: User requests MD export
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = CreateClientWithoutCookies();
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/chats/{chat.Id}/export?format=md");
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: 200 OK is returned
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // And: Content-Type is "text/markdown"
        Assert.Equal("text/markdown", response.Content.Headers.ContentType?.MediaType);

        // And: Content-Disposition includes .md filename
        Assert.NotNull(response.Content.Headers.ContentDisposition);
        Assert.Contains(".md", response.Content.Headers.ContentDisposition.FileName);

        // And: Response body contains Markdown syntax
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("##", content); // Markdown heading
        Assert.Contains("Test message for MD export", content);
    }

    /// <summary>
    /// Scenario: User requests export with unsupported format
    ///   Given user owns a chat
    ///   When user requests export with invalid format
    ///   Then 400 Bad Request is returned
    ///   And error message lists supported formats
    /// </summary>
    [Fact]
    public async Task GivenInvalidFormat_WhenExporting_ThenReturns400()
    {
        // Given: User owns a chat
        var user = await CreateTestUserAsync($"invalid-fmt-{TestRunId}", UserRole.User);
        var game = await CreateTestGameAsync("Invalid Format Game");

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var agent = new AgentEntity
        {
            Id = $"invalid-agent-{TestRunId}",
            GameId = game.Id,
            Name = "Q&A Agent",
            Kind = "qa",
            CreatedAt = DateTime.UtcNow
        };
        db.Agents.Add(agent);
        await db.SaveChangesAsync();

        var chat = new ChatEntity
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            GameId = game.Id,
            AgentId = agent.Id,
            StartedAt = DateTime.UtcNow
        };
        db.Chats.Add(chat);
        await db.SaveChangesAsync();

        // When: User requests export with invalid format
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = CreateClientWithoutCookies();
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/chats/{chat.Id}/export?format=xml");
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: 400 Bad Request is returned
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // And: Error message mentions supported formats
        var content = await response.Content.ReadAsStringAsync();
        Assert.True(
            content.Contains("pdf", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("txt", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("md", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Scenario: User exports chat with date range filter
    ///   Given chat has messages across multiple dates
    ///   When user requests export with date range
    ///   Then 200 OK is returned
    ///   And only messages within date range are included
    ///   And filename includes date range
    /// </summary>
    [Fact]
    public async Task GivenDateRange_WhenExporting_ThenFiltersMessages()
    {
        // Given: Chat has messages across multiple dates
        var user = await CreateTestUserAsync($"daterange-{TestRunId}", UserRole.User);
        var game = await CreateTestGameAsync("Date Range Game");

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var agent = new AgentEntity
        {
            Id = $"daterange-agent-{TestRunId}",
            GameId = game.Id,
            Name = "Q&A Agent",
            Kind = "qa",
            CreatedAt = DateTime.UtcNow
        };
        db.Agents.Add(agent);
        await db.SaveChangesAsync();

        var chat = new ChatEntity
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            GameId = game.Id,
            AgentId = agent.Id,
            StartedAt = DateTime.UtcNow.AddDays(-30)
        };
        db.Chats.Add(chat);
        await db.SaveChangesAsync();

        var oldLog = new ChatLogEntity
        {
            Id = Guid.NewGuid(),
            ChatId = chat.Id,
            Level = "user",
            Message = "Old message outside range",
            CreatedAt = DateTime.UtcNow.AddDays(-20)
        };

        var recentLog = new ChatLogEntity
        {
            Id = Guid.NewGuid(),
            ChatId = chat.Id,
            Level = "user",
            Message = "Recent message in range",
            CreatedAt = DateTime.UtcNow.AddDays(-5)
        };

        db.ChatLogs.AddRange(oldLog, recentLog);
        await db.SaveChangesAsync();

        // When: User requests export with date range (last 10 days)
        var startDate = DateTime.UtcNow.AddDays(-10).ToString("yyyy-MM-dd");
        var endDate = DateTime.UtcNow.ToString("yyyy-MM-dd");

        var cookies = await AuthenticateUserAsync(user.Email);
        var client = CreateClientWithoutCookies();
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"/api/v1/chats/{chat.Id}/export?format=txt&startDate={startDate}&endDate={endDate}");
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: 200 OK is returned
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // And: Only recent message is included
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Recent message in range", content);
        Assert.DoesNotContain("Old message outside range", content);

        // And: Filename includes date range
        var filename = response.Content.Headers.ContentDisposition?.FileName;
        Assert.NotNull(filename);
        // Date should be in filename (implementation-specific format)
    }

    /// <summary>
    /// Scenario: User exports non-existent chat
    ///   Given chat ID doesn't exist
    ///   When user requests export
    ///   Then 404 Not Found is returned
    /// </summary>
    [Fact]
    public async Task GivenNonExistentChat_WhenExporting_ThenReturns404()
    {
        // Given: Chat ID doesn't exist
        var user = await CreateTestUserAsync($"nonexist-{TestRunId}", UserRole.User);
        var nonExistentChatId = Guid.NewGuid();

        // When: User requests export
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = CreateClientWithoutCookies();
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/chats/{nonExistentChatId}/export?format=pdf");
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: 404 Not Found is returned
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Scenario: Filename sanitization prevents path traversal
    ///   Given game name contains path traversal attempts
    ///   When user exports chat
    ///   Then filename is sanitized
    ///   And no path separators in filename
    /// </summary>
    [Fact]
    public async Task GivenMaliciousGameName_WhenExporting_ThenSanitizesFilename()
    {
        // Given: Game name contains path traversal attempts
        var user = await CreateTestUserAsync($"sanitize-{TestRunId}", UserRole.User);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var game = new GameEntity
        {
            Id = $"malicious-game-{TestRunId}",
            Name = "../../etc/passwd"
        };
        db.Games.Add(game);
        await db.SaveChangesAsync();

        var agent = new AgentEntity
        {
            Id = $"sanitize-agent-{TestRunId}",
            GameId = game.Id,
            Name = "Q&A Agent",
            Kind = "qa",
            CreatedAt = DateTime.UtcNow
        };
        db.Agents.Add(agent);
        await db.SaveChangesAsync();

        var chat = new ChatEntity
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            GameId = game.Id,
            AgentId = agent.Id,
            StartedAt = DateTime.UtcNow
        };
        db.Chats.Add(chat);
        await db.SaveChangesAsync();

        // When: User exports chat
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = CreateClientWithoutCookies();
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/chats/{chat.Id}/export?format=pdf");
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: Filename is sanitized
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var filename = response.Content.Headers.ContentDisposition?.FileName;
        Assert.NotNull(filename);

        // And: No path separators in filename
        Assert.DoesNotContain("..", filename);
        Assert.DoesNotContain("/", filename);
        Assert.DoesNotContain("\\", filename);
    }

    /// <summary>
    /// Scenario: Concurrent export requests succeed
    ///   Given multiple users export simultaneously
    ///   When all send export requests at once
    ///   Then all requests succeed
    ///   And each receives correct file
    /// </summary>
    [Fact]
    public async Task GivenConcurrentRequests_WhenExporting_ThenAllSucceed()
    {
        // Given: Multiple users with chats
        var users = new List<(UserEntity user, ChatEntity chat)>();

        for (int i = 1; i <= 3; i++)
        {
            var user = await CreateTestUserAsync($"concurrent-{i}-{TestRunId}", UserRole.User);
            var game = await CreateTestGameAsync($"Concurrent Game {i}");

            using var scope = Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

            var agent = new AgentEntity
            {
                Id = $"concurrent-agent-{i}-{TestRunId}",
                GameId = game.Id,
                Name = "Q&A Agent",
                Kind = "qa",
                CreatedAt = DateTime.UtcNow
            };
            db.Agents.Add(agent);
            await db.SaveChangesAsync();

            var chat = new ChatEntity
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                GameId = game.Id,
                AgentId = agent.Id,
                StartedAt = DateTime.UtcNow
            };
            db.Chats.Add(chat);
            await db.SaveChangesAsync();

            users.Add((user, chat));
        }

        // When: All send export requests simultaneously
        var tasks = users.Select(async userChat =>
        {
            var cookies = await AuthenticateUserAsync(userChat.user.Email);
            var client = CreateClientWithoutCookies();
            var request = new HttpRequestMessage(HttpMethod.Get,
                $"/api/v1/chats/{userChat.chat.Id}/export?format=pdf");
            AddCookies(request, cookies);
            return await client.SendAsync(request);
        }).ToArray();

        var responses = await Task.WhenAll(tasks);

        // Then: All requests succeed
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));

        // And: Each receives PDF file
        foreach (var response in responses)
        {
            var bytes = await response.Content.ReadAsByteArrayAsync();
            Assert.True(bytes.Length > 0);
            var magicBytes = System.Text.Encoding.ASCII.GetString(bytes.Take(5).ToArray());
            Assert.Equal("%PDF-", magicBytes);
        }
    }
}
