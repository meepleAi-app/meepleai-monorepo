using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.BoundedContexts.UserLibrary.Application.Commands.PrivateGames;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.UserLibrary.Application.Handlers.PrivateGames;

/// <summary>
/// Handler for proposing private games to the shared catalog.
/// Issue #3665: Phase 4 - Proposal System.
/// </summary>
internal sealed class ProposePrivateGameCommandHandler : ICommandHandler<ProposePrivateGameCommand, CreateShareRequestResponse>
{
    private readonly IPrivateGameRepository _privateGameRepository;
    private readonly IShareRequestRepository _shareRequestRepository;
    private readonly IPdfDocumentRepository _documentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ProposePrivateGameCommandHandler> _logger;

    public ProposePrivateGameCommandHandler(
        IPrivateGameRepository privateGameRepository,
        IShareRequestRepository shareRequestRepository,
        IPdfDocumentRepository documentRepository,
        IUnitOfWork unitOfWork,
        ILogger<ProposePrivateGameCommandHandler> logger)
    {
        _privateGameRepository = privateGameRepository ?? throw new ArgumentNullException(nameof(privateGameRepository));
        _shareRequestRepository = shareRequestRepository ?? throw new ArgumentNullException(nameof(shareRequestRepository));
        _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<CreateShareRequestResponse> Handle(
        ProposePrivateGameCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation(
            "User {UserId} proposing private game {PrivateGameId} to catalog",
            command.UserId, command.PrivateGameId);

        // 1. Verify private game exists and belongs to user
        var privateGame = await _privateGameRepository.GetByIdAsync(
            command.PrivateGameId,
            cancellationToken).ConfigureAwait(false);

        if (privateGame == null)
        {
            throw new NotFoundException($"Private game {command.PrivateGameId} not found");
        }

        if (privateGame.OwnerId != command.UserId)
        {
            throw new UnauthorizedAccessException(
                $"User {command.UserId} does not own private game {command.PrivateGameId}");
        }

        // 2. Check for existing pending proposals for this private game
        var existingProposal = await _shareRequestRepository.GetPendingProposalForPrivateGameAsync(
            command.PrivateGameId,
            cancellationToken).ConfigureAwait(false);

        if (existingProposal != null)
        {
            throw new ConflictException(
                $"A pending proposal already exists for this private game (ShareRequest ID: {existingProposal.Id})");
        }

        // 3. Create share request using CreateGameProposal factory
        var shareRequest = ShareRequest.CreateGameProposal(
            command.UserId,
            command.PrivateGameId,
            command.Notes);

        // 4. Attach documents if provided
        if (command.AttachedDocumentIds != null && command.AttachedDocumentIds.Count > 0)
        {
            var documents = await _documentRepository.GetByIdsAsync(
                command.AttachedDocumentIds,
                cancellationToken).ConfigureAwait(false);

            foreach (var doc in documents)
            {
                shareRequest.AttachDocument(
                    doc.Id,
                    doc.FileName.Value,
                    doc.ContentType,
                    doc.FileSize.Bytes);

                _logger.LogDebug(
                    "Attached document {DocumentId} ({FileName}) to game proposal",
                    doc.Id, doc.FileName.Value);
            }
        }

        // 5. Persist
        await _shareRequestRepository.AddAsync(shareRequest, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Game proposal {ShareRequestId} created successfully for private game {PrivateGameId}",
            shareRequest.Id, command.PrivateGameId);

        // Domain event ShareRequestCreatedEvent is dispatched automatically

        return new CreateShareRequestResponse(
            shareRequest.Id,
            shareRequest.Status,
            ContributionType.NewGameProposal,
            shareRequest.CreatedAt);
    }
}
