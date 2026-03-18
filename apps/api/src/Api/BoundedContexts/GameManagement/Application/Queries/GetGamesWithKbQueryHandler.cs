using Api.BoundedContexts.EntityRelationships.Domain.Enums;
using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Application.Queries;

/// <summary>
/// Returns all games that have at least one KB-linked PDF via EntityLink (Game→KbCard).
/// Joins: UserLibraryEntries → EntityLinks → PdfDocuments → Games.
/// Does NOT use PdfDocument.GameId — uses EntityLink.TargetEntityId = PdfDocument.Id.
/// </summary>
internal sealed class GetGamesWithKbQueryHandler
    : IQueryHandler<GetGamesWithKbQuery, IReadOnlyList<GameWithKbDto>>
{
    private readonly MeepleAiDbContext _db;

    public GetGamesWithKbQueryHandler(MeepleAiDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<IReadOnlyList<GameWithKbDto>> Handle(
        GetGamesWithKbQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Cross-context read via direct EF joins through EntityLink.
        // UserLibraryEntries (user's games) → EntityLink (Game→KbCard) → PdfDocument
        var rows = await (
            from lib in _db.UserLibraryEntries
            where lib.UserId == query.UserId
            join link in _db.EntityLinks
                on new { Type = MeepleEntityType.Game, Id = lib.GameId }
                equals new { Type = link.SourceEntityType, Id = link.SourceEntityId }
            where link.TargetEntityType == MeepleEntityType.KbCard
                && !link.IsDeleted
            join pdf in _db.PdfDocuments
                on link.TargetEntityId equals pdf.Id
            join game in _db.Games
                on lib.GameId equals game.Id
            select new
            {
                game.Id,
                game.Name,
                game.ImageUrl,
                PdfDocumentId = pdf.Id,
                pdf.FileName,
                pdf.ProcessingState,
                pdf.ProcessedAt
            }
        ).AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);

        // Group by game, map to DTOs
        var result = rows
            .GroupBy(x => new { x.Id, x.Name, x.ImageUrl })
            .Select(g =>
            {
                var rulebooks = g.Select(r => new RulebookDto(
                    r.PdfDocumentId,
                    r.FileName,
                    MapKbStatus(r.ProcessingState),
                    string.Equals(r.ProcessingState, "Ready", StringComparison.Ordinal) ? r.ProcessedAt : null
                )).ToList();

                // overallKbStatus: "ready" if any ready, else "processing" if any in-progress, else "failed"
                var overallStatus = rulebooks.Any(r => string.Equals(r.KbStatus, "ready", StringComparison.Ordinal)) ? "ready"
                    : rulebooks.Any(r => r.KbStatus is "pending" or "processing") ? "processing"
                    : "failed";

                return new GameWithKbDto(
                    g.Key.Id,
                    g.Key.Name,
                    g.Key.ImageUrl,
                    overallStatus,
                    rulebooks);
            })
            .ToList();

        return result;
    }

    /// <summary>
    /// Maps PdfDocumentEntity.ProcessingState (stored as string) to client-facing KB status.
    /// Local to this handler to avoid cross-BC dependency on DocumentProcessing DTOs.
    /// </summary>
    private static string MapKbStatus(string? processingState) => processingState switch
    {
        "Pending" or "Uploading" => "pending",
        "Extracting" or "Chunking" or "Embedding" or "Indexing" => "processing",
        "Ready" => "ready",
        "Failed" => "failed",
        _ => "unknown"
    };
}
