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
/// <see cref="WebApplicationFactory{T}"/> lo ricostruisce a ogni test. Misurato sui <c>.trx</c> di
/// CI: i test in classi «database + host per test» hanno mediana <b>54,0s</b> e valgono il
/// <b>79%</b> del tempo dell'intero gate, contro i <b>3,4s</b> delle classi che creano solo il
/// database. I ~50s di differenza sono la costruzione dell'host: MediatR e FluentValidation
/// scandiscono per riflessione un assembly con 445 handler e 678 validator.
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
/// all'host per test: il database costa 3,4s, l'host ~50s, e i due sono separabili.
/// </para>
///
/// <para>
/// Sono inoltre da escludere le classi che manipolano variabili d'ambiente di processo attorno alla
/// costruzione dell'host (<c>AdminProviderEndpointsIntegrationTests</c>,
/// <c>GameNightTokenRateLimitTests</c>): spostare la costruzione in una fixture separa la
/// manipolazione dalla build e le romperebbe.
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
    // Richiede `diagnosticMessages: true` in xunit.runner.json. Costo misurato: su Category=Unit
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
        TestContext.Current.SendDiagnosticMessage(
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
            // il database: sopravvivrebbe per tutta la vita del container. La versione per-test non
            // lo eliminava affatto, quindi ne lasciava uno per ogni test.
            if (_databaseCreated)
            {
                await _shared.DropIsolatedDatabaseAsync(_databaseName);
            }
        }
    }
}
