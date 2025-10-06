using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Linq;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests;

public class LogsEndpointTests : IClassFixture<WebApplicationFactoryFixture>
{
    private readonly WebApplicationFactoryFixture _factory;

    public LogsEndpointTests(WebApplicationFactoryFixture factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetLogs_ReturnsLatestEntriesFromAiRequestLogService()
    {
        var now = DateTime.UtcNow;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

            db.AiRequestLogs.AddRange(
                new AiRequestLogEntity
                {
                    Id = "req-001",
                    UserId = "user-123",
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
                    Id = "req-002",
                    UserId = "user-456",
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
        }

        using var client = _factory.CreateHttpsClient();
        var cookies = await RegisterAndAuthenticateAsync(client, $"admin-logs-{Guid.NewGuid():N}@example.com", "Admin");

        var request = new HttpRequestMessage(HttpMethod.Get, "/logs");
        AddCookies(request, cookies);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var entries = await response.Content.ReadFromJsonAsync<List<LogEntryResponse>>();

        Assert.NotNull(entries);
        Assert.Equal(2, entries!.Count);

        var newest = entries[0];
        Assert.Equal("req-002", newest.RequestId);
        Assert.Equal("ERROR", newest.Level);
        Assert.Equal("Explain setup", newest.Message);
        Assert.Equal("user-456", newest.UserId);
        Assert.Equal("demo-chess", newest.GameId);

        var older = entries[1];
        Assert.Equal("req-001", older.RequestId);
        Assert.Equal("INFO", older.Level);
        Assert.Equal("Two players.", older.Message);
    }

    [Fact]
    public async Task GetLogs_ReturnsUnauthorizedWhenSessionMissing()
    {
        using var client = _factory.CreateHttpsClient();
        var response = await client.GetAsync("/logs");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    public static IEnumerable<object[]> NonAdminRoles => new[]
    {
        new object[] { "Editor" },
        new object[] { "User" }
    };

    [Theory]
    [MemberData(nameof(NonAdminRoles))]
    public async Task GetLogs_ReturnsForbiddenForNonAdminRoles(string role)
    {
        using var client = _factory.CreateHttpsClient();
        var cookies = await RegisterAndAuthenticateAsync(client, $"{role.ToLowerInvariant()}-logs-{Guid.NewGuid():N}@example.com", role);

        var request = new HttpRequestMessage(HttpMethod.Get, "/logs");
        AddCookies(request, cookies);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static void AddCookies(HttpRequestMessage request, IReadOnlyCollection<string> cookies)
    {
        if (cookies.Count == 0)
        {
            return;
        }

        request.Headers.Add("Cookie", string.Join("; ", cookies));
    }

    private static List<string> ExtractCookies(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var values))
        {
            return new List<string>();
        }

        return values
            .Select(value => value.Split(';')[0])
            .ToList();
    }

    private static async Task<List<string>> RegisterAndAuthenticateAsync(HttpClient client, string email, string role)
    {
        var payload = new RegisterPayload(email, "Password123!", "Logs Tester", role);
        var response = await client.PostAsJsonAsync("/auth/register", payload);
        response.EnsureSuccessStatusCode();
        return ExtractCookies(response);
    }
}
