using System.Net;
using System.Net.Http.Json;
using Api.Infrastructure;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
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

namespace Api.Tests.Integration.KnowledgeBase;

/// <summary>
/// Integration smoke tests for Agent HTTP endpoints.
/// Issue #3173: [RAG-002] Agent Endpoints Smoke Test.
/// Validates all 8 agent endpoints return 2xx responses for happy paths.
/// </summary>
[Collection("SharedTestcontainers")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Epic", "RAG-Prerequisites")]
public sealed class AgentEndpointsSmokeTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    // Test data IDs (populated during test execution)
    private Guid _agentId;
    private Guid _gameId;
    private string _adminCookie = null!;
    private string _userCookie = null!;

    public AgentEndpointsSmokeTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"agent_smoke_{Guid.NewGuid():N}";
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
                        options.UseNpgsql(connectionString));

                    // Mock Redis for HybridCache
                    services.RemoveAll(typeof(IConnectionMultiplexer));
                    var mockRedis = new Mock<IConnectionMultiplexer>();
                    services.AddSingleton(mockRedis.Object);

                    // Mock vector/embedding services to avoid external dependencies
                    services.RemoveAll(typeof(Api.Services.IQdrantService));
                    services.RemoveAll(typeof(Api.Services.IEmbeddingService));

                    var mockQdrant = new Mock<Api.Services.IQdrantService>();
                    var mockEmbedding = new Mock<Api.Services.IEmbeddingService>();

                    // Setup mock responses for InvokeAgent test
                    var dummyEmbedding = new float[384];
                    var embeddingResult = new Api.Services.EmbeddingResult
                    {
                        Success = true,
                        Embeddings = new List<float[]> { dummyEmbedding }
                    };

                    mockEmbedding
                        .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync(embeddingResult);

                    mockQdrant
                        .Setup(q => q.SearchAsync(
                            It.IsAny<string>(),
                            It.IsAny<float[]>(),
                            It.IsAny<int>(),
                            It.IsAny<IReadOnlyList<string>?>(),
                            It.IsAny<CancellationToken>()))
                        .ReturnsAsync(Api.Services.SearchResult.CreateSuccess(new List<Api.Services.SearchResultItem>()));

                    services.AddScoped<Api.Services.IQdrantService>(_ => mockQdrant.Object);
                    services.AddScoped<Api.Services.IEmbeddingService>(_ => mockEmbedding.Object);

                    // Mock HybridCache infrastructure (both HybridCache + IHybridCacheService required for event handlers)
                    services.AddHybridCache();
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

        // Seed test data via API (using registration + admin wizard)
        await SeedTestDataAsync();
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        if (_factory != null)
        {
            await _factory.DisposeAsync();
        }
        await _fixture.DropIsolatedDatabaseAsync(_testDbName);
    }

    private async Task SeedTestDataAsync()
    {
        // Create admin user via registration
        var adminEmail = $"admin-{Guid.NewGuid():N}@test.com";
        var adminRegisterResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            Email = adminEmail,
            Password = "Admin123!",
            DisplayName = "Admin User"
        });
        Assert.Equal(HttpStatusCode.OK, adminRegisterResponse.StatusCode);
        _adminCookie = adminRegisterResponse.Headers.GetValues("Set-Cookie").First();

        // Manually set admin role in DB (bypass wizard for smoke test)
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var adminUser = await dbContext.Users.FirstAsync(u => u.Email == adminEmail);
            adminUser.Role = "Admin";
            await dbContext.SaveChangesAsync();
            dbContext.ChangeTracker.Clear();
        }

        // Create regular user via registration
        var userEmail = $"user-{Guid.NewGuid():N}@test.com";
        var userRegisterResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            Email = userEmail,
            Password = "User123!",
            DisplayName = "Regular User"
        });
        Assert.Equal(HttpStatusCode.OK, userRegisterResponse.StatusCode);
        _userCookie = userRegisterResponse.Headers.GetValues("Set-Cookie").First();

        // Create test game directly in DB
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            _gameId = Guid.NewGuid();
            var game = new Api.Infrastructure.Entities.GameEntity
            {
                Id = _gameId,
                Name = "Chess",
                BggId = 171,
                CreatedAt = DateTime.UtcNow
            };
            dbContext.Games.Add(game);
            await dbContext.SaveChangesAsync();
            dbContext.ChangeTracker.Clear();
        }
    }

    // ========================================
    // SMOKE TEST 1: POST /agents - Create Agent (Admin)
    // ========================================

    [Fact]
    public async Task Test01_CreateAgent_WithAdminAuth_Returns201Created()
    {
        // Arrange
        var request = new
        {
            name = "Chess Rules Expert",
            type = "RulesExpert",
            strategyName = "HybridSearch",
            strategyParameters = new Dictionary<string, object>
            {
                ["vectorWeight"] = 0.7,
                ["topK"] = 5
            },
            isActive = true
        };

        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("Cookie", _adminCookie);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/agents", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<AgentResponse>();
        Assert.NotNull(result);
        Assert.Equal("Chess Rules Expert", result.Name);
        Assert.Equal("RulesExpert", result.Type);
        Assert.Equal("HybridSearch", result.StrategyName);
        Assert.NotEqual(Guid.Empty, result.Id);

        // Store agent ID for subsequent tests
        _agentId = result.Id;
    }

    // ========================================
    // SMOKE TEST 2: GET /agents - List All Agents
    // ========================================

    [Fact]
    public async Task Test02_GetAllAgents_WithAuth_Returns200Ok()
    {
        // Arrange
        await Test01_CreateAgent_WithAdminAuth_Returns201Created(); // Ensure agent exists
        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("Cookie", _userCookie);

        // Act
        var response = await _client.GetAsync("/api/v1/agents");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<AgentsListResponse>();
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Agents);
        Assert.True(result.Count > 0);
    }

    // ========================================
    // SMOKE TEST 3: GET /agents/{id} - Get Agent by ID
    // ========================================

    [Fact]
    public async Task Test03_GetAgentById_WithAuth_Returns200Ok()
    {
        // Arrange
        await Test01_CreateAgent_WithAdminAuth_Returns201Created();
        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("Cookie", _userCookie);

        // Act
        var response = await _client.GetAsync($"/api/v1/agents/{_agentId}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<AgentResponse>();
        Assert.NotNull(result);
        Assert.Equal(_agentId, result.Id);
        Assert.Equal("Chess Rules Expert", result.Name);
    }

    // ========================================
    // SMOKE TEST 4: PUT /agents/{id}/configure - Configure Agent (Admin)
    // ========================================

    [Fact]
    public async Task Test04_ConfigureAgent_WithAdminAuth_Returns200Ok()
    {
        // Arrange
        await Test01_CreateAgent_WithAdminAuth_Returns201Created();
        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("Cookie", _adminCookie);

        var request = new
        {
            strategyName = "HybridSearch",
            strategyParameters = new Dictionary<string, object>
            {
                ["vectorWeight"] = 0.8,
                ["topK"] = 10
            }
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/v1/agents/{_agentId}/configure", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<ConfigureAgentResponse>();
        Assert.NotNull(result);
        Assert.True(result.Success);
    }

    // ========================================
    // SMOKE TEST 5: POST /agents/{id}/invoke - Invoke Agent with RAG
    // ========================================

    [Fact]
    public async Task Test05_InvokeAgent_WithAuth_Returns200Ok()
    {
        // Arrange
        await Test01_CreateAgent_WithAdminAuth_Returns201Created();
        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("Cookie", _userCookie);

        var request = new
        {
            query = "How do pawns move in chess?",
            gameId = _gameId
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/v1/agents/{_agentId}/invoke", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<InvokeAgentResponse>();
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.InvocationId);
        Assert.NotNull(result.Answer);
    }

    // ========================================
    // SMOKE TEST 6: POST /agents/qa/stream - SSE Streaming QA
    // ========================================

    [Fact]
    public async Task Test06_QaStream_WithAuth_Returns200OkWithSseEvents()
    {
        // Arrange
        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("Cookie", _userCookie);
        _client.DefaultRequestHeaders.Add("Accept", "text/event-stream");

        var request = new
        {
            query = "Explain castling rules in chess",
            gameId = _gameId,
            generateFollowUps = true
        };

        var content = JsonContent.Create(request);

        // Act
        var response = await _client.PostAsync("/api/v1/agents/qa/stream", content);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);

        // Read first few SSE events to validate streaming works
        using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);

        var eventsRead = 0;
        var maxEvents = 5; // Read first 5 events
        var timeout = TimeSpan.FromSeconds(10);
        using var cts = new CancellationTokenSource(timeout);

        try
        {
            while (eventsRead < maxEvents)
            {
                var line = await reader.ReadLineAsync(cts.Token);
                if (line == null) break; // End of stream

                if (!string.IsNullOrWhiteSpace(line))
                {
                    eventsRead++;
                }
            }

            Assert.True(eventsRead > 0, "Should receive at least one SSE event");
        }
        catch (OperationCanceledException)
        {
            // Timeout is acceptable for smoke test (streaming validation passed)
            Assert.True(eventsRead > 0, "Should receive at least one SSE event before timeout");
        }
    }

    // ========================================
    // SMOKE TEST 7: GET /library/games/{gameId}/agent-config - Get User Agent Config
    // ========================================

    [Fact]
    public async Task Test07_GetGameAgentConfig_WithAuth_Returns200Or404()
    {
        // Arrange
        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("Cookie", _userCookie);

        // Act
        var response = await _client.GetAsync($"/api/v1/library/games/{_gameId}/agent-config");

        // Assert
        // Config may not exist yet (404) or exist (200), both are valid for smoke test
        Assert.True(
            response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.NotFound,
            $"Expected OK or NotFound, got {response.StatusCode}");
    }

    // ========================================
    // SMOKE TEST 8: POST /library/games/{gameId}/agent-config - Save User Agent Config
    // ========================================

    [Fact]
    public async Task Test08_SaveGameAgentConfig_WithAuth_Returns200Ok()
    {
        // Arrange
        await Test01_CreateAgent_WithAdminAuth_Returns201Created();
        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("Cookie", _userCookie);

        var request = new
        {
            agentId = _agentId,
            strategyName = "HybridSearch",
            strategyParameters = new Dictionary<string, object>
            {
                ["vectorWeight"] = 0.75,
                ["topK"] = 8
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/v1/library/games/{_gameId}/agent-config", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ========================================
    // Response DTOs (internal to test project)
    // ========================================

    private record AgentResponse(
        Guid Id,
        string Name,
        string Type,
        string StrategyName,
        bool IsActive
    );

    private record AgentsListResponse(
        bool Success,
        List<AgentResponse> Agents,
        int Count
    );

    private record ConfigureAgentResponse(
        bool Success,
        string? Message = null
    );

    private record InvokeAgentResponse(
        Guid InvocationId,
        string Answer,
        double Confidence,
        int ResultCount
    );
}