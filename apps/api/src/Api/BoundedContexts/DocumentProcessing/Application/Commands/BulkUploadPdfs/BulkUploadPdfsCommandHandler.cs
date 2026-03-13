using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands.BulkUploadPdfs;

/// <summary>
/// Handler for BulkUploadPdfsCommand.
/// Iterates over files and delegates each to the existing UploadPdfCommand.
/// Issue #117: Bulk PDF Upload.
/// </summary>
internal sealed class BulkUploadPdfsCommandHandler
    : ICommandHandler<BulkUploadPdfsCommand, BulkUploadPdfsResult>
{
    private readonly IMediator _mediator;
    private readonly ILogger<BulkUploadPdfsCommandHandler> _logger;

    public BulkUploadPdfsCommandHandler(
        IMediator mediator,
        ILogger<BulkUploadPdfsCommandHandler> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<BulkUploadPdfsResult> Handle(
        BulkUploadPdfsCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation(
            "Bulk uploading {Count} PDFs for shared game {SharedGameId} by user {UserId}",
            command.Files.Count, command.SharedGameId, command.UserId);

        var items = new List<BulkUploadItemResult>();
        var successCount = 0;
        var failedCount = 0;

        foreach (var file in command.Files)
        {
            try
            {
                var uploadCommand = new UploadPdfCommand(
                    GameId: command.SharedGameId.ToString(),
                    Metadata: null,
                    PrivateGameId: null,
                    UserId: command.UserId,
                    File: file);

                var result = await _mediator.Send(uploadCommand, cancellationToken).ConfigureAwait(false);

                if (result.Success && result.Document != null)
                {
                    items.Add(new BulkUploadItemResult(file.FileName, true, result.Document.Id, null));
                    successCount++;
                }
                else
                {
                    items.Add(new BulkUploadItemResult(file.FileName, false, null, result.Message ?? "Upload failed"));
                    failedCount++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to upload file {FileName} in bulk operation", file.FileName);
                items.Add(new BulkUploadItemResult(file.FileName, false, null, ex.Message));
                failedCount++;
            }
        }

        _logger.LogInformation(
            "Bulk upload complete: {Success}/{Total} succeeded for shared game {SharedGameId}",
            successCount, command.Files.Count, command.SharedGameId);

        return new BulkUploadPdfsResult(
            command.Files.Count,
            successCount,
            failedCount,
            items);
    }
}
