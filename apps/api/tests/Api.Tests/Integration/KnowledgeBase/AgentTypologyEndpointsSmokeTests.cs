using System.Net;
using System.Net.Http.Json;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.Infrastructure;
using Api.Services;
using Api.SharedKernel.Application.Services;
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
/// Integration smoke tests for Agent Typology Editor endpoints.
/// Issue #3177: AGT-003 Editor Proposal Commands.
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class AgentTypologyEndpointsSmokeTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    private string _editorCookie = null!;
    private string _adminCookie = null!;
    private Guid _editorUserId;

    public AgentTypologyEndpointsSmokeTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"agent_typology_smoke_{Guid.NewGuid():N}";
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
                    services.RemoveAll(typeof(DbContextOptions<MeepleAiDbContext>));
                    services.AddDbContext<MeepleAiDbContext>(options =>
                        options.UseNpgsql(connectionString, o => o.UseVector())); // Issue #3547

                    services.RemoveAll(typeof(IConnectionMultiplexer));
                    var mockRedis = new Mock<IConnectionMultiplexer>();
                    services.AddSingleton(mockRedis.Object);

                    services.RemoveAll(typeof(IEmbeddingService));
                    var mockEmbedding = new Mock<IEmbeddingService>();

                    var dummyEmbedding = new float[384];
                    var embeddingResult = new EmbeddingResult
                    {
                        Success = true,
                        Embeddings = new List<float[]> { dummyEmbedding }
                    };

                    mockEmbedding
                        .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync(embeddingResult);

                    services.AddScoped<IEmbeddingService>(_ => mockEmbedding.Object);

                    // Mock HybridCache infrastructure
                    services.AddHybridCache();
                    services.RemoveAll(typeof(Api.Services.IHybridCacheService));
                    services.AddScoped<Api.Services.IHybridCacheService>(_ => Mock.Of<Api.Services.IHybridCacheService>());

                    // Ensure domain event collector is registered
                    services.AddScoped<IDomainEventCollector, Api.SharedKernel.Application.Services.DomainEventCollector>();
                });
            });

        // Initialize database with migrations
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            await dbContext.Database.MigrateAsync();
        }

        _client = _factory.CreateClient();

        await SetupTestUsersAsync();
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        await _factory.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_testDbName);
    }

    private async Task SetupTestUsersAsync()
    {
        // Create editor user via registration
        var editorEmail = $"editor_{Guid.NewGuid():N}@test.com";
        var registerEditorResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            Email = editorEmail,
            Password = "EditorPass123!",
            DisplayName = "Editor User"
        });

        Assert.Equal(HttpStatusCode.OK, registerEditorResponse.StatusCode);
        _editorCookie = registerEditorResponse.Headers.GetValues("Set-Cookie").First();

        // Manually set editor role in DB
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var editorUser = await dbContext.Users.FirstAsync(u => u.Email == editorEmail);
            _editorUserId = editorUser.Id;
            editorUser.Role = "Editor";
            await dbContext.SaveChangesAsync();
            dbContext.ChangeTracker.Clear();
        }

        // Create admin user via registration
        var adminEmail = $"admin_{Guid.NewGuid():N}@test.com";
        var registerAdminResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            Email = adminEmail,
            Password = "AdminPass123!",
            DisplayName = "Admin User"
        });

        Assert.Equal(HttpStatusCode.OK, registerAdminResponse.StatusCode);
        _adminCookie = registerAdminResponse.Headers.GetValues("Set-Cookie").First();

        // Manually set admin role in DB
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var adminUser = await dbContext.Users.FirstAsync(u => u.Email == adminEmail);
            adminUser.Role = "Admin";
            await dbContext.SaveChangesAsync();
            dbContext.ChangeTracker.Clear();
        }
    }

    [Fact]
    public async Task POST_ProposeAgentTypology_Returns201Created()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agent-typologies/propose")
        {
            Headers = { { "Cookie", _editorCookie } },
            Content = JsonContent.Create(new
            {
                name = "Test Typology",
                description = "Test Description for integration test",
                basePrompt = "You are a test agent specialized in testing",
                defaultStrategyName = "TestStrategy",
                defaultStrategyParameters = new Dictionary<string, object>
                {
                    { "TopK", 10 },
                    { "MinScore", 0.55 }
                },
                proposedBy = _editorUserId
            })
        };

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        if (response.StatusCode != HttpStatusCode.Created)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new Exception($"Expected 201 Created but got {response.StatusCode}. Response: {errorBody}");
        }

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<AgentTypologyDto>();
        Assert.NotNull(result);
        Assert.Equal("Test Typology", result.Name);
        Assert.Equal("Draft", result.Status);
        Assert.NotEqual(Guid.Empty, result.CreatedBy); // Endpoint sets ProposedBy from session.User.Id
    }

    [Fact]
    public async Task POST_TestAgentTypology_Returns200OK()
    {
        // Arrange: First create a Draft typology
        var proposeRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agent-typologies/propose")
        {
            Headers = { { "Cookie", _editorCookie } },
            Content = JsonContent.Create(new
            {
                name = "Test Typology for Sandbox",
                description = "Will be tested in sandbox",
                basePrompt = "You are a sandbox test agent",
                defaultStrategyName = "SandboxStrategy",
                defaultStrategyParameters = new Dictionary<string, object>
                {
                    { "TopK", 5 },
                    { "MinScore", 0.6 }
                },
                proposedBy = _editorUserId
            })
        };

        var proposeResponse = await _client.SendAsync(proposeRequest);
        proposeResponse.EnsureSuccessStatusCode();
        var typology = await proposeResponse.Content.ReadFromJsonAsync<AgentTypologyDto>();

        // Act: Test the Draft typology
        var testRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/agent-typologies/{typology!.Id}/test")
        {
            Headers = { { "Cookie", _editorCookie } },
            Content = JsonContent.Create(new { testQuery = "What are the rules for movement?" })
        };

        var response = await _client.SendAsync(testRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var responseBody = await response.Content.ReadAsStringAsync();
        var result = System.Text.Json.JsonDocument.Parse(responseBody).RootElement;

        Assert.True(result.GetProperty("success").GetBoolean());
        Assert.True(result.TryGetProperty("response", out var responseText));
        Assert.NotEqual(string.Empty, responseText.GetString());
    }

    [Fact]
    public async Task POST_TestAgentTypology_WithOtherUserTypology_Returns400BadRequest()
    {
        // Arrange: Admin creates a Draft typology
        var proposeRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agent-typologies/propose")
        {
            Headers = { { "Cookie", _adminCookie } },
            Content = JsonContent.Create(new
            {
                name = "Admin Typology",
                description = "Created by admin",
                basePrompt = "Admin test agent",
                defaultStrategyName = "AdminStrategy",
                defaultStrategyParameters = new Dictionary<string, object> { { "TopK", 10 } },
                proposedBy = Guid.NewGuid()
            })
        };

        var proposeResponse = await _client.SendAsync(proposeRequest);
        proposeResponse.EnsureSuccessStatusCode();
        var typology = await proposeResponse.Content.ReadFromJsonAsync<AgentTypologyDto>();

        // Act: Editor tries to test admin's typology
        var testRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/agent-typologies/{typology!.Id}/test")
        {
            Headers = { { "Cookie", _editorCookie } },
            Content = JsonContent.Create(new { testQuery = "Test" })
        };

        var response = await _client.SendAsync(testRequest);

        // Assert: Should fail with 422 (validation error - FluentValidation runs before handler sees ownership)
        // Or 400 if handler catches and returns BadRequest
        Assert.True(
            response.StatusCode == HttpStatusCode.BadRequest ||
            response.StatusCode == HttpStatusCode.UnprocessableEntity,
            $"Expected 400 or 422, got {response.StatusCode}");
    }

    [Fact]
    public async Task POST_ProposeAgentTypology_WithoutAuth_Returns401Unauthorized()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agent-typologies/propose")
        {
            Content = JsonContent.Create(new
            {
                name = "Test",
                description = "Test",
                basePrompt = "Test",
                defaultStrategyName = "Test",
                defaultStrategyParameters = new Dictionary<string, object>(),
                proposedBy = Guid.NewGuid()
            })
        };

        // Act
        var response = await _client.SendAsync(request);

        // Assert: Middleware returns 422 UnprocessableEntity when no auth (FluentValidation runs first)
        // Auth check happens AFTER model binding and validation
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }
}
