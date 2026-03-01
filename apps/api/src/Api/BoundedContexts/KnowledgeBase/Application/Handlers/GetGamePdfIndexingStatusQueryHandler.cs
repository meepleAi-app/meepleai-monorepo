using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handler for GetGamePdfIndexingStatusQuery.
/// Issue #4943: Returns PDF indexing/processing status for a game owned by the user.
/// Issue #5217: Extended to support shared catalog game IDs (not only private game IDs).
/// </summary>
/// <remarks>
/// Authorization strategy (two paths):
/// - Private game: verifies via IPrivateGameRepository — user must be the game owner.
/// - Shared catalog game: verifies via IUserLibraryRepository.IsGameInLibraryAsync — game must be in user's library.
/// Not found: throws NotFoundException (404) if game is neither private-owned nor in shared library,
/// or if no PDF has been indexed for the game yet.
/// </remarks>
internal sealed class GetGamePdfIndexingStatusQueryHandler
    : IQueryHandler<GetGamePdfIndexingStatusQuery, PdfIndexingStatusDto>
{
    private readonly IVectorDocumentRepository _vectorDocumentRepository;
    private readonly IPrivateGameRepository _privateGameRepository;
    private readonly IUserLibraryRepository _userLibraryRepository;
    private readonly ILogger<GetGamePdfIndexingStatusQueryHandler> _logger;

    public GetGamePdfIndexingStatusQueryHandler(
        IVectorDocumentRepository vectorDocumentRepository,
        IPrivateGameRepository privateGameRepository,
        IUserLibraryRepository userLibraryRepository,
        ILogger<GetGamePdfIndexingStatusQueryHandler> logger)
    {
        _vectorDocumentRepository = vectorDocumentRepository ?? throw new ArgumentNullException(nameof(vectorDocumentRepository));
        _privateGameRepository = privateGameRepository ?? throw new ArgumentNullException(nameof(privateGameRepository));
        _userLibraryRepository = userLibraryRepository ?? throw new ArgumentNullException(nameof(userLibraryRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PdfIndexingStatusDto> Handle(
        GetGamePdfIndexingStatusQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 1. Verify user is authorized to query this game's PDF status.
        //    Path A: private game — user must be the owner.
        //    Path B: shared catalog game — game must be in user's library (Issue #5217).
        var privateGame = await _privateGameRepository
            .GetByIdAsync(request.GameId, cancellationToken)
            .ConfigureAwait(false);

        if (privateGame is not null)
        {
            // Path A: private game ownership check
            if (privateGame.OwnerId != request.UserId)
                throw new ForbiddenException("Access to this game's PDF status is not permitted.");
        }
        else
        {
            // Path B: shared catalog game — verify it is in user's library
            var isInLibrary = await _userLibraryRepository
                .IsGameInLibraryAsync(request.UserId, request.GameId, cancellationToken)
                .ConfigureAwait(false);

            if (!isInLibrary)
                throw new NotFoundException("Game", request.GameId.ToString());
        }

        // 2. Fetch indexing info for this game
        var info = await _vectorDocumentRepository
            .GetIndexingInfoByGameIdAsync(request.GameId, cancellationToken)
            .ConfigureAwait(false);

        if (info is null)
        {
            _logger.LogDebug(
                "No VectorDocument found for game {GameId}. Returning 404.",
                request.GameId);
            throw new NotFoundException("VectorDocument", request.GameId.ToString());
        }

        // 3. Map entity status → public status (Completed → "indexed" avoids leaking internal names)
        var publicStatus = info.Status switch
        {
            VectorDocumentIndexingStatus.Completed => "indexed",
            VectorDocumentIndexingStatus.Pending => "pending",
            VectorDocumentIndexingStatus.Processing => "processing",
            VectorDocumentIndexingStatus.Failed => "failed",
            _ => info.Status.ToString().ToLowerInvariant()
        };

        // 4. Derive progress (best-effort: no granular tracking yet)
        int? progress = info.Status switch
        {
            VectorDocumentIndexingStatus.Pending => 0,
            VectorDocumentIndexingStatus.Processing => null, // unknown mid-flight
            VectorDocumentIndexingStatus.Completed => 100,
            VectorDocumentIndexingStatus.Failed => null,
            _ => null
        };

        _logger.LogDebug(
            "PDF indexing status for game {GameId}: {Status} ({ChunkCount} chunks)",
            request.GameId, publicStatus, info.ChunkCount);

        return new PdfIndexingStatusDto(
            Status: publicStatus,
            Progress: progress,
            ChunkCount: info.Status == VectorDocumentIndexingStatus.Completed ? info.ChunkCount : null,
            ErrorMessage: info.Status == VectorDocumentIndexingStatus.Failed ? info.IndexingError : null);
    }
}
