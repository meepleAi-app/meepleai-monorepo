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
    public async Task PostGames_CreatesGame_ForAdmin()
    {
        // Arrange
        var cookies = await RegisterAndAuthenticateAsync("creator@example.com", "Admin");

        var request = new HttpRequestMessage(HttpMethod.Post, "/games")
        {
            Content = JsonContent.Create(new CreateGameRequest("Terraforming Mars", "terraforming-mars"))
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
        var entity = await db.Games.FirstOrDefaultAsync(g => g.Id == game.Id);
        Assert.NotNull(entity);
        Assert.Equal("terraforming-mars", entity!.Id);
        Assert.Equal("Terraforming Mars", entity.Name);
    }

    [Fact]
    public async Task PostGames_CreatesGame_ForEditor()
    {
        // Arrange
        var cookies = await RegisterAndAuthenticateAsync("editor@example.com", "Editor");

        var request = new HttpRequestMessage(HttpMethod.Post, "/games")
        {
            Content = JsonContent.Create(new CreateGameRequest("Brass Birmingham", "brass-birmingham"))
        };

        foreach (var cookie in cookies)
        {
            request.Headers.Add("Cookie", cookie);
        }

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task PostGames_ReturnsForbidden_ForUserRole()
    {
        // Arrange
        var cookies = await RegisterAndAuthenticateAsync("player@example.com", "User");

        var request = new HttpRequestMessage(HttpMethod.Post, "/games")
        {
            Content = JsonContent.Create(new CreateGameRequest("Catan", "catan"))
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

    private async Task<List<string>> RegisterAndAuthenticateAsync(string email, string role)
    {
        var registerRequest = new RegisterPayload(
            email,
            "Password123!",
            "Test User",
            role);

        var response = await _client.PostAsJsonAsync("/auth/register", registerRequest);
        response.EnsureSuccessStatusCode();

        if (!response.Headers.TryGetValues("Set-Cookie", out var setCookie))
        {
            return new List<string>();
        }

        return setCookie.Select(cookie => cookie.Split(';')[0]).ToList();
    }
}
