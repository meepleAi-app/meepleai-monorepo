using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests;

/// <summary>
/// BDD-style integration tests for logs endpoint.
///
/// Feature: Admin logs viewing
/// As an admin
/// I want to view AI request logs via API
/// So that I can monitor system usage and errors
/// </summary>
public class LogsEndpointTests : IntegrationTestBase
{
    public LogsEndpointTests(WebApplicationFactoryFixture factory) : base(factory)
    {
    }

    /// <summary>
    /// Scenario: Admin views AI request logs
    ///   Given AI request logs exist in the database
    ///   And admin user is authenticated
    ///   When admin requests /logs
    ///   Then logs are returned sorted by newest first
    ///   And cleanup is automatic
    /// </summary>
    [Fact]
    public async Task GetLogs_ReturnsLatestEntriesFromAiRequestLogService()
    {
        // Given: AI request logs exist in the database
        var user1 = await CreateTestUserAsync("log-user-1");
        var user2 = await CreateTestUserAsync("log-user-2");
        var now = DateTime.UtcNow;

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        db.AiRequestLogs.AddRange(
            new AiRequestLogEntity
            {
                Id = $"req-001-{TestRunId}",
                UserId = user1.Id,
                GameId = "demo-chess",
                Endpoint = "qa",
                Query = "How many players?",
                ResponseSnippet = "Two players.",
                LatencyMs = 120,
                Status = "Success",
                CreatedAt = now.AddMinutes(-5)
            },
            new AiRequestLogEntity
            {
                Id = $"req-002-{TestRunId}",
                UserId = user2.Id,
                GameId = "demo-chess",
                Endpoint = "qa",
                Query = "Explain setup",
                LatencyMs = 240,
                Status = "Error",
                ErrorMessage = "LLM timeout",
                CreatedAt = now
            }
        );

        await db.SaveChangesAsync();

        // And: Admin user is authenticated
        var adminUser = await CreateTestUserAsync("admin-logs", UserRole.Admin);
        var cookies = await AuthenticateUserAsync(adminUser.Email);
        var client = CreateClientWithoutCookies();

        // When: Admin requests /logs
        var request = new HttpRequestMessage(HttpMethod.Get, "/logs");
        AddCookies(request, cookies);
        var response = await client.SendAsync(request);

        // Then: Logs are returned sorted by newest first
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var entries = await response.Content.ReadFromJsonAsync<List<LogEntryResponse>>();

        Assert.NotNull(entries);
        Assert.Equal(2, entries!.Count);

        var newest = entries[0];
        Assert.Equal($"req-002-{TestRunId}", newest.RequestId);
        Assert.Equal("ERROR", newest.Level);
        Assert.Equal("Explain setup", newest.Message);
        Assert.Equal(user2.Id, newest.UserId);
        Assert.Equal("demo-chess", newest.GameId);

        var older = entries[1];
        Assert.Equal($"req-001-{TestRunId}", older.RequestId);
        Assert.Equal("INFO", older.Level);
        Assert.Equal("Two players.", older.Message);
        // Cleanup happens automatically via DisposeAsync
    }

    /// <summary>
    /// Scenario: Unauthenticated user tries to view logs
    ///   Given user is not authenticated
    ///   When user requests /logs
    ///   Then request is unauthorized
    /// </summary>
    [Fact]
    public async Task GetLogs_ReturnsUnauthorizedWhenSessionMissing()
    {
        // Given: User is not authenticated
        using var client = Factory.CreateHttpsClient();

        // When: User requests /logs
        var response = await client.GetAsync("/logs");

        // Then: Request is unauthorized
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    public static IEnumerable<object[]> NonAdminRoles => new[]
    {
        new object[] { UserRole.Editor },
        new object[] { UserRole.User }
    };

    /// <summary>
    /// Scenario: Non-admin user tries to view logs
    ///   Given user with non-admin role is authenticated
    ///   When user requests /logs
    ///   Then request is forbidden
    ///   And cleanup is automatic
    /// </summary>
    [Theory]
    [MemberData(nameof(NonAdminRoles))]
    public async Task GetLogs_ReturnsForbiddenForNonAdminRoles(UserRole role)
    {
        // Given: User with non-admin role is authenticated
        var username = $"non-admin-{Guid.NewGuid():N}";
        var user = await CreateTestUserAsync(username, role);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = CreateClientWithoutCookies();

        // When: User requests /logs
        var request = new HttpRequestMessage(HttpMethod.Get, "/logs");
        AddCookies(request, cookies);
        var response = await client.SendAsync(request);

        // Then: Request is forbidden
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        // Cleanup happens automatically via DisposeAsync
    }
}
