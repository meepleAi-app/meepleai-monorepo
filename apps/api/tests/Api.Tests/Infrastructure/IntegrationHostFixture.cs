using System.Diagnostics;
using Api.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Infrastructure;

/// <summary>
/// Base per le fixture che condividono host e database fra tutti i test di una classe. Issue #3742.
///
/// <para>
/// <b>Il difetto che risolve.</b> xUnit istanzia la classe di test <b>una volta per metodo</b>,
/// quindi un <c>IAsyncLifetime.InitializeAsync</c> sulla classe di test che costruisce un
/// <see cref="WebApplicationFactory{T}"/> lo ricostruisce a ogni test. Misurato dalla
/// strumentazione qui sotto, non inferito: <c>db=0,1s host=19,2s migrate=5,1s</c> — l'host e'
/// <b>~24,4s</b> di costo per costruzione, di cui il database e' una frazione trascurabile.
/// L'A/B su 52 test (24m59s contro 84s) da' 27,2s risparmiati per test, coerente.
/// </para>
/// <para>
/// La firma diagnostica è che <b>tutti</b> i test di una classe sono lenti in modo uniforme, non
/// «il primo lento e gli altri veloci». Quest'ultimo sarebbe il costo pagato una volta per classe.
/// </para>
///
/// <para>
/// <b>Uso.</b> Una fixture derivata per classe di test, poi
/// <c>IClassFixture&lt;LaTuaFixture&gt;</c> sulla classe:
/// <code>
/// public sealed class LibraryActivityHostFixture(SharedTestcontainersFixture shared)
///     : IntegrationHostFixture(shared, "library_activity");
/// </code>
/// La classe di test deve restare in una <c>[Collection("Integration-Group?")]</c>: è da lì che
/// xUnit risolve il parametro <see cref="SharedTestcontainersFixture"/> del costruttore. Senza,
/// fallisce a <b>runtime</b>, non in compilazione.
/// </para>
///
/// <para>
/// 🔴 <b>Quando NON si può usare.</b> Condividere il database fra i test di una classe è sicuro
/// solo se la classe supera questa domanda: <i>«passerebbe ugualmente se i suoi test girassero in
/// ordine qualsiasi contro un database che contiene già le righe di tutti gli altri?»</i>.
/// Non basta che ogni test crei la propria entità. Tre schemi la falliscono:
/// <list type="bullet">
///   <item>asserzioni su conteggi o liste complete da query <b>non</b> filtrate per utente —
///     endpoint di amministrazione, analytics, metriche;</item>
///   <item>test di unicità o conflitto che attendono 409 o 201 a seconda di cosa esiste già;</item>
///   <item>asserzioni sull'ordinamento globale, dove le righe di un altro test si interpongono.</item>
/// </list>
/// Per una classe che fallisce la domanda serve host condiviso ma database per test — non si torna
/// all'host per test. Il margine reale, dalla strumentazione: creare e migrare un database costa
/// <b>~5,2s</b> (db 0,1 + migrate 5,1) contro i <b>~24,4s</b> del ciclo completo. Attenzione: la
/// migrazione e' il 21% del costo della fixture, non una voce trascurabile — una versione
/// «database per test» la ripaga a ogni test.
///
/// 🔴 Il verdetto di isolamento deve considerare anche cosa i test <b>scrivono</b>, non solo cosa
/// leggono. Un seeder che inserisce senza find-or-create contro un indice unico (per esempio
/// <c>UserEntity.Email</c>) e' strutturalmente impossibile da violare con un database per test, e
/// diventa un 23505 con un database condiviso.
/// </para>
///
/// <para>
/// Sono inoltre da escludere le classi che manipolano variabili d'ambiente di processo attorno alla
/// costruzione dell'host (<c>AdminProviderEndpointsIntegrationTests</c>,
/// <c>GameNightTokenRateLimitTests</c>): spostare la costruzione in una fixture separa la
/// manipolazione dalla build e le romperebbe.
/// </para>
///
/// <para>
/// ✅ <b>La prontezza di Postgres NON e' piu' un motivo di esclusione.</b> Le classi che chiamavano
/// <c>TestcontainersWaitHelpers.WaitForPostgresReadyAsync</c> prima di costruire l'host — trentanove
/// in tutta la suite — possono convertirsi senza perdere quella guardia: e' nella base, subito dopo
/// la creazione del database isolato. Issue #3742.
/// </para>
/// </summary>
public abstract class IntegrationHostFixture : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _shared;
    private readonly string _databaseName;
    private bool _databaseCreated;

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;
    public HttpClient Client { get; private set; } = null!;

    protected IntegrationHostFixture(SharedTestcontainersFixture shared, string databasePrefix)
    {
        _shared = shared;
        _databaseName = $"{databasePrefix}_{Guid.NewGuid():N}";
    }

    // Il tempo speso qui NON viene attribuito da xUnit ad alcun test: nel .trx non esiste. Senza
    // strumentazione, convertire le classi farebbe uscire dalla misura il costo dominante del gate
    // (79% del tempo) lasciandolo sul runner — la stessa forma di difetto da cui nasce #3742.
    //
    // Il canale e' `SendDiagnosticMessage`, non `Console.WriteLine`: quest'ultimo e' stato provato e
    // NON compare da nessuna parte, ne' in locale con `--logger "console;verbosity=detailed"` ne'
    // nei log di CI — dove nemmeno la convenzione preesistente di SharedTestcontainersFixture
    // ("✅ Database '...' created in Xs") e' mai comparsa.
    //
    // Richiede `diagnosticMessages: true` in xunit.runner.json — che ha un effetto collaterale:
    // riattiva anche `longRunningTestSeconds`, finora inerte perche' xUnit lo riporta solo tramite
    // il canale diagnostico. Misurato sui .trx reali del gate: con la soglia a 30s sarebbero
    // 100-205 righe in piu' per shard (+22%..+49% sul log), che seppellirebbero proprio le righe
    // fixture-timing. Per questo la soglia e' stata portata a 300s: intercetta ancora un test
    // davvero bloccato, non la popolazione normale. Costo misurato: su Category=Unit
    // (22.568 test) il log resta di 86 righe; su 3 test passa da 4 a 5. Il messaggio finisce nel log
    // che dev-async pubblica come artifact (#3744), quindi e' aggregabile per shard con:
    //   grep -o 'fixture-timing .*' integration-<shard>.log | sort -t' ' -k3 -rn | head -20
    public async ValueTask InitializeAsync()
    {
        var started = Stopwatch.GetTimestamp();
        long afterDb;
        long afterHost;

        try
        {
            var connectionString = await _shared.CreateIsolatedDatabaseAsync(_databaseName);
            _databaseCreated = true;

            // Issue #3742. La guardia di #2031: Docker Desktop su Windows fallisce a intermittenza
            // l'hijack dell'exec, e il retry TCP con backoff e' piu' affidabile della wait strategy
            // di Testcontainers. Trentanove classi la chiamavano prima di costruire l'host; questa
            // base no, quindi convertirle avrebbe significato deciderne il destino trentanove volte
            // dentro commit meccanici. E' qui perche' vada decisa una volta sola.
            //
            // Nel caso normale la connessione riesce al primo tentativo e il costo e' una OpenAsync:
            // sta dentro il campo `db=` della strumentazione qui sotto, che infatti resta a 0,0-0,1s.
            await TestcontainersWaitHelpers.WaitForPostgresReadyAsync(connectionString);
            afterDb = Stopwatch.GetTimestamp();

            Factory = IntegrationWebApplicationFactory.Create(connectionString);

            // `WithWebHostBuilder` e' PIGRO: l'host si costruisce al primo accesso a `.Services`.
            // Senza questa riga il tempo dell'host finirebbe nel campo `migrate=` e la
            // strumentazione riporterebbe `host=0,0` — misurato davvero, prima di accorgersene.
            _ = Factory.Services;
            afterHost = Stopwatch.GetTimestamp();

            using (var scope = Factory.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
                await dbContext.Database.MigrateAsync();
            }

            Client = Factory.CreateClient();
        }
        catch
        {
            // Un fallimento a meta' non deve lasciare in giro il database: se xUnit disponga o meno
            // una fixture la cui inizializzazione ha lanciato e' un dettaglio del framework su cui
            // non conviene che 93 classi scommettano.
            await SafeDisposeAsync();
            throw;
        }

        // Prefisso stabile e campi in secondi: e' pensato per essere greppato e ordinato, non letto.
        TestContext.Current?.SendDiagnosticMessage(
            $"fixture-timing {GetType().Name} " +
            $"{Stopwatch.GetElapsedTime(started).TotalSeconds:F1} " +
            $"db={Stopwatch.GetElapsedTime(started, afterDb).TotalSeconds:F1} " +
            $"host={Stopwatch.GetElapsedTime(afterDb, afterHost).TotalSeconds:F1} " +
            $"migrate={Stopwatch.GetElapsedTime(afterHost).TotalSeconds:F1}");
    }

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
        finally
        {
            // Nel finally perche' un fallimento nella dismissione dell'host non deve far trapelare
            // il database: sopravvivrebbe per tutta la vita del container. Sette delle tredici
            // classi convertite non lo eliminavano affatto, quindi ne lasciavano uno per ogni test;
            // le altre sei lo eliminavano per test. In entrambi i casi ora e' una volta per classe.
            if (_databaseCreated)
            {
                // Azzerato PRIMA di rilasciare: su fallimento in InitializeAsync questo metodo viene
                // chiamato due volte (una da noi, una da xUnit alla dismissione della fixture) e
                // DropIsolatedDatabaseAsync invoca NpgsqlConnection.ClearAllPools(), che e'
                // process-global e strappa le connessioni alle altre collection in parallelo.
                _databaseCreated = false;
                await _shared.DropIsolatedDatabaseAsync(_databaseName);
            }
        }
    }
}
