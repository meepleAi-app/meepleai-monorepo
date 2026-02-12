using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
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
/// Integration tests for Agent Chat SSE streaming endpoints.
/// Issue #4126: API Integration for Agent Chat
/// </summary>
[Collection("SharedTestcontainers")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class AgentChatEndpointsIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private Guid _testAgentId;

    public AgentChatEndpointsIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"agent_chat_{Guid.NewGuid():N}";
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
                        options.UseNpgsql(connectionString, o => o.UseVector()));

                    // Mock Redis for HybridCache
                    services.RemoveAll(typeof(IConnectionMultiplexer));
                    var mockRedis = new Mock<IConnectionMultiplexer>();
                    services.AddSingleton(mockRedis.Object);

                    // Mock IHybridCacheService
                    services.RemoveAll(typeof(IHybridCacheService));
                    services.AddScoped<IHybridCacheService>(_ => Mock.Of<IHybridCacheService>());

                    // Mock ILlmService for controlled streaming
                    services.RemoveAll(typeof(ILlmService));
                    var mockLlmService = new Mock<ILlmService>();
                    mockLlmService
                        .Setup(s => s.GenerateCompletionStreamAsync(
                            It.IsAny<string>(),
                            It.IsAny<string>(),
                            It.IsAny<CancellationToken>()))
                        .Returns(GetMockStreamChunks());
                    services.AddScoped<ILlmService>(_ => mockLlmService.Object);

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

            // Seed test agent
            var agentId = Guid.NewGuid();
            var strategy = AgentStrategy.Custom("default", new Dictionary<string, object>(StringComparer.Ordinal));
            var agent = new AgentEntity
            {
                Id = agentId,
                Name = "Test Agent",
                Type = AgentType.RagAgent.Value,
                StrategyName = strategy.Name,
                StrategyParametersJson = System.Text.Json.JsonSerializer.Serialize(strategy.Parameters),
                IsActive = true
            };
            _testAgentId = agent.Id;
            dbContext.Agents.Add(agent);
            await dbContext.SaveChangesAsync();
        }

        _client = _factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        _factory?.Dispose();
        await _fixture.DropIsolatedDatabaseAsync(_testDbName);
    }

    [Fact]
    public async Task ChatWithAgent_Should_Return_SSE_Stream_When_Authenticated()
    {
        // Arrange
        var sessionToken = await CreateAuthenticatedSessionAsync();
        _client.DefaultRequestHeaders.Add("X-Session-Token", sessionToken);

        var request = new { Message = "What are the rules for Catan?" };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/v1/agents/{_testAgentId}/chat", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);

        // Validate SSE stream
        var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);

        var events = new List<RagStreamingEvent>();
        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            if (line.StartsWith("data: ", StringComparison.Ordinal))
            {
                var json = line["data: ".Length..];
                var @event = JsonSerializer.Deserialize<RagStreamingEvent>(json);
                Assert.NotNull(@event);
                events.Add(@event);
            }
        }

        // Verify event sequence
        Assert.NotEmpty(events);
        Assert.Contains(events, e => e.Type == StreamingEventType.StateUpdate);
        Assert.Contains(events, e => e.Type == StreamingEventType.Token);
        Assert.Contains(events, e => e.Type == StreamingEventType.Complete);

        // Verify event order: StateUpdate → Token(s) → Complete
        var stateUpdateIndex = events.FindIndex(e => e.Type == StreamingEventType.StateUpdate);
        var completeIndex = events.FindIndex(e => e.Type == StreamingEventType.Complete);
        Assert.True(stateUpdateIndex < completeIndex, "StateUpdate should come before Complete");
    }

    [Fact]
    public async Task ChatWithAgent_Should_Return_401_When_Not_Authenticated()
    {
        // Arrange
        var request = new { Message = "What are the rules for Catan?" };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/v1/agents/{_testAgentId}/chat", request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ChatWithAgent_Should_Return_Error_Event_When_Agent_Not_Found()
    {
        // Arrange
        var sessionToken = await CreateAuthenticatedSessionAsync();
        _client.DefaultRequestHeaders.Add("X-Session-Token", sessionToken);

        var nonExistentAgentId = Guid.NewGuid();
        var request = new { Message = "Test question" };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/v1/agents/{nonExistentAgentId}/chat", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); // SSE returns 200 even for errors

        // Validate error event in stream
        var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);

        var events = new List<RagStreamingEvent>();
        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            if (line.StartsWith("data: ", StringComparison.Ordinal))
            {
                var json = line["data: ".Length..];
                var @event = JsonSerializer.Deserialize<RagStreamingEvent>(json);
                Assert.NotNull(@event);
                events.Add(@event);
            }
        }

        Assert.Single(events);
        Assert.Equal(StreamingEventType.Error, events[0].Type);

        var error = JsonSerializer.Deserialize<StreamingError>(((JsonElement)events[0].Data!).GetRawText());
        Assert.NotNull(error);
        Assert.Equal("AGENT_NOT_FOUND", error.errorCode);
    }

    [Fact]
    public async Task ChatWithAgent_Should_Return_400_When_Message_Is_Empty()
    {
        // Arrange
        var sessionToken = await CreateAuthenticatedSessionAsync();
        _client.DefaultRequestHeaders.Add("X-Session-Token", sessionToken);

        var request = new { Message = "" };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/v1/agents/{_testAgentId}/chat", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ChatWithAgent_Should_Return_400_When_Message_Exceeds_MaxLength()
    {
        // Arrange
        var sessionToken = await CreateAuthenticatedSessionAsync();
        _client.DefaultRequestHeaders.Add("X-Session-Token", sessionToken);

        var longMessage = new string('x', 2001);
        var request = new { Message = longMessage };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/v1/agents/{_testAgentId}/chat", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ChatWithAgent_Should_Set_SSE_Headers_Correctly()
    {
        // Arrange
        var sessionToken = await CreateAuthenticatedSessionAsync();
        _client.DefaultRequestHeaders.Add("X-Session-Token", sessionToken);

        var request = new { Message = "Test question" };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/v1/agents/{_testAgentId}/chat", request);

        // Assert
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);
        Assert.True(response.Headers.CacheControl?.NoCache ?? false);
        Assert.Contains(response.Headers.Connection, c => c == "keep-alive");
    }

    private async Task<string> CreateAuthenticatedSessionAsync()
    {
        // Register user
        var email = $"test_{Guid.NewGuid():N}@test.com";
        var registerPayload = new
        {
            Email = email,
            Password = "TestPassword123!",
            DisplayName = "Test User"
        };

        var registerResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", registerPayload);
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        // Extract session token from Set-Cookie header
        var setCookieHeader = registerResponse.Headers.GetValues("Set-Cookie").First();
        var token = ExtractSessionTokenFromCookie(setCookieHeader);
        return token;
    }

    private static string ExtractSessionTokenFromCookie(string setCookieHeader)
    {
        // Parse: meepleai_session=TOKEN; Path=/; HttpOnly; SameSite=Lax
        var parts = setCookieHeader.Split(';');
        var cookiePart = parts[0];
        var tokenPart = cookiePart.Split('=')[1];
        return tokenPart;
    }

    private static async IAsyncEnumerable<StreamChunk> GetMockStreamChunks()
    {
        var chunks = new[]
        {
            new StreamChunk("Catan "),
            new StreamChunk("is "),
            new StreamChunk("a strategy game.")
        };

        foreach (var chunk in chunks)
        {
            await Task.Yield();
            yield return chunk;
        }
    }
}
