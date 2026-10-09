using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Le guardie di idempotenza del repo stanno nei handler di coda (<c>ProcessingJob</c>) e nello stato del
/// documento, NON in <see cref="IndexPdfCommand"/>, che ri-indicizza sempre. Questo handler le applica prima di
/// delegare: un messaggio riconsegnato o duplicato non deve far ripartire una pipeline già in corso o conclusa.
/// </summary>
internal sealed class IndexPdfIfIdleCommandHandler : ICommandHandler<IndexPdfIfIdleCommand, IndexPdfIfIdleResult>
{
    private static readonly string[] PipelineStates = ["Pending", "Uploading", "Extracting", "Chunking", "Embedding", "Indexing"];
    private static readonly string[] ActiveJobStatuses = ["Queued", "Processing"];

    private readonly MeepleAiDbContext _db;
    private readonly IMediator _mediator;
    private readonly ILogger<IndexPdfIfIdleCommandHandler> _logger;

    public IndexPdfIfIdleCommandHandler(MeepleAiDbContext db, IMediator mediator, ILogger<IndexPdfIfIdleCommandHandler> logger)
    {
        _db = db;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<IndexPdfIfIdleResult> Handle(IndexPdfIfIdleCommand request, CancellationToken cancellationToken)
    {
        var pdf = await _db.PdfDocuments.AsNoTracking()
            .Where(p => p.Id == request.PdfDocumentId)
            .Select(p => new { p.Id, p.ProcessingState })
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (pdf is null)
        {
            return new IndexPdfIfIdleResult(IndexPdfIfIdleOutcome.NotFound, null);
        }

        var activeJob = await _db.ProcessingJobs.AsNoTracking()
            .AnyAsync(j => j.PdfDocumentId == pdf.Id && ActiveJobStatuses.Contains(j.Status), cancellationToken).ConfigureAwait(false);
        if (activeJob || PipelineStates.Contains(pdf.ProcessingState, StringComparer.Ordinal))
        {
            _logger.LogInformation("PDF {PdfId} pipeline busy (state {State}, activeJob {ActiveJob}): no re-index", pdf.Id, pdf.ProcessingState, activeJob);
            return new IndexPdfIfIdleResult(IndexPdfIfIdleOutcome.PipelineBusy, null);
        }

        var hasVectors = await _db.VectorDocuments.AsNoTracking()
            .AnyAsync(v => v.PdfDocumentId == pdf.Id, cancellationToken).ConfigureAwait(false);
        if (string.Equals(pdf.ProcessingState, "Ready", StringComparison.Ordinal) && hasVectors)
        {
            return new IndexPdfIfIdleResult(IndexPdfIfIdleOutcome.AlreadyIndexed, null);
        }

        _logger.LogInformation("PDF {PdfId} idle (state {State}, vectors {HasVectors}): indexing", pdf.Id, pdf.ProcessingState, hasVectors);
        var indexing = await _mediator.Send(new IndexPdfCommand(pdf.Id.ToString()), cancellationToken).ConfigureAwait(false);
        return new IndexPdfIfIdleResult(indexing.Success ? IndexPdfIfIdleOutcome.Indexed : IndexPdfIfIdleOutcome.Failed, indexing);
    }
}
