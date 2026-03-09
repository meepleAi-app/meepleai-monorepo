using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.UserLibrary.Application.Handlers;

/// <summary>
/// Handler for GetGamePdfsQuery.
/// Retrieves all PDFs associated with a game from the pdf_documents table.
/// Issue #3152: Game Detail Split View - PDF selector support
/// </summary>
internal class GetGamePdfsQueryHandler : IRequestHandler<GetGamePdfsQuery, List<GamePdfDto>>
{
    private readonly MeepleAiDbContext _db;
    private readonly ILogger<GetGamePdfsQueryHandler> _logger;

    public GetGamePdfsQueryHandler(
        MeepleAiDbContext db,
        ILogger<GetGamePdfsQueryHandler> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<List<GamePdfDto>> Handle(
        GetGamePdfsQuery request,
        CancellationToken cancellationToken)
    {
        // Resolve gameId: may be a shared_games.Id, games.Id, or private_games.Id
        // Query pdf_documents matching any of these associations
        var resolvedGameId = await _db.Games
            .AsNoTracking()
            .Where(g => g.Id == request.GameId || g.SharedGameId == request.GameId)
            .Select(g => (Guid?)g.Id)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        // For private games, verify the requesting user owns the game
        var ownedPrivateGameId = await _db.PrivateGames
            .AsNoTracking()
            .Where(pg => pg.Id == request.GameId && pg.OwnerId == request.UserId)
            .Select(pg => (Guid?)pg.Id)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        var entities = await _db.PdfDocuments
            .AsNoTracking()
            .Where(p =>
                (resolvedGameId != null && p.GameId == resolvedGameId) ||
                p.SharedGameId == request.GameId ||
                (ownedPrivateGameId != null && p.PrivateGameId == ownedPrivateGameId))
            .OrderByDescending(p => p.UploadedAt)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var pdfs = entities.Select(p => new GamePdfDto(
            Id: p.Id.ToString(),
            Name: p.FileName.Replace(".pdf", "", StringComparison.OrdinalIgnoreCase),
            PageCount: p.PageCount ?? 0,
            FileSizeBytes: p.FileSizeBytes,
            UploadedAt: p.UploadedAt,
            Source: p.PrivateGameId != null ? "Custom" : "Catalog",
            Language: p.Language,
            ProcessingState: p.ProcessingState
        )).ToList();

        _logger.LogInformation(
            "Retrieved {Count} PDFs for game {GameId}, user {UserId}",
            pdfs.Count, request.GameId, request.UserId);

        return pdfs;
    }
}
