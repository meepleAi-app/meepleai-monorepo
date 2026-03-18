using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetRecentlyProcessedDocuments;

/// <summary>
/// Query to retrieve recently processed PDF documents linked to SharedGames.
/// Cross-BC read query joining DocumentProcessing and SharedGameCatalog data.
/// </summary>
public sealed record GetRecentlyProcessedDocumentsQuery(int Limit = 10)
    : IRequest<List<RecentlyProcessedDocumentDto>>;
