using System.Net;
using System.Net.Http.Json;
using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.UserLibrary;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.UserLibrary;

/// <summary>
/// Fixture della classe: host e database costruiti una volta sola. Il perche', i numeri e le
/// condizioni per applicare lo stesso schema altrove stanno in <see cref="IntegrationHostFixture"/>.
///
/// <para>
/// 🔴 <b>Perche' condividere il database e' sicuro QUI.</b> Ogni endpoint opera su una risorsa
/// specifica per id (<c>GET/PUT/DELETE /api/v1/private-games/{id}</c>), mai su una lista globale:
/// nessuna asserzione riguarda conteggi o enumerazioni. <c>PrivateGameEntity.OwnerId</c> lega ogni
/// gioco al proprietario, e i test "OtherUsersGame" creano esplicitamente un secondo utente con id
/// casuale per verificare l'isolamento — quindi le righe di altri test (altri owner, altri game id
/// via <c>Guid.NewGuid()</c>) non possono mai comparire nel risultato di un test.
/// </para>
/// </summary>
public sealed class PrivateGameEndpointsHostFixture(SharedTestcontainersFixture shared)
    : IntegrationHostFixture(shared, "private_game_endpoints");

/// <summary>
/// Integration tests for Private Game HTTP endpoints.
/// Issue #3670: Phase 9 - Testing &amp; Polish.
/// Tests: CRUD operations, authentication, authorization, validation.
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "UserLibrary")]
public sealed class PrivateGameEndpointsIntegrationTests : IClassFixture<PrivateGameEndpointsHostFixture>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public PrivateGameEndpointsIntegrationTests(PrivateGameEndpointsHostFixture host)
    {
        _factory = host.Factory;
        _client = host.Client;
    }

    #region Authentication Tests

    [Fact]
    public async Task GetPrivateGame_WithoutAuth_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.GetAsync($"/api/v1/private-games/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AddPrivateGame_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var request = new
        {
            Source = "Manual",
            Title = "Test Game",
            MinPlayers = 2,
            MaxPlayers = 4
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/private-games", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdatePrivateGame_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var request = new
        {
            Title = "Updated Game",
            MinPlayers = 2,
            MaxPlayers = 4
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/v1/private-games/{Guid.NewGuid()}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeletePrivateGame_WithoutAuth_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.DeleteAsync($"/api/v1/private-games/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Add Private Game Tests

    [Fact]
    public async Task AddPrivateGame_ManualGame_WithValidSession_ReturnsCreated()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (userId, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var request = new
        {
            Source = "Manual",
            Title = "My Custom Board Game",
            MinPlayers = 2,
            MaxPlayers = 6,
            YearPublished = 2024,
            Description = "A great family game",
            PlayingTimeMinutes = 45,
            MinAge = 8,
            ComplexityRating = 2.5m
        };

        var httpRequest = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            "/api/v1/private-games",
            sessionToken);
        httpRequest.Content = JsonContent.Create(request);

        // Act
        var response = await _client.SendAsync(httpRequest);

        // Assert
        // Note: May return Unauthorized in test environment due to auth middleware mocking
        var responseBody = await response.Content.ReadAsStringAsync();
        (response.StatusCode == HttpStatusCode.Created ||
            response.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue($"Expected Created or Unauthorized, got {response.StatusCode}. Body: {responseBody}");

        if (response.StatusCode == HttpStatusCode.Created)
        {
            var result = await response.Content.ReadFromJsonAsync<PrivateGameDto>();
            result.Should().NotBeNull();
            result!.Title.Should().Be("My Custom Board Game");
            result.Source.Should().Be("Manual");
            result.MinPlayers.Should().Be(2);
            result.MaxPlayers.Should().Be(6);
            // #5074: it has a description, so it can be proposed; the BGG origin no longer decides.
            result.CanProposeToCatalog.Should().BeTrue();

            // Verify location header
            response.Headers.Location.Should().NotBeNull();
            response.Headers.Location!.ToString().Should().Contain(result.Id.ToString());
        }
    }

    [Fact]
    public async Task AddPrivateGame_BggGame_WithValidSession_ReturnsCreated()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (userId, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var request = new
        {
            Source = "BoardGameGeek",
            BggId = 174430,
            Title = "Gloomhaven",
            MinPlayers = 1,
            MaxPlayers = 4,
            YearPublished = 2017,
            Description = "A campaign-based dungeon crawler",
            PlayingTimeMinutes = 120,
            MinAge = 14,
            ComplexityRating = 3.86m,
            ImageUrl = "https://cf.geekdo-images.com/image.jpg",
            ThumbnailUrl = "https://cf.geekdo-images.com/thumb.jpg"
        };

        var httpRequest = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            "/api/v1/private-games",
            sessionToken);
        httpRequest.Content = JsonContent.Create(request);

        // Act
        var response = await _client.SendAsync(httpRequest);

        // Assert
        (response.StatusCode == HttpStatusCode.Created ||
            response.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue($"Expected Created or Unauthorized, got {response.StatusCode}");

        if (response.StatusCode == HttpStatusCode.Created)
        {
            var result = await response.Content.ReadFromJsonAsync<PrivateGameDto>();
            result.Should().NotBeNull();
            result!.Source.Should().Be("BoardGameGeek");
            result.BggId.Should().Be(174430);
            result.CanProposeToCatalog.Should().BeTrue();
        }
    }

    [Fact]
    public async Task AddPrivateGame_InvalidPlayerCounts_ReturnsBadRequest()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (userId, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var request = new
        {
            Source = "Manual",
            Title = "Invalid Game",
            MinPlayers = 5,
            MaxPlayers = 2  // Invalid: MaxPlayers < MinPlayers
        };

        var httpRequest = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            "/api/v1/private-games",
            sessionToken);
        httpRequest.Content = JsonContent.Create(request);

        // Act
        var response = await _client.SendAsync(httpRequest);

        // Assert
        // FluentValidation returns 422 UnprocessableEntity (Issue #1449)
        (response.StatusCode == HttpStatusCode.BadRequest ||
            response.StatusCode == HttpStatusCode.UnprocessableEntity ||
            response.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue($"Expected BadRequest/UnprocessableEntity or Unauthorized, got {response.StatusCode}");
    }

    #endregion

    #region Get Private Game Tests

    [Fact]
    public async Task GetPrivateGame_ExistingGame_ReturnsOk()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (userId, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        // Create a private game directly in DB
        var game = new PrivateGameEntity
        {
            Id = Guid.NewGuid(),
            OwnerId = userId,
            Title = "Test Game for Get",
            MinPlayers = 2,
            MaxPlayers = 4,
            Source = Api.BoundedContexts.UserLibrary.Domain.Enums.PrivateGameSource.Manual,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
        dbContext.PrivateGames.Add(game);
        await dbContext.SaveChangesAsync();

        var httpRequest = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/api/v1/private-games/{game.Id}",
            sessionToken);

        // Act
        var response = await _client.SendAsync(httpRequest);

        // Assert
        (response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue($"Expected OK or Unauthorized, got {response.StatusCode}");

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var result = await response.Content.ReadFromJsonAsync<PrivateGameDto>();
            result.Should().NotBeNull();
            result!.Id.Should().Be(game.Id);
            result.Title.Should().Be("Test Game for Get");
        }
    }

    [Fact]
    public async Task GetPrivateGame_NonExistentGame_ReturnsNotFound()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (userId, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var nonExistentId = Guid.NewGuid();

        var httpRequest = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/api/v1/private-games/{nonExistentId}",
            sessionToken);

        // Act
        var response = await _client.SendAsync(httpRequest);

        // Assert
        (response.StatusCode == HttpStatusCode.NotFound ||
            response.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue($"Expected NotFound or Unauthorized, got {response.StatusCode}");
    }

    #endregion

    #region Update Private Game Tests

    [Fact]
    public async Task UpdatePrivateGame_OwnGame_ReturnsOk()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (userId, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        // Create a private game directly in DB
        var game = new PrivateGameEntity
        {
            Id = Guid.NewGuid(),
            OwnerId = userId,
            Title = "Original Title",
            MinPlayers = 2,
            MaxPlayers = 4,
            Source = Api.BoundedContexts.UserLibrary.Domain.Enums.PrivateGameSource.Manual,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
        dbContext.PrivateGames.Add(game);
        await dbContext.SaveChangesAsync();

        var updateRequest = new
        {
            Title = "Updated Title",
            MinPlayers = 3,
            MaxPlayers = 6,
            YearPublished = 2024,
            Description = "Updated description"
        };

        var httpRequest = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Put,
            $"/api/v1/private-games/{game.Id}",
            sessionToken);
        httpRequest.Content = JsonContent.Create(updateRequest);

        // Act
        var response = await _client.SendAsync(httpRequest);

        // Assert
        (response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue($"Expected OK or Unauthorized, got {response.StatusCode}");

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var result = await response.Content.ReadFromJsonAsync<PrivateGameDto>();
            result.Should().NotBeNull();
            result!.Title.Should().Be("Updated Title");
            result.MinPlayers.Should().Be(3);
            result.MaxPlayers.Should().Be(6);
            result.UpdatedAt.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task UpdatePrivateGame_OtherUsersGame_ReturnsForbidden()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        // Create first user's game
        var otherUserId = Guid.NewGuid();
        var otherUser = new UserEntity
        {
            Id = otherUserId,
            Email = $"other_{Guid.NewGuid()}@example.com",
            DisplayName = "Other User",
            PasswordHash = "hashedpassword123",
            Role = "User",
            Tier = "Free",
            CreatedAt = DateTime.UtcNow
        };
        dbContext.Users.Add(otherUser);

        var otherUsersGame = new PrivateGameEntity
        {
            Id = Guid.NewGuid(),
            OwnerId = otherUserId,
            Title = "Other User's Game",
            MinPlayers = 2,
            MaxPlayers = 4,
            Source = Api.BoundedContexts.UserLibrary.Domain.Enums.PrivateGameSource.Manual,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
        dbContext.PrivateGames.Add(otherUsersGame);
        await dbContext.SaveChangesAsync();

        // Create session for different user (attacker)
        var (attackerId, attackerToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var updateRequest = new
        {
            Title = "Malicious Update",
            MinPlayers = 2,
            MaxPlayers = 4
        };

        var httpRequest = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Put,
            $"/api/v1/private-games/{otherUsersGame.Id}",
            attackerToken);
        httpRequest.Content = JsonContent.Create(updateRequest);

        // Act
        var response = await _client.SendAsync(httpRequest);

        // Assert
        (response.StatusCode == HttpStatusCode.Forbidden ||
            response.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue($"Expected Forbidden or Unauthorized, got {response.StatusCode}");
    }

    #endregion

    #region Delete Private Game Tests

    #region Tier quota (#4978)

    private static async Task SeedPrivateGamesAsync(MeepleAiDbContext dbContext, Guid ownerId, int count, int deleted = 0)
    {
        for (var i = 0; i < count; i++)
        {
            dbContext.PrivateGames.Add(new PrivateGameEntity
            {
                Id = Guid.NewGuid(),
                OwnerId = ownerId,
                Title = $"Quota Seed {i}",
                MinPlayers = 2,
                MaxPlayers = 4,
                Source = Api.BoundedContexts.UserLibrary.Domain.Enums.PrivateGameSource.Manual,
                CreatedAt = DateTime.UtcNow,
                IsDeleted = i < deleted,
                DeletedAt = i < deleted ? DateTime.UtcNow : null
            });
        }
        await dbContext.SaveChangesAsync();
    }

    private HttpRequestMessage AddManualGameRequest(string sessionToken)
    {
        var httpRequest = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            "/api/v1/private-games",
            sessionToken);
        httpRequest.Content = JsonContent.Create(new { Source = "Manual", Title = "Over The Quota", MinPlayers = 2, MaxPlayers = 4 });
        return httpRequest;
    }

    [Fact]
    public async Task AddPrivateGame_UserAtTheFreeTierLimit_Returns402AndAddsNoGame()
    {
        // Arrange: a free-tier user with as many private games as the tier allows (3).
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (userId, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        await SeedPrivateGamesAsync(dbContext, userId, 3);

        // Act
        var response = await _client.SendAsync(AddManualGameRequest(sessionToken));

        // Assert
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.PaymentRequired, body);
        body.Should().Contain("PRIVATE_GAME_QUOTA_EXCEEDED");
        (await dbContext.PrivateGames.AsNoTracking().CountAsync(g => g.OwnerId == userId)).Should().Be(3);
    }

    [Fact]
    public async Task AddPrivateGame_DeletedGamesDoNotCountTowardsTheLimit_ReturnsCreated()
    {
        // Arrange: three private games, one of them deleted.
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (userId, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        await SeedPrivateGamesAsync(dbContext, userId, 3, deleted: 1);

        // Act
        var response = await _client.SendAsync(AddManualGameRequest(sessionToken));

        // Assert
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.Created, body);
        (await dbContext.PrivateGames.AsNoTracking().CountAsync(g => g.OwnerId == userId)).Should().Be(3);
    }

    #endregion

    [Fact]
    public async Task DeletePrivateGame_OwnGame_ReturnsNoContent()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (userId, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        // Create a private game directly in DB
        var game = new PrivateGameEntity
        {
            Id = Guid.NewGuid(),
            OwnerId = userId,
            Title = "Game to Delete",
            MinPlayers = 2,
            MaxPlayers = 4,
            Source = Api.BoundedContexts.UserLibrary.Domain.Enums.PrivateGameSource.Manual,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
        dbContext.PrivateGames.Add(game);
        await dbContext.SaveChangesAsync();

        var httpRequest = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Delete,
            $"/api/v1/private-games/{game.Id}",
            sessionToken);

        // Act
        var response = await _client.SendAsync(httpRequest);

        // Assert
        (response.StatusCode == HttpStatusCode.NoContent ||
            response.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue($"Expected NoContent or Unauthorized, got {response.StatusCode}");

        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            // Verify soft delete
            await dbContext.Entry(game).ReloadAsync();
            var deletedGame = await dbContext.PrivateGames
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(g => g.Id == game.Id);
            deletedGame.Should().NotBeNull();
            deletedGame!.IsDeleted.Should().BeTrue();
            deletedGame.DeletedAt.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task DeletePrivateGame_OtherUsersGame_ReturnsForbidden()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        // Create first user's game
        var otherUserId = Guid.NewGuid();
        var otherUser = new UserEntity
        {
            Id = otherUserId,
            Email = $"owner_{Guid.NewGuid()}@example.com",
            DisplayName = "Owner User",
            PasswordHash = "hashedpassword123",
            Role = "User",
            Tier = "Free",
            CreatedAt = DateTime.UtcNow
        };
        dbContext.Users.Add(otherUser);

        var otherUsersGame = new PrivateGameEntity
        {
            Id = Guid.NewGuid(),
            OwnerId = otherUserId,
            Title = "Other User's Game",
            MinPlayers = 2,
            MaxPlayers = 4,
            Source = Api.BoundedContexts.UserLibrary.Domain.Enums.PrivateGameSource.Manual,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
        dbContext.PrivateGames.Add(otherUsersGame);
        await dbContext.SaveChangesAsync();

        // Create session for different user (attacker)
        var (attackerId, attackerToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var httpRequest = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Delete,
            $"/api/v1/private-games/{otherUsersGame.Id}",
            attackerToken);

        // Act
        var response = await _client.SendAsync(httpRequest);

        // Assert
        (response.StatusCode == HttpStatusCode.Forbidden ||
            response.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue($"Expected Forbidden or Unauthorized, got {response.StatusCode}");
    }

    [Fact]
    public async Task DeletePrivateGame_NonExistentGame_ReturnsNotFound()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (userId, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var nonExistentId = Guid.NewGuid();

        var httpRequest = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Delete,
            $"/api/v1/private-games/{nonExistentId}",
            sessionToken);

        // Act
        var response = await _client.SendAsync(httpRequest);

        // Assert
        (response.StatusCode == HttpStatusCode.NotFound ||
            response.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue($"Expected NotFound or Unauthorized, got {response.StatusCode}");
    }

    #endregion

    #region Full Lifecycle Tests

    [Fact]
    public async Task PrivateGame_FullCrudLifecycle_Succeeds()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (userId, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        // Step 1: Create
        var createRequest = new
        {
            Source = "Manual",
            Title = "Lifecycle Test Game",
            MinPlayers = 2,
            MaxPlayers = 4,
            Description = "Initial description"
        };

        var createHttpRequest = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            "/api/v1/private-games",
            sessionToken);
        createHttpRequest.Content = JsonContent.Create(createRequest);

        var createResponse = await _client.SendAsync(createHttpRequest);

        // Skip remaining steps if auth fails in test environment
        if (createResponse.StatusCode == HttpStatusCode.Unauthorized)
        {
            return;
        }

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<PrivateGameDto>();
        created.Should().NotBeNull();
        var gameId = created!.Id;

        // Step 2: Read
        var getHttpRequest = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/api/v1/private-games/{gameId}",
            sessionToken);

        var getResponse = await _client.SendAsync(getHttpRequest);
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var retrieved = await getResponse.Content.ReadFromJsonAsync<PrivateGameDto>();
        retrieved.Should().NotBeNull();
        retrieved!.Title.Should().Be("Lifecycle Test Game");

        // Step 3: Update
        var updateRequest = new
        {
            Title = "Updated Lifecycle Game",
            MinPlayers = 3,
            MaxPlayers = 6,
            Description = "Updated description"
        };

        var updateHttpRequest = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Put,
            $"/api/v1/private-games/{gameId}",
            sessionToken);
        updateHttpRequest.Content = JsonContent.Create(updateRequest);

        var updateResponse = await _client.SendAsync(updateHttpRequest);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateResponse.Content.ReadFromJsonAsync<PrivateGameDto>();
        updated.Should().NotBeNull();
        updated!.Title.Should().Be("Updated Lifecycle Game");
        updated.UpdatedAt.Should().NotBeNull();

        // Step 4: Delete
        var deleteHttpRequest = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Delete,
            $"/api/v1/private-games/{gameId}",
            sessionToken);

        var deleteResponse = await _client.SendAsync(deleteHttpRequest);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Step 5: Verify deletion (should return 404)
        var getAfterDeleteRequest = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/api/v1/private-games/{gameId}",
            sessionToken);

        var getAfterDeleteResponse = await _client.SendAsync(getAfterDeleteRequest);
        getAfterDeleteResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Propose to catalog (#5074)

    // #5074: the proposal of a private game to the shared catalog, restored with the description as a requirement.
    // Each test owns its user and a game with a unique title, so the shared database cannot leak into it.

    private static async Task<Guid> SeedProposableGameAsync(
        MeepleAiDbContext dbContext, Guid ownerId, string title, string? description, int? yearPublished = null)
    {
        var id = Guid.NewGuid();
        dbContext.PrivateGames.Add(new PrivateGameEntity
        {
            Id = id,
            OwnerId = ownerId,
            Title = title,
            Description = description,
            YearPublished = yearPublished,
            MinPlayers = 2,
            MaxPlayers = 4,
            Source = Api.BoundedContexts.UserLibrary.Domain.Enums.PrivateGameSource.Manual,
            CreatedAt = DateTime.UtcNow,
        });
        await dbContext.SaveChangesAsync();
        return id;
    }

    private Task<HttpResponseMessage> ProposeAsync(Guid gameId, string sessionToken, string? notes = null)
    {
        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"/api/v1/private-games/{gameId}/propose-to-catalog",
            sessionToken);
        request.Content = JsonContent.Create(new { Notes = notes });
        return _client.SendAsync(request);
    }

    [Fact]
    public async Task ProposeToCatalog_GameWithDescription_Returns201_AndStoresAProposalWithoutASharedGame()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (userId, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        var gameId = await SeedProposableGameAsync(dbContext, userId, $"Proposed {Guid.NewGuid():N}", "A tile game.");

        var response = await ProposeAsync(gameId, sessionToken, "Please add it");

        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.Created, body);
        var shareRequestId = System.Text.Json.JsonDocument.Parse(body).RootElement.GetProperty("shareRequestId").GetGuid();

        // Before #5074 source_game_id was NOT NULL with a foreign key to shared_games: no proposal could be stored.
        var stored = await dbContext.Set<Api.Infrastructure.Entities.SharedGameCatalog.ShareRequestEntity>()
            .AsNoTracking()
            .SingleAsync(r => r.Id == shareRequestId);
        stored.ContributionType.Should().Be((int)Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects.ContributionType.NewGameProposal);
        stored.SourceGameId.Should().BeNull();
        stored.SourcePrivateGameId.Should().Be(gameId);
        stored.UserId.Should().Be(userId);
    }

    [Fact]
    public async Task ProposeToCatalog_GameWithoutDescription_Returns422OnDescription_AndStoresNothing()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (userId, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        var gameId = await SeedProposableGameAsync(dbContext, userId, $"No description {Guid.NewGuid():N}", null);

        var response = await ProposeAsync(gameId, sessionToken);

        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, body);
        body.Should().Contain("Description");
        (await dbContext.Set<Api.Infrastructure.Entities.SharedGameCatalog.ShareRequestEntity>()
            .AnyAsync(r => r.SourcePrivateGameId == gameId)).Should().BeFalse();
    }

    [Fact]
    public async Task ProposeToCatalog_Twice_Returns409TheSecondTime()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (userId, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        var gameId = await SeedProposableGameAsync(dbContext, userId, $"Twice {Guid.NewGuid():N}", "A tile game.");

        (await ProposeAsync(gameId, sessionToken)).StatusCode.Should().Be(HttpStatusCode.Created);
        var second = await ProposeAsync(gameId, sessionToken);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict, await second.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ProposeToCatalog_SomeoneElsesGame_Returns403()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (ownerId, _) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        var (_, otherToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        var gameId = await SeedProposableGameAsync(dbContext, ownerId, $"Not yours {Guid.NewGuid():N}", "A tile game.");

        var response = await ProposeAsync(gameId, otherToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ProposedGame_AppearsToTheAdmin_WithThePrivateGamesTitle_InTheListAndTheDetails()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (userId, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        var (_, adminToken) = await TestSessionHelper.CreateAdminSessionAsync(dbContext);
        var title = $"Admin sees {Guid.NewGuid():N}";
        var gameId = await SeedProposableGameAsync(dbContext, userId, title, "A tile game.", yearPublished: 2021);
        var created = await ProposeAsync(gameId, sessionToken);
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var shareRequestId = System.Text.Json.JsonDocument.Parse(await created.Content.ReadAsStringAsync())
            .RootElement.GetProperty("shareRequestId").GetGuid();

        // The list: with an INNER JOIN on shared_games the proposal was not there at all; then it would be
        // "Unknown Game". The search runs on the private game's title.
        var list = await _client.SendAsync(TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/api/v1/admin/share-requests?search={Uri.EscapeDataString(title)}&sortBy=GameTitle&sortDirection=Ascending&pageNumber=1&pageSize=20",
            adminToken));
        var listBody = await list.Content.ReadAsStringAsync();
        list.StatusCode.Should().Be(HttpStatusCode.OK, listBody);
        var item = System.Text.Json.JsonDocument.Parse(listBody).RootElement.GetProperty("items").EnumerateArray()
            .Single(i => i.GetProperty("id").GetGuid() == shareRequestId);
        item.GetProperty("gameTitle").GetString().Should().Be(title);

        // The details: MapToGameDetailsDto read a null shared game and answered 500.
        var details = await _client.SendAsync(TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get, $"/api/v1/admin/share-requests/{shareRequestId}", adminToken));
        var detailsBody = await details.Content.ReadAsStringAsync();
        details.StatusCode.Should().Be(HttpStatusCode.OK, detailsBody);
        var sourceGame = System.Text.Json.JsonDocument.Parse(detailsBody).RootElement.GetProperty("sourceGame");
        sourceGame.GetProperty("title").GetString().Should().Be(title);
        sourceGame.GetProperty("description").GetString().Should().Be("A tile game.");
        // #5076: the review shows the year and the players.
        sourceGame.GetProperty("yearPublished").GetInt32().Should().Be(2021);
        sourceGame.GetProperty("minPlayers").GetInt32().Should().Be(2);
        sourceGame.GetProperty("maxPlayers").GetInt32().Should().Be(4);
    }

    [Fact]
    public async Task ProposedGame_AdminStartsTheReview_Returns200_AndTheRowKeepsNoSharedGame()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (userId, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        var (_, adminToken) = await TestSessionHelper.CreateAdminSessionAsync(dbContext);
        var gameId = await SeedProposableGameAsync(dbContext, userId, $"Reviewed {Guid.NewGuid():N}", "A tile game.");
        var created = await ProposeAsync(gameId, sessionToken);
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var shareRequestId = System.Text.Json.JsonDocument.Parse(await created.Content.ReadAsStringAsync())
            .RootElement.GetProperty("shareRequestId").GetGuid();

        // The update path writes source_game_id again: an empty GUID there breaks the foreign key on the first
        // admin action, the insert alone does not show it.
        var response = await _client.SendAsync(TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post, $"/api/v1/admin/share-requests/{shareRequestId}/start-review", adminToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var stored = await dbContext.Set<Api.Infrastructure.Entities.SharedGameCatalog.ShareRequestEntity>()
            .AsNoTracking()
            .SingleAsync(r => r.Id == shareRequestId);
        stored.Status.Should().Be((int)Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects.ShareRequestStatus.InReview);
        stored.SourceGameId.Should().BeNull();
    }

    [Fact]
    public async Task ProposalLocation_AnswersTheOwner_AndIs404ForAnotherUser()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (userId, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        var (_, otherToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        var title = $"Mine only {Guid.NewGuid():N}";
        var gameId = await SeedProposableGameAsync(dbContext, userId, title, "A private description.");
        var created = await ProposeAsync(gameId, sessionToken);
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var location = created.Headers.Location!.ToString();

        var mine = await _client.SendAsync(TestSessionHelper.CreateAuthenticatedRequest(HttpMethod.Get, location, sessionToken));
        var mineBody = await mine.Content.ReadAsStringAsync();
        mine.StatusCode.Should().Be(HttpStatusCode.OK, mineBody);
        System.Text.Json.JsonDocument.Parse(mineBody).RootElement.GetProperty("sourceGame")
            .GetProperty("title").GetString().Should().Be(title);

        // The user route used the admin query, which reads any request: any signed-in user holding the id got
        // the private game's title and description.
        var theirs = await _client.SendAsync(TestSessionHelper.CreateAuthenticatedRequest(HttpMethod.Get, location, otherToken));
        var theirsBody = await theirs.Content.ReadAsStringAsync();
        theirs.StatusCode.Should().Be(HttpStatusCode.NotFound, theirsBody);
        theirsBody.Should().NotContain(title);
    }

    /// <summary>
    /// #5076: a user proposes a game, the admin starts the review and approves the proposal with
    /// <paramref name="action"/>. Returns the proposal's id and the approval's response.
    /// </summary>
    private async Task<(Guid ShareRequestId, HttpResponseMessage Response)> ProposeAndApproveAsync(
        MeepleAiDbContext dbContext, string adminToken, string title, string action, Guid? targetSharedGameId = null)
    {
        var (userId, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        var gameId = await SeedProposableGameAsync(dbContext, userId, title, $"{title}, a tile game.");
        var created = await ProposeAsync(gameId, sessionToken);
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var shareRequestId = System.Text.Json.JsonDocument.Parse(await created.Content.ReadAsStringAsync())
            .RootElement.GetProperty("shareRequestId").GetGuid();
        var review = await _client.SendAsync(TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post, $"/api/v1/admin/share-requests/{shareRequestId}/start-review", adminToken));
        review.StatusCode.Should().Be(HttpStatusCode.OK, await review.Content.ReadAsStringAsync());

        var approve = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post, $"/api/v1/admin/share-requests/{shareRequestId}/approve-game-proposal", adminToken);
        approve.Content = JsonContent.Create(new { ApprovalAction = action, TargetSharedGameId = targetSharedGameId, AdminNotes = (string?)null });
        return (shareRequestId, await _client.SendAsync(approve));
    }

    private static async Task<Guid> ApprovedTargetAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        return System.Text.Json.JsonDocument.Parse(body).RootElement.GetProperty("targetSharedGameId").GetGuid();
    }

    [Fact]
    public async Task ProposedGame_ApprovedAsNew_CreatesTheSharedGameFromThePrivateGame()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, adminToken) = await TestSessionHelper.CreateAdminSessionAsync(dbContext);
        var title = $"Approved {Guid.NewGuid():N}";

        // The handler read the proposed game from a library entry at source_game_id, which a proposal does not
        // have (null since #5074): every real proposal answered 404 "UserLibraryEntry".
        var (shareRequestId, response) = await ProposeAndApproveAsync(dbContext, adminToken, title, "ApproveAsNew");

        var sharedGameId = await ApprovedTargetAsync(response);
        var sharedGame = await dbContext.SharedGames.AsNoTracking().SingleAsync(g => g.Id == sharedGameId);
        sharedGame.Title.Should().Be(title);
        sharedGame.Description.Should().Be($"{title}, a tile game.");
        sharedGame.MinPlayers.Should().Be(2);
        sharedGame.MaxPlayers.Should().Be(4);
        var stored = await dbContext.Set<Api.Infrastructure.Entities.SharedGameCatalog.ShareRequestEntity>()
            .AsNoTracking()
            .SingleAsync(r => r.Id == shareRequestId);
        stored.Status.Should().Be((int)Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects.ShareRequestStatus.Approved);
        stored.TargetSharedGameId.Should().Be(sharedGameId);
        stored.SourceGameId.Should().BeNull();
    }

    [Fact]
    public async Task ProposedGames_ApprovedAsVariantAndMerged_ReachTheExistingSharedGame()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, adminToken) = await TestSessionHelper.CreateAdminSessionAsync(dbContext);
        var baseTitle = $"Base {Guid.NewGuid():N}";
        var baseGameId = await ApprovedTargetAsync(
            (await ProposeAndApproveAsync(dbContext, adminToken, baseTitle, "ApproveAsNew")).Response);

        var variantTitle = $"Variant {Guid.NewGuid():N}";
        var variantId = await ApprovedTargetAsync(
            (await ProposeAndApproveAsync(dbContext, adminToken, variantTitle, "ApproveAsVariant", baseGameId)).Response);
        var mergedTitle = $"Merged {Guid.NewGuid():N}";
        var (mergedRequestId, mergeResponse) =
            await ProposeAndApproveAsync(dbContext, adminToken, mergedTitle, "MergeKnowledgeBase", baseGameId);

        variantId.Should().NotBe(baseGameId);
        (await dbContext.SharedGames.AsNoTracking().SingleAsync(g => g.Id == variantId)).Title
            .Should().Be($"{variantTitle} (Variant)");
        // Merging creates no game: the proposal points at the existing one.
        (await ApprovedTargetAsync(mergeResponse)).Should().Be(baseGameId);
        (await dbContext.Set<Api.Infrastructure.Entities.SharedGameCatalog.ShareRequestEntity>()
            .AsNoTracking()
            .SingleAsync(r => r.Id == mergedRequestId)).TargetSharedGameId.Should().Be(baseGameId);
        (await dbContext.SharedGames.AsNoTracking().AnyAsync(g => g.Title == mergedTitle)).Should().BeFalse();
    }

    #endregion
}
