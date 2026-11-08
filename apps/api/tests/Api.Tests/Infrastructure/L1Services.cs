using Xunit;

namespace Api.Tests.Infrastructure;

/// <summary>
/// I servizi **L1** del progetto: locali, deterministici, e che <b>devono esserci</b>.
/// </summary>
/// <remarks>
/// <para>
/// #4022 / spec R3 — questa lista è il «posto eseguibile» che la spec chiede, e non un commento:
/// esiste perché un test possa interrogarla e perché un gate possa verificare che nessuno la
/// contraddica.
/// </para>
/// <para>
/// La distinzione che porta: per un servizio L1 un salto è un <b>difetto mascherato</b>. MinIO è L1 e
/// le due suite S3 lo trattavano come opzionale — per questo la sparizione dell'immagine da Docker
/// Hub ha prodotto un mese di gialli invece di un rosso immediato (#3978). Un servizio che deve
/// esserci, se non c'è, è un ambiente rotto: va riparato, non aggirato.
/// </para>
/// <para>
/// 🔴 Non aggiungere qui un servizio solo perché «di solito c'è». Il criterio è: <i>la sua assenza è
/// un guasto da riparare subito, oppure una condizione normale?</i> Ollama, embedding e reranker sono
/// locali ma **L2**: pesanti, non in ogni gate, e la loro assenza è normale su una macchina che non ha
/// avviato il profilo <c>ai</c>. BGG e Wikidata sono **L3**. I modelli a pagamento sono **L4**.
/// </para>
/// </remarks>
internal static class L1Services
{
    /// <summary>
    /// Un servizio L1, col nome che compare nel compose e il modo di averlo.
    /// </summary>
    internal sealed record L1Service(string Name, string HowToStart);

    internal static readonly IReadOnlyList<L1Service> All =
    [
        new("postgres", "cd infra && make dev-core   (oppure i Testcontainers della fixture condivisa)"),
        new("redis", "cd infra && make dev-core   (oppure i Testcontainers della fixture condivisa)"),
        new("minio", "cd infra && make dev   (il profilo `storage` e' incluso; vedi #4018)"),
        new("mailpit", "cd infra && make dev   (profilo `automation`; UI su http://localhost:8025)"),
    ];

    /// <summary>
    /// Da chiamare quando un prerequisito L1 non è soddisfatto. <b>Fallisce</b>, non salta, e il
    /// messaggio distingue «ambiente non pronto» da «asserzione violata» — che è la richiesta R3.2
    /// della spec: chi legge un rosso deve sapere in un colpo d'occhio se deve guardare il codice o
    /// avviare un container.
    /// </summary>
    /// <param name="service">
    /// Il nome del servizio, come in <see cref="All"/>. Se non è un L1 conosciuto il metodo lo dice,
    /// perché usare questa via per un L2/L3 significherebbe trasformare un'assenza normale in un
    /// rosso quotidiano.
    /// </param>
    /// <param name="observed">
    /// Cosa è stato osservato. Deve essere l'errore <i>concreto</i>, non la condizione generica: il
    /// motivo «richiede Docker» con Docker attivo è il controesempio da cui nasce tutta questa
    /// famiglia di lavoro.
    /// </param>
    internal static void FailBecauseUnavailable(string service, string observed)
    {
        var known = All.FirstOrDefault(s => string.Equals(s.Name, service, StringComparison.OrdinalIgnoreCase));

        var howTo = known is null
            ? $"'{service}' non è fra i servizi L1 dichiarati ({string.Join(", ", All.Select(s => s.Name))}). " +
              "Se è un L2/L3, la sua assenza è normale e va trattata con Assert.Skip(\"PREVISTO: …\"), " +
              "non con un fallimento."
            : known.HowToStart;

        Assert.Fail(
            $"AMBIENTE NON PRONTO — il servizio L1 '{service}' non è utilizzabile. " +
            $"Questo NON è un'asserzione violata: il codice sotto test non è stato eseguito.{Environment.NewLine}" +
            $"  osservato: {observed}{Environment.NewLine}" +
            $"  come averlo: {howTo}{Environment.NewLine}" +
            $"  per eseguire solo ciò che non richiede Docker: cd infra && make test-no-docker{Environment.NewLine}" +
            "Perché un fallimento e non un salto: un servizio L1 deve esserci, quindi la sua assenza è " +
            "un guasto da riparare. Un salto la nasconderebbe — è così che l'immagine MinIO sparita è " +
            "passata inosservata per un mese (#3978).");
    }
}
