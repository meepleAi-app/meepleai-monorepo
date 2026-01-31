using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Handler for removing a document from a shared game.
/// </summary>
internal sealed class RemoveDocumentFromSharedGameCommandHandler : ICommandHandler<RemoveDocumentFromSharedGameCommand>
{
    private readonly ISharedGameDocumentRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMediator _mediator;
    private readonly ILogger<RemoveDocumentFromSharedGameCommandHandler> _logger;

    public RemoveDocumentFromSharedGameCommandHandler(
        ISharedGameDocumentRepository repository,
        IUnitOfWork unitOfWork,
        IMediator mediator,
        ILogger<RemoveDocumentFromSharedGameCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(RemoveDocumentFromSharedGameCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation(
            "Removing document {DocumentId} from shared game {SharedGameId}",
            command.DocumentId, command.SharedGameId);

        // Fetch the document
        var document = await _repository.GetByIdAsync(command.DocumentId, cancellationToken).ConfigureAwait(false);
        if (document is null)
        {
            throw new InvalidOperationException($"Document with ID {command.DocumentId} not found");
        }

        // Verify it belongs to the specified game
        if (document.SharedGameId != command.SharedGameId)
        {
            throw new InvalidOperationException(
                $"Document {command.DocumentId} does not belong to game {command.SharedGameId}");
        }

        // Cannot delete active version without setting another as active first
        if (document.IsActive)
        {
            var otherVersions = await _repository.GetBySharedGameIdAndTypeAsync(
                command.SharedGameId,
                document.DocumentType,
                cancellationToken).ConfigureAwait(false);

            if (otherVersions.Count > 1)
            {
                throw new InvalidOperationException(
                    "Cannot delete active version. Set another version as active first.");
            }
        }

        // Remove the document
        _repository.Remove(document);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Publish domain event
        await _mediator.Publish(
            new SharedGameDocumentRemovedEvent(
                command.SharedGameId,
                command.DocumentId,
                document.DocumentType),
            cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Document {DocumentId} removed successfully from game {SharedGameId}",
            command.DocumentId, command.SharedGameId);
    }
}
