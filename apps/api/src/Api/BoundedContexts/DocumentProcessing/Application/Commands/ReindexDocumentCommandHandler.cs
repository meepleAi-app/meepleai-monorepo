using Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Handler for ReindexDocumentCommand. Issue #1673 estende il flusso con:
/// 1. Risoluzione versione: <c>command.IndexerVersion ?? pdf.IndexerVersion ?? Current</c>.
/// 2. Conflict guard: se il documento è in pipeline (stati non-terminali), 409 Conflict.
/// 3. Persistenza della versione risolta su <c>pdf.IndexerVersion</c>.
/// 4. Audit via <c>[AuditableAction("DocumentReindex", "Document", Level=2)]</c> sul command.
/// </summary>
internal sealed class ReindexDocumentCommandHandler : ICommandHandler<ReindexDocumentCommand>
{
    // Stati pre-terminali. Reindex bloccato finché non si raggiunge Ready o Failed.
    // Derived from Enum.GetNames so adding a new pre-terminal state to PdfProcessingState
    // (or renaming an existing one) automatically updates this set — no manual sync needed. Issue #1801.
    private static readonly HashSet<string> InFlightStates =
        new(
            Enum.GetNames<PdfProcessingState>()
                .Except(
                    new[]
                    {
                        nameof(PdfProcessingState.Ready),
                        nameof(PdfProcessingState.Failed),
                    },
                    StringComparer.Ordinal),
            StringComparer.Ordinal);

    private readonly MeepleAiDbContext _dbContext;
    private readonly IMediator _mediator;
    private readonly ILogger<ReindexDocumentCommandHandler> _logger;

    public ReindexDocumentCommandHandler(
        MeepleAiDbContext dbContext,
        IMediator mediator,
        ILogger<ReindexDocumentCommandHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(ReindexDocumentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var pdf = await _dbContext.PdfDocuments
            .FirstOrDefaultAsync(p => p.Id == command.PdfId, cancellationToken)
            .ConfigureAwait(false);

        if (pdf is null)
        {
            throw new NotFoundException("PdfDocument", command.PdfId.ToString());
        }

        // Conflict guard: rifiuta il reindex se la pipeline è in-flight.
        if (InFlightStates.Contains(pdf.ProcessingState))
        {
            throw new ConflictException(
                $"Document {command.PdfId} is currently being processed (state={pdf.ProcessingState}); cannot reindex until it reaches Ready or Failed.");
        }

        // Risoluzione versione: explicit → stored → current.
        var resolvedVersion = command.IndexerVersion
            ?? pdf.IndexerVersion
            ?? IndexerVersionRegistry.Current.Version;

        // Cancella chunks associati.
        var chunks = await _dbContext.TextChunks
            .Where(tc => tc.PdfDocumentId == command.PdfId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (chunks.Count > 0)
        {
            _dbContext.TextChunks.RemoveRange(chunks);
        }

        // Reset state + scrive la versione risolta.
        pdf.ProcessingState = nameof(PdfProcessingState.Pending);
        pdf.ProcessedAt = null;
        pdf.ProcessingError = null;
        pdf.RetryCount = 0;
        pdf.ErrorCategory = null;
        pdf.FailedAtState = null;
        pdf.IndexerVersion = resolvedVersion;

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Enqueue Quartz.
        try
        {
            var userId = pdf.UploadedByUserId;
            await _mediator.Send(
                new EnqueuePdfCommand(command.PdfId, userId, Priority: 0),
                cancellationToken).ConfigureAwait(false);
            _logger.LogInformation(
                "Reindexed PDF {PdfId} with version {IndexerVersion} enqueued for processing",
                command.PdfId, resolvedVersion);
        }
#pragma warning disable CA1031 // Best-effort enqueue
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not enqueue reindexed PDF {PdfId} (may already be queued)", command.PdfId);
        }
#pragma warning restore CA1031
    }
}
