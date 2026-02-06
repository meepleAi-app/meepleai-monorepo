using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.ApproveGameProposal;

/// <summary>
/// Handler for approving NewGameProposal share requests with enhanced admin actions.
/// Issue #3667: Phase 6 - Admin Review Enhancements.
/// </summary>
internal sealed class ApproveGameProposalCommandHandler : ICommandHandler<ApproveGameProposalCommand, ApproveShareRequestResponse>
{
    private readonly IShareRequestRepository _shareRequestRepository;
    private readonly IPrivateGameRepository _privateGameRepository;
    private readonly ISharedGameRepository _sharedGameRepository;
    private readonly IShareRequestDocumentService _documentService;
    private readonly MeepleAiDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ApproveGameProposalCommandHandler> _logger;

    public ApproveGameProposalCommandHandler(
        IShareRequestRepository shareRequestRepository,
        IPrivateGameRepository privateGameRepository,
        ISharedGameRepository sharedGameRepository,
        IShareRequestDocumentService documentService,
        MeepleAiDbContext context,
        IUnitOfWork unitOfWork,
        ILogger<ApproveGameProposalCommandHandler> logger)
    {
        _shareRequestRepository = shareRequestRepository ?? throw new ArgumentNullException(nameof(shareRequestRepository));
        _privateGameRepository = privateGameRepository ?? throw new ArgumentNullException(nameof(privateGameRepository));
        _sharedGameRepository = sharedGameRepository ?? throw new ArgumentNullException(nameof(sharedGameRepository));
        _documentService = documentService ?? throw new ArgumentNullException(nameof(documentService));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ApproveShareRequestResponse> Handle(
        ApproveGameProposalCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation(
            "Approving game proposal {ShareRequestId} with action {Action} by admin {AdminId}",
            command.ShareRequestId, command.ApprovalAction, command.AdminId);

        // 1. Get share request for update (with tracking)
        var shareRequest = await _shareRequestRepository.GetByIdForUpdateAsync(
            command.ShareRequestId,
            cancellationToken).ConfigureAwait(false);

        if (shareRequest == null)
        {
            throw new NotFoundException("ShareRequest", command.ShareRequestId.ToString());
        }

        // 2. Validate request type
        if (shareRequest.ContributionType != ContributionType.NewGameProposal)
        {
            throw new ConflictException(
                $"ShareRequest {shareRequest.Id} is not a NewGameProposal (type: {shareRequest.ContributionType})");
        }

        // 3. Get the private game from the UserLibraryEntry
        // For NewGameProposal, SourceGameId references UserLibraryEntry.Id
        // Note: Using DbContext directly for cross-context access (UserLibrary domain entity doesn't expose PrivateGameId)
        var libraryEntry = await _context.UserLibraryEntries
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == shareRequest.SourceGameId, cancellationToken)
            .ConfigureAwait(false);

        if (libraryEntry == null)
        {
            throw new NotFoundException("UserLibraryEntry", shareRequest.SourceGameId.ToString());
        }

        if (!libraryEntry.PrivateGameId.HasValue || libraryEntry.PrivateGameId.Value == Guid.Empty)
        {
            throw new ConflictException(
                $"UserLibraryEntry {libraryEntry.Id} has no valid PrivateGameId");
        }

        var privateGame = await _privateGameRepository.GetByIdAsync(
            libraryEntry.PrivateGameId.Value,
            cancellationToken).ConfigureAwait(false);

        if (privateGame == null)
        {
            throw new NotFoundException("PrivateGame", libraryEntry.PrivateGameId.Value.ToString());
        }

        // 4. Execute approval action based on type
        Guid targetSharedGameId = command.ApprovalAction switch
        {
            ProposalApprovalAction.ApproveAsNew =>
                await ApproveAsNewGameAsync(privateGame, shareRequest.UserId, cancellationToken).ConfigureAwait(false),

            ProposalApprovalAction.MergeKnowledgeBase =>
                await MergeKnowledgeBaseAsync(command.TargetSharedGameId!.Value, cancellationToken).ConfigureAwait(false),

            ProposalApprovalAction.ApproveAsVariant =>
                await ApproveAsVariantAsync(privateGame, command.TargetSharedGameId!.Value, shareRequest.UserId, cancellationToken).ConfigureAwait(false),

            _ => throw new InvalidOperationException($"Unknown approval action: {command.ApprovalAction}")
        };

        // 5. Copy documents to shared game (if any attached)
        var documentIds = shareRequest.AttachedDocuments.Select(d => d.DocumentId).ToList();
        if (documentIds.Count > 0)
        {
            await _documentService.CopyDocumentsToSharedGame(
                documentIds,
                targetSharedGameId,
                shareRequest.UserId,
                cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Copied {Count} documents from ShareRequest {ShareRequestId} to SharedGame {SharedGameId}",
                documentIds.Count, shareRequest.Id, targetSharedGameId);
        }

        // 6. Approve the share request (domain validates state and raises events)
        shareRequest.Approve(
            command.AdminId,
            targetSharedGameId,
            command.AdminNotes);

        // 7. Update repository
        _shareRequestRepository.Update(shareRequest);

        // 8. Persist all changes
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Game proposal {ShareRequestId} approved successfully. Action: {Action}, Target game: {TargetSharedGameId}",
            shareRequest.Id, command.ApprovalAction, targetSharedGameId);

        // Domain event ShareRequestApprovedEvent is dispatched automatically
        // ProposalMigration is created by event handler

        // Verify ResolvedAt was set by Approve() (defensive programming)
        if (!shareRequest.ResolvedAt.HasValue)
        {
            throw new InvalidOperationException(
                $"ShareRequest.Approve() failed to set ResolvedAt for request {shareRequest.Id}");
        }

        return new ApproveShareRequestResponse(
            shareRequest.Id,
            shareRequest.Status,
            targetSharedGameId,
            shareRequest.ResolvedAt.Value);
    }

    /// <summary>
    /// Creates a new SharedGame from the PrivateGame data.
    /// </summary>
    private async Task<Guid> ApproveAsNewGameAsync(
        UserLibrary.Domain.Entities.PrivateGame privateGame,
        Guid contributorId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Creating new SharedGame from PrivateGame {PrivateGameId}",
            privateGame.Id);

        var promotionData = privateGame.ToSharedGame();

        var sharedGame = SharedGame.Create(
            title: promotionData.Title,
            yearPublished: promotionData.YearPublished,
            description: promotionData.Description,
            minPlayers: promotionData.MinPlayers,
            maxPlayers: promotionData.MaxPlayers,
            playingTimeMinutes: promotionData.PlayingTimeMinutes,
            minAge: promotionData.MinAge,
            complexityRating: promotionData.ComplexityRating,
            averageRating: null, // Not available from private game
            imageUrl: promotionData.ImageUrl,
            thumbnailUrl: promotionData.ThumbnailUrl,
            rules: null, // Rules will be added from documents
            createdBy: contributorId,
            bggId: promotionData.BggId);

        await _sharedGameRepository.AddAsync(sharedGame, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Created new SharedGame {SharedGameId} from PrivateGame {PrivateGameId}",
            sharedGame.Id, privateGame.Id);

        return sharedGame.Id;
    }

    /// <summary>
    /// Merges knowledge base (PDFs) into an existing SharedGame.
    /// Does not create a new SharedGame.
    /// </summary>
    private async Task<Guid> MergeKnowledgeBaseAsync(
        Guid targetSharedGameId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Merging knowledge base into existing SharedGame {SharedGameId}",
            targetSharedGameId);

        // Validate target game exists
        var existingGame = await _sharedGameRepository.GetByIdAsync(
            targetSharedGameId,
            cancellationToken).ConfigureAwait(false);

        if (existingGame == null)
        {
            throw new NotFoundException("SharedGame", targetSharedGameId.ToString());
        }

        // No new game creation - documents will be copied by caller
        return targetSharedGameId;
    }

    /// <summary>
    /// Creates a new SharedGame as a variant of an existing game.
    /// Appends " (Variant)" suffix to the title.
    /// </summary>
    private async Task<Guid> ApproveAsVariantAsync(
        UserLibrary.Domain.Entities.PrivateGame privateGame,
        Guid baseGameId,
        Guid contributorId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Creating variant SharedGame from PrivateGame {PrivateGameId} based on {BaseGameId}",
            privateGame.Id, baseGameId);

        // Validate base game exists
        var baseGame = await _sharedGameRepository.GetByIdAsync(
            baseGameId,
            cancellationToken).ConfigureAwait(false);

        if (baseGame == null)
        {
            throw new NotFoundException("SharedGame", baseGameId.ToString());
        }

        var promotionData = privateGame.ToSharedGame();

        // Create variant with modified title
        var variantTitle = $"{promotionData.Title} (Variant)";

        var sharedGame = SharedGame.Create(
            title: variantTitle,
            yearPublished: promotionData.YearPublished,
            description: promotionData.Description,
            minPlayers: promotionData.MinPlayers,
            maxPlayers: promotionData.MaxPlayers,
            playingTimeMinutes: promotionData.PlayingTimeMinutes,
            minAge: promotionData.MinAge,
            complexityRating: promotionData.ComplexityRating,
            averageRating: null,
            imageUrl: promotionData.ImageUrl,
            thumbnailUrl: promotionData.ThumbnailUrl,
            rules: null,
            createdBy: contributorId,
            bggId: null); // Variants don't use BGG ID

        await _sharedGameRepository.AddAsync(sharedGame, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Created variant SharedGame {SharedGameId} ('{Title}') from PrivateGame {PrivateGameId}",
            sharedGame.Id, variantTitle, privateGame.Id);

        return sharedGame.Id;
    }
}
