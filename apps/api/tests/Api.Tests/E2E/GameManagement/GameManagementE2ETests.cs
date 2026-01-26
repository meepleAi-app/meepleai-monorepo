using Api.Tests.E2E.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;
using Xunit;

#pragma warning disable S1144 // Unused private types or members should be removed (DTOs for deserialization)

namespace Api.Tests.E2E.GameManagement;

/// <summary>
/// E2E tests for game management flows.
/// Tests the complete user journey for game operations.
///
/// Issue #3023: Backend E2E Test Suite - Game Management Flows
///
/// Critical Journeys Covered:
/// - Browse games (public, paginated)
/// - Get game details
/// - Start game session → add players → complete session
/// - Session lifecycle management
/// </summary>
[Collection("E2ETests")]
[Trait("Category", "E2E")]
public sealed class GameManagementE2ETests : E2ETestBase
{
    public GameManagementE2ETests(E2ETestFixture fixture) : base(fixture) { }

    protected override async Task SeedTestDataAsync()
    {
        // Seed a test game for E2E tests
        var game = new Api.Infrastructure.Entities.GameEntity
        {
            Id = Guid.NewGuid(),
            Name = "E2E Test Game",
            MinPlayers = 2,
            MaxPlayers = 4,
            MinPlayTimeMinutes = 60,
            YearPublished = 2024,
            BggId = null,
            ImageUrl = "https://example.com/image.png",
            CreatedAt = DateTime.UtcNow
        };

        DbContext.Games.Add(game);
        await DbContext.SaveChangesAsync();
    }

    #region Public Game Browsing Tests

    [Fact]
    public async Task GetGames_PublicEndpoint_ReturnsPaginatedList()
    {
        // Arrange - No authentication needed
        ClearAuthentication();

        // Act
        var response = await Client.GetAsync("/api/v1/games");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PaginatedGamesResponse>();
        result.Should().NotBeNull();
        result!.Games.Should().NotBeNull();
        result.Games!.Any(g => g.Name == "E2E Test Game").Should().BeTrue();
    }

    [Fact]
    public async Task GetGames_WithPagination_ReturnsCorrectPage()
    {
        // Arrange
        ClearAuthentication();

        // Act
        var response = await Client.GetAsync("/api/v1/games?pageSize=10&page=1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PaginatedGamesResponse>();
        result.Should().NotBeNull();
        result!.PageSize.Should().Be(10);
    }

    [Fact]
    public async Task GetGameById_ExistingGame_ReturnsGameDetails()
    {
        // Arrange
        ClearAuthentication();
        var game = await DbContext.Games.FirstAsync(g => g.Name == "E2E Test Game");

        // Act
        var response = await Client.GetAsync($"/api/v1/games/{game.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<GameDto>();
        result.Should().NotBeNull();
        result!.Name.Should().Be("E2E Test Game");
        result.MinPlayers.Should().Be(2);
        result.MaxPlayers.Should().Be(4);
    }

    [Fact]
    public async Task GetGameById_NonexistentGame_ReturnsNotFound()
    {
        // Arrange
        ClearAuthentication();
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await Client.GetAsync($"/api/v1/games/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Game Session Lifecycle Tests

    [Fact]
    public async Task StartSession_WithValidGame_CreatesSession()
    {
        // Arrange - Need authentication for session endpoints
        var email = $"sessiontest_{Guid.NewGuid():N}@example.com";
        var (sessionToken, userId) = await RegisterUserAsync(email, "ValidPassword123!");
        SetSessionCookie(sessionToken);

        var game = await DbContext.Games.FirstAsync(g => g.Name == "E2E Test Game");

        var payload = new
        {
            gameId = game.Id,
            players = new[]
            {
                new { playerName = "Player 1", playerOrder = 1 },
                new { playerName = "Player 2", playerOrder = 2 }
            }
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/sessions", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<GameSessionDto>();
        result.Should().NotBeNull();
        result!.GameId.Should().Be(game.Id);
        result.Players.Should().HaveCount(2);
        result.Status.Should().Be("InProgress");
    }

    [Fact]
    public async Task StartSession_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        ClearAuthentication();
        var game = await DbContext.Games.FirstAsync(g => g.Name == "E2E Test Game");

        var payload = new
        {
            gameId = game.Id,
            players = new[]
            {
                new { playerName = "Player 1", playerOrder = 1 }
            }
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/sessions", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CompleteSession_ValidSession_MarksAsCompleted()
    {
        // Arrange - Create session first
        var email = $"complete_{Guid.NewGuid():N}@example.com";
        var (sessionToken, _) = await RegisterUserAsync(email, "ValidPassword123!");
        SetSessionCookie(sessionToken);

        var game = await DbContext.Games.FirstAsync(g => g.Name == "E2E Test Game");

        var createPayload = new
        {
            gameId = game.Id,
            players = new[]
            {
                new { playerName = "Player 1", playerOrder = 1 },
                new { playerName = "Player 2", playerOrder = 2 }
            }
        };

        var createResponse = await Client.PostAsJsonAsync("/api/v1/sessions", createPayload);
        createResponse.EnsureSuccessStatusCode();
        var session = await createResponse.Content.ReadFromJsonAsync<GameSessionDto>();

        // Act - Complete the session
        var completePayload = new { winnerName = "Player 1" };
        var completeResponse = await Client.PostAsJsonAsync($"/api/v1/sessions/{session!.Id}/complete", completePayload);

        // Assert
        completeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await completeResponse.Content.ReadFromJsonAsync<GameSessionDto>();
        result.Should().NotBeNull();
        result!.Status.Should().Be("Completed");
    }

    [Fact]
    public async Task AbandonSession_ValidSession_MarksAsAbandoned()
    {
        // Arrange - Create session first
        var email = $"abandon_{Guid.NewGuid():N}@example.com";
        var (sessionToken, _) = await RegisterUserAsync(email, "ValidPassword123!");
        SetSessionCookie(sessionToken);

        var game = await DbContext.Games.FirstAsync(g => g.Name == "E2E Test Game");

        var createPayload = new
        {
            gameId = game.Id,
            players = new[]
            {
                new { playerName = "Player 1", playerOrder = 1 }
            }
        };

        var createResponse = await Client.PostAsJsonAsync("/api/v1/sessions", createPayload);
        createResponse.EnsureSuccessStatusCode();
        var session = await createResponse.Content.ReadFromJsonAsync<GameSessionDto>();

        // Act - Abandon the session
        var abandonResponse = await Client.PostAsJsonAsync($"/api/v1/sessions/{session!.Id}/abandon", new { });

        // Assert
        abandonResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await abandonResponse.Content.ReadFromJsonAsync<GameSessionDto>();
        result.Should().NotBeNull();
        result!.Status.Should().Be("Abandoned");
    }

    [Fact]
    public async Task PauseAndResumeSession_ValidSession_ChangesStatusCorrectly()
    {
        // Arrange - Create session first
        var email = $"pauseresume_{Guid.NewGuid():N}@example.com";
        var (sessionToken, _) = await RegisterUserAsync(email, "ValidPassword123!");
        SetSessionCookie(sessionToken);

        var game = await DbContext.Games.FirstAsync(g => g.Name == "E2E Test Game");

        var createPayload = new
        {
            gameId = game.Id,
            players = new[]
            {
                new { playerName = "Player 1", playerOrder = 1 }
            }
        };

        var createResponse = await Client.PostAsJsonAsync("/api/v1/sessions", createPayload);
        createResponse.EnsureSuccessStatusCode();
        var session = await createResponse.Content.ReadFromJsonAsync<GameSessionDto>();

        // Act - Pause the session
        var pauseResponse = await Client.PostAsJsonAsync($"/api/v1/sessions/{session!.Id}/pause", new { });
        pauseResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var pausedSession = await pauseResponse.Content.ReadFromJsonAsync<GameSessionDto>();
        pausedSession!.Status.Should().Be("Paused");

        // Act - Resume the session
        var resumeResponse = await Client.PostAsJsonAsync($"/api/v1/sessions/{session.Id}/resume", new { });
        resumeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var resumedSession = await resumeResponse.Content.ReadFromJsonAsync<GameSessionDto>();
        resumedSession!.Status.Should().Be("InProgress");
    }

    #endregion

    #region Complete Game Journey Tests

    [Fact]
    public async Task CompleteGameJourney_BrowseStartPlayComplete_Succeeds()
    {
        // Step 1: Browse games (public)
        ClearAuthentication();
        var gamesResponse = await Client.GetAsync("/api/v1/games");
        gamesResponse.EnsureSuccessStatusCode();

        var games = await gamesResponse.Content.ReadFromJsonAsync<PaginatedGamesResponse>();
        var testGame = games!.Games!.First(g => g.Name == "E2E Test Game");

        // Step 2: View game details (public)
        var gameDetailsResponse = await Client.GetAsync($"/api/v1/games/{testGame.Id}");
        gameDetailsResponse.EnsureSuccessStatusCode();

        // Step 3: Register/Login to play
        var email = $"gamejourney_{Guid.NewGuid():N}@example.com";
        var (sessionToken, userId) = await RegisterUserAsync(email, "ValidPassword123!");
        SetSessionCookie(sessionToken);

        // Step 4: Start a game session
        var startPayload = new
        {
            gameId = testGame.Id,
            players = new[]
            {
                new { playerName = "Alice", playerOrder = 1 },
                new { playerName = "Bob", playerOrder = 2 },
                new { playerName = "Charlie", playerOrder = 3 }
            }
        };

        var startResponse = await Client.PostAsJsonAsync("/api/v1/sessions", startPayload);
        startResponse.EnsureSuccessStatusCode();
        var session = await startResponse.Content.ReadFromJsonAsync<GameSessionDto>();

        session.Should().NotBeNull();
        session!.Status.Should().Be("InProgress");
        session.Players.Should().HaveCount(3);

        // Step 5: Complete the game with a winner
        var completePayload = new { winnerName = "Bob" };
        var completeResponse = await Client.PostAsJsonAsync($"/api/v1/sessions/{session.Id}/complete", completePayload);
        completeResponse.EnsureSuccessStatusCode();

        var completedSession = await completeResponse.Content.ReadFromJsonAsync<GameSessionDto>();
        completedSession!.Status.Should().Be("Completed");
        completedSession.Winner.Should().Be("Bob");
    }

    [Fact]
    public async Task AddPlayerToSession_DuringGame_UpdatesPlayers()
    {
        // Arrange - Create session first
        var email = $"addplayer_{Guid.NewGuid():N}@example.com";
        var (sessionToken, _) = await RegisterUserAsync(email, "ValidPassword123!");
        SetSessionCookie(sessionToken);

        var game = await DbContext.Games.FirstAsync(g => g.Name == "E2E Test Game");

        var createPayload = new
        {
            gameId = game.Id,
            players = new[]
            {
                new { playerName = "Player 1", playerOrder = 1 }
            }
        };

        var createResponse = await Client.PostAsJsonAsync("/api/v1/sessions", createPayload);
        createResponse.EnsureSuccessStatusCode();
        var session = await createResponse.Content.ReadFromJsonAsync<GameSessionDto>();

        // Act - Add another player
        var addPlayerPayload = new { playerName = "Player 2", playerOrder = 2 };
        var addResponse = await Client.PostAsJsonAsync($"/api/v1/sessions/{session!.Id}/players", addPlayerPayload);

        // Assert
        addResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var updatedSession = await addResponse.Content.ReadFromJsonAsync<GameSessionDto>();
        updatedSession.Should().NotBeNull();
        updatedSession!.Players.Should().HaveCount(2);
        updatedSession.Players.Should().Contain(p => p.PlayerName == "Player 2");
    }

    #endregion

    // Response DTOs
    private sealed record PaginatedGamesResponse(
        List<GameDto>? Games,
        int TotalCount,
        int Page,
        int PageSize);

    private sealed record GameDto(
        Guid Id,
        string Name,
        string? Description,
        int MinPlayers,
        int MaxPlayers,
        int? PlayTimeMinutes,
        int? YearPublished);

    private sealed record GameSessionDto(
        Guid Id,
        Guid GameId,
        string Status,
        List<PlayerDto> Players,
        string? Winner,
        DateTime CreatedAt);

    private sealed record PlayerDto(
        string PlayerName,
        int PlayerOrder,
        string? Color);
}
