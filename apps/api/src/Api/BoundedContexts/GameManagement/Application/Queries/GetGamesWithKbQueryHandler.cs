using Api.BoundedContexts.EntityRelationships.Domain.Enums;
using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Application.Queries;

/// <summary>
/// Handler for <see cref="GetGamesWithKbQuery"/>.
/// Cross-BC read query joining UserLibraryEntries → EntityLinks (Game→KbCard) → PdfDocuments → Games
/// to build the list of games with KB status for the chat selection screen.
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

        // Step 1: Get all game IDs in the user's library (SharedGame or PrivateGame)
        var libraryGameIds = await _db.UserLibraryEntries
            .Where(ule => ule.UserId == query.UserId && ule.SharedGameId != null)
            .Select(ule => ule.SharedGameId!.Value)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (libraryGameIds.Count == 0)
            return Array.Empty<GameWithKbDto>();

        // Step 2: Cross-BC join — EntityLinks (Game→KbCard) joined to PdfDocuments and Games.
        // EntityLink: SourceEntityType=Game, TargetEntityType=KbCard
        // TargetEntityId points to PdfDocument.Id (the KB card IS the PDF document)
        var rulebookRows = await (
            from link in _db.EntityLinks
            join pdf in _db.PdfDocuments on link.TargetEntityId equals pdf.Id
            join game in _db.Games on link.SourceEntityId equals game.Id
            where !link.IsDeleted
                  && link.SourceEntityType == MeepleEntityType.Game
                  && link.TargetEntityType == MeepleEntityType.KbCard
                  && link.LinkType == EntityLinkType.RelatedTo
                  && libraryGameIds.Contains(link.SourceEntityId)
            select new
            {
                GameId = game.Id,
                GameTitle = game.Name,
                game.ImageUrl,
                PdfDocumentId = pdf.Id,
                pdf.FileName,
                pdf.ProcessingState,
                pdf.ProcessedAt
            }
        )
        .ToListAsync(cancellationToken)
        .ConfigureAwait(false);

        if (rulebookRows.Count == 0)
            return Array.Empty<GameWithKbDto>();

        // Step 3: Group by game and project into DTOs
        var result = rulebookRows
            .GroupBy(r => new { r.GameId, r.GameTitle, r.ImageUrl })
            .Select(g =>
            {
                var rulebooks = g.Select(r => new RulebookDto(
                    r.PdfDocumentId,
                    r.FileName,
                    MapKbStatus(r.ProcessingState),
                    r.ProcessedAt
                )).ToList();

                var overallStatus = ComputeOverallStatus(rulebooks);

                return new GameWithKbDto(
                    g.Key.GameId,
                    g.Key.GameTitle,
                    g.Key.ImageUrl,
                    overallStatus,
                    rulebooks);
            })
            .OrderBy(g => g.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return result;
    }

    /// <summary>
    /// Maps the raw PdfDocument.ProcessingState string to a simplified KB status.
    /// </summary>
    private static string MapKbStatus(string processingState) => processingState switch
    {
        "Pending" or "Uploading" => "pending",
        "Extracting" or "Chunking" or "Embedding" or "Indexing" => "processing",
        "Ready" => "ready",
        "Failed" => "failed",
        _ => "unknown"
    };

    /// <summary>
    /// Computes the overall KB status for a game:
    /// "ready" if any rulebook is ready, else "processing" if any is in-progress, else "failed".
    /// </summary>
    private static string ComputeOverallStatus(List<RulebookDto> rulebooks)
    {
        if (rulebooks.Any(r => string.Equals(r.KbStatus, "ready", StringComparison.Ordinal)))
            return "ready";
        if (rulebooks.Any(r => r.KbStatus is "processing" or "pending"))
            return "processing";
        return "failed";
    }
}
