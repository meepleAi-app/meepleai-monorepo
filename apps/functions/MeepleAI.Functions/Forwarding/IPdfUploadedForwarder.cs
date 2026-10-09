using MeepleAI.Functions.Contracts;

namespace MeepleAI.Functions.Forwarding;

public enum ForwardOutcome
{
    /// <summary>L'API ha risposto in modo definitivo (2xx, 400, 404 not_found, 422): il messaggio si completa.</summary>
    Settled,
    /// <summary>Errore di rete, 5xx o 404 senza esito: il messaggio si abbandona e Service Bus lo riconsegna fino a MaxDeliveryCount.</summary>
    TransientFailure,
}

public interface IPdfUploadedForwarder
{
    Task<ForwardOutcome> ForwardAsync(PdfUploadedEventV1 integrationEvent, CancellationToken cancellationToken);
}
