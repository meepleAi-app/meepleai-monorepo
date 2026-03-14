using Api.SharedKernel.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Stub handler for AutoDownloadPdfCommand.
/// Real implementation (pending DocumentProcessing BC integration) will:
///   1. Download PDF via SsrfSafeHttpClient
///   2. Store blob via IBlobStorageService.StoreAsync
///   3. Create PdfDocument entity via repository
///   4. Publish PdfReadyForProcessingEvent
///   5. Update SharedGame status
/// </summary>
internal sealed class AutoDownloadPdfCommandHandler : ICommandHandler<AutoDownloadPdfCommand, Unit>
{
    private readonly ILogger<AutoDownloadPdfCommandHandler> _logger;

    public AutoDownloadPdfCommandHandler(ILogger<AutoDownloadPdfCommandHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<Unit> Handle(AutoDownloadPdfCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "AutoDownloadPdf requested for SharedGame {SharedGameId} from URL {PdfUrl} by user {UserId}. " +
            "Stub handler — real implementation pending DocumentProcessing BC integration.",
            request.SharedGameId,
            request.PdfUrl,
            request.RequestedByUserId);

        return Task.FromResult(Unit.Value);
    }
}
