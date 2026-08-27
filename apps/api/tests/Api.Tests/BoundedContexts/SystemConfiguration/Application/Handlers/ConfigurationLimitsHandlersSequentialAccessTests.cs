using Api.BoundedContexts.SystemConfiguration.Application.Queries;
using Api.Models;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Application.Handlers;

/// <summary>
/// #3843 — i query handler dei limiti non devono interrogare la configurazione in parallelo.
///
/// Tutti risolvono lo stesso <c>IConfigurationService</c>, che a valle usa il
/// <c>MeepleAiDbContext</c> scoped della richiesta. Un <c>Task.WhenAll</c> su piu' letture fa
/// partire query concorrenti sullo stesso context: EF Core lo vieta e solleva
/// <c>InvalidOperationException</c>, che l'endpoint restituisce come 500. Quattro endpoint
/// <c>/admin/config/*-limits</c> erano irraggiungibili per questo.
///
/// Il test non si limita a chiedere che il metodo non esploda: conta la concorrenza osservata.
/// Un handler corretto arriva a 1. Verificare solo l'assenza di eccezione lascerebbe passare una
/// correzione che sposta il parallelismo altrove senza eliminarlo.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SystemConfiguration")]
public class ConfigurationLimitsHandlersSequentialAccessTests
{
    /// <summary>
    /// Riproduce il <c>ConcurrencyDetector</c> di EF Core: una seconda chiamata iniziata prima che
    /// la precedente sia completata solleva la stessa eccezione che solleverebbe il DbContext.
    ///
    /// <c>Task.Yield()</c> e' la parte essenziale — senza, ogni chiamata completerebbe in linea e
    /// nessun parallelismo sarebbe osservabile: il test passerebbe anche sul codice difettoso.
    /// </summary>
    private sealed class RilevatoreDiConcorrenza : IConfigurationService
    {
        private int _inCorso;

        public int MassimaConcorrenzaOsservata { get; private set; }
        public int ChiamateTotali { get; private set; }

        public async Task<SystemConfigurationDto?> GetConfigurationByKeyAsync(
            string key,
            string? environment = null,
            CancellationToken cancellationToken = default)
        {
            var contemporanee = Interlocked.Increment(ref _inCorso);
            MassimaConcorrenzaOsservata = Math.Max(MassimaConcorrenzaOsservata, contemporanee);
            ChiamateTotali++;

            if (contemporanee > 1)
            {
                Interlocked.Decrement(ref _inCorso);
                throw new InvalidOperationException(
                    "A second operation was started on this context instance before a previous " +
                    "operation completed. This is usually caused by different threads concurrently " +
                    "using the same instance of DbContext.");
            }

            await Task.Yield();
            Interlocked.Decrement(ref _inCorso);

            // null = chiave non configurata: gli handler ricadono sui default, che e' il percorso
            // piu' comune in un ambiente appena installato — e quello che l'audit ha incontrato.
            return null;
        }

        public Task<T?> GetValueAsync<T>(string key, T? defaultValue = default, string? environment = null)
            => Task.FromResult(defaultValue);

        public Task InvalidateAsync(string key, string? environment = null, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    // La MemberData espone solo la stringa: IConfigurationService e' internal e non puo'
    // comparire nella firma di un membro pubblico (CS0050/CS0051).
    public static TheoryData<string> Endpoint() => new()
    {
        "pdf-limits",
        "pdf-upload-limits",
        "pdf-tier-upload-limits",
        "chat-history-limits",
        "game-library-limits",
        "session-limits",
    };

    private static Task Esegui(string endpoint, IConfigurationService svc, CancellationToken ct) => endpoint switch
    {
        "pdf-limits" => new GetAllPdfLimitsQueryHandler(svc).Handle(new GetAllPdfLimitsQuery(), ct),
        "pdf-upload-limits" => new GetPdfUploadLimitsQueryHandler(svc).Handle(new GetPdfUploadLimitsQuery(), ct),
        "pdf-tier-upload-limits" => new GetPdfTierUploadLimitsQueryHandler(svc).Handle(new GetPdfTierUploadLimitsQuery(), ct),
        "chat-history-limits" => new GetChatHistoryLimitsQueryHandler(svc).Handle(new GetChatHistoryLimitsQuery(), ct),
        "game-library-limits" => new GetGameLibraryLimitsQueryHandler(svc).Handle(new GetGameLibraryLimitsQuery(), ct),
        "session-limits" => new GetSessionLimitsQueryHandler(svc).Handle(new GetSessionLimitsQuery(), ct),
        _ => throw new ArgumentOutOfRangeException(nameof(endpoint), endpoint, "endpoint non previsto"),
    };

    [Theory]
    [MemberData(nameof(Endpoint))]
    public async Task Handle_LeggeLaConfigurazioneUnaChiaveAllaVolta(string endpoint)
    {
        var servizio = new RilevatoreDiConcorrenza();

        var azione = () => Esegui(endpoint, servizio, CancellationToken.None);

        await azione.Should().NotThrowAsync(
            $"/admin/config/{endpoint} risponde 500 quando l'handler interroga il DbContext in parallelo (#3843)");

        servizio.MassimaConcorrenzaOsservata.Should().Be(1,
            "le letture devono essere sequenziali: il DbContext scoped non e' thread-safe");
        servizio.ChiamateTotali.Should().BeGreaterThan(1,
            "un handler che legge una sola chiave non proverebbe nulla: il test perderebbe di significato");
    }
}
