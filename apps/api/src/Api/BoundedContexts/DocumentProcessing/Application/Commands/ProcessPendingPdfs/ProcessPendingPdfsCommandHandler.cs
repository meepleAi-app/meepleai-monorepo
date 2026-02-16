using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands.ProcessPendingPdfs;

/// <summary>
/// Handler for ProcessPendingPdfsCommand.
/// Finds all PDFs with status "pending" or "processing" and triggers IndexPdfCommand for each.
/// Admin operation for fixing stuck PDF processing pipeline.
/// </summary>
internal sealed class ProcessPendingPdfsCommandHandler : ICommandHandler<ProcessPendingPdfsCommand, ProcessPendingPdfsResult>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly IMediator _mediator;
    private readonly ILogger<ProcessPendingPdfsCommandHandler> _logger;

    public ProcessPendingPdfsCommandHandler(
        MeepleAiDbContext dbContext,
        IMediator mediator,
        ILogger<ProcessPendingPdfsCommandHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ProcessPendingPdfsResult> Handle(
        ProcessPendingPdfsCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Find all PDFs in pending or processing status
        var pendingPdfs = await _dbContext.PdfDocuments
            .Where(p => p.ProcessingStatus == "pending" || p.ProcessingStatus == "processing")
            .Select(p => new { p.Id, p.FileName, p.ProcessingStatus })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Found {Count} pending PDFs to process",
            pendingPdfs.Count);

        var triggered = 0;
        var failed = 0;
        var pdfIds = new List<string>();

        foreach (var pdf in pendingPdfs)
        {
            try
            {
                // Trigger IndexPdfCommand for each pending PDF
                var indexCommand = new IndexPdfCommand(pdf.Id.ToString());
                await _mediator.Send(indexCommand, cancellationToken).ConfigureAwait(false);

                triggered++;
                pdfIds.Add(pdf.Id.ToString());

                _logger.LogInformation(
                    "Triggered processing for PDF {PdfId} ({FileName})",
                    pdf.Id,
                    pdf.FileName);
            }
            catch (Exception ex)
            {
                failed++;
                _logger.LogError(
                    ex,
                    "Failed to trigger processing for PDF {PdfId} ({FileName})",
                    pdf.Id,
                    pdf.FileName);
            }
        }

        _logger.LogInformation(
            "Batch PDF processing completed: {Triggered} triggered, {Failed} failed out of {Total} total",
            triggered,
            failed,
            pendingPdfs.Count);

        return new ProcessPendingPdfsResult(
            TotalPending: pendingPdfs.Count,
            Triggered: triggered,
            Failed: failed,
            PdfIds: pdfIds
        );
    }
}
