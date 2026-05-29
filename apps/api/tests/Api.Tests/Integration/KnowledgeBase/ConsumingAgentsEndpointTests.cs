using System.Net;
using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.KnowledgeBase;

/// <summary>
/// Integration tests for GET /api/v1/admin/kb/docs/{docId}/agents.
/// Issue #1651: F3-FU-2 Used-by tab — verifies JSONB containment, soft-delete exclusion,
/// system-agent visibility, GameName bulk resolution, and admin authentication.
/// Uses Testcontainers Postgres because the @> containment is not translatable on EF InMemory.
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "1651")]
public sealed class ConsumingAgentsEndpointTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public ConsumingAgentsEndpointTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"used_by_{Guid.NewGuid():N}";
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
        await _fixture.DropIsolatedDatabaseAsync(_testDbName);
    }

    // ========================================
    // AC8 — Authentication
    // ========================================

    [Fact]
    public async Task GET_NoSession_Returns401()
    {
        var docId = Guid.NewGuid();
        var response = await _client.GetAsync($"/api/v1/admin/kb/docs/{docId}/agents");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ========================================
    // AC5 — Zero consumers
    // ========================================

    [Fact]
    public async Task GET_NoConsumingAgents_Returns200WithEmptyArray()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateAdminSessionAsync(dbContext);

        var docId = Guid.NewGuid();
        var request = AuthRequest(HttpMethod.Get, $"/api/v1/admin/kb/docs/{docId}/agents", sessionToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Be("[]");
    }

    // ========================================
    // AC7 — Guid.Empty defensive
    // ========================================

    [Fact]
    public async Task GET_EmptyGuid_Returns200WithEmptyArray()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateAdminSessionAsync(dbContext);

        var request = AuthRequest(
            HttpMethod.Get,
            $"/api/v1/admin/kb/docs/{Guid.Empty}/agents",
            sessionToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("[]");
    }

    // ========================================
    // AC1 + AC2 — Includes consumers, excludes non-consumers
    // ========================================

    [Fact]
    public async Task GET_OneConsumingAgent_ReturnsThatAgentOnly()
    {
        // Arrange — Agent A consumes docId; Agent B does not.
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateAdminSessionAsync(dbContext);

        var docId = Guid.NewGuid();
        var otherDocId = Guid.NewGuid();

        var agentA = MakeAgent("Agent A");
        agentA.UpdateKbCardIds(new[] { docId });
        var agentB = MakeAgent("Agent B");
        agentB.UpdateKbCardIds(new[] { otherDocId });

        dbContext.Set<AgentDefinition>().AddRange(agentA, agentB);
        await dbContext.SaveChangesAsync();

        // Act
        var response = await _client.SendAsync(
            AuthRequest(HttpMethod.Get, $"/api/v1/admin/kb/docs/{docId}/agents", sessionToken));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await ReadArray(response);
        items.Should().HaveCount(1);
        items[0].GetProperty("name").GetString().Should().Be("Agent A");
    }

    // ========================================
    // AC3 — Soft-deleted excluded
    // ========================================

    [Fact]
    public async Task GET_SoftDeletedAgent_IsExcluded()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateAdminSessionAsync(dbContext);

        var docId = Guid.NewGuid();
        var agent = MakeAgent("Soft Deleted");
        agent.UpdateKbCardIds(new[] { docId });
        agent.SoftDelete();

        dbContext.Set<AgentDefinition>().Add(agent);
        await dbContext.SaveChangesAsync();

        var response = await _client.SendAsync(
            AuthRequest(HttpMethod.Get, $"/api/v1/admin/kb/docs/{docId}/agents", sessionToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadArray(response)).Should().BeEmpty();
    }

    // ========================================
    // AC4 — System agent included with flag
    // ========================================

    [Fact]
    public async Task GET_SystemAgent_IsIncludedWithIsSystemDefinedTrue()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateAdminSessionAsync(dbContext);

        var docId = Guid.NewGuid();
        var sysAgent = AgentDefinition.CreateSystem(
            name: "Arbitro",
            description: "system",
            type: AgentType.Custom("HybridSearch", "Hybrid search"),
            config: AgentDefinitionConfig.Create("gpt-4o-mini", 1024, 0.5f),
            typologySlug: "arbitro");
        sysAgent.UpdateKbCardIds(new[] { docId });

        dbContext.Set<AgentDefinition>().Add(sysAgent);
        await dbContext.SaveChangesAsync();

        var response = await _client.SendAsync(
            AuthRequest(HttpMethod.Get, $"/api/v1/admin/kb/docs/{docId}/agents", sessionToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await ReadArray(response);
        items.Should().HaveCount(1);
        items[0].GetProperty("isSystemDefined").GetBoolean().Should().BeTrue();
        items[0].GetProperty("typologySlug").GetString().Should().Be("arbitro");
    }

    // ========================================
    // AC6 — GameName null when game is missing
    // ========================================

    [Fact]
    public async Task GET_AgentWithMissingGame_HasGameNameNull()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateAdminSessionAsync(dbContext);

        var docId = Guid.NewGuid();
        var phantomGameId = Guid.NewGuid(); // not in shared_games table

        var agent = MakeAgent("Beta");
        agent.SetGameId(phantomGameId);
        agent.UpdateKbCardIds(new[] { docId });

        dbContext.Set<AgentDefinition>().Add(agent);
        await dbContext.SaveChangesAsync();

        var response = await _client.SendAsync(
            AuthRequest(HttpMethod.Get, $"/api/v1/admin/kb/docs/{docId}/agents", sessionToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await ReadArray(response);
        items.Should().HaveCount(1);
        items[0].GetProperty("gameId").GetGuid().Should().Be(phantomGameId);
        items[0].TryGetProperty("gameName", out var gameNameProp).Should().BeTrue();
        (gameNameProp.ValueKind == JsonValueKind.Null).Should().BeTrue();
    }

    // ========================================
    // Helpers
    // ========================================

    private static AgentDefinition MakeAgent(string name) =>
        AgentDefinition.Create(
            name: name,
            description: "test",
            type: AgentType.Custom("HybridSearch", "Hybrid search"),
            config: AgentDefinitionConfig.Create("gpt-4o-mini", 1024, 0.5f));

    private static HttpRequestMessage AuthRequest(HttpMethod method, string uri, string sessionToken)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Add("Cookie", $"{TestSessionHelper.SessionCookieName}={sessionToken}");
        return request;
    }

    private static async Task<List<JsonElement>> ReadArray(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        return json.RootElement.EnumerateArray().Select(e => e.Clone()).ToList();
    }
}
