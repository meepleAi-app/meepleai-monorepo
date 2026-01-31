using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Handler for creating share requests from user library to shared catalog.
/// Determines contribution type and attaches documents.
/// </summary>
internal sealed class CreateShareRequestCommandHandler : ICommandHandler<CreateShareRequestCommand, CreateShareRequestResponse>
{
    private readonly IShareRequestRepository _shareRequestRepository;
    private readonly ISharedGameRepository _sharedGameRepository;
    private readonly IUserLibraryRepository _userLibraryRepository;
    private readonly IPdfDocumentRepository _documentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateShareRequestCommandHandler> _logger;

    public CreateShareRequestCommandHandler(
        IShareRequestRepository shareRequestRepository,
        ISharedGameRepository sharedGameRepository,
        IUserLibraryRepository userLibraryRepository,
        IPdfDocumentRepository documentRepository,
        IUnitOfWork unitOfWork,
        ILogger<CreateShareRequestCommandHandler> logger)
    {
        _shareRequestRepository = shareRequestRepository ?? throw new ArgumentNullException(nameof(shareRequestRepository));
        _sharedGameRepository = sharedGameRepository ?? throw new ArgumentNullException(nameof(sharedGameRepository));
        _userLibraryRepository = userLibraryRepository ?? throw new ArgumentNullException(nameof(userLibraryRepository));
        _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<CreateShareRequestResponse> Handle(
        CreateShareRequestCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation(
            "Creating share request for user {UserId}, source game {SourceGameId}",
            command.UserId, command.SourceGameId);

        // 1. Get source game from user library to find the SharedGame reference
        var libraryEntry = await _userLibraryRepository.GetByUserAndGameAsync(
            command.UserId,
            command.SourceGameId,
            cancellationToken).ConfigureAwait(false);

        if (libraryEntry == null)
        {
            throw new InvalidOperationException($"Game {command.SourceGameId} not found in user's library");
        }

        // 2. Get the shared game to check its BggId and determine contribution type
        var sourceSharedGame = await _sharedGameRepository.GetByIdAsync(
            command.SourceGameId,
            cancellationToken).ConfigureAwait(false);

        if (sourceSharedGame == null)
        {
            throw new InvalidOperationException($"SharedGame {command.SourceGameId} not found");
        }

        // 3. Determine contribution type by checking if a game with same BggId already exists
        // (for cases where user wants to add content to an existing shared catalog game)
        ContributionType contributionType;
        Guid? targetSharedGameId = null;

        if (sourceSharedGame.BggId.HasValue)
        {
            // Check if there's already a published game with this BggId in the shared catalog
            var existingPublishedGame = await _sharedGameRepository.GetByBggIdAsync(
                sourceSharedGame.BggId.Value,
                cancellationToken).ConfigureAwait(false);

            if (existingPublishedGame != null && existingPublishedGame.Id != sourceSharedGame.Id)
            {
                // Contributing additional content to an existing game
                contributionType = ContributionType.AdditionalContent;
                targetSharedGameId = existingPublishedGame.Id;

                _logger.LogInformation(
                    "Share request will contribute additional content to existing game {TargetGameId}",
                    targetSharedGameId);
            }
            else
            {
                // New game contribution
                contributionType = ContributionType.NewGame;
            }
        }
        else
        {
            // No BggId means it's definitely a new game
            contributionType = ContributionType.NewGame;
        }

        // 4. Create share request
        var shareRequest = ShareRequest.Create(
            command.UserId,
            command.SourceGameId,
            contributionType,
            command.Notes,
            targetSharedGameId);

        // 5. Attach documents if provided
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
                    "Attached document {DocumentId} ({FileName}) to share request",
                    doc.Id, doc.FileName.Value);
            }
        }

        // 6. Persist
        await _shareRequestRepository.AddAsync(shareRequest, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Share request {ShareRequestId} created successfully. Type: {ContributionType}, Status: {Status}",
            shareRequest.Id, contributionType, shareRequest.Status);

        // Domain event ShareRequestCreatedEvent is dispatched automatically via AggregateRoot

        return new CreateShareRequestResponse(
            shareRequest.Id,
            shareRequest.Status,
            contributionType,
            shareRequest.CreatedAt);
    }
}
