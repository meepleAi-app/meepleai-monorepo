namespace Api.BoundedContexts.SessionTracking.Domain.Services;

/// <summary>
/// Guard di autorizzazione per gli endpoint di scrittura di SessionTracking. Issue #3756.
///
/// <para>
/// <b>La regola.</b> Su una sessione puo' scrivere <b>solo</b> chi ne e' l'owner oppure un
/// partecipante registrato, cioe' con <c>UserId</c> valorizzato. I partecipanti guest (senza
/// <c>UserId</c>) non hanno un'identita' autenticata, quindi non passano da qui.
/// </para>
///
/// <para>
/// <b>Perche' un servizio e non il controllo in linea.</b> #3263 aveva applicato il controllo
/// direttamente nei cinque handler di <c>SessionCommandEndpoints</c>, che caricavano gia' la
/// sessione per conto proprio. Gli endpoint coperti da #3756 sono dieci e la maggior parte
/// (timer, mazzo) <b>non carica affatto la sessione</b>: replicare la regola dieci volte
/// significherebbe dieci occasioni di sbagliarla e dieci punti da riverificare a ogni modifica.
/// Qui la regola sta in un posto solo, ed e' testabile in un posto solo.
/// </para>
///
/// <para>
/// 🔴 Va chiamato <b>prima</b> di qualunque mutazione o effetto osservabile — inclusi i broadcast
/// SSE. I timer, in particolare, pubblicano eventi <c>EventVisibility.Public</c> a tutti i
/// partecipanti della sessione: un guard applicato dopo il <c>PublishAsync</c> lascerebbe passare
/// esattamente l'abuso che deve impedire.
/// </para>
/// </summary>
public interface ISessionAccessGuard
{
    /// <summary>
    /// Verifica che <paramref name="requestedBy"/> sia owner o partecipante registrato della
    /// sessione indicata.
    /// </summary>
    /// <exception cref="Api.Middleware.Exceptions.NotFoundException">La sessione non esiste.</exception>
    /// <exception cref="Api.Middleware.Exceptions.ForbiddenException">
    /// Il chiamante non e' ne' owner ne' partecipante registrato.
    /// </exception>
    Task EnsureOwnerOrParticipantAsync(Guid sessionId, Guid requestedBy, CancellationToken cancellationToken);
}
