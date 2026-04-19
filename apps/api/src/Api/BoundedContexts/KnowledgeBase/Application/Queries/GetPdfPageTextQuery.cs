using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

public sealed record GetPdfPageTextQuery(
    Guid PdfId,
    int PageNumber,
    Guid UserId
) : IRequest<PdfPageTextDto>;
