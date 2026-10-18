using Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Application.Commands.GameWizard;

/// <summary>
/// Handler for LaunchAdminPdfProcessingCommand.
/// Sets Priority=Admin on the PDF document and enqueues it; the queue worker processes it.
/// </summary>
internal sealed class LaunchAdminPdfProcessingCommandHandler
    : ICommandHandler<LaunchAdminPdfProcessingCommand, LaunchProcessingResult>
{
    /// <summary>The wizard's job priority, above any upload (normal 10, urgent 30).</summary>
    internal const int WizardPriority = 100;

    private readonly MeepleAiDbContext _dbContext;
    private readonly IMediator _mediator;
    private readonly ILogger<LaunchAdminPdfProcessingCommandHandler> _logger;

    public LaunchAdminPdfProcessingCommandHandler(
        MeepleAiDbContext dbContext,
        IMediator mediator,
        ILogger<LaunchAdminPdfProcessingCommandHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<LaunchProcessingResult> Handle(
        LaunchAdminPdfProcessingCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation(
            "Admin wizard: Launching processing for PDF {PdfId} (Game {GameId}) by user {UserId}",
            command.PdfDocumentId, command.GameId, command.LaunchedByUserId);

        // Resolve GameId: the wizard passes SharedGameId, but PdfDocuments reference games.Id
        var resolvedGameId = command.GameId;
        var gameExistsDirectly = await _dbContext.SharedGames
            .AnyAsync(g => g.Id == command.GameId, cancellationToken)
            .ConfigureAwait(false);

        if (!gameExistsDirectly)
        {
            // The wizard uses SharedGameId in the URL - resolve to the actual Game.Id
            var game = await _dbContext.SharedGames
                .FirstOrDefaultAsync(g => g.Id == command.GameId, cancellationToken)
                .ConfigureAwait(false);

            if (game is not null)
            {
                resolvedGameId = game.Id;
                _logger.LogInformation(
                    "Admin wizard: Resolved SharedGameId {SharedGameId} to GameId {GameId}",
                    command.GameId, resolvedGameId);
            }
        }

        // Find the PDF document (verify it belongs to the resolved game)
        var pdfEntity = await _dbContext.PdfDocuments
            // #3866: tracked on purpose — the mutation below only reaches the DB on a tracked entity (production defaults to NoTracking, PERF-06).
            .AsTracking()
            .FirstOrDefaultAsync(
                p => p.Id == command.PdfDocumentId && p.SharedGameId == resolvedGameId,
                cancellationToken)
            .ConfigureAwait(false);

        if (pdfEntity is null)
        {
            throw new NotFoundException("PdfDocument", command.PdfDocumentId.ToString());
        }

        // Set admin priority
        pdfEntity.ProcessingPriority = "Admin";

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Admin wizard: Set Priority=Admin for PDF {PdfId}",
            command.PdfDocumentId);

        // #4443 R3: the queue is the only owner of an admin upload's processing. This used to enqueue
        // AND run extraction + indexing inside the request, so the document had two pipelines: the
        // request's and the worker's, which picked up the job a few seconds later.
        try
        {
            var jobId = await _mediator.Send(
                new EnqueuePdfCommand(command.PdfDocumentId, command.LaunchedByUserId, WizardPriority),
                cancellationToken).ConfigureAwait(false);
            _logger.LogInformation(
                "Admin wizard: Enqueued PDF {PdfId} as job {JobId} with priority {Priority}",
                command.PdfDocumentId, jobId, WizardPriority);
        }
        catch (ConflictException ex)
        {
            // Already has an active job: the queue will process it, which is what was asked. A full
            // queue is a ProcessingQueueFullException (R8), not a ConflictException, so it propagates
            // as a 429 instead of being mistaken for this case.
            _logger.LogInformation(ex,
                "Admin wizard: PDF {PdfId} already has an active job",
                command.PdfDocumentId);
        }

        return new LaunchProcessingResult
        {
            PdfDocumentId = command.PdfDocumentId,
            GameId = command.GameId,
            Status = "queued",
            Priority = "Admin"
        };
    }
}
