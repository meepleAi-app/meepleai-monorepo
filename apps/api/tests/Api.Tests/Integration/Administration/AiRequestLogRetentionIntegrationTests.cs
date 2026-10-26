using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Administration.Infrastructure.BackgroundJobs;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.Integration.Administration;

/// <summary>
/// #4311 (assorbita da #4308): <c>ai_request_logs</c> conserva, per ogni richiesta all'AI, la domanda
/// (<c>Query</c>), fino a 1024 caratteri della risposta (<c>ResponseSnippet</c>) e il testo dei passaggi recuperati
/// (<c>ChunksJson</c>), senza nessuna scadenza. Sono contenuto derivato dai PDF, anche da quelli privati, leggibile
/// dalle pagine di amministrazione. Decisione (2026-10-25): una scadenza per tutti che svuota i testi e tiene le
/// metriche.
/// </summary>
/// <remarks>
/// Su Postgres. Le età sono relative a ora (centoventi giorni fa contro un giorno fa) e non a una data fissa, perché
/// una data fissa invecchia. Il servizio è costruito a mano: gli host di integrazione tolgono i servizi periodici.
/// </remarks>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Administration")]
[Trait("Issue", "4311")]
public sealed class AiRequestLogRetentionIntegrationTests : IAsyncLifetime
{
    private const string Chunks = """[{"id":"c1","score":0.91,"text":"Each player draws five cards.","page":3,"chunkIndex":2,"pdfName":"regolamento.pdf","used":true}]""";
    private const string Breakdown = """{"retrievalMs":120,"rerankMs":45,"llmMs":900,"postMs":10}""";

    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private ServiceProvider? _serviceProvider;
    private MeepleAiDbContext? _dbContext;

    public AiRequestLogRetentionIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_ai_log_retention_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        _serviceProvider = IntegrationServiceCollectionBuilder.CreateBase(connectionString).BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();
        await _dbContext.Database.MigrateAsync(Ct);
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
                // Best-effort cleanup — l'isolamento è già garantito dal database dedicato.
            }
        }
    }

    /// <summary>
    /// Il caso dell'issue: la riga vecchia perde i tre testi e tiene tutto il resto, le metriche comprese. Il
    /// <c>BreakdownJson</c> sono solo millisecondi: resta.
    /// </summary>
    [Fact]
    public async Task ARowOlderThanTheRetention_LosesItsTexts_AndKeepsItsMetrics()
    {
        var old = await SeedAsync(ageDays: 120);

        var blanked = await RunAsync();

        var row = await ReadAsync(old);
        row.Query.Should().BeNull();
        row.ResponseSnippet.Should().BeNull();
        row.ChunksJson.Should().BeNull("the retrieved passages are the largest copy of the PDF text in this table");
        // Le metriche e il contesto restano: le statistiche degli amministratori lavorano su queste.
        row.BreakdownJson.Should().NotBeNull();
        row.Endpoint.Should().Be("qa");
        row.Status.Should().Be("Success");
        row.LatencyMs.Should().Be(1200);
        row.TokenCount.Should().Be(512);
        row.PromptTokens.Should().Be(400);
        row.CompletionTokens.Should().Be(112);
        row.Confidence.Should().Be(0.42);
        row.OverallConfidence.Should().Be(0.37);
        row.IsLowQuality.Should().BeTrue();
        row.GameId.Should().NotBeNull();
        row.Model.Should().Be("test-model");
        row.CreatedAt.Should().BeCloseTo(DateTime.UtcNow.AddDays(-120), TimeSpan.FromMinutes(5));
        blanked.Should().Be(1);
    }

    /// <summary>La soglia è di novanta giorni: una riga di ottantanove li tiene, una di novantuno no.</summary>
    [Fact]
    public async Task ARowWithinTheRetention_IsLeftAlone()
    {
        var recent = await SeedAsync(ageDays: 1);
        var justInside = await SeedAsync(ageDays: 89);
        var justOutside = await SeedAsync(ageDays: 91);

        await RunAsync();

        (await ReadAsync(recent)).Query.Should().NotBeNull();
        (await ReadAsync(justInside)).Query.Should().NotBeNull();
        (await ReadAsync(justInside)).ChunksJson.Should().NotBeNull();
        (await ReadAsync(justOutside)).Query.Should().BeNull();
    }

    /// <summary>
    /// Ripetibile e senza scritture inutili: la seconda volta non c'è più niente da svuotare, e una riga vecchia
    /// che i testi non li ha mai avuti non si conta.
    /// </summary>
    [Fact]
    public async Task ThePurge_IsRepeatable_AndCountsOnlyWhatItBlanked()
    {
        await SeedAsync(ageDays: 120);
        await SeedAsync(ageDays: 150, withTexts: false);

        var first = await RunAsync();
        var second = await RunAsync();

        first.Should().Be(1, "only one of the two old rows still had texts");
        second.Should().Be(0);
    }

    /// <summary>Un lotto più piccolo delle righe da svuotare non lascia indietro nessuno.</summary>
    [Fact]
    public async Task EveryOldRow_IsBlanked_WhateverTheBatchSize()
    {
        var ids = new List<Guid>();
        for (var i = 0; i < 5; i++)
        {
            ids.Add(await SeedAsync(ageDays: 100 + i));
        }

        var blanked = await RunAsync(batchSize: 2);

        blanked.Should().Be(5);
        foreach (var id in ids)
        {
            (await ReadAsync(id)).Query.Should().BeNull();
        }
    }

    /// <summary>
    /// Una scadenza di zero giorni (o negativa) non è «cancella tutto subito»: è un'impostazione sbagliata, e la
    /// reazione sicura è non toccare niente.
    /// </summary>
    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    public async Task ARetentionThatIsNotPositive_TouchesNothing(string days)
    {
        var old = await SeedAsync(ageDays: 400);

        var blanked = await RunAsync(retentionDays: days);

        blanked.Should().Be(0);
        (await ReadAsync(old)).Query.Should().NotBeNull();
    }

    /// <summary>
    /// I lettori della tabella reggono una riga svuotata: il dettaglio di una richiesta torna con i passaggi vuoti, e
    /// l'elenco delle risposte di bassa qualità mostra la riga con la domanda vuota.
    /// </summary>
    [Fact]
    public async Task TheAdminReaders_HoldOnABlankedRow()
    {
        var old = await SeedAsync(ageDays: 120);
        await RunAsync();

        var drill = await new GetAiQueryDrillQueryHandler(
                _dbContext!, NullLogger<GetAiQueryDrillQueryHandler>.Instance)
            .Handle(new GetAiQueryDrillQuery(old), Ct);
        var lowQuality = await new GetLowQualityResponsesQueryHandler(
                _dbContext!, NullLogger<GetLowQualityResponsesQueryHandler>.Instance)
            .Handle(new GetLowQualityResponsesQuery(), Ct);

        drill.Should().NotBeNull();
        drill!.Chunks.Should().BeEmpty();
        drill.Breakdown.Should().NotBeNull("the latency breakdown is metrics, and it stays");
        drill.Request.Query.Should().BeNull();
        lowQuality.TotalCount.Should().Be(1);
        lowQuality.Responses.Should().ContainSingle().Which.Query.Should().BeEmpty();
    }

    // ─── Supporto ────────────────────────────────────────────────

    private async Task<int> RunAsync(string? retentionDays = null, int batchSize = AiRequestLogRetentionService.DefaultBatchSize)
    {
        var settings = new Dictionary<string, string?>();
        if (retentionDays is not null)
        {
            settings[AiRequestLogRetentionService.RetentionDaysKey] = retentionDays;
        }

        var service = new AiRequestLogRetentionService(
            _serviceProvider!.GetRequiredService<IServiceScopeFactory>(),
            new ConfigurationBuilder().AddInMemoryCollection(settings).Build(),
            NullLogger<AiRequestLogRetentionService>.Instance,
            batchSize);

        return await service.PurgeOnceAsync(Ct);
    }

    private async Task<Guid> SeedAsync(int ageDays, bool withTexts = true)
    {
        var entity = new AiRequestLogEntity
        {
            Id = Guid.NewGuid(),
            GameId = Guid.NewGuid(),
            Endpoint = "qa",
            Query = withTexts ? "How many cards does each player draw at setup?" : null,
            ResponseSnippet = withTexts ? "Each player draws five cards at setup." : null,
            ChunksJson = withTexts ? Chunks : null,
            BreakdownJson = Breakdown,
            LatencyMs = 1200,
            TokenCount = 512,
            PromptTokens = 400,
            CompletionTokens = 112,
            Confidence = 0.42,
            OverallConfidence = 0.37,
            IsLowQuality = true,
            Status = "Success",
            Model = "test-model",
            CreatedAt = DateTime.UtcNow.AddDays(-ageDays),
        };
        _dbContext!.AiRequestLogs.Add(entity);
        await _dbContext.SaveChangesAsync(Ct);
        return entity.Id;
    }

    private async Task<AiRequestLogEntity> ReadAsync(Guid id)
    {
        using var scope = _serviceProvider!.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return await db.AiRequestLogs.AsNoTracking().SingleAsync(l => l.Id == id, Ct);
    }
}
