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
/// BDD-style integration tests for game management endpoints.
///
/// Feature: Game CRUD operations with role-based access control
/// As a game editor or admin
/// I want to create and manage games via API
/// So that users can discover and interact with board game rules
/// </summary>
public class GameEndpointsTests : IntegrationTestBase
{
    public GameEndpointsTests(WebApplicationFactoryFixture factory) : base(factory)
    {
    }

    /// <summary>
    /// Scenario: Admin creates game via API
    ///   Given admin user is authenticated
    ///   When admin posts to /games with valid game data
    ///   Then game is created with HTTP 201
    ///   And game is persisted in database
    ///   And game is automatically tracked for cleanup
    /// </summary>
    [Fact]
    public async Task PostGames_CreatesGame_ForAdmin()
    {
        // Given: Admin user is authenticated
        var user = await CreateTestUserAsync("creator", UserRole.Admin);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = CreateClientWithoutCookies();

        // When: Admin posts to /games with valid game data
        var request = new HttpRequestMessage(HttpMethod.Post, "/games")
        {
            Content = JsonContent.Create(new CreateGameRequest("Terraforming Mars", "terraforming-mars"))
        };
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: Game is created with HTTP 201
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var game = await response.Content.ReadFromJsonAsync<GameResponse>();
        Assert.NotNull(game);
        Assert.Equal("Terraforming Mars", game!.Name);
        Assert.False(string.IsNullOrWhiteSpace(game.Id));

        // And: Game is persisted in database
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var entity = await db.Games.FirstOrDefaultAsync(g => g.Id == game.Id);
        Assert.NotNull(entity);
        Assert.Equal("terraforming-mars", entity!.Id);
        Assert.Equal("Terraforming Mars", entity.Name);

        // And: Game is automatically tracked for cleanup
        TrackGameId(game.Id);
        // Cleanup happens automatically via DisposeAsync
    }

    /// <summary>
    /// Scenario: Editor creates game via API
    ///   Given editor user is authenticated
    ///   When editor posts to /games
    ///   Then game is created successfully
    ///   And cleanup is automatic
    /// </summary>
    [Fact]
    public async Task PostGames_CreatesGame_ForEditor()
    {
        // Given: Editor user is authenticated
        var user = await CreateTestUserAsync("editor", UserRole.Editor);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = CreateClientWithoutCookies();

        // When: Editor posts to /games
        var request = new HttpRequestMessage(HttpMethod.Post, "/games")
        {
            Content = JsonContent.Create(new CreateGameRequest("Brass Birmingham", "brass-birmingham"))
        };
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: Game is created successfully
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var game = await response.Content.ReadFromJsonAsync<GameResponse>();
        if (game?.Id != null)
        {
            TrackGameId(game.Id); // And: cleanup is automatic
        }
    }

    /// <summary>
    /// Scenario: Regular user attempts to create game
    ///   Given regular user (non-editor/non-admin) is authenticated
    ///   When user posts to /games
    ///   Then request is forbidden (HTTP 403)
    ///   And no game is created
    /// </summary>
    [Fact]
    public async Task PostGames_ReturnsForbidden_ForUserRole()
    {
        // Given: Regular user (non-editor/non-admin) is authenticated
        var user = await CreateTestUserAsync("player", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = CreateClientWithoutCookies();

        // When: User posts to /games
        var request = new HttpRequestMessage(HttpMethod.Post, "/games")
        {
            Content = JsonContent.Create(new CreateGameRequest("Catan", "catan"))
        };
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: Request is forbidden (HTTP 403)
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        // And: no game is created (no TrackGameId needed)
    }
}
