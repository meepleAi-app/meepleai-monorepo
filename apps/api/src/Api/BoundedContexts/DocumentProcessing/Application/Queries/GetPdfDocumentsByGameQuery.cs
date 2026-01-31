using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries;

internal record GetPdfDocumentsByGameQuery(
    Guid GameId
) : IQuery<IReadOnlyList<PdfDocumentDto>>;
