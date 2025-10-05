using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using Api.Infrastructure;
using Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests;

public class GameEndpointsTests : IClassFixture<WebApplicationFactoryFixture>
{
    private readonly HttpClient _client;
    private readonly WebApplicationFactoryFixture _factory;

    public GameEndpointsTests(WebApplicationFactoryFixture factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PostGames_CreatesGameForTenant()
    {
        // Arrange
        var tenantId = "tenant-games";
        var cookies = await RegisterAndAuthenticateAsync(tenantId, "creator@example.com");

        var request = new HttpRequestMessage(HttpMethod.Post, "/games")
        {
            Content = JsonContent.Create(new
            {
                tenantId,
                name = "Terraforming Mars"
            })
        };

        foreach (var cookie in cookies)
        {
            request.Headers.Add("Cookie", cookie);
        }

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var game = await response.Content.ReadFromJsonAsync<GameResponse>();
        Assert.NotNull(game);
        Assert.Equal("Terraforming Mars", game!.Name);
        Assert.False(string.IsNullOrWhiteSpace(game.Id));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var entity = await db.Games.FirstOrDefaultAsync(g => g.Id == game.Id && g.TenantId == tenantId);
        Assert.NotNull(entity);
    }

    [Fact]
    public async Task PostGames_ReturnsForbidden_WhenTenantMismatch()
    {
        // Arrange
        var cookies = await RegisterAndAuthenticateAsync("tenant-alpha", "alpha@example.com");

        var request = new HttpRequestMessage(HttpMethod.Post, "/games")
        {
            Content = JsonContent.Create(new
            {
                tenantId = "tenant-beta",
                name = "Forbidden Game"
            })
        };

        foreach (var cookie in cookies)
        {
            request.Headers.Add("Cookie", cookie);
        }

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task<List<string>> RegisterAndAuthenticateAsync(string tenantId, string email)
    {
        var registerRequest = new
        {
            tenantId,
            tenantName = tenantId,
            email,
            password = "Password123!",
            displayName = "Test User",
            role = "Admin"
        };

        var response = await _client.PostAsJsonAsync("/auth/register", registerRequest);
        response.EnsureSuccessStatusCode();

        if (!response.Headers.TryGetValues("Set-Cookie", out var setCookie))
        {
            return new List<string>();
        }

        return setCookie.Select(cookie => cookie.Split(';')[0]).ToList();
    }
}
