using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.DocumentProcessing.Application.IntegrationEvents;

/// <summary>
/// Contratto v1 (spec azure-local §4.3): un PDF è stato ricevuto dall'API e la pipeline è partita (transizione
/// Pending → Uploading; a quel punto il blob è già stato scritto e <c>FilePath</c> è valorizzato). Chiavi sul
/// filo congelate in <c>tests/fixtures/contracts/pdf-uploaded.v1.json</c>; cambiarle significa una v2.
/// </summary>
internal sealed class PdfUploadedIntegrationEvent : IntegrationEventBase, IVersionedContract
{
    public const string CurrentContractVersion = "v1";

    public string ContractVersion => CurrentContractVersion;
    public Guid PdfDocumentId { get; }
    public Guid GameId { get; }
    public Guid? SharedGameId { get; }
    public string FilePath { get; }

    public PdfUploadedIntegrationEvent(Guid pdfDocumentId, Guid gameId, Guid? sharedGameId, string filePath)
        : base("DocumentProcessing")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        PdfDocumentId = pdfDocumentId;
        GameId = gameId;
        SharedGameId = sharedGameId;
        FilePath = filePath;
    }
}
