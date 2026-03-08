using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.MechanicExtractor;

/// <summary>
/// Query to load an existing mechanic draft for a game+PDF pair.
/// </summary>
internal record GetMechanicDraftQuery(
    Guid SharedGameId,
    Guid PdfDocumentId)
    : IQuery<MechanicDraftDto?>;
