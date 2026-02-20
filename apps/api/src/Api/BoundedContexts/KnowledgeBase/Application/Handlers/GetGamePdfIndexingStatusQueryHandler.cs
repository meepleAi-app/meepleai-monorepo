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
/// </summary>
/// <remarks>
/// Authorization: throws ForbiddenException (403) if user doesn't own the game.
/// Not found: throws NotFoundException (404) if no PDF has been associated with the game.
/// Crosses BC boundary to check ownership via IPrivateGameRepository (acceptable for queries).
/// </remarks>
internal sealed class GetGamePdfIndexingStatusQueryHandler
    : IQueryHandler<GetGamePdfIndexingStatusQuery, PdfIndexingStatusDto>
{
    private readonly IVectorDocumentRepository _vectorDocumentRepository;
    private readonly IPrivateGameRepository _privateGameRepository;
    private readonly ILogger<GetGamePdfIndexingStatusQueryHandler> _logger;

    public GetGamePdfIndexingStatusQueryHandler(
        IVectorDocumentRepository vectorDocumentRepository,
        IPrivateGameRepository privateGameRepository,
        ILogger<GetGamePdfIndexingStatusQueryHandler> logger)
    {
        _vectorDocumentRepository = vectorDocumentRepository ?? throw new ArgumentNullException(nameof(vectorDocumentRepository));
        _privateGameRepository = privateGameRepository ?? throw new ArgumentNullException(nameof(privateGameRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PdfIndexingStatusDto> Handle(
        GetGamePdfIndexingStatusQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 1. Verify game exists and user owns it
        var game = await _privateGameRepository
            .GetByIdAsync(request.GameId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("PrivateGame", request.GameId.ToString());

        if (game.OwnerId != request.UserId)
            throw new ForbiddenException("Access to this game's PDF status is not permitted.");

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
