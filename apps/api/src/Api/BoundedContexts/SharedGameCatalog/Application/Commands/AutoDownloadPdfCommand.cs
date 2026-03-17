using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Command to auto-download a PDF from a URL for a shared game.
/// Downloads via SsrfSafeHttpClient, stores via IBlobStorageService,
/// creates a PdfDocument entity, and publishes domain events.
/// </summary>
/// <param name="SharedGameId">The ID of the shared game to associate the PDF with.</param>
/// <param name="PdfUrl">The HTTPS URL to download the PDF from.</param>
/// <param name="RequestedByUserId">The ID of the user who initiated the download.</param>
internal sealed record AutoDownloadPdfCommand(
    Guid SharedGameId, string PdfUrl, Guid RequestedByUserId) : ICommand<Unit>;
