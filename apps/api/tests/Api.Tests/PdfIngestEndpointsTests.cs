using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests;

public class PdfIngestEndpointsTests : IClassFixture<WebApplicationFactoryFixture>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly WebApplicationFactoryFixture _factory;

    public PdfIngestEndpointsTests(WebApplicationFactoryFixture factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PostRulespecIngest_ReturnsGeneratedRuleSpec()
    {
        using var client = _factory.CreateClient();
        var email = "pdf-ingest@example.com";
        var cookies = await RegisterAndAuthenticateAsync(client, email);

        const string gameId = "game-ingest";
        const string pdfId = "pdf-ingest";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var user = await db.Users.SingleAsync(u => u.Email == email);

            db.Games.Add(new GameEntity
            {
                Id = gameId,
                Name = "Ingest Game",
                CreatedAt = DateTime.UtcNow
            });

            db.PdfDocuments.Add(new PdfDocumentEntity
            {
                Id = pdfId,
                GameId = gameId,
                FileName = "rules.pdf",
                FilePath = "/tmp/rules.pdf",
                FileSizeBytes = 2048,
                UploadedByUserId = user.Id,
                UploadedAt = DateTime.UtcNow,
                AtomicRules = JsonSerializer.Serialize(new[]
                {
                    "[Table on page 4] Setup: Distribute four cards to each player"
                })
            });

            await db.SaveChangesAsync();
        }

        var request = new HttpRequestMessage(HttpMethod.Post, $"/ingest/pdf/{pdfId}/rulespec");
        foreach (var cookie in cookies)
        {
            request.Headers.TryAddWithoutValidation("Cookie", cookie);
        }

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var spec = await response.Content.ReadFromJsonAsync<RuleSpec>(JsonOptions);
        Assert.NotNull(spec);
        Assert.Equal(gameId, spec!.gameId);
        Assert.Single(spec.rules);
        Assert.Equal("Setup: Distribute four cards to each player", spec.rules[0].text);
        Assert.Equal("4", spec.rules[0].page);
    }

    private static async Task<List<string>> RegisterAndAuthenticateAsync(HttpClient client, string email, string role = "Admin")
    {
        var registerRequest = new RegisterPayload(
            email,
            "Password123!",
            "Test User",
            role);

        var response = await client.PostAsJsonAsync("/auth/register", registerRequest);
        response.EnsureSuccessStatusCode();

        if (!response.Headers.TryGetValues("Set-Cookie", out var setCookie))
        {
            return new List<string>();
        }

        return new List<string>(setCookie.Select(cookie => cookie.Split(';')[0]));
    }
}
