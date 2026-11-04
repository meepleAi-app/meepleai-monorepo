using Api.BoundedContexts.Administration.Application.Queries.Resources;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.Administration;

/// <summary>
/// #3833 — <c>GET /resources/database/tables/top</c> rispondeva 500.
///
/// Il difetto era doppio, e il secondo sopravvive alla correzione del primo:
///
/// 1. la query leggeva <c>tablename</c> da <c>pg_stat_user_tables</c>, dove la colonna si chiama
///    <c>relname</c> (<c>tablename</c> appartiene a <c>pg_tables</c>) — errore 42703;
/// 2. passava poi <c>schemaname||'.'||relname</c> alle funzioni di dimensione. Quella stringa e'
///    un identificatore NON quotato: Postgres la ripiega in minuscolo, e uno schema come
///    <c>SystemConfiguration</c> — che esiste in questo database — diventa
///    <c>systemconfiguration</c>, che non esiste. Errore 3F000.
///
/// Per questo il test crea uno schema con una maiuscola: senza, verificherebbe solo il primo
/// difetto e lascerebbe passare la correzione ingenua.
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Administration")]
[Trait("Issue", "3833")]
public sealed class TopTablesBySizeQueryIntegrationTests : IAsyncLifetime
{
    private const string SchemaConMaiuscole = "SchemaConMaiuscole";
    private const string NomeTabella = "righe_di_prova";

    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public TopTablesBySizeQueryIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_top_tables_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        services.AddSingleton<IMediator>(new Mock<IMediator>().Object);
        services.AddSingleton<IDomainEventCollector>(new Mock<IDomainEventCollector>().Object);
        services.AddDbContext<MeepleAiDbContext>(options =>
        {
            options.UseNpgsql(connectionString, o => o.UseVector());
            options.ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        _dbContext = services.BuildServiceProvider().GetRequiredService<MeepleAiDbContext>();

        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                await _dbContext.Database.MigrateAsync(TestCancellationToken);
                break;
            }
            catch (NpgsqlException) when (attempt < 2)
            {
                await Task.Delay(TestConstants.Timing.RetryDelay, TestCancellationToken);
            }
        }

        // Lo schema quotato riproduce cio' che il database reale ha davvero ("SystemConfiguration").
        // ANALYZE serve perche' pg_stat_user_tables elenca solo le tabelle di cui il collettore di
        // statistiche sa qualcosa: senza, la tabella appena creata potrebbe non comparire e il test
        // fallirebbe per una ragione che non c'entra con il difetto.
        await _dbContext.Database.ExecuteSqlRawAsync(
            $"""
             CREATE SCHEMA IF NOT EXISTS "{SchemaConMaiuscole}";
             CREATE TABLE IF NOT EXISTS "{SchemaConMaiuscole}"."{NomeTabella}" (id int primary key, testo text);
             INSERT INTO "{SchemaConMaiuscole}"."{NomeTabella}"
                 SELECT g, repeat('x', 200) FROM generate_series(1, 500) g;
             ANALYZE "{SchemaConMaiuscole}"."{NomeTabella}";
             """,
            TestCancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext is not null)
        {
            await _dbContext.DisposeAsync();
        }

        if (!string.IsNullOrEmpty(_databaseName))
        {
            await _fixture.DropIsolatedDatabaseAsync(_databaseName);
        }
    }

    [Fact]
    public async Task Handle_ConUnoSchemaConMaiuscole_ElencaLeTabelleConLeLoroDimensioni()
    {
        var handler = new GetTopTablesBySizeQueryHandler(_dbContext!);

        var risultato = await handler.Handle(new GetTopTablesBySizeQuery(100), TestCancellationToken);

        risultato.Should().NotBeEmpty(
            "l'endpoint deve elencare le tabelle, non rispondere 500 (#3833)");

        var nostra = risultato.SingleOrDefault(t => t.TableName == $"{SchemaConMaiuscole}.{NomeTabella}");
        nostra.Should().NotBeNull(
            "una tabella in uno schema con maiuscole deve comparire come le altre: e' il caso che " +
            "la concatenazione non quotata faceva fallire");

        // Le dimensioni provano che le funzioni pg_*_size hanno risolto davvero la tabella: se
        // l'identificatore non fosse stato risolto, la query sarebbe morta prima di arrivare qui.
        nostra!.TotalSizeBytes.Should().BeGreaterThan(0);
        nostra.SizeBytes.Should().BeGreaterThan(0);
        nostra.TotalSizeBytes.Should().BeGreaterThanOrEqualTo(nostra.SizeBytes,
            "la dimensione totale comprende indici e TOAST");
        nostra.SizeFormatted.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Handle_RispettaIlLimiteRichiesto()
    {
        var handler = new GetTopTablesBySizeQueryHandler(_dbContext!);

        var risultato = await handler.Handle(new GetTopTablesBySizeQuery(3), TestCancellationToken);

        risultato.Should().HaveCountLessThanOrEqualTo(3);
    }
}
