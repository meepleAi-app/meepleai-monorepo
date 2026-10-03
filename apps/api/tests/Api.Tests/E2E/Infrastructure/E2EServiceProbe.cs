using System.Text.Json;
using Xunit;

namespace Api.Tests.E2E.Infrastructure;

/// <summary>
/// Accerta la disponibilità di un servizio <b>prima</b> di eseguire un test, interrogando l'health
/// endpoint dell'app sotto test.
/// </summary>
/// <remarks>
/// <para>
/// #4023 / spec R1.6 — esiste per sostituire una deduzione con una misura. Il pattern che rimpiazza
/// leggeva un <c>500</c> come «servizio esterno probabilmente non disponibile» e saltava:
/// </para>
/// <code>
/// if (response.StatusCode == HttpStatusCode.InternalServerError)
///     Assert.Skip($"{context} returned 500 (external service likely unavailable)");
/// </code>
/// <para>
/// Ma un <c>500</c> è anche — e soprattutto — ciò che produce un <b>bug del prodotto</b>. Quel
/// pattern quindi nascondeva esattamente i difetti che un test E2E esiste per trovare: scrivi una
/// regressione che fa esplodere l'endpoint, e la suite diventa gialla invece di rossa. È la stessa
/// classe di difetto per cui l'immagine MinIO sparita è passata inosservata un mese (#3978), qui
/// applicata al codice invece che all'ambiente.
/// </para>
/// <para>
/// 🔴 La regola che ne deriva, e che questa classe rende praticabile: <b>l'assenza si accerta con una
/// sonda, non si deduce da una risposta.</b> Se il prerequisito non c'è, il test salta PRIMA di
/// eseguire, come PREVISTO. Se c'è, un errore del codice sotto test è un <b>fallimento</b>.
/// </para>
/// <para>
/// Il bersaglio della sonda è <c>/health</c> dell'app <b>in-processo</b>, raggiunto con lo STESSO
/// <c>HttpClient</c> che il test usa per la chiamata sotto esame. Non è un dettaglio: l'helper
/// precedente (<c>E2ETestPrerequisites</c>) sondava <c>localhost:8080</c> e Qdrant <c>:6333</c> —
/// un processo esterno che queste suite non usano, e un servizio che questo repo non ha più. Sondare
/// qualcosa che il test non usa produce salti permanenti o verdi falsi, a seconda del verso.
/// </para>
/// <para>
/// 🔴 <b>Un check assente è un difetto della sonda, non un servizio mancante</b> — e questa classe
/// ci è cascata. La prima stesura trattava <see cref="CheckStatus.Absent"/> come un motivo di salto.
/// Nel run <c>37108228227</c> tre test di <c>ArbitroAgentE2ETests</c> — che nel run di controllo su
/// main-dev (<c>37109312581</c>) <b>passavano</b> — sono diventati salti, perché la sonda chiedeva
/// di un check <c>orchestrator</c> che in quell'host non è registrato. Tre test smessi di eseguire,
/// e visibili solo confrontando due run. Dedurre «il servizio non c'è» dal fatto che non si trova
/// il suo check è la stessa deduzione vietata di prima, spostata di un livello: l'unica cosa che
/// quell'assenza prova è che la sonda non può accertare niente. Ora <b>fallisce</b>, elencando i
/// check che ci sono davvero.
/// </para>
/// </remarks>
internal static class E2EServiceProbe
{
    /// <summary>
    /// I nomi dei check di <c>/health</c> su cui le suite E2E si appoggiano, così un test non deve
    /// indovinare la stringa.
    /// </summary>
    /// <remarks>
    /// Non è decorazione: le tre chiamate che hanno smesso di eseguire tre test passavano una
    /// stringa cruda (<c>"orchestrator"</c>) invece di una costante di qui, aggirando l'unico punto
    /// che tiene allineati i nomi al prodotto.
    /// </remarks>
    internal static class Checks
    {
        internal const string Embedding = "embedding";
        internal const string OpenRouter = "openrouter";
        internal const string Ollama = "ollama";
        internal const string Reranker = "reranker";
        internal const string S3Storage = "s3storage";
    }

    /// <summary>
    /// Cosa deve fare il test dopo la sonda.
    /// </summary>
    internal enum ProbeOutcome
    {
        /// <summary>Il prerequisito è accertato: il test prosegue.</summary>
        Proceed,

        /// <summary>Il prerequisito non c'è in questo ambiente: salto PREVISTO.</summary>
        Skip,

        /// <summary>La sonda non può accertare niente: è un difetto da correggere, non un salto.</summary>
        Fail,
    }

    /// <summary>
    /// Salta il test — come <b>PREVISTO</b> — se il check indicato non è sano, e lo <b>fa fallire</b>
    /// se la sonda non è in grado di accertarlo. Da chiamare in testa al test, prima di qualunque
    /// chiamata sotto esame.
    /// </summary>
    /// <param name="client">
    /// Lo stesso client che il test usa per la chiamata sotto esame, così la sonda misura l'app che
    /// il test interroga davvero.
    /// </param>
    /// <param name="checkName">Uno dei nomi in <see cref="Checks"/>.</param>
    /// <param name="howToEnable">
    /// Come rendere disponibile il servizio. Obbligatorio: uno skip che non dice cosa fare equivale a
    /// un test cancellato — è la lezione che <c>GoldenDatasetLoaderTests</c> aveva già scritto, in un
    /// punto solo su cinquanta.
    /// </param>
    internal static async Task SkipUnlessHealthyAsync(
        HttpClient client,
        string checkName,
        string howToEnable)
    {
        var reading = await ReadAsync(client, checkName).ConfigureAwait(false);
        var (outcome, message) = Decide(reading, checkName, howToEnable);

        switch (outcome)
        {
            case ProbeOutcome.Skip:
                Assert.Skip(message!);
                break;
            case ProbeOutcome.Fail:
                Assert.Fail(message!);
                break;
        }
    }

    /// <summary>
    /// La decisione per una lettura della sonda, separata dall'effetto.
    /// </summary>
    /// <remarks>
    /// Separata per una ragione pratica scoperta scrivendo i test: <c>Assert.Skip</c> lancia una
    /// <c>SkipException</c> che xUnit intercetta a livello di framework, quindi
    /// <c>Record.ExceptionAsync</c> non la cattura e il test che volesse verificare il salto risulta
    /// <b>esso stesso skippato</b> — passando per «ignorato» invece di «passato», il che è
    /// precisamente il modo di non misurare niente contro cui tutto questo lavoro è diretto. Con la
    /// decisione separata dall'effetto, la decisione si testa e il wrapper resta banale.
    /// </remarks>
    internal static (ProbeOutcome Outcome, string? Message) Decide(
        ProbeReading reading,
        string checkName,
        string howToEnable) =>
        reading.Status switch
        {
            CheckStatus.Healthy => (ProbeOutcome.Proceed, null),

            // 🔴 Degradato NON è assente. Un servizio che risponde male è un guasto da vedere, e
            // saltare qui riporterebbe il difetto che questa classe esiste per correggere.
            CheckStatus.Degraded => (ProbeOutcome.Proceed, null),

            // 🔴 Assente NON è «servizio mancante»: è «la sonda non può accertare niente». Vedi il
            // paragrafo sul run 37108228227 nella documentazione della classe.
            CheckStatus.Absent => (
                ProbeOutcome.Fail,
                $"DIFETTO DELLA SONDA: il check '{checkName}' non compare fra quelli di /health di "
                + $"quest'app, quindi la sua disponibilità non è accertabile e questo test non sa "
                + $"cosa sta aspettando. Non è un servizio assente: è un nome che quest'host non "
                + $"registra.{Environment.NewLine}"
                + $"Check presenti: {FormatPresent(reading.PresentChecks)}.{Environment.NewLine}"
                + $"Correggi la chiamata usando una costante di E2EServiceProbe.Checks, oppure "
                + $"togli la sonda se il test non dipende da quel servizio (se passa senza, non "
                + $"dipende)."),

            CheckStatus.Unhealthy => (
                ProbeOutcome.Skip,
                $"PREVISTO: il servizio '{checkName}' risulta non sano su /health dell'app sotto "
                + $"test. Per eseguire il test: {howToEnable}"),

            CheckStatus.Unreachable => (
                ProbeOutcome.Skip,
                $"PREVISTO: /health dell'app sotto test non è interrogabile, quindi la disponibilità "
                + $"di '{checkName}' non è accertabile. Per eseguire il test: {howToEnable}"),

            _ => (ProbeOutcome.Proceed, null),
        };

    private static string FormatPresent(IReadOnlyList<string> present) =>
        present.Count == 0 ? "nessuno (il corpo di /health non ne elencava)" : string.Join(", ", present);

    /// <summary>
    /// Lo stato di un check, con i nomi dei check effettivamente presenti.
    /// </summary>
    /// <remarks>
    /// I nomi presenti servono al messaggio di <see cref="CheckStatus.Absent"/>: un errore che dice
    /// «non trovato» senza dire cosa c'era costringe chi legge a rifare la misura a mano.
    /// </remarks>
    internal sealed record ProbeReading(CheckStatus Status, IReadOnlyList<string> PresentChecks)
    {
        internal static ProbeReading Of(CheckStatus status) => new(status, Array.Empty<string>());
    }

    internal enum CheckStatus
    {
        /// <summary>Il check esiste e riporta Healthy.</summary>
        Healthy,

        /// <summary>Il check esiste e riporta Degraded: il servizio c'è ma risponde male.</summary>
        Degraded,

        /// <summary>Il check esiste e riporta Unhealthy.</summary>
        Unhealthy,

        /// <summary>Il check non compare in /health: la sonda non può accertare nulla.</summary>
        Absent,

        /// <summary>/health non risponde o non è interpretabile.</summary>
        Unreachable,
    }

    internal static async Task<ProbeReading> ReadAsync(HttpClient client, string checkName)
    {
        try
        {
            // /health risponde 200 quando è sano e 503 quando un check critico non lo è: in ENTRAMBI
            // i casi il corpo porta l'elenco dei check, che è quello che serve. Leggere solo lo
            // status code qui sarebbe lo stesso errore di leggere un 500 come «servizio assente».
            using var response = await client.GetAsync("/health").ConfigureAwait(false);
            var payload = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(payload))
            {
                return ProbeReading.Of(CheckStatus.Unreachable);
            }

            using var document = JsonDocument.Parse(payload);
            if (!document.RootElement.TryGetProperty("checks", out var checks))
            {
                return ProbeReading.Of(CheckStatus.Unreachable);
            }

            var present = new List<string>();
            var status = CheckStatus.Absent;

            foreach (var check in checks.EnumerateArray())
            {
                if (!check.TryGetProperty("name", out var name))
                {
                    continue;
                }

                var current = name.GetString();
                if (current is not null)
                {
                    present.Add(current);
                }

                if (!string.Equals(current, checkName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var state = check.TryGetProperty("status", out var s) ? s.GetString() : null;
                status = state switch
                {
                    "Healthy" => CheckStatus.Healthy,
                    "Degraded" => CheckStatus.Degraded,
                    "Unhealthy" => CheckStatus.Unhealthy,
                    _ => CheckStatus.Unreachable,
                };
            }

            return new ProbeReading(status, present);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            // Solo i modi in cui /health può essere IRRAGGIUNGIBILE o illeggibile. Un catch largo
            // qui tradurrebbe un bug della sonda in «servizio assente», che è la deduzione vietata.
            return ProbeReading.Of(CheckStatus.Unreachable);
        }
    }
}
