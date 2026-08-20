
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

    // 🔴 PREREQUISITO DEL ROLLOUT — il tempo speso in InitializeAsync NON viene attribuito da xUnit
    // ad alcun test. Convertendo le altre 92 classi, il costo dominante del gate (79% del tempo,
    // mediana 54s per test) uscirebbe dal .trx pur restando sul runner: resterebbe visibile solo il
    // wall-clock aggregato per shard, senza piu' sapere QUALE classe lo consuma. E' la stessa forma
    // di difetto da cui nasce #3742 — una misura che diventa verde smettendo di guardare.
    //
    // Un primo tentativo usava Console.WriteLine seguendo la convenzione dei messaggi di
    // SharedTestcontainersFixture ("✅ Database '...' created in Xs"). VERIFICATO CHE NON FUNZIONA:
    // quell'output non compare ne' in locale con `--logger "console;verbosity=detailed"`, ne' nei
    // log di CI raccolti da #3744 — dove nemmeno la convenzione preesistente compare mai. E' stato
    // rimosso invece di lasciarlo: una strumentazione che non strumenta e' peggio della sua assenza,
    // perche' il prossimo lettore smette di cercarne una vera.
    //
    // Le opzioni per il rollout, da decidere prima di convertire le altre classi:
    //   a) scrivere i tempi su file e aggiungerlo al glob dell'artifact in dev-async.yml;
    //   b) TestContext.Current.SendDiagnosticMessage, che richiede diagnosticMessages: true in
    //      xunit.runner.json (oggi false) e aggiunge rumore globale;
    //   c) accettare la sola granularita' per shard, rinunciando all'attribuzione per classe.
    public async ValueTask InitializeAsync()
    {
        try
        {
            var connectionString = await _shared.CreateIsolatedDatabaseAsync(_databaseName);
            _databaseCreated = true;

            Factory = IntegrationWebApplicationFactory.Create(connectionString);

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
