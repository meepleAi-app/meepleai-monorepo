using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.UserLibrary.Application.Queries;

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
        // request.GameId may be a games.Id, a shared_games.Id, or a private_games.Id.
        //
        // Bug fix (post-PR #267): the previous implementation resolved a SINGLE games row via
        // FirstOrDefault and then filtered pdf_documents.GameId by that one Id. When multiple
        // games rows share the same SharedGameId (e.g. different version/language entries),
        // PDFs uploaded against the other games rows were silently dropped.
        //
        // Fix: query through games.SharedGameId via EXISTS subquery so we catch every PDF whose
        // games row links to the requested shared game, regardless of which games row was picked
        // by the legacy resolver.

        // For private games, verify the requesting user owns the game
        var ownedPrivateGameId = await _db.PrivateGames
            .AsNoTracking()
            .Where(pg => pg.Id == request.GameId && pg.OwnerId == request.UserId)
            .Select(pg => (Guid?)pg.Id)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        var entities = await _db.PdfDocuments
            .AsNoTracking()
            .Where(p =>
                // Direct shared catalog link (request.GameId == SharedGames.Id)
                p.SharedGameId == request.GameId ||
                // Resolve via games: p.PrivateGameId points to a games row whose SharedGameId == request.GameId
                // (catches PDFs uploaded against any games row linked to the same shared catalog entry)
                _db.Games.Any(g => g.Id == p.PrivateGameId && g.SharedGameId == request.GameId) ||
                // Owned private games
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
