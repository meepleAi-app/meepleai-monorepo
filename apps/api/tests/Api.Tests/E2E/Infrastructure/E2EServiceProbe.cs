using System.Net;
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
/// </remarks>
internal static class E2EServiceProbe
{
    /// <summary>
    /// I nomi dei check di <c>/health</c> su cui le suite E2E si appoggiano, così un test non deve
    /// indovinare la stringa. Se un nome cambia nel prodotto, <see cref="KnownCheckNamesExist"/> lo
    /// rileva invece di far degradare silenziosamente la sonda a «servizio assente».
    /// </summary>
    internal static class Checks
    {
        internal const string Embedding = "embedding";
        internal const string OpenRouter = "openrouter";
        internal const string Ollama = "ollama";
        internal const string Reranker = "reranker";
        internal const string S3Storage = "s3storage";
    }

    /// <summary>
    /// Salta il test — come <b>PREVISTO</b> — se il check indicato non è sano. Da chiamare in testa
    /// al test, prima di qualunque chiamata sotto esame.
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
        var status = await GetCheckStatusAsync(client, checkName).ConfigureAwait(false);
        var reason = BuildSkipReason(status, checkName, howToEnable);

        if (reason is not null)
        {
            Assert.Skip(reason);
        }
    }

    /// <summary>
    /// Il motivo del salto per uno stato, oppure <c>null</c> se il test deve proseguire.
    /// </summary>
    /// <remarks>
    /// Separato da <see cref="SkipUnlessHealthyAsync"/> per una ragione pratica scoperta scrivendo i
    /// test: <c>Assert.Skip</c> lancia una <c>SkipException</c> che xUnit intercetta a livello di
    /// framework, quindi <c>Record.ExceptionAsync</c> non la cattura e il test che volesse verificare
    /// il salto risulta <b>esso stesso skippato</b> — passando per «ignorato» invece di «passato», il
    /// che è precisamente il modo di non misurare niente contro cui tutto questo lavoro è diretto.
    /// Con la decisione separata dall'effetto, la decisione si testa e il wrapper resta banale.
    /// </remarks>
    internal static string? BuildSkipReason(CheckStatus status, string checkName, string howToEnable) =>
        status switch
        {
            CheckStatus.Healthy => null,

            // 🔴 Degradato NON è assente. Un servizio che risponde male è un guasto da vedere, e
            // saltare qui riporterebbe il difetto che questa classe esiste per correggere.
            CheckStatus.Degraded => null,

            CheckStatus.Absent =>
                $"PREVISTO: il servizio '{checkName}' non è configurato in questo ambiente " +
                $"(non compare fra i check di /health). Per eseguire il test: {howToEnable}",

            CheckStatus.Unhealthy =>
                $"PREVISTO: il servizio '{checkName}' risulta non sano su /health dell'app sotto " +
                $"test. Per eseguire il test: {howToEnable}",

            CheckStatus.Unreachable =>
                $"PREVISTO: /health dell'app sotto test non è interrogabile, quindi la disponibilità " +
                $"di '{checkName}' non è accertabile. Per eseguire il test: {howToEnable}",

            _ => null,
        };

    internal enum CheckStatus
    {
        /// <summary>Il check esiste e riporta Healthy.</summary>
        Healthy,

        /// <summary>Il check esiste e riporta Degraded: il servizio c'è ma risponde male.</summary>
        Degraded,

        /// <summary>Il check esiste e riporta Unhealthy.</summary>
        Unhealthy,

        /// <summary>Il check non compare in /health: il servizio non è registrato in questo ambiente.</summary>
        Absent,

        /// <summary>/health non risponde o non è interpretabile.</summary>
        Unreachable,
    }

    internal static async Task<CheckStatus> GetCheckStatusAsync(HttpClient client, string checkName)
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
                return CheckStatus.Unreachable;
            }

            using var document = JsonDocument.Parse(payload);
            if (!document.RootElement.TryGetProperty("checks", out var checks))
            {
                return CheckStatus.Unreachable;
            }

            foreach (var check in checks.EnumerateArray())
            {
                if (!check.TryGetProperty("name", out var name) ||
                    !string.Equals(name.GetString(), checkName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var state = check.TryGetProperty("status", out var s) ? s.GetString() : null;
                return state switch
                {
                    "Healthy" => CheckStatus.Healthy,
                    "Degraded" => CheckStatus.Degraded,
                    "Unhealthy" => CheckStatus.Unhealthy,
                    _ => CheckStatus.Unreachable,
                };
            }

            return CheckStatus.Absent;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            // Solo i modi in cui /health può essere IRRAGGIUNGIBILE o illeggibile. Un catch largo
            // qui tradurrebbe un bug della sonda in «servizio assente», che è la deduzione vietata.
            return CheckStatus.Unreachable;
        }
    }
}
