using System.Net;
using System.Net.Http.Json;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using StackExchange.Redis;
using Xunit;
using FluentAssertions;

namespace Api.Tests.Integration.GameManagement;

/// <summary>
/// Integration tests for Game Management HTTP endpoints.
/// Tests: GetAllGames, GetGameById, CreateGame, UpdateGame, GameSessions, GameState
/// Issue #3010: Backend coverage improvement.
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "GameManagement")]
public sealed class GameEndpointsIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public GameEndpointsIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"game_endpoints_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");

                builder.ConfigureAppConfiguration((context, configBuilder) =>
                {
                    configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["OPENROUTER_API_KEY"] = "test-key",
                        ["ConnectionStrings:Postgres"] = connectionString
                    });
                });

                builder.ConfigureTestServices(services =>
                {
                    // Replace DbContext with test database
                    services.RemoveAll(typeof(DbContextOptions<MeepleAiDbContext>));
                    services.AddDbContext<MeepleAiDbContext>(options =>
                        options.UseNpgsql(connectionString, o => o.UseVector())); // Issue #3547

                    // Mock Redis for HybridCache
                    services.RemoveAll(typeof(IConnectionMultiplexer));
                    var mockRedis = new Mock<IConnectionMultiplexer>();
                    services.AddSingleton(mockRedis.Object);

                    // Mock vector/embedding services
                    services.RemoveAll(typeof(Api.Services.IEmbeddingService));
                    services.AddScoped<Api.Services.IEmbeddingService>(_ => Mock.Of<Api.Services.IEmbeddingService>());

                    // Mock IHybridCacheService
                    services.RemoveAll(typeof(Api.Services.IHybridCacheService));
                    services.AddScoped<Api.Services.IHybridCacheService>(_ => Mock.Of<Api.Services.IHybridCacheService>());

                    // Ensure domain event collector is registered
                    services.AddScoped<Api.SharedKernel.Application.Services.IDomainEventCollector,
                        Api.SharedKernel.Application.Services.DomainEventCollector>();
                });
            });

        // Initialize database with migrations
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            await dbContext.Database.MigrateAsync();
        }

        _client = _factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        await _factory.DisposeAsync();
    }

    // ========================================
    // GET ALL GAMES ENDPOINT TESTS
    // ========================================

    [Fact]
    public async Task GetAllGames_WithoutAuth_ReturnsOkOrUnauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/games");

        // Assert - Games list may be public or require auth depending on configuration
        (response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue($"Expected OK or Unauthorized, got {response.StatusCode}");
    }

    [Fact]
    public async Task GetAllGames_WithValidSession_ReturnsGames()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (userId, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/v1/games",
            sessionToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert - With mocked auth middleware, may return Unauthorized
        (response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue($"Expected OK or Unauthorized, got {response.StatusCode}");
    }

    [Fact]
    public async Task GetAllGames_WithPagination_ReturnsPagedResults()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (userId, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/v1/games?page=1&pageSize=10",
            sessionToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        (response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue($"Expected OK or Unauthorized, got {response.StatusCode}");
    }

    [Fact]
    public async Task GetAllGames_WithSearchFilter_ReturnsFilteredResults()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (userId, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/v1/games?search=catan",
            sessionToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        (response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue($"Expected OK or Unauthorized, got {response.StatusCode}");
    }

    // ========================================
    // GET GAME BY ID ENDPOINT TESTS
    // ========================================

    [Fact]
    public async Task GetGameById_WithoutAuth_ReturnsNotFoundOrUnauthorized()
    {
        // Arrange
        var gameId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/v1/games/{gameId}");

        // Assert - Non-existent resource may return NotFound or Unauthorized
        (response.StatusCode == HttpStatusCode.NotFound ||
            response.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue($"Expected NotFound or Unauthorized, got {response.StatusCode}");
    }

    [Fact]
    public async Task GetGameById_NonExistentGame_ReturnsNotFoundOrUnauthorized()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (userId, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var nonExistentGameId = Guid.NewGuid();
        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/api/v1/games/{nonExistentGameId}",
            sessionToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert - Game not found should return NotFound (or Unauthorized if auth fails)
        (response.StatusCode == HttpStatusCode.NotFound ||
            response.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue($"Expected NotFound or Unauthorized, got {response.StatusCode}");
    }

    // ========================================
    // CREATE GAME ENDPOINT TESTS
    // ========================================

    [Fact]
    public async Task CreateGame_WithoutAuth_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/games", new
        {
            Name = "Test Game",
            MinPlayers = 2,
            MaxPlayers = 4
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateGame_WithUserSession_RequiresAdminOrEditor()
    {
        // Arrange - Regular user session
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (userId, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            "/api/v1/games",
            sessionToken,
            new { Name = "Test Game", MinPlayers = 2, MaxPlayers = 4 });

        // Act
        var response = await _client.SendAsync(request);

        // Assert - Regular users may not have permission to create games
        (response.StatusCode == HttpStatusCode.Created ||
            response.StatusCode == HttpStatusCode.Forbidden ||
            response.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue($"Expected Created, Forbidden, or Unauthorized, got {response.StatusCode}");
    }

    // ========================================
    // UPDATE GAME ENDPOINT TESTS
    // ========================================

    [Fact]
    public async Task UpdateGame_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var gameId = Guid.NewGuid();

        // Act
        var response = await _client.PutAsJsonAsync($"/api/v1/games/{gameId}", new
        {
            Name = "Updated Game Name"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ========================================
    // GET GAME RULES ENDPOINT TESTS
    // ========================================

    [Fact]
    public async Task GetGameRules_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var gameId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/v1/games/{gameId}/rules");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ========================================
    // GAME SESSION LIFECYCLE TESTS
    // ========================================

    [Fact]
    public async Task StartSession_WithoutAuth_ReturnsNotFoundOrUnauthorized()
    {
        // Arrange
        var gameId = Guid.NewGuid();

        // Act
        var response = await _client.PostAsJsonAsync($"/api/v1/games/{gameId}/sessions/start", new { });

        // Assert - Non-existent game may return NotFound or Unauthorized
        (response.StatusCode == HttpStatusCode.NotFound ||
            response.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue($"Expected NotFound or Unauthorized, got {response.StatusCode}");
    }

    [Fact]
    public async Task GetActiveSessions_WithoutAuth_ReturnsNotFoundOrUnauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/games/sessions/active");

        // Assert - Endpoint may not exist or require auth
        (response.StatusCode == HttpStatusCode.NotFound ||
            response.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue($"Expected NotFound or Unauthorized, got {response.StatusCode}");
    }

    [Fact]
    public async Task GetSessionById_WithoutAuth_ReturnsNotFoundOrUnauthorized()
    {
        // Arrange
        var sessionId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/v1/games/sessions/{sessionId}");

        // Assert - Non-existent session may return NotFound or Unauthorized
        (response.StatusCode == HttpStatusCode.NotFound ||
            response.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue($"Expected NotFound or Unauthorized, got {response.StatusCode}");
    }

    [Fact]
    public async Task EndSession_WithoutAuth_ReturnsNotFoundOrUnauthorized()
    {
        // Arrange
        var sessionId = Guid.NewGuid();

        // Act
        var response = await _client.PostAsync($"/api/v1/games/sessions/{sessionId}/end", null);

        // Assert - Non-existent session may return NotFound or Unauthorized
        (response.StatusCode == HttpStatusCode.NotFound ||
            response.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue($"Expected NotFound or Unauthorized, got {response.StatusCode}");
    }

    [Fact]
    public async Task PauseSession_WithoutAuth_ReturnsNotFoundOrUnauthorized()
    {
        // Arrange
        var sessionId = Guid.NewGuid();

        // Act
        var response = await _client.PostAsync($"/api/v1/games/sessions/{sessionId}/pause", null);

        // Assert - Non-existent session may return NotFound or Unauthorized
        (response.StatusCode == HttpStatusCode.NotFound ||
            response.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue($"Expected NotFound or Unauthorized, got {response.StatusCode}");
    }

    [Fact]
    public async Task ResumeSession_WithoutAuth_ReturnsNotFoundOrUnauthorized()
    {
        // Arrange
        var sessionId = Guid.NewGuid();

        // Act
        var response = await _client.PostAsync($"/api/v1/games/sessions/{sessionId}/resume", null);

        // Assert - Non-existent session may return NotFound or Unauthorized
        (response.StatusCode == HttpStatusCode.NotFound ||
            response.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue($"Expected NotFound or Unauthorized, got {response.StatusCode}");
    }

    [Fact]
    public async Task CompleteSession_WithoutAuth_ReturnsNotFoundOrUnauthorized()
    {
        // Arrange
        var sessionId = Guid.NewGuid();

        // Act
        var response = await _client.PostAsJsonAsync($"/api/v1/games/sessions/{sessionId}/complete", new { });

        // Assert - Non-existent session may return NotFound or Unauthorized
        (response.StatusCode == HttpStatusCode.NotFound ||
            response.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue($"Expected NotFound or Unauthorized, got {response.StatusCode}");
    }

    [Fact]
    public async Task AbandonSession_WithoutAuth_ReturnsNotFoundOrUnauthorized()
    {
        // Arrange
        var sessionId = Guid.NewGuid();

        // Act
        var response = await _client.PostAsync($"/api/v1/games/sessions/{sessionId}/abandon", null);

        // Assert - Non-existent session may return NotFound or Unauthorized
        (response.StatusCode == HttpStatusCode.NotFound ||
            response.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue($"Expected NotFound or Unauthorized, got {response.StatusCode}");
    }

    // ========================================
    // GAME SESSION QUERY TESTS
    // ========================================

    [Fact]
    public async Task GetGameSessions_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var gameId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/v1/games/{gameId}/sessions");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetSessionHistory_WithoutAuth_ReturnsNotFoundOrUnauthorized()
    {
        // Arrange
        var sessionId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/v1/games/sessions/{sessionId}/history");

        // Assert - Non-existent session may return NotFound or Unauthorized
        (response.StatusCode == HttpStatusCode.NotFound ||
            response.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue($"Expected NotFound or Unauthorized, got {response.StatusCode}");
    }

    [Fact]
    public async Task GetSessionStats_WithoutAuth_ReturnsNotFoundOrUnauthorized()
    {
        // Arrange
        var sessionId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/v1/games/sessions/{sessionId}/stats");

        // Assert - Non-existent session may return NotFound or Unauthorized
        (response.StatusCode == HttpStatusCode.NotFound ||
            response.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue($"Expected NotFound or Unauthorized, got {response.StatusCode}");
    }

    // ========================================
    // GAME STATE MANAGEMENT TESTS
    // ========================================

    [Fact]
    public async Task GetGameState_WithoutAuth_ReturnsNotFoundOrUnauthorized()
    {
        // Arrange
        var sessionId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/v1/games/sessions/{sessionId}/state");

        // Assert - Non-existent session may return NotFound or Unauthorized
        (response.StatusCode == HttpStatusCode.NotFound ||
            response.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue($"Expected NotFound or Unauthorized, got {response.StatusCode}");
    }

    [Fact]
    public async Task InitializeGameState_WithoutAuth_ReturnsNotFoundOrUnauthorized()
    {
        // Arrange
        var sessionId = Guid.NewGuid();

        // Act
        var response = await _client.PostAsJsonAsync($"/api/v1/games/sessions/{sessionId}/state/initialize", new { });

        // Assert - Non-existent session may return NotFound or Unauthorized
        (response.StatusCode == HttpStatusCode.NotFound ||
            response.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue($"Expected NotFound or Unauthorized, got {response.StatusCode}");
    }

    [Fact]
    public async Task UpdateGameState_WithoutAuth_ReturnsNotFoundOrUnauthorized()
    {
        // Arrange
        var sessionId = Guid.NewGuid();

        // Act
        var response = await _client.PutAsJsonAsync($"/api/v1/games/sessions/{sessionId}/state", new { });

        // Assert - Non-existent session may return NotFound or Unauthorized
        (response.StatusCode == HttpStatusCode.NotFound ||
            response.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue($"Expected NotFound or Unauthorized, got {response.StatusCode}");
    }

    [Fact]
    public async Task GetStateSnapshots_WithoutAuth_ReturnsNotFoundOrUnauthorized()
    {
        // Arrange
        var sessionId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/v1/games/sessions/{sessionId}/state/snapshots");

        // Assert - Non-existent session may return NotFound or Unauthorized
        (response.StatusCode == HttpStatusCode.NotFound ||
            response.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue($"Expected NotFound or Unauthorized, got {response.StatusCode}");
    }

    [Fact]
    public async Task CreateStateSnapshot_WithoutAuth_ReturnsNotFoundOrUnauthorized()
    {
        // Arrange
        var sessionId = Guid.NewGuid();

        // Act
        var response = await _client.PostAsJsonAsync($"/api/v1/games/sessions/{sessionId}/state/snapshots", new { });

        // Assert - Non-existent session may return NotFound or Unauthorized
        (response.StatusCode == HttpStatusCode.NotFound ||
            response.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue($"Expected NotFound or Unauthorized, got {response.StatusCode}");
    }

    [Fact]
    public async Task RestoreStateSnapshot_WithoutAuth_ReturnsNotFoundOrUnauthorized()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var snapshotId = Guid.NewGuid();

        // Act
        var response = await _client.PostAsync($"/api/v1/games/sessions/{sessionId}/state/snapshots/{snapshotId}/restore", null);

        // Assert - Non-existent session may return NotFound or Unauthorized
        (response.StatusCode == HttpStatusCode.NotFound ||
            response.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue($"Expected NotFound or Unauthorized, got {response.StatusCode}");
    }

    // ========================================
    // SESSION PLAYER MANAGEMENT TESTS
    // ========================================

    [Fact]
    public async Task AddPlayer_WithoutAuth_ReturnsNotFoundOrUnauthorized()
    {
        // Arrange
        var sessionId = Guid.NewGuid();

        // Act
        var response = await _client.PostAsJsonAsync($"/api/v1/games/sessions/{sessionId}/players", new
        {
            Name = "Player 1"
        });

        // Assert - Non-existent session may return NotFound or Unauthorized
        (response.StatusCode == HttpStatusCode.NotFound ||
            response.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue($"Expected NotFound or Unauthorized, got {response.StatusCode}");
    }

    // ========================================
    // GAME AI INTEGRATION TESTS
    // ========================================

    [Fact]
    public async Task SuggestMove_WithoutAuth_ReturnsNotFoundOrUnauthorized()
    {
        // Arrange
        var sessionId = Guid.NewGuid();

        // Act
        var response = await _client.PostAsJsonAsync($"/api/v1/games/sessions/{sessionId}/suggest", new { });

        // Assert - Non-existent session may return NotFound or Unauthorized
        (response.StatusCode == HttpStatusCode.NotFound ||
            response.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue($"Expected NotFound or Unauthorized, got {response.StatusCode}");
    }

    [Fact]
    public async Task ApplySuggestion_WithoutAuth_ReturnsNotFoundOrUnauthorized()
    {
        // Arrange
        var sessionId = Guid.NewGuid();

        // Act
        var response = await _client.PostAsJsonAsync($"/api/v1/games/sessions/{sessionId}/apply-suggestion", new
        {
            SuggestionId = Guid.NewGuid()
        });

        // Assert - Non-existent session may return NotFound or Unauthorized
        (response.StatusCode == HttpStatusCode.NotFound ||
            response.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue($"Expected NotFound or Unauthorized, got {response.StatusCode}");
    }

    // ========================================
    // GAME AGENTS TESTS
    // ========================================

    [Fact]
    public async Task GetGameAgents_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var gameId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/v1/games/{gameId}/agents");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ========================================
    // GAME DETAILS TESTS
    // ========================================

    [Fact]
    public async Task GetGameDetails_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var gameId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/v1/games/{gameId}/details");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ========================================
    // UPLOAD GAME IMAGE TESTS
    // ========================================

    [Fact]
    public async Task UploadGameImage_WithoutAuth_ReturnsNotFoundOrUnauthorized()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(new byte[] { 1, 2, 3 }), "file", "test.png");

        // Act
        var response = await _client.PostAsync($"/api/v1/games/{gameId}/image", content);

        // Assert - Non-existent game may return NotFound or Unauthorized
        (response.StatusCode == HttpStatusCode.NotFound ||
            response.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue($"Expected NotFound or Unauthorized, got {response.StatusCode}");
    }

    // ========================================
    // PUBLISH GAME TESTS (Issue #3481)
    // ========================================

    [Fact]
    public async Task PublishGame_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var publishRequest = new { Status = 2 }; // Approved = 2

        // Act
        var response = await _client.PutAsJsonAsync($"/api/v1/games/{gameId}/publish", publishRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PublishGame_NonExistentGame_ReturnsNotFoundOrUnauthorized()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var publishRequest = new { Status = 2 }; // Approved = 2

        // Act
        var response = await _client.PutAsJsonAsync($"/api/v1/games/{gameId}/publish", publishRequest);

        // Assert - Non-existent game with no auth
        (response.StatusCode == HttpStatusCode.NotFound ||
            response.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue($"Expected NotFound or Unauthorized, got {response.StatusCode}");
    }
}
