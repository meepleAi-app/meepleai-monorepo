using System.Net;
using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.Infrastructure;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.KnowledgeBase;

/// <summary>
/// Fixture della classe: host e database costruiti una volta sola (vedi <see cref="IntegrationHostFixture"/>).
/// <para>
/// Condividere il database è sicuro qui: l'unico test semina un agente che nessun altro conosce e legge solo quello.
/// </para>
/// </summary>
public sealed class AgentCatalogStatsHostFixture(SharedTestcontainersFixture shared)
    : IntegrationHostFixture(shared, "agent_catalog_stats");

/// <summary>
/// #4635: <c>RagExecutionRepository.GetStatsByAgentAsync</c> non era traducibile da EF e lanciava sempre, e il suo solo
/// chiamante (<c>GetAgentCatalogStatsQueryHandler</c>) non ha un <c>try</c>/<c>catch</c>: il catalogo degli agenti
/// dell'amministrazione non poteva mostrare le statistiche d'uso. Nessun test attraversava la rotta fino al
/// database, perché quelli del gestore usano un repository finto che non traduce niente.
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4635")]
public sealed class AgentCatalogStatsRouteIntegrationTests : IClassFixture<AgentCatalogStatsHostFixture>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public AgentCatalogStatsRouteIntegrationTests(AgentCatalogStatsHostFixture host)
    {
        _factory = host.Factory;
        _client = host.Client;
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    /// L'admin legge le statistiche: 200, e il suo agente ha le esecuzioni seminate, la fiducia media dei soli valori
    /// presenti e le altre colonne coerenti con le righe.
    /// </summary>
    [Fact]
    public async Task AnAdmin_ReadsTheCatalogStats_OfTheSeededExecutions()
    {
        var agent = Guid.NewGuid();
        await SeedAsync(agent, confidence: 0.2, tokens: 100, latencyMs: 1000, status: "Success");
        await SeedAsync(agent, confidence: 0.6, tokens: 300, latencyMs: 3000, status: "Error");
        await SeedAsync(agent, confidence: null, tokens: 200, latencyMs: 2000, status: "Success");

        var (_, token) = await CreateAdminSessionAsync();
        using var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get, "/api/v1/admin/agent-definitions/catalog-stats?range=7d", token);
        using var response = await _client.SendAsync(request, Ct);
        var body = await response.Content.ReadAsStringAsync(Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        using var json = JsonDocument.Parse(body);
        var ours = json.RootElement.GetProperty("agents").EnumerateArray()
            .Single(a => a.GetProperty("agentDefinitionId").GetGuid() == agent);
        ours.GetProperty("executionCount").GetInt32().Should().Be(3);
        ours.GetProperty("totalTokens").GetInt32().Should().Be(600);
        ours.GetProperty("avgLatencyMs").GetDouble().Should().BeApproximately(2000, 0.1);
        ours.GetProperty("successRate").GetDouble().Should().BeApproximately(0.6667, 0.0001);
        ours.GetProperty("avgConfidence").GetDouble().Should().BeApproximately(0.4, 0.0001,
            "(0.2 + 0.6) / 2: the execution with no confidence is not counted as a zero");
        json.RootElement.GetProperty("global").GetProperty("totalExecutions").GetInt32().Should().BeGreaterThanOrEqualTo(3);
    }

    private async Task SeedAsync(Guid agentDefinitionId, double? confidence, int tokens, int latencyMs, string status)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        db.Set<RagExecution>().Add(RagExecution.Create(
            query: "How many cards does each player draw at setup?",
            agentDefinitionId: agentDefinitionId,
            agentName: "Rules Agent",
            strategy: "SingleModel",
            model: "test-model",
            provider: "test-provider",
            gameId: null,
            isPlayground: true,
            totalLatencyMs: latencyMs,
            promptTokens: tokens / 2,
            completionTokens: tokens - tokens / 2,
            totalTokens: tokens,
            totalCost: 0.01m,
            confidence: confidence,
            cacheHit: false,
            status: status,
            errorMessage: null,
            executionTrace: null));
        await db.SaveChangesAsync(Ct);
    }

    private async Task<(Guid UserId, string Token)> CreateAdminSessionAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return await TestSessionHelper.CreateAdminSessionAsync(db, null, Ct);
    }
}
