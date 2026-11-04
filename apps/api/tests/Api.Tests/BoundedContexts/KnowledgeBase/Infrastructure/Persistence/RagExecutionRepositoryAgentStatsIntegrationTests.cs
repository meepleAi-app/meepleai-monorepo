using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;

/// <summary>
/// #4635: <c>GetStatsByAgentAsync</c> averaged the confidence with
/// <c>g.Where(e => e.Confidence != null).Select(e => e.Confidence!.Value).DefaultIfEmpty(0).Average()</c> inside the
/// <c>GroupBy</c> projection, a shape EF Core cannot translate (the same one #4059 fixed in <c>GetStatsAsync</c>): it
/// threw an <see cref="ArgumentException"/> every time, on an empty table and on a full one. Its only caller feeds
/// <c>GET /api/v1/admin/agent-definitions/catalog-stats</c>.
/// </summary>
/// <remarks>
/// On Postgres, because the failure is in the translation of the query: the handler tests use a fake repository that
/// translates nothing, which is why nobody saw it. The dates are relative to now, never fixed, because a fixed date ages.
/// </remarks>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Dependency", "PostgreSQL")]
[Trait("Issue", "4635")]
public sealed class RagExecutionRepositoryAgentStatsIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private ServiceProvider? _serviceProvider;
    private MeepleAiDbContext? _dbContext;
    private IRagExecutionRepository? _repository;

    public RagExecutionRepositoryAgentStatsIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateTime From => DateTime.UtcNow.AddDays(-30);

    private static DateTime To => DateTime.UtcNow.AddMinutes(1);

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_rag_exec_agent_stats_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        _serviceProvider = IntegrationServiceCollectionBuilder.CreateBase(connectionString).BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();
        await _dbContext.Database.MigrateAsync(Ct);

        _repository = new RagExecutionRepository(
            _dbContext, _serviceProvider.GetRequiredService<IDomainEventCollector>());
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext is not null)
            await _dbContext.DisposeAsync();

        if (_serviceProvider is not null)
            await _serviceProvider.DisposeAsync();

        if (!string.IsNullOrEmpty(_databaseName))
        {
            try
            {
                await _fixture.DropIsolatedDatabaseAsync(_databaseName);
            }
            catch
            {
                // Best-effort cleanup: isolation is already guaranteed by the dedicated database.
            }
        }
    }

    /// <summary>The empty table: the translation failure did not depend on there being rows.</summary>
    [Fact]
    public async Task WithAnEmptyTable_TheListIsEmpty()
    {
        var stats = await _repository!.GetStatsByAgentAsync(From, To, agentDefinitionId: null, Ct);

        stats.Should().BeEmpty();
    }

    /// <summary>
    /// The semantics the old shape promised and the engine now keeps: the average is over the values that are there,
    /// a run with no confidence is not a zero.
    /// </summary>
    [Fact]
    public async Task TheConfidence_IsTheAverageOfThePresentValuesOnly()
    {
        var agent = Guid.NewGuid();
        await SeedAsync(agent, confidence: 0.2);
        await SeedAsync(agent, confidence: 0.6);
        await SeedAsync(agent, confidence: null);

        var stats = await _repository!.GetStatsByAgentAsync(From, To, agentDefinitionId: null, Ct);

        stats.Should().ContainSingle().Which.AvgConfidence.Should().BeApproximately(0.4, 1e-9,
            "(0.2 + 0.6) / 2: the run with no confidence is not counted as a zero, which would give 0.2667");
    }

    /// <summary>An agent whose runs carry no confidence at all reads zero, as before.</summary>
    [Fact]
    public async Task WhenEveryConfidenceIsNull_TheAverageIsZero()
    {
        var agent = Guid.NewGuid();
        await SeedAsync(agent, confidence: null);
        await SeedAsync(agent, confidence: null);

        var stats = await _repository!.GetStatsByAgentAsync(From, To, agentDefinitionId: null, Ct);

        stats.Should().ContainSingle().Which.AvgConfidence.Should().Be(0);
    }

    /// <summary>
    /// The other columns keep their meaning once the projection is translatable: they are computed per agent, not over
    /// the whole table.
    /// </summary>
    [Fact]
    public async Task TheOtherColumns_AreComputedPerAgent()
    {
        var busy = Guid.NewGuid();
        var quiet = Guid.NewGuid();
        var newest = DateTime.UtcNow.AddHours(-1);
        await SeedAsync(busy, tokens: 100, cost: 0.10m, latencyMs: 1000, status: "Success", createdAt: DateTime.UtcNow.AddDays(-3));
        await SeedAsync(busy, tokens: 300, cost: 0.30m, latencyMs: 3000, status: "Error", createdAt: DateTime.UtcNow.AddDays(-2));
        await SeedAsync(busy, tokens: 200, cost: 0.20m, latencyMs: 2000, status: "Success", createdAt: newest);
        await SeedAsync(quiet, tokens: 50, cost: 0.05m, latencyMs: 500, status: "Success", createdAt: DateTime.UtcNow.AddDays(-5));

        var stats = await _repository!.GetStatsByAgentAsync(From, To, agentDefinitionId: null, Ct);

        stats.Select(s => s.AgentDefinitionId).Should().Equal([busy, quiet], "ordered by execution count, descending");
        var first = stats[0];
        first.ExecutionCount.Should().Be(3);
        first.TotalTokens.Should().Be(600);
        first.AvgTokens.Should().BeApproximately(200, 1e-9);
        first.TotalCost.Should().Be(0.60m);
        first.SuccessRate.Should().BeApproximately(2.0 / 3.0, 1e-9);
        first.AvgLatencyMs.Should().BeApproximately(2000, 1e-9);
        first.LastExecutedAt.Should().BeCloseTo(newest, TimeSpan.FromMilliseconds(5));
        first.AgentName.Should().Be("Rules Agent");
        first.Model.Should().Be("test-model");
        first.Provider.Should().Be("test-provider");

        var second = stats[1];
        second.ExecutionCount.Should().Be(1);
        second.TotalTokens.Should().Be(50);
    }

    /// <summary>The window and the agent filter still apply, and a run with no agent never counts.</summary>
    [Fact]
    public async Task TheWindowAndTheAgentFilter_StillApply_AndARunWithNoAgentNeverCounts()
    {
        var wanted = Guid.NewGuid();
        var other = Guid.NewGuid();
        await SeedAsync(wanted, createdAt: DateTime.UtcNow.AddDays(-1));
        await SeedAsync(wanted, createdAt: DateTime.UtcNow.AddDays(-90));
        await SeedAsync(other, createdAt: DateTime.UtcNow.AddDays(-1));
        await SeedAsync(agentDefinitionId: null, createdAt: DateTime.UtcNow.AddDays(-1));

        var all = await _repository!.GetStatsByAgentAsync(From, To, agentDefinitionId: null, Ct);
        var onlyWanted = await _repository.GetStatsByAgentAsync(From, To, wanted, Ct);

        all.Select(s => s.AgentDefinitionId).Should().BeEquivalentTo([wanted, other],
            "the run with no agent is out, and so is the run of ninety days ago");
        onlyWanted.Should().ContainSingle().Which.ExecutionCount.Should().Be(1);
    }

    // ─── Support ────────────────────────────────────────────────

    private async Task SeedAsync(
        Guid? agentDefinitionId,
        double? confidence = 0.5,
        int tokens = 512,
        decimal cost = 0.0123m,
        int latencyMs = 1200,
        string status = "Success",
        DateTime? createdAt = null)
    {
        var execution = RagExecution.Create(
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
            totalCost: cost,
            confidence: confidence,
            cacheHit: false,
            status: status,
            errorMessage: null,
            executionTrace: null);
        _dbContext!.Set<RagExecution>().Add(execution);
        await _dbContext.SaveChangesAsync(Ct);

        if (createdAt is { } at)
        {
            // Create stamps "now"; the instant is set afterwards, the way the rows really age.
            await _dbContext.Set<RagExecution>()
                .Where(e => e.Id == execution.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.CreatedAt, at), Ct);
        }
    }
}
