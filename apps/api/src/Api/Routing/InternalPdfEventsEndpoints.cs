using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.IntegrationEvents;
using Api.Filters;
using Api.Observability;
using MediatR;

namespace Api.Routing;

/// <summary>Corpo accettato dal consumer interno: il sottoinsieme del contratto v1 che l'API deve rileggere.</summary>
internal sealed record PdfUploadedEventRequest(Guid EventId, Guid PdfDocumentId, string? ContractVersion);

/// <summary>
/// Lato API del ciclo PdfUploaded (spec azure-local §4.3): la Function consuma il messaggio dal bus e richiama
/// qui. L'indicizzazione passa da <see cref="IndexPdfIfIdleCommand"/>: parte SOLO per un PDF inattivo che ne ha
/// bisogno; pipeline in corso e documenti già indicizzati sono <c>noop</c>.
/// </summary>
internal static class InternalPdfEventsEndpoints
{
    public static void MapInternalPdfEventsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/internal/pdf-events")
            .WithTags("Internal - PDF events")
            .AddEndpointFilter<InternalServiceTokenFilter>();

        group.MapPost("/uploaded", HandleUploaded)
            .WithName("InternalPdfUploaded")
            .WithSummary("Consumer-side hook for the PdfUploaded v1 integration event (service token)");
    }

    private static async Task<IResult> HandleUploaded(PdfUploadedEventRequest body, IMediator mediator, ILogger<Program> logger, CancellationToken ct)
    {
        MeepleAiMetrics.RecordPdfUploadedEvent(MeepleAiMetrics.PdfUploadedEventOutcomes.Received);

        if (!string.Equals(body.ContractVersion, PdfUploadedIntegrationEvent.CurrentContractVersion, StringComparison.Ordinal))
        {
            MeepleAiMetrics.RecordPdfUploadedEvent(MeepleAiMetrics.PdfUploadedEventOutcomes.Rejected);
            return Results.BadRequest(new { outcome = MeepleAiMetrics.PdfUploadedEventOutcomes.Rejected, error = $"unsupported contractVersion '{body.ContractVersion}'" });
        }

        logger.LogInformation("PdfUploaded {EventId} received for {PdfId}", body.EventId, body.PdfDocumentId);
        var result = await mediator.Send(new IndexPdfIfIdleCommand(body.PdfDocumentId), ct).ConfigureAwait(false);

        switch (result.Outcome)
        {
            case IndexPdfIfIdleOutcome.Indexed:
                MeepleAiMetrics.RecordPdfUploadedEvent(MeepleAiMetrics.PdfUploadedEventOutcomes.Indexed);
                return Results.Ok(new { outcome = MeepleAiMetrics.PdfUploadedEventOutcomes.Indexed, chunkCount = result.Indexing?.ChunkCount });
            case IndexPdfIfIdleOutcome.AlreadyIndexed:
                MeepleAiMetrics.RecordPdfUploadedEvent(MeepleAiMetrics.PdfUploadedEventOutcomes.Noop);
                return Results.Ok(new { outcome = MeepleAiMetrics.PdfUploadedEventOutcomes.Noop, reason = "already_indexed" });
            case IndexPdfIfIdleOutcome.PipelineBusy:
                MeepleAiMetrics.RecordPdfUploadedEvent(MeepleAiMetrics.PdfUploadedEventOutcomes.Noop);
                return Results.Accepted(value: new { outcome = MeepleAiMetrics.PdfUploadedEventOutcomes.Noop, reason = "pipeline_busy" });
            case IndexPdfIfIdleOutcome.NotFound:
                MeepleAiMetrics.RecordPdfUploadedEvent(MeepleAiMetrics.PdfUploadedEventOutcomes.NotFound);
                return Results.NotFound(new { outcome = MeepleAiMetrics.PdfUploadedEventOutcomes.NotFound });
            default:
                MeepleAiMetrics.RecordPdfUploadedEvent(MeepleAiMetrics.PdfUploadedEventOutcomes.Failed);
                return Results.UnprocessableEntity(new { outcome = MeepleAiMetrics.PdfUploadedEventOutcomes.Failed, error = result.Indexing?.ErrorMessage });
        }
    }
}
