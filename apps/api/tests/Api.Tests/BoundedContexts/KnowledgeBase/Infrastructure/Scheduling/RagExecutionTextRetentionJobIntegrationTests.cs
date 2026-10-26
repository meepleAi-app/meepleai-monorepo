using Api.BoundedContexts.KnowledgeBase.Application.Configuration;
using Api.BoundedContexts.KnowledgeBase.Application.Queries.RagExecution;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Scheduling;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Quartz;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Infrastructure.Scheduling;

/// <summary>
/// #4634: the playground saves, for every run, the question and a trace with previews of the retrieved passages in
/// <c>knowledge_base.rag_executions</c>, with no expiry. They are content derived from the indexed PDFs, and the admin
/// pages read them. Decision (2026-10-26, "same remedy" as #4311): an expiry for everyone that blanks the texts and
/// keeps the metrics.
/// </summary>
/// <remarks>
/// On Postgres. Ages are relative to now (120 days ago against one day ago), never a fixed date, because a fixed date
/// ages. The job is built by hand: the integration hosts remove the scheduled services.
/// </remarks>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Dependency", "PostgreSQL")]
[Trait("Issue", "4634")]
public sealed class RagExecutionTextRetentionJobIntegrationTests : IAsyncLifetime
{
    private const string Question = "How many cards does each player draw at setup?";
    private const string Trace = """[{"name":"RAG Search","type":"search","items":[{"label":"Page 3, chunk 2","score":0.91,"preview":"Each player draws five cards at setup..."}]}]""";

    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private ServiceProvider? _serviceProvider;
    private MeepleAiDbContext? _dbContext;
    private IRagExecutionRepository? _repository;

    public RagExecutionTextRetentionJobIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_rag_exec_retention_{Guid.NewGuid():N}";
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

    /// <summary>
    /// The case of the issue: the old row loses the question and the trace and keeps everything else, the metrics
    /// included.
    /// </summary>
    [Fact]
    public async Task ARowOlderThanTheRetention_LosesItsQueryAndTrace_AndKeepsItsMetrics()
    {
        var agent = Guid.NewGuid();
        var game = Guid.NewGuid();
        var old = await SeedAsync(ageDays: 120, agentDefinitionId: agent, gameId: game);

        await RunJobAsync();

        var row = await ReadAsync(old);
        row.Query.Should().BeEmpty("the column is NOT NULL, and an empty question is how an expired row reads");
        row.ExecutionTrace.Should().BeNull("the trace carries previews of the retrieved passages");
        // Identity and metrics stay: the dashboard and the per-agent statistics work on these.
        row.AgentDefinitionId.Should().Be(agent);
        row.AgentName.Should().Be("Rules Agent");
        row.Strategy.Should().Be("HybridRag");
        row.Model.Should().Be("test-model");
        row.Provider.Should().Be("test-provider");
        row.GameId.Should().Be(game);
        row.IsPlayground.Should().BeTrue();
        row.TotalLatencyMs.Should().Be(1200);
        row.PromptTokens.Should().Be(400);
        row.CompletionTokens.Should().Be(112);
        row.TotalTokens.Should().Be(512);
        row.TotalCost.Should().Be(0.0123m);
        row.Confidence.Should().Be(0.42);
        row.CacheHit.Should().BeTrue();
        row.CragVerdict.Should().Be("Correct");
        row.Status.Should().Be("Success");
        row.CreatedAt.Should().BeCloseTo(DateTime.UtcNow.AddDays(-120), TimeSpan.FromMinutes(5));
    }

    /// <summary>"An expiry for everyone": a row that did not come from the playground expires too.</summary>
    [Fact]
    public async Task ARowThatDidNotComeFromThePlayground_ExpiresToo()
    {
        var old = await SeedAsync(ageDays: 120, isPlayground: false);

        await RunJobAsync();

        var row = await ReadAsync(old);
        row.Query.Should().BeEmpty();
        row.ExecutionTrace.Should().BeNull();
    }

    /// <summary>The threshold is ninety days: a row of eighty-nine keeps its texts, one of ninety-one does not.</summary>
    [Fact]
    public async Task ARowWithinTheRetention_IsLeftAlone()
    {
        var recent = await SeedAsync(ageDays: 1);
        var justInside = await SeedAsync(ageDays: 89);
        var justOutside = await SeedAsync(ageDays: 91);

        await RunJobAsync();

        (await ReadAsync(recent)).Query.Should().Be(Question);
        (await ReadAsync(justInside)).Query.Should().Be(Question);
        // jsonb hands the JSON back normalized (spaces after the colons), so the trace is compared by content.
        (await ReadAsync(justInside)).ExecutionTrace.Should().NotBeNull().And.Contain("RAG Search");
        (await ReadAsync(justOutside)).Query.Should().BeEmpty();
    }

    /// <summary>
    /// The point of "keep the metrics": what the RAG dashboard computes (the totals and the per-strategy metrics) is
    /// the same before and after the texts go.
    /// </summary>
    [Fact]
    public async Task TheStatistics_AreTheSame_BeforeAndAfterTheTextsAreBlanked()
    {
        var agent = Guid.NewGuid();
        await SeedAsync(ageDays: 120, agentDefinitionId: agent, strategy: "HybridRag", latencyMs: 800, tokens: 300);
        await SeedAsync(ageDays: 100, agentDefinitionId: agent, strategy: "VectorOnly", latencyMs: 1500, tokens: 700);
        await SeedAsync(ageDays: 2, agentDefinitionId: agent, strategy: "HybridRag", latencyMs: 400, tokens: 200);

        var from = DateTime.UtcNow.AddDays(-400);
        var to = DateTime.UtcNow.AddDays(1);
        var statsBefore = await _repository!.GetStatsAsync(from, to, Ct);
        var byStrategyBefore = await _repository.GetAggregatedMetricsAsync(null, null, null, Ct);

        var blanked = await _repository.BlankTextsOlderThanAsync(DateTime.UtcNow.AddDays(-90), 5000, Ct);

        blanked.Should().Be(2, "the test is empty if nothing was blanked");
        (await _repository.GetStatsAsync(from, to, Ct)).Should().BeEquivalentTo(statsBefore);
        (await _repository.GetAggregatedMetricsAsync(null, null, null, Ct)).Should().BeEquivalentTo(byStrategyBefore);
    }

    /// <summary>
    /// Repeatable and without useless writes: the second time there is nothing left to blank, and an old row that
    /// has no text left is not counted. A row with only one of the two texts left still counts.
    /// </summary>
    [Fact]
    public async Task ThePurge_IsRepeatable_AndCountsOnlyWhatItBlanked()
    {
        var withBoth = await SeedAsync(ageDays: 120);
        var alreadyBlank = await SeedAsync(ageDays: 150, alreadyBlank: Blank.Both);
        var traceLeft = await SeedAsync(ageDays: 130, alreadyBlank: Blank.Query);
        var queryLeft = await SeedAsync(ageDays: 140, alreadyBlank: Blank.Trace);

        var first = await _repository!.BlankTextsOlderThanAsync(DateTime.UtcNow.AddDays(-90), 5000, Ct);
        var second = await _repository.BlankTextsOlderThanAsync(DateTime.UtcNow.AddDays(-90), 5000, Ct);

        first.Should().Be(3, "the row with no text left is not counted, the two with one text left are");
        second.Should().Be(0);
        foreach (var id in new[] { withBoth, alreadyBlank, traceLeft, queryLeft })
        {
            var row = await ReadAsync(id);
            row.Query.Should().BeEmpty();
            row.ExecutionTrace.Should().BeNull();
        }
    }

    /// <summary>A batch smaller than the rows to blank leaves nobody behind.</summary>
    [Fact]
    public async Task EveryOldRow_IsBlanked_WhateverTheBatchSize()
    {
        var ids = new List<Guid>();
        for (var i = 0; i < 5; i++)
        {
            ids.Add(await SeedAsync(ageDays: 100 + i));
        }

        var blanked = await _repository!.BlankTextsOlderThanAsync(DateTime.UtcNow.AddDays(-90), 2, Ct);

        blanked.Should().Be(5);
        foreach (var id in ids)
        {
            var row = await ReadAsync(id);
            row.Query.Should().BeEmpty();
            row.ExecutionTrace.Should().BeNull();
        }
    }

    /// <summary>
    /// A shutdown cancels the token. The sweep stops at the next batch instead of finishing the whole backlog, and
    /// what it did not reach is still there for the next round.
    /// </summary>
    [Fact]
    public async Task ACancelledToken_StopsTheSweep_BeforeBlankingAnything()
    {
        var old = await SeedAsync(ageDays: 120);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        var act = () => _repository!.BlankTextsOlderThanAsync(DateTime.UtcNow.AddDays(-90), 5000, cancelled.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        (await ReadAsync(old)).Query.Should().Be(Question);
    }

    /// <summary>
    /// A batch size that is not positive is a caller bug: it would read no rows and report a clean sweep, so it is
    /// rejected.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ABatchSizeThatIsNotPositive_IsRejected(int batchSize)
    {
        var old = await SeedAsync(ageDays: 120);

        var act = () => _repository!.BlankTextsOlderThanAsync(DateTime.UtcNow.AddDays(-90), batchSize, Ct);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
        (await ReadAsync(old)).Query.Should().Be(Question);
    }

    /// <summary>
    /// A retention of zero days (or less) is not "blank everything now": it is a wrong setting, and the reaction is to
    /// touch nothing.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task ARetentionThatIsNotPositive_TouchesNothing(int days)
    {
        var old = await SeedAsync(ageDays: 400);

        await RunJobAsync(retentionDays: days);

        (await ReadAsync(old)).Query.Should().Be(Question);
    }

    /// <summary>
    /// The readers of the table hold on a blanked row: the detail comes back with an empty question and no trace, the
    /// list shows the row with an empty question.
    /// </summary>
    [Fact]
    public async Task TheAdminReaders_HoldOnABlankedRow()
    {
        var old = await SeedAsync(ageDays: 120);
        await RunJobAsync();

        var detail = await new GetRagExecutionByIdQueryHandler(_repository!).Handle(new GetRagExecutionByIdQuery(old), Ct);
        var list = await new GetRagExecutionsQueryHandler(_repository!).Handle(new GetRagExecutionsQuery(), Ct);

        detail.Should().NotBeNull();
        detail!.Query.Should().BeEmpty();
        detail.ExecutionTrace.Should().BeNull();
        detail.AgentName.Should().Be("Rules Agent");
        detail.TotalTokens.Should().Be(512);
        list.TotalCount.Should().Be(1);
        list.Items.Should().ContainSingle().Which.Query.Should().BeEmpty();
    }

    // ─── Support ────────────────────────────────────────────────

    /// <summary>Which texts a seeded row has already lost.</summary>
    private enum Blank
    {
        None,
        Query,
        Trace,
        Both,
    }

    private async Task RunJobAsync(int? retentionDays = null)
    {
        var options = new RagExecutionTextRetentionOptions();
        if (retentionDays is not null)
        {
            options.RetentionDays = retentionDays.Value;
        }

        var context = new Mock<IJobExecutionContext>();
        context.Setup(c => c.CancellationToken).Returns(Ct);
        context.Setup(c => c.FireTimeUtc).Returns(DateTimeOffset.UtcNow);

        var job = new RagExecutionTextRetentionJob(
            _repository!, Options.Create(options), NullLogger<RagExecutionTextRetentionJob>.Instance);

        await job.Execute(context.Object);
    }

    /// <summary>
    /// <paramref name="alreadyBlank"/> builds the rows a previous round (or a half-finished one) leaves behind:
    /// <see cref="Blank.Query"/> blanks the question and keeps the trace.
    /// </summary>
    private async Task<Guid> SeedAsync(
        int ageDays,
        bool isPlayground = true,
        Guid? agentDefinitionId = null,
        Guid? gameId = null,
        string strategy = "HybridRag",
        int latencyMs = 1200,
        int tokens = 512,
        Blank alreadyBlank = Blank.None)
    {
        var execution = RagExecution.Create(
            query: Question,
            agentDefinitionId: agentDefinitionId,
            agentName: "Rules Agent",
            strategy: strategy,
            model: "test-model",
            provider: "test-provider",
            gameId: gameId,
            isPlayground: isPlayground,
            totalLatencyMs: latencyMs,
            promptTokens: 400,
            completionTokens: 112,
            totalTokens: tokens,
            totalCost: 0.0123m,
            confidence: 0.42,
            cacheHit: true,
            status: "Success",
            errorMessage: null,
            executionTrace: Trace,
            cragVerdict: "Correct");
        _dbContext!.Set<RagExecution>().Add(execution);
        await _dbContext.SaveChangesAsync(Ct);

        // Create stamps "now"; the age is set afterwards, the way the rows really age.
        var createdAt = DateTime.UtcNow.AddDays(-ageDays);
        await _dbContext.Set<RagExecution>()
            .Where(e => e.Id == execution.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.CreatedAt, createdAt), Ct);

        if (alreadyBlank is Blank.Both or Blank.Query)
        {
            await _dbContext.Set<RagExecution>()
                .Where(e => e.Id == execution.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.Query, string.Empty), Ct);
        }

        if (alreadyBlank is Blank.Both or Blank.Trace)
        {
            await _dbContext.Set<RagExecution>()
                .Where(e => e.Id == execution.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.ExecutionTrace, (string?)null), Ct);
        }

        return execution.Id;
    }

    private async Task<RagExecution> ReadAsync(Guid id)
    {
        using var scope = _serviceProvider!.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return await db.Set<RagExecution>().AsNoTracking().SingleAsync(e => e.Id == id, Ct);
    }
}
