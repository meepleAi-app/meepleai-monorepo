using System.Diagnostics;
using Api.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Api.Tests.Infrastructure;

/// <summary>
/// Base per le fixture che condividono l'<b>host</b> fra i test di una classe ma danno a ciascun
/// test un <b>database</b> suo. Issue #4050, residuo di #3742.
///
/// <para>
/// <b>Perché esiste, accanto a <see cref="IntegrationHostFixture"/>.</b> Quella condivide host
/// <i>e</i> database, ed è la più economica, ma è usabile solo da una classe che supera la domanda
/// «passerebbe ugualmente se i suoi test girassero in ordine qualsiasi contro un database che
/// contiene già le righe di tutti gli altri?». Il bersaglio più costoso dello shard Core —
/// <c>AgentsEndpointsIntegrationTests</c>, 20,5 min su 70,6 della sua collection — la <b>fallisce</b>:
/// interroga <c>/api/v1/agents</c>, che è una lista globale, e asserisce <c>BeEmpty()</c> e
/// <c>HaveCount(2)</c>. La documentazione di <see cref="IntegrationHostFixture"/> prevedeva questa
/// variante («host condiviso ma database per test») e non era implementata: le classi che
/// fallivano la domanda restavano sull'host per test, cioè sul costo dominante.
/// </para>
///
/// <para>
/// <b>Il conto, dalla strumentazione di #3742</b> (<c>db=0,1s host=19,2s migrate=5,1s</c>):
/// </para>
/// <list type="bullet">
///   <item>l'host si paga <b>una volta</b> per classe, non per test: −19,2 s per test;</item>
///   <item>la migrazione si paga <b>una volta</b> su un database <i>template</i>: −5,1 s per test;</item>
///   <item>per test resta <c>CREATE DATABASE … TEMPLATE</c>, misurato in #3742 a <b>135-159 ms</b>
///     contro i 5,1-7,4 s di una migrazione.</item>
/// </list>
///
/// <para>
/// <b>Uso.</b> Una fixture derivata per classe di test, più la chiamata per test:
/// <code>
/// public sealed class AgentsHostFixture(SharedTestcontainersFixture shared)
///     : SharedHostPerTestDatabaseFixture(shared, "agents_endpoints");
///
/// [Collection("Integration-GroupC")]
/// public sealed class AgentsEndpointsIntegrationTests(AgentsHostFixture fixture)
///     : IClassFixture&lt;AgentsHostFixture&gt;, IAsyncLifetime
/// {
///     public ValueTask InitializeAsync() =&gt; fixture.BeginTestAsync();   // database fresco
/// }
/// </code>
/// Il punto di aggancio per test è l'<c>InitializeAsync</c> della <b>classe di test</b>, perché
/// xUnit non offre un hook per-test sulla class fixture. I metodi di una stessa classe non girano
/// in parallelo, quindi mutare la configurazione dell'host condiviso è sicuro.
/// </para>
///
/// <para>
/// 🔴 <b>Cosa questa fixture NON isola, e va saputo prima di usarla.</b> L'host è condiviso, quindi
/// tutto ciò che non sta nel database <b>sopravvive</b> fra i test della classe: singleton, cache in
/// memoria, mock di Redis, contatori di metriche. Una classe che dipende dall'azzeramento di quello
/// stato — per esempio asserendo un contatore Prometheus da zero, o contando le chiamate a un mock —
/// non può usare né questa fixture né <see cref="IntegrationHostFixture"/>: deve restare sull'host
/// per test. La promessa qui è <i>isolamento del database</i>, non <i>isolamento dell'host</i>.
/// </para>
/// </summary>
public abstract class SharedHostPerTestDatabaseFixture : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _shared;
    private readonly string _prefix;
    private readonly MutableConnectionStringSource _connectionSource = new();
    private readonly List<string> _createdDatabases = new();

    private string? _templateDatabase;
    private string _adminConnectionString = null!;
    private int _testOrdinal;

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;
    public HttpClient Client { get; private set; } = null!;

    /// <summary>Il nome del database che il test corrente sta usando. Per diagnostica.</summary>
    public string? CurrentDatabase { get; private set; }

    protected SharedHostPerTestDatabaseFixture(SharedTestcontainersFixture shared, string databasePrefix)
    {
        _shared = shared;
        // Il prefisso entra in un nome di database: va validato qui, perché più in basso finirebbe
        // in una CREATE DATABASE interpolata.
        if (!System.Text.RegularExpressions.Regex.IsMatch(
                databasePrefix, "^[a-z0-9_]+$", System.Text.RegularExpressions.RegexOptions.None, TimeSpan.FromSeconds(1)))
        {
            throw new ArgumentException(
                "il prefisso del database deve contenere solo [a-z0-9_]", nameof(databasePrefix));
        }

        _prefix = $"{databasePrefix}_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        var started = Stopwatch.GetTimestamp();
        long afterTemplate;
        long afterHost;

        try
        {
            // 1. Il TEMPLATE: creato e migrato una volta sola.
            _templateDatabase = $"{_prefix}_tmpl";
            var templateConnection = await _shared.CreateIsolatedDatabaseAsync(_templateDatabase);
            await TestcontainersWaitHelpers.WaitForPostgresReadyAsync(templateConnection);

            _adminConnectionString = new NpgsqlConnectionStringBuilder(templateConnection)
            {
                Database = "postgres",
            }.ConnectionString;

            // La migrazione del template gira su un host provvisorio e usa-e-getta: deve chiudere
            // ogni connessione prima che il template venga usato come sorgente di una CREATE
            // DATABASE, perché Postgres la rifiuta se qualcuno è connesso al template.
            await using (var migrationContext = await TestHelpers.CreateDbContextAndMigrateAsync(templateConnection))
            {
                // CreateDbContextAndMigrateAsync migra; qui si chiude e basta.
            }

            // 🔴 Chiudere il DbContext non basta: Npgsql tiene un POOL di connessioni per stringa, e
            // le connessioni in pool restano aperte verso il template. Senza questo, la prima
            // CREATE DATABASE … TEMPLATE fallirebbe con
            // «source database is being accessed by other users» — un errore che si manifesta solo
            // al secondo uso della fixture, quindi facile da scambiare per un flake.
            NpgsqlConnection.ClearAllPools();
            afterTemplate = Stopwatch.GetTimestamp();

            // 2. Un database di AVVIO, clonato dal template, su cui puntare l'host.
            //
            // 🔴 Perché serve, misurato e non previsto correttamente alla prima stesura: puntare
            // l'host al TEMPLATE lo rende inutilizzabile come sorgente. L'host apre una connessione
            // al proprio database quando lo si costruisce, e `ClearAllPools()` qui sopra gira PRIMA,
            // quindi non la tocca. La prima `CREATE DATABASE … TEMPLATE` falliva con:
            //
            //     Npgsql.PostgresException : 55006: source database "…_tmpl" is being accessed
            //     by other users
            //
            // Clonare una volta in più costa ~0,15s per classe e lascia il template senza
            // connessioni per sempre. L'alternativa — non scaldare l'host qui — sposterebbe i suoi
            // ~19s dentro il primo test, cioè fuori dalla misura della fixture: il difetto da cui
            // nasce #3742.
            var bootDatabase = $"{_prefix}_boot";
            await CloneFromTemplateAsync(bootDatabase);
            _createdDatabases.Add(bootDatabase);

            var bootConnection = ConnectionTo(bootDatabase);
            _connectionSource.Provider.SetConnectionString(bootConnection);

            // 3. L'HOST: costruito una volta, con la stringa di connessione mutabile.
            Factory = IntegrationWebApplicationFactory.Create(
                bootConnection,
                mutableConnectionString: _connectionSource);

            // `WithWebHostBuilder` è PIGRO: senza questa riga il costo dell'host finirebbe nel primo
            // test invece che qui, e la strumentazione riporterebbe host=0,0 — lo stesso inganno
            // documentato in IntegrationHostFixture.
            _ = Factory.Services;
            afterHost = Stopwatch.GetTimestamp();

            Client = Factory.CreateClient();
        }
        catch
        {
            await SafeDisposeAsync();
            throw;
        }

        TestContext.Current?.SendDiagnosticMessage(
            $"fixture-timing {GetType().Name} " +
            $"{Stopwatch.GetElapsedTime(started).TotalSeconds:F1} " +
            $"template={Stopwatch.GetElapsedTime(started, afterTemplate).TotalSeconds:F1} " +
            $"host={Stopwatch.GetElapsedTime(afterTemplate, afterHost).TotalSeconds:F1}");
    }

    /// <summary>
    /// Dà al test corrente un database fresco, clonato dal template, e punta l'host condiviso lì.
    /// Da chiamare dall'<c>InitializeAsync</c> della classe di test.
    /// </summary>
    public async ValueTask BeginTestAsync()
    {
        if (_templateDatabase is null)
        {
            throw new InvalidOperationException(
                "BeginTestAsync chiamata prima di InitializeAsync della fixture");
        }

        var started = Stopwatch.GetTimestamp();
        var name = $"{_prefix}_t{Interlocked.Increment(ref _testOrdinal)}";

        await CloneFromTemplateAsync(name);
        _createdDatabases.Add(name);
        CurrentDatabase = name;
        _connectionSource.Provider.SetConnectionString(ConnectionTo(name));

        TestContext.Current?.SendDiagnosticMessage(
            $"per-test-db {GetType().Name} {name} " +
            $"{Stopwatch.GetElapsedTime(started).TotalSeconds:F2}s");
    }

    private string ConnectionTo(string database) =>
        new NpgsqlConnectionStringBuilder(_adminConnectionString) { Database = database }
            .ConnectionString;

    /// <summary>
    /// Clona il template in <paramref name="name"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 🔴 <b>Perché non è una sola CREATE DATABASE.</b> Postgres rifiuta un template a cui qualcuno
    /// è connesso (<c>55006: source database … is being accessed by other users</c>), e
    /// <c>NpgsqlConnection.ClearAllPools()</c> da solo non basta: chiude la connessione lato client,
    /// ma il backend sul server resta per un istante. La firma misurata è una CORSA, non un
    /// detentore permanente — nella prima stesura il primo clone falliva e i successivi passavano
    /// (<c>t2=1,97s t3=0,14s t4=0,05s</c>); spostando il primo clone subito dopo la migrazione
    /// fallivano <b>tutti</b>, perché era il momento più vicino alla chiusura.
    /// </para>
    /// <para>
    /// Quindi: si terminano i backend residui sul template, e se il 55006 arriva comunque si
    /// riprova. E prima di terminare si <b>elencano</b> i detentori nel canale diagnostico: se un
    /// giorno il colpevole fosse un detentore vero e non una coda di chiusura, il log lo nomina
    /// invece di lasciare un retry che maschera il difetto.
    /// </para>
    /// </remarks>
    private async Task CloneFromTemplateAsync(string name)
    {
        const int maxAttempts = 3;

        for (var attempt = 1; ; attempt++)
        {
            await TerminateTemplateBackendsAsync(attempt);

            try
            {
                await using var connection = new NpgsqlConnection(_adminConnectionString);
                await connection.OpenAsync();
                await using var cmd = connection.CreateCommand();
                // I nomi vengono da _prefix (validato nel costruttore) e da un contatore: nessun
                // input esterno entra in questa stringa.
#pragma warning disable CA2100 // SQL injection safe: _prefix validato con ^[a-z0-9_]+$, l ordinale e un int
                cmd.CommandText = $"CREATE DATABASE \"{name}\" TEMPLATE \"{_templateDatabase}\";";
#pragma warning restore CA2100
                await cmd.ExecuteNonQueryAsync();
                return;
            }
            catch (PostgresException ex) when (ex.SqlState == "55006" && attempt < maxAttempts)
            {
                // La coda di chiusura dura millisecondi: un'attesa breve e crescente basta.
                TestContext.Current?.SendDiagnosticMessage(
                    $"per-test-db {GetType().Name} 55006 al tentativo {attempt}, riprovo");
                await Task.Delay(TimeSpan.FromMilliseconds(100 * attempt));
            }
        }
    }

    private async Task TerminateTemplateBackendsAsync(int attempt)
    {
        NpgsqlConnection.ClearAllPools();

        try
        {
            await using var connection = new NpgsqlConnection(_adminConnectionString);
            await connection.OpenAsync();

            // Chi è connesso al template, prima di terminarlo: serve a distinguere una coda di
            // chiusura (nessuno, o una connessione `idle` che sparisce) da un detentore vero.
            await using (var who = connection.CreateCommand())
            {
                who.CommandText =
                    "SELECT coalesce(string_agg(application_name || '/' || state, ', '), '(nessuno)') " +
                    "FROM pg_stat_activity WHERE datname = @db AND pid <> pg_backend_pid();";
                who.Parameters.AddWithValue("db", _templateDatabase!);
                var holders = (await who.ExecuteScalarAsync())?.ToString() ?? "(sconosciuto)";
                if (holders != "(nessuno)")
                {
                    TestContext.Current?.SendDiagnosticMessage(
                        $"per-test-db {GetType().Name} detentori del template al tentativo {attempt}: {holders}");
                }
            }

            await using var kill = connection.CreateCommand();
            kill.CommandText =
                "SELECT pg_terminate_backend(pid) FROM pg_stat_activity " +
                "WHERE datname = @db AND pid <> pg_backend_pid();";
            kill.Parameters.AddWithValue("db", _templateDatabase!);
            await kill.ExecuteNonQueryAsync();
        }
        catch (PostgresException)
        {
            // Non poter terminare non è di per sé un fallimento: la CREATE DATABASE che segue dirà
            // se il template è libero o no, e con un messaggio più utile di questo.
        }
    }

    /// <summary>
    /// Uno scope sul database del test corrente. Comodità per i test che devono seminare dati:
    /// equivale a <c>Factory.Services.CreateScope()</c>, ma dice nel nome che la connessione è
    /// quella impostata da <see cref="BeginTestAsync"/>.
    /// </summary>
    public IServiceScope CreateScopeOnCurrentDatabase() => Factory.Services.CreateScope();

    public ValueTask DisposeAsync() => SafeDisposeAsync();

    private async ValueTask SafeDisposeAsync()
    {
        try
        {
            Client?.Dispose();
            if (Factory is not null)
            {
                await Factory.DisposeAsync();
            }
        }
        catch
        {
            // La dismissione dell'host non deve nascondere l'esito dei test.
        }

        // I database per test e il template: lasciarli in giro riempie il container di Postgres, e
        // su un runner condiviso diventa il difetto di qualcun altro.
        NpgsqlConnection.ClearAllPools();
        if (_adminConnectionString is null)
        {
            return;
        }

        foreach (var db in _createdDatabases)
        {
            await DropQuietlyAsync(db);
        }

        if (_templateDatabase is not null)
        {
            await DropQuietlyAsync(_templateDatabase);
        }
    }

    private async Task DropQuietlyAsync(string database)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_adminConnectionString);
            await connection.OpenAsync();
            await using var cmd = connection.CreateCommand();
#pragma warning disable CA2100 // SQL injection safe: i nomi vengono da _prefix, validato nel costruttore
            cmd.CommandText = $"DROP DATABASE IF EXISTS \"{database}\" WITH (FORCE);";
#pragma warning restore CA2100
            await cmd.ExecuteNonQueryAsync();
        }
        catch
        {
            // Un database non rimosso è spreco, non un fallimento del test: non deve mascherare
            // l'esito.
        }
    }
}
