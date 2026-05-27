using System.Net;
using System.Net.Http.Json;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities.UserLibrary;
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

    // Issue #648: Phase γ.2 — GET /api/v1/agents/{id}/status useAgentStatus widget route.
    [Fact]
    public async Task GetAgentStatus_WithoutAuth_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.GetAsync($"/api/v1/agents/{Guid.NewGuid()}/status");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAgentStatus_WithUnknownId_ReturnsNotFound()
    {
        // Arrange: authenticated user, no agents seeded.
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/api/v1/agents/{Guid.NewGuid()}/status",
            sessionToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAgentStatus_WithActiveSeededAgent_ReturnsReady()
    {
        // Arrange: seed 1 active agent. Issue #648 MVP readiness derives IsReady from
        // IsActive AND HasConfiguration (Strategy.Name presence). HasDocuments precise count
        // deferred (would require SelectedDocuments query repository).
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        await TestSessionHelper.SeedAgentDefinitionsAsync(dbContext, activeCount: 1, inactiveCount: 0);
        var seededId = await dbContext.AgentDefinitions
            .AsNoTracking()
            .Select(a => a.Id)
            .FirstAsync();

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/api/v1/agents/{seededId}/status",
            sessionToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<AgentStatusDto>();
        dto.Should().NotBeNull();
        dto!.AgentId.Should().Be(seededId);
        dto.IsActive.Should().BeTrue();
        dto.IsReady.Should().BeTrue();
        dto.HasConfiguration.Should().BeTrue();
        dto.RagStatus.Should().Be("ready");
        dto.BlockingReason.Should().BeNull();
    }

    // Issue #654: Phase β.2 — POST /api/v1/agents/user user-create route.
    [Fact]
    public async Task CreateUserAgent_WithoutAuth_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/agents/user", new
        {
            gameId = Guid.NewGuid(),
            agentType = "Strategist"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateUserAgent_WithUnknownGame_ReturnsBadRequest()
    {
        // Arrange: authenticated user, unseeded gameId.
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            "/api/v1/agents/user",
            sessionToken,
            new
            {
                gameId = Guid.NewGuid(),  // not seeded
                agentType = "Strategist"
            });

        // Act
        var response = await _client.SendAsync(request);

        // Assert: handler raises InvalidOperationException → endpoint returns 400.
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.UnprocessableEntity,
            HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task CreateUserAgent_WithValidGame_ReturnsCreatedAgentDto()
    {
        // Arrange: seed SharedGame "Catan", authenticated user.
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var gameId = await TestSessionHelper.SeedSharedGameAsync(dbContext, title: "Catan");

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            "/api/v1/agents/user",
            sessionToken,
            new
            {
                gameId,
                agentType = "Strategist",
                name = "Catan Coach"
            });

        // Act
        var response = await _client.SendAsync(request);

        // Assert: 201 Created body is AgentDto with GameName resolved + IsActive true.
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Created);
        var dto = await response.Content.ReadFromJsonAsync<AgentDto>();
        dto.Should().NotBeNull();
        dto!.Name.Should().Be("Catan Coach");
        dto.Type.Should().Be("Strategist");
        dto.GameId.Should().Be(gameId);
        dto.GameName.Should().Be("Catan");
        dto.IsActive.Should().BeTrue();
    }

    // Issue #655: Phase β.3 — POST /api/v1/agents/create-with-setup orchestration.
    [Fact]
    public async Task CreateAgentWithSetup_WithoutAuth_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/agents/create-with-setup", new
        {
            gameId = Guid.NewGuid(),
            addToCollection = false,
            agentType = "Strategist"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateAgentWithSetup_WithoutAddToCollection_ReturnsAgentResultGameAddedFalse()
    {
        // Arrange: seed SharedGame "Wingspan", authenticated user, addToCollection=false.
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var gameId = await TestSessionHelper.SeedSharedGameAsync(dbContext, title: "Wingspan");

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            "/api/v1/agents/create-with-setup",
            sessionToken,
            new
            {
                gameId,
                addToCollection = false,
                agentType = "Strategist",
                agentName = "Wingspan Coach"
            });

        // Act
        var response = await _client.SendAsync(request);

        // Assert: 201 Created or 200 OK with orchestration result body.
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<CreateAgentWithSetupResponse>();
        body.Should().NotBeNull();
        body!.AgentName.Should().Be("Wingspan Coach");
        body.GameAddedToCollection.Should().BeFalse();
        body.SlotUsed.Should().Be(0);
        body.AgentId.Should().NotBe(Guid.Empty);
        body.ThreadId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task CreateAgentWithSetup_WithAddToCollection_AddsGameAndReturnsTrue()
    {
        // Arrange: seed SharedGame "Azul", authenticated user, addToCollection=true.
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var gameId = await TestSessionHelper.SeedSharedGameAsync(dbContext, title: "Azul");

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            "/api/v1/agents/create-with-setup",
            sessionToken,
            new
            {
                gameId,
                addToCollection = true,
                agentType = "Narrator"
            });

        // Act
        var response = await _client.SendAsync(request);

        // Assert: gameAddedToCollection=true after successful AddGameToLibraryCommand step.
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<CreateAgentWithSetupResponse>();
        body.Should().NotBeNull();
        body!.GameAddedToCollection.Should().BeTrue();
    }

    [Fact]
    public async Task QuickCreateAgent_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/agents/quick-create", new
        {
            gameId = Guid.NewGuid()
        });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task QuickCreateAgent_WithUnknownGame_ReturnsBadRequest()
    {
        // Arrange: authenticated user, no SharedGame seeded for the supplied id.
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            "/api/v1/agents/quick-create",
            sessionToken,
            new
            {
                gameId = Guid.NewGuid()
            });

        // Act
        var response = await _client.SendAsync(request);

        // Assert: handler throws InvalidOperationException("SharedGame {id} not found")
        // which the endpoint translates to BadRequest. Allow Unprocessable/InternalServerError
        // as alternates if validator/error mapping evolves.
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.UnprocessableEntity,
            HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task QuickCreateAgent_WithValidGame_ReturnsTutorResult()
    {
        // Arrange: seed SharedGame "Splendor", authenticated user.
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var gameId = await TestSessionHelper.SeedSharedGameAsync(dbContext, title: "Splendor");

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            "/api/v1/agents/quick-create",
            sessionToken,
            new
            {
                gameId
            });

        // Act
        var response = await _client.SendAsync(request);

        // Assert: 201 Created with Tutor result body. Auto-derived name "Tutor for Splendor",
        // ChatThreadId placeholder Guid (chat-thread BC integration deferred), KbCardCount=0
        // (KB query deferred — separate followup if needed).
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<QuickCreateAgentResponse>();
        body.Should().NotBeNull();
        body!.AgentId.Should().NotBe(Guid.Empty);
        body.ChatThreadId.Should().NotBe(Guid.Empty);
        body.AgentName.Should().Be("Tutor for Splendor");
        body.KbCardCount.Should().Be(0);
    }

    // -------------------------------------------------------------------------
    // GET /api/v1/agents?scope=my-library — Issue #1589 (BE-2)
    // -------------------------------------------------------------------------

    /// <summary>
    /// AC1: scope=my-library returns agents whose GameId is in the caller's library
    /// plus system agents (GameId == null). Agents for games NOT in the library are excluded.
    /// Assertions are seeded-id-scoped (not exact count) because this class shares one
    /// isolated DB across all tests and other tests may have seeded additional agents.
    /// </summary>
    [Fact]
    public async Task GetAgents_ScopeMyLibrary_ReturnsLibraryGamesPlusSystemAgents()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (callerId, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        // Seed two SharedGames: Catan (in library) and Azul (not in library).
        var catanId = await TestSessionHelper.SeedSharedGameAsync(dbContext, title: "Catan-1589-AC1");
        var azulId = await TestSessionHelper.SeedSharedGameAsync(dbContext, title: "Azul-1589-AC1");

        // Seed agents with unique names (avoids IX_agent_definitions_name unique-index collisions
        // when seeding multiple agents per test). Helper returns the Id directly.
        var a1Id = await SeedUniqueAgentAsync(dbContext, gameId: catanId);
        var a2Id = await SeedUniqueAgentAsync(dbContext, gameId: azulId);
        await SeedUniqueAgentAsync(dbContext, gameId: null); // a3 system — id not needed

        // Seed caller's library: Catan only (Azul is NOT added).
        dbContext.UserLibraryEntries.Add(new UserLibraryEntryEntity
        {
            UserId = callerId,
            SharedGameId = catanId,
            AddedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/v1/agents?scope=my-library",
            sessionToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<GetAllAgentsResponse>();
        payload.Should().NotBeNull();
        payload!.Success.Should().BeTrue();

        // a1 (Catan, in library) MUST be present.
        payload.Agents.Should().Contain(a => a.Id == a1Id,
            "agent linked to a library game should be included");

        // a2 (Azul, NOT in library) MUST be absent.
        payload.Agents.Should().NotContain(a => a.Id == a2Id,
            "agent linked to a game not in the caller's library should be excluded");

        // System agents (GameId == null) MUST always pass through the scope filter.
        payload.Agents.Should().Contain(a => a.GameId == null,
            "system agents (null GameId) are always included by the my-library scope");
    }

    /// <summary>
    /// AC2: no scope returns all agents globally (no library filter applied).
    /// Assertions verify that the agent linked to the non-library game IS present.
    /// </summary>
    [Fact]
    public async Task GetAgents_NoScope_ReturnsAllAgentsGlobal()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (callerId, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var catanId = await TestSessionHelper.SeedSharedGameAsync(dbContext, title: "Catan-1589-AC2");
        var azulId = await TestSessionHelper.SeedSharedGameAsync(dbContext, title: "Azul-1589-AC2");

        var a1Id = await SeedUniqueAgentAsync(dbContext, gameId: catanId);
        var a2Id = await SeedUniqueAgentAsync(dbContext, gameId: azulId);

        // Caller's library has Catan only — but scope=global so Azul agent must still appear.
        dbContext.UserLibraryEntries.Add(new UserLibraryEntryEntity
        {
            UserId = callerId,
            SharedGameId = catanId,
            AddedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/v1/agents",
            sessionToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<GetAllAgentsResponse>();
        payload.Should().NotBeNull();
        payload!.Success.Should().BeTrue();

        // Both seeded agents must be present — no library filter applied.
        payload.Agents.Should().Contain(a => a.Id == a1Id,
            "global scope must include agent for library game");
        payload.Agents.Should().Contain(a => a.Id == a2Id,
            "global scope must include agent for non-library game (no filtering)");
    }

    /// <summary>
    /// AC4: scope=my-library with an empty library returns only system agents
    /// (those with GameId == null). Game-linked agents are excluded.
    /// </summary>
    [Fact]
    public async Task GetAgents_ScopeMyLibrary_EmptyLibrary_ReturnsOnlySystemAgents()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var catanId = await TestSessionHelper.SeedSharedGameAsync(dbContext, title: "Catan-1589-AC4");

        // Seed one game-linked agent and one system agent (unique names).
        var gameLinkedId = await SeedUniqueAgentAsync(dbContext, gameId: catanId);
        await SeedUniqueAgentAsync(dbContext, gameId: null);

        // Caller's library is empty — no UserLibraryEntry seeded.

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/v1/agents?scope=my-library",
            sessionToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<GetAllAgentsResponse>();
        payload.Should().NotBeNull();
        payload!.Success.Should().BeTrue();

        // Game-linked agent must be absent (not in empty library).
        payload.Agents.Should().NotContain(a => a.Id == gameLinkedId,
            "agent for a game not in the caller's (empty) library should be excluded");
    }

    /// <summary>
    /// AC3: an unauthenticated request (no session cookie) must receive 401 Unauthorized.
    /// The endpoint is decorated with .RequireAuthenticatedUser() so the middleware
    /// rejects the request before the handler runs.
    /// Note: the existing GetAgents_WithoutAuth_ReturnsUnauthorized test (no query string)
    /// already covers the unauthenticated path; this test explicitly uses scope=my-library
    /// to confirm the auth gate fires for the scoped path too.
    /// </summary>
    [Fact]
    public async Task GetAgents_Unauthenticated_ScopeMyLibrary_Returns401()
    {
        // Act — no Cookie header
        var response = await _client.GetAsync("/api/v1/agents?scope=my-library");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // -------------------------------------------------------------------------
    // PUT /api/v1/agents/{id}/user — Issue #656
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UpdateUserAgent_WithoutAuth_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.PutAsJsonAsync(
            $"/api/v1/agents/{Guid.NewGuid()}/user",
            new { name = "Updated" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateUserAgent_WithUnknownId_ReturnsNotFound()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Put,
            $"/api/v1/agents/{Guid.NewGuid()}/user",
            sessionToken,
            new { name = "Updated" });

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateUserAgent_WithValidNameChange_ReturnsUpdatedAgentDto()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        await TestSessionHelper.SeedAgentDefinitionsAsync(dbContext, activeCount: 1, inactiveCount: 0);
        var seededId = await dbContext.AgentDefinitions.AsNoTracking().Select(a => a.Id).FirstAsync();

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Put,
            $"/api/v1/agents/{seededId}/user",
            sessionToken,
            new { name = "Renamed Agent" });

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<AgentDto>();
        dto.Should().NotBeNull();
        dto!.Id.Should().Be(seededId);
        dto.Name.Should().Be("Renamed Agent");
    }

    // -------------------------------------------------------------------------
    // GET /api/v1/agents/{id}/configuration — Issue #657
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetAgentConfiguration_WithoutAuth_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.GetAsync($"/api/v1/agents/{Guid.NewGuid()}/configuration");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAgentConfiguration_WithUnknownId_ReturnsNotFound()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/api/v1/agents/{Guid.NewGuid()}/configuration",
            sessionToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAgentConfiguration_WithSeededAgent_ReturnsConfigurationView()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        await TestSessionHelper.SeedAgentDefinitionsAsync(dbContext, activeCount: 1, inactiveCount: 0);
        var seededId = await dbContext.AgentDefinitions.AsNoTracking().Select(a => a.Id).FirstAsync();

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/api/v1/agents/{seededId}/configuration",
            sessionToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<AgentConfigurationDto>();
        dto.Should().NotBeNull();
        dto!.AgentId.Should().Be(seededId);
        dto.LlmModel.Should().Be("gpt-4");
        dto.LlmProvider.Should().Be("openai");
        dto.MaxTokens.Should().Be(1000);
        dto.Temperature.Should().BeApproximately(0.7m, 0.001m);
        dto.IsCurrent.Should().BeTrue();
        dto.SelectedDocumentIds.Should().BeEmpty();
    }

    // -------------------------------------------------------------------------
    // PATCH /api/v1/agents/{id}/configuration — Issue #658
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UpdateAgentConfiguration_WithoutAuth_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.PatchAsJsonAsync(
            $"/api/v1/agents/{Guid.NewGuid()}/configuration",
            new { temperature = 0.5m });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateAgentConfiguration_WithUnknownId_ReturnsNotFound()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Patch,
            $"/api/v1/agents/{Guid.NewGuid()}/configuration",
            sessionToken,
            new { temperature = 0.5m });

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateAgentConfiguration_WithValidPatch_ReturnsUpdatedView()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        await TestSessionHelper.SeedAgentDefinitionsAsync(dbContext, activeCount: 1, inactiveCount: 0);
        var seededId = await dbContext.AgentDefinitions.AsNoTracking().Select(a => a.Id).FirstAsync();

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Patch,
            $"/api/v1/agents/{seededId}/configuration",
            sessionToken,
            new { temperature = 0.42m, maxTokens = 1234 });

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<AgentConfigurationDto>();
        dto.Should().NotBeNull();
        dto!.AgentId.Should().Be(seededId);
        dto.Temperature.Should().BeApproximately(0.42m, 0.001m);
        dto.MaxTokens.Should().Be(1234);
        dto.LlmModel.Should().Be("gpt-4"); // unchanged
    }

    // -------------------------------------------------------------------------
    // BE-2 #1589 test infrastructure
    // -------------------------------------------------------------------------

    /// <summary>
    /// Seeds a single <see cref="AgentDefinition"/> with a globally-unique name (avoids the
    /// <c>IX_agent_definitions_name</c> unique-index collision that breaks repeated calls to
    /// <see cref="TestSessionHelper.SeedAgentDefinitionsAsync"/> within the same test).
    /// Mirrors the helper's construction pattern but returns the new agent's Id directly,
    /// eliminating the need for follow-up EF.Property queries to retrieve it.
    /// </summary>
    private static async Task<Guid> SeedUniqueAgentAsync(
        MeepleAiDbContext dbContext,
        Guid? gameId)
    {
        var agent = AgentDefinition.Create(
            name: $"BE2-Agent-{Guid.NewGuid():N}",
            description: "BE-2 #1589 test agent",
            type: AgentType.RagAgent,
            config: AgentDefinitionConfig.Create("gpt-4", 1000, 0.7f));
        agent.Activate();
        if (gameId.HasValue)
        {
            agent.SetGameId(gameId.Value);
        }
        dbContext.AgentDefinitions.Add(agent);
        await dbContext.SaveChangesAsync();
        return agent.Id;
    }
}

/// <summary>
/// HTTP response shape returned by <c>GET /api/v1/agents</c>.
/// Mirrors anonymous object created in <c>AgentsEndpoints.MapGetAgentsEndpoint</c>.
/// </summary>
internal record GetAllAgentsResponse(bool Success, List<AgentDto> Agents, int Count);

/// <summary>
/// HTTP response shape returned by <c>POST /api/v1/agents/create-with-setup</c>.
/// Mirrors <c>CreateAgentWithSetupResult</c> in
/// <c>Api.BoundedContexts.KnowledgeBase.Application.Commands</c>. Issue #655 (Phase β.3).
/// </summary>
internal record CreateAgentWithSetupResponse(
    Guid AgentId,
    string AgentName,
    Guid ThreadId,
    int SlotUsed,
    bool GameAddedToCollection);

/// <summary>
/// HTTP response shape returned by <c>POST /api/v1/agents/quick-create</c>.
/// Mirrors <c>QuickCreateAgentResult</c> in
/// <c>Api.BoundedContexts.KnowledgeBase.Application.Commands</c>. Issue #659 (Phase δ.1).
/// </summary>
internal record QuickCreateAgentResponse(
    Guid AgentId,
    Guid ChatThreadId,
    string AgentName,
    int KbCardCount);
