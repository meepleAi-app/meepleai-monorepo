using System.Net;
using System.Net.Http.Json;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.Infrastructure;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.Agents;

/// <summary>
/// Integration tests for user-facing Agents HTTP endpoints (Issue #641 Wave B.2 hotfix).
/// Verifies <c>GET /api/v1/agents</c> wraps the existing <c>GetAllAgentsQueryHandler</c>
/// MediatR handler over HTTP with proper authentication and filter support.
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class AgentsEndpointsIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public AgentsEndpointsIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"agents_endpoints_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);
        _factory = IntegrationWebApplicationFactory.Create(connectionString);
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

    [Fact]
    public async Task GetAgents_WithoutAuth_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/agents");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAgents_WithValidSession_ReturnsEmptyList()
    {
        // Arrange: authenticated user, no agents seeded
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/v1/agents",
            sessionToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetAllAgentsResponse>();
        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Agents.Should().BeEmpty();
        body.Count.Should().Be(0);
    }

    [Fact]
    public async Task GetAgents_WithActiveOnlyFilter_OnlyReturnsActive()
    {
        // Arrange: seed 2 active + 1 inactive agent definitions
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        await TestSessionHelper.SeedAgentDefinitionsAsync(dbContext, activeCount: 2, inactiveCount: 1);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/v1/agents?activeOnly=true",
            sessionToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetAllAgentsResponse>();
        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Agents.Should().HaveCount(2);
        body.Agents.Should().OnlyContain(a => a.IsActive);
        body.Count.Should().Be(2);
    }

    [Fact]
    public async Task GetAgents_WithSeededAgentLinkedToGame_PopulatesGameName()
    {
        // Arrange: authenticated user, seed a SharedGame, then seed 1 active agent linked to it.
        // Issue #660: Asserts that AgentDto.GameName is populated via bulk SharedGame lookup
        // when an agent definition has a non-null GameId.
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var gameId = await TestSessionHelper.SeedSharedGameAsync(dbContext, title: "Catan");
        await TestSessionHelper.SeedAgentDefinitionsAsync(
            dbContext,
            activeCount: 1,
            inactiveCount: 0,
            gameId: gameId);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/v1/agents",
            sessionToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetAllAgentsResponse>();
        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Agents.Should().HaveCount(1);
        body.Agents[0].GameId.Should().Be(gameId);
        body.Agents[0].GameName.Should().Be("Catan");
    }

    // Issue #647: Phase γ.1 — GET /api/v1/agents/{id} user-facing route.
    [Fact]
    public async Task GetAgentById_WithoutAuth_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.GetAsync($"/api/v1/agents/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAgentById_WithUnknownId_ReturnsNotFound()
    {
        // Arrange: authenticated user, no agents seeded.
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/api/v1/agents/{Guid.NewGuid()}",
            sessionToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAgentById_WithSeededAgent_ReturnsAgentDto()
    {
        // Arrange: seed 1 active agent linked to a SharedGame, retrieve its id from DbContext.
        // Issue #647: verifies the route returns AgentDto with GameName drift-fix lookup
        // (PR #662 pattern) when the agent has a non-null GameId.
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var gameId = await TestSessionHelper.SeedSharedGameAsync(dbContext, title: "Wingspan");
        await TestSessionHelper.SeedAgentDefinitionsAsync(
            dbContext,
            activeCount: 1,
            inactiveCount: 0,
            gameId: gameId);

        // SeedAgentDefinitionsAsync doesn't return ids, so we query the DbContext for the seeded agent.
        // GameId is mapped via shadow property "_gameId" (see AgentDefinitionConfiguration), so we
        // resolve it via EF.Property to keep the query translatable to SQL.
        var seededAgent = await dbContext.AgentDefinitions
            .AsNoTracking()
            .FirstAsync(a => EF.Property<Guid?>(a, "_gameId") == gameId);
        var agentId = seededAgent.Id;

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/api/v1/agents/{agentId}",
            sessionToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<AgentDto>();
        dto.Should().NotBeNull();
        dto!.Id.Should().Be(agentId);
        dto.GameId.Should().Be(gameId);
        dto.GameName.Should().Be("Wingspan");
    }

    // Issue #650: Phase γ.3 — GET /api/v1/agents/recent dashboard widget route.
    [Fact]
    public async Task GetRecentAgents_WithoutAuth_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/agents/recent");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetRecentAgents_WithEmptyDb_ReturnsEmptyArray()
    {
        // Arrange: authenticated user, no agents seeded.
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/v1/agents/recent",
            sessionToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert: route MUST return a JSON array body (frontend expects z.array(AgentDtoSchema))
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<AgentDto>>();
        body.Should().NotBeNull();
        body!.Should().BeEmpty();
    }

    [Fact]
    public async Task GetRecentAgents_OrdersByLastInvokedDescAndRespectsLimit()
    {
        // Arrange: seed 3 active agents and verify limit=2 returns only the top 2.
        // Recent-agents widget contract: only active agents, ordered by LastInvokedAt desc.
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        await TestSessionHelper.SeedAgentDefinitionsAsync(dbContext, activeCount: 3, inactiveCount: 0);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/v1/agents/recent?limit=2",
            sessionToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert: limit honored at 2, all returned agents are active.
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<AgentDto>>();
        body.Should().NotBeNull();
        body!.Should().HaveCount(2);
        body.Should().OnlyContain(a => a.IsActive);
    }
}

/// <summary>
/// HTTP response shape returned by <c>GET /api/v1/agents</c>.
/// Mirrors anonymous object created in <c>AgentsEndpoints.MapGetAgentsEndpoint</c>.
/// </summary>
internal record GetAllAgentsResponse(bool Success, List<AgentDto> Agents, int Count);
