using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.Services;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Handler for setting a document version as active.
/// </summary>
internal sealed class SetActiveDocumentVersionCommandHandler : ICommandHandler<SetActiveDocumentVersionCommand>
{
    private readonly ISharedGameDocumentRepository _repository;
    private readonly DocumentVersioningService _versioningService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMediator _mediator;
    private readonly ILogger<SetActiveDocumentVersionCommandHandler> _logger;

    public SetActiveDocumentVersionCommandHandler(
        ISharedGameDocumentRepository repository,
        DocumentVersioningService versioningService,
        IUnitOfWork unitOfWork,
        IMediator mediator,
        ILogger<SetActiveDocumentVersionCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _versioningService = versioningService ?? throw new ArgumentNullException(nameof(versioningService));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(SetActiveDocumentVersionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation(
            "Setting document {DocumentId} as active for shared game {SharedGameId}",
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

        // Already active? No-op
        if (document.IsActive)
        {
            _logger.LogInformation(
                "Document {DocumentId} is already active, no action needed",
                command.DocumentId);
            return;
        }

        // Set as active using versioning service (deactivates others)
        await _versioningService.SetActiveVersionAsync(document, cancellationToken).ConfigureAwait(false);

        // Update repository with race condition protection
        // The unique partial index ix_shared_game_documents_single_active ensures
        // only ONE active document per (shared_game_id, document_type) at DB level
        _repository.Update(document);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // Race condition: another concurrent request already activated a document
            // This is expected behavior - the first request wins, subsequent ones fail safely
            _logger.LogWarning(ex,
                "Race condition detected: Document {DocumentId} activation failed - another document was activated concurrently for game {SharedGameId}",
                command.DocumentId, command.SharedGameId);

            throw new InvalidOperationException(
                $"Another document version was activated concurrently. Please refresh and try again.", ex);
        }

        // Publish domain event
        await _mediator.Publish(
            new SharedGameDocumentActivatedEvent(
                command.SharedGameId,
                command.DocumentId,
                document.DocumentType,
                document.Version),
            cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Document {DocumentId} set as active successfully for game {SharedGameId}",
            command.DocumentId, command.SharedGameId);
    }

    /// <summary>
    /// Checks if a DbUpdateException is caused by a unique constraint violation (PostgreSQL 23505).
    /// Used to detect race conditions when concurrent requests try to activate documents.
    /// </summary>
    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        // PostgreSQL unique constraint violation: 23505
        if (ex.InnerException is Npgsql.PostgresException pgEx)
        {
            return string.Equals(pgEx.SqlState, "23505", StringComparison.Ordinal);
        }

        // Fallback: check message for constraint violation keywords
        return ex.InnerException?.Message?.Contains("unique constraint", StringComparison.OrdinalIgnoreCase) == true
            || ex.InnerException?.Message?.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) == true;
    }
}
