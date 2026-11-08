using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries;

/// <param name="GameId">Un gioco del catalogo o un gioco privato.</param>
/// <param name="RequestingUserId">
/// Chi chiede (#4348). Serve per un gioco privato, i cui PDF vede solo il proprietario; senza, un
/// gioco privato risponde come uno inesistente.
/// </param>
internal record GetPdfDocumentsByGameQuery(
    Guid GameId,
    Guid? RequestingUserId = null
) : IQuery<IReadOnlyList<PdfDocumentDto>>;
