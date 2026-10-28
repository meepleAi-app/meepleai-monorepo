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
/// Condividere il database è sicuro qui: i test seminano esecuzioni con una strategia che nessun altro usa e leggono
/// solo quella.
/// </para>
/// </summary>
public sealed class RagDashboardStrategyRoutesHostFixture(SharedTestcontainersFixture shared)
    : IntegrationHostFixture(shared, "rag_dashboard_strategies");

/// <summary>
/// Le rotte di metriche per strategia di <c>/api/v1/rag-dashboard</c> validavano <c>{strategyId}</c> contro l'elenco
/// delle strategie di retrieval di <c>/options</c> (<c>Hybrid</c>, <c>Semantic</c>, …), mentre il playground salva in
/// <c>RagExecution.Strategy</c> la strategia di generazione (<c>SingleModel</c>, <c>RetrievalOnly</c>,
/// <c>MultiModelConsensus</c>) e <c>/overview</c> restituisce gli id salvati: l'id che l'overview mostrava dava 404
/// sulla rotta di dettaglio, e gli id accettati non potevano avere dati.
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class RagDashboardStrategyRoutesIntegrationTests : IClassFixture<RagDashboardStrategyRoutesHostFixture>
{
    private const string Group = "/api/v1/rag-dashboard";

    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public RagDashboardStrategyRoutesIntegrationTests(RagDashboardStrategyRoutesHostFixture host)
    {
        _factory = host.Factory;
        _client = host.Client;
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    /// Il caso della issue: ogni id che l'overview restituisce per le esecuzioni salvate è accettato dalle due rotte di
    /// dettaglio, con i dati di quella strategia.
    /// </summary>
    [Fact]
    public async Task EveryStrategyIdTheOverviewReturns_IsAcceptedByTheDetailRoutes_WithItsData()
    {
        await SeedAsync("SingleModel", count: 3);
        await SeedAsync("RetrievalOnly", count: 1);
        var token = await CreateAdminTokenAsync();

        var overview = await GetJsonAsync($"{Group}/overview", token);
        var ids = overview.GetProperty("strategies").EnumerateArray()
            .Select(s => s.GetProperty("strategyId").GetString()!)
            .ToList();
        ids.Should().Contain(["SingleModel", "RetrievalOnly"], "the overview shows the strategies the runs were saved with");

        var expected = new Dictionary<string, int>(StringComparer.Ordinal) { ["SingleModel"] = 3, ["RetrievalOnly"] = 1 };
        foreach (var id in ids.Where(expected.ContainsKey))
        {
            var metrics = await GetJsonAsync($"{Group}/strategies/{id}/metrics", token);
            metrics.GetProperty("totalQueries").GetInt32().Should().Be(expected[id], $"metrics of {id}");

            var series = await GetJsonAsync($"{Group}/strategies/{id}/time-series?granularity=day", token);
            series.GetProperty("queryCountTrend").EnumerateArray()
                .Sum(p => p.GetProperty("value").GetDouble()).Should().Be(expected[id], $"time series of {id}");
        }
    }

    /// <summary>
    /// Le strategie di retrieval di <c>/options</c> restano accettate (rispondono con zeri: nessuna esecuzione le ha
    /// salvate), e un id che non è di nessuna delle due famiglie resta 404.
    /// </summary>
    [Fact]
    public async Task ARetrievalStrategyOfTheOptions_StillAnswersWithZeros_AndAnUnknownIdIsNotFound()
    {
        var token = await CreateAdminTokenAsync();

        var hybrid = await GetJsonAsync($"{Group}/strategies/Hybrid/metrics", token);
        hybrid.GetProperty("totalQueries").GetInt32().Should().Be(0);

        var (status, _) = await SendAsync($"{Group}/strategies/NoSuchStrategy/metrics", token);
        status.Should().Be(HttpStatusCode.NotFound);
        var (seriesStatus, _) = await SendAsync($"{Group}/strategies/NoSuchStrategy/time-series", token);
        seriesStatus.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// La validazione ignorava le maiuscole ma il filtro sulle esecuzioni no: <c>singlemodel</c> passava e non
    /// trovava mai niente. Ora si risolve nell'id canonico, e la risposta porta quello.
    /// </summary>
    [Fact]
    public async Task TheStrategyId_IsMatchedIgnoringCase_AndAnsweredInItsCanonicalForm()
    {
        await SeedAsync("MultiModelConsensus", count: 2);
        var token = await CreateAdminTokenAsync();

        var metrics = await GetJsonAsync($"{Group}/strategies/multimodelconsensus/metrics", token);

        metrics.GetProperty("strategyId").GetString().Should().Be("MultiModelConsensus");
        metrics.GetProperty("totalQueries").GetInt32().Should().Be(2);
    }

    // ─── Support ────────────────────────────────────────────────

    private async Task SeedAsync(string strategy, int count)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        for (var i = 0; i < count; i++)
        {
            db.Set<RagExecution>().Add(RagExecution.Create(
                query: "How many cards does each player draw at setup?",
                agentDefinitionId: null,
                agentName: "Rules Agent",
                strategy: strategy,
                model: "test-model",
                provider: "test-provider",
                gameId: null,
                isPlayground: true,
                totalLatencyMs: 1200,
                promptTokens: 400,
                completionTokens: 112,
                totalTokens: 512,
                totalCost: 0.0123m,
                confidence: 0.42,
                cacheHit: false,
                status: "Success",
                errorMessage: null,
                executionTrace: null));
        }

        await db.SaveChangesAsync(Ct);
    }

    private async Task<string> CreateAdminTokenAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, token) = await TestSessionHelper.CreateAdminSessionAsync(db, null, Ct);
        return token;
    }

    private async Task<(HttpStatusCode Status, string Body)> SendAsync(string path, string token)
    {
        using var request = TestSessionHelper.CreateAuthenticatedRequest(HttpMethod.Get, path, token);
        using var response = await _client.SendAsync(request, Ct);
        return (response.StatusCode, await response.Content.ReadAsStringAsync(Ct));
    }

    private async Task<JsonElement> GetJsonAsync(string path, string token)
    {
        var (status, body) = await SendAsync(path, token);
        status.Should().Be(HttpStatusCode.OK, $"GET {path}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }
}
