using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Command to delete a PDF document and its associated vectors.
/// </summary>
/// <param name="PdfId">The PDF document identifier.</param>
/// <param name="CallerGameId">
/// Optional: the game on whose behalf this delete is issued. Catalog dedup (Task 2 of
/// #2943) means a PDF record's <c>SharedGameId</c> stays pinned to the original uploader
/// game even after other games link the same PDF via reuse. When a delete targets a
/// specific caller game (e.g. <c>RemoveRagFromSharedGameCommandHandler</c> cleaning up
/// that game's RAG), pass it here so the handler removes the correct game's EntityLink
/// instead of defaulting to the uploader's. Left null for call sites where caller ==
/// uploader's game (user/admin/bulk delete by pdfId with no distinct game context).
/// </param>
internal record DeletePdfCommand(string PdfId, Guid? CallerGameId = null) : ICommand<PdfDeleteResult>;
