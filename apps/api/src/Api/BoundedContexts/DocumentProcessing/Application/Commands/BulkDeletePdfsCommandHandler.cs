using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Handler for BulkDeletePdfsCommand.
/// Delegates each deletion to DeletePdfCommand to reuse existing cascade logic.
/// PDF Storage Management Hub: Phase 4.
/// </summary>
internal sealed class BulkDeletePdfsCommandHandler : ICommandHandler<BulkDeletePdfsCommand, BulkDeleteResult>
{
    private readonly IMediator _mediator;

    public BulkDeletePdfsCommandHandler(IMediator mediator)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    public async Task<BulkDeleteResult> Handle(BulkDeletePdfsCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var results = new List<BulkDeleteItemResult>();

        foreach (var pdfId in command.PdfIds)
        {
            try
            {
                await _mediator.Send(new DeletePdfCommand(pdfId.ToString()), cancellationToken)
                    .ConfigureAwait(false);
                results.Add(new BulkDeleteItemResult(pdfId, true, null));
            }
            catch (Exception ex)
            {
                results.Add(new BulkDeleteItemResult(pdfId, false, ex.Message));
            }
        }

        var successCount = results.Count(r => r.Success);

        return new BulkDeleteResult(
            TotalRequested: command.PdfIds.Count,
            SuccessCount: successCount,
            FailedCount: command.PdfIds.Count - successCount,
            Items: results
        );
    }
}
