using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>Indicizza un PDF SOLO se è inattivo e ne ha bisogno (Failed, oppure Ready senza vettori). Spec azure-local §4.3.</summary>
internal sealed record IndexPdfIfIdleCommand(Guid PdfDocumentId) : ICommand<IndexPdfIfIdleResult>;

internal enum IndexPdfIfIdleOutcome
{
    NotFound,
    PipelineBusy,
    AlreadyIndexed,
    Indexed,
    Failed,
}

internal sealed record IndexPdfIfIdleResult(IndexPdfIfIdleOutcome Outcome, IndexingResultDto? Indexing);
