using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
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

        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/logs");

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
}
