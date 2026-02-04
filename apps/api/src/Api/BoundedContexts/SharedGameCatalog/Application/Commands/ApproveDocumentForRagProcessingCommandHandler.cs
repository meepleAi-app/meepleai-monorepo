using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Handler for approving a document for RAG processing.
/// </summary>
internal sealed class ApproveDocumentForRagProcessingCommandHandler : ICommandHandler<ApproveDocumentForRagProcessingCommand, Unit>
{
    private readonly ISharedGameDocumentRepository _documentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMediator _mediator;
    private readonly ILogger<ApproveDocumentForRagProcessingCommandHandler> _logger;

    public ApproveDocumentForRagProcessingCommandHandler(
        ISharedGameDocumentRepository documentRepository,
        IUnitOfWork unitOfWork,
        IMediator mediator,
        ILogger<ApproveDocumentForRagProcessingCommandHandler> logger)
    {
        _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Unit> Handle(ApproveDocumentForRagProcessingCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation(
            "Approving document for RAG processing: {DocumentId}",
            command.DocumentId);

        // Load document
        var document = await _documentRepository.GetByIdAsync(command.DocumentId, cancellationToken).ConfigureAwait(false);
        if (document is null)
        {
            throw new NotFoundException("SharedGameDocument", command.DocumentId.ToString());
        }

        // Call domain method to approve - ConflictException for business rule violations
        try
        {
            document.Approve(command.ApprovedBy, command.Notes);
        }
        catch (InvalidOperationException ex)
        {
            throw new ConflictException($"Document approval failed: {ex.Message}");
        }

        // Save changes
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Publish domain event
        await _mediator.Publish(
            new DocumentApprovedForRagEvent(
                command.DocumentId,
                document.SharedGameId,
                document.PdfDocumentId,
                command.ApprovedBy,
                document.ApprovedAt ?? DateTime.UtcNow),
            cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Document approved successfully: {DocumentId} by {ApprovedBy}",
            command.DocumentId, command.ApprovedBy);

        return Unit.Value;
    }
}
