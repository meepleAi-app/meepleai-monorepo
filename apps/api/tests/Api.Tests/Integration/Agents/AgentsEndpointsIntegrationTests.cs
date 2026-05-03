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
}

/// <summary>
/// HTTP response shape returned by <c>GET /api/v1/agents</c>.
/// Mirrors anonymous object created in <c>AgentsEndpoints.MapGetAgentsEndpoint</c>.
/// </summary>
internal record GetAllAgentsResponse(bool Success, List<AgentDto> Agents, int Count);
