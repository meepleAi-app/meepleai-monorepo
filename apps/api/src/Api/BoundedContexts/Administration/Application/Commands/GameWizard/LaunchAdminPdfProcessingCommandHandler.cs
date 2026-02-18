using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.Commands.ProcessPendingPdfs;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Application.Commands.GameWizard;

/// <summary>
/// Handler for LaunchAdminPdfProcessingCommand.
/// Sets Priority=Admin on the PDF document and triggers the processing pipeline.
/// </summary>
internal sealed class LaunchAdminPdfProcessingCommandHandler
    : ICommandHandler<LaunchAdminPdfProcessingCommand, LaunchProcessingResult>
{
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

        // Find the PDF document (also verify it belongs to the specified game)
        var pdfEntity = await _dbContext.PdfDocuments
            .FirstOrDefaultAsync(
                p => p.Id == command.PdfDocumentId && p.GameId == command.GameId,
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

        // Trigger processing pipeline in background
        try
        {
            // Step 1: Extract text
            var extractCommand = new ExtractPdfTextCommand(command.PdfDocumentId);
            await _mediator.Send(extractCommand, cancellationToken).ConfigureAwait(false);

            // Step 2: Index PDF (chunking + embedding + indexing)
            var indexCommand = new IndexPdfCommand(command.PdfDocumentId.ToString());
            await _mediator.Send(indexCommand, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Admin wizard: Processing pipeline launched for PDF {PdfId}",
                command.PdfDocumentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Admin wizard: Failed to launch processing for PDF {PdfId}",
                command.PdfDocumentId);
            throw;
        }

        return new LaunchProcessingResult
        {
            PdfDocumentId = command.PdfDocumentId,
            GameId = command.GameId,
            Status = "processing",
            Priority = "Admin"
        };
    }
}
