using Api.BoundedContexts.EntityRelationships.Domain.Aggregates;
using Api.BoundedContexts.EntityRelationships.Domain.Enums;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// #4325: the one rule for "a game really uses this PDF". An <c>EntityLink</c> Game → KbCard is a claim, not a fact:
/// <c>POST /library/entity-links</c> lets any authenticated user write one between any two ids. Code that reads a link
/// as "this PDF is still in use" (the direct delete of a PDF, the purge of a deleted private game) has to count only
/// the links that stand up.
/// </summary>
/// <remarks>
/// <para>
/// A link stands up when it is alive, goes from a game to the PDF, <b>was written by whoever uploaded the PDF</b>, and
/// starts from a private game of that same person that exists, or from a catalog game that exists. It is the link the
/// upload itself creates, and the one the deduplication creates when the same user uploads the file again.
/// </para>
/// <para>
/// What it leaves out: a link from an id that is no game, from a game that is deleted, from a private game of someone
/// else, and every link written by somebody who is not the uploader, <b>whatever the game</b>.
/// </para>
/// <para>
/// That last part is a choice, and it has a cost. The catalog deduplication reuses a PDF across users on the same
/// catalog game (#4326): a second user who uploads a file that is already there is linked to the PDF of the first
/// uploader, and that link is left out when the first uploader deletes the PDF. The same holds for the links to a PDF
/// of another game that the earlier, global deduplication wrote. It cannot be told from a forged link: <c>SharedGameId</c> is set on every PDF a user
/// uploads against a catalog game (<c>AddRulebook</c> asks only for a library entry), so it is no sign of "belongs to
/// the catalog", and a link carries nothing that says a trusted flow wrote it. A sign like that would have to be
/// written by the flows that reuse a PDF; until then the safe side is the one that does not let a stranger decide
/// whether somebody else's PDF can be deleted.
/// </para>
/// <para>
/// <c>IgnoreQueryFilters</c> holds for the whole query, including the sub-queries below and whatever the caller
/// appends: the deletion filters are written by hand (<c>!IsDeleted</c>), as the purge always did.
/// </para>
/// </remarks>
internal static class KbCardLinkRule
{
    /// <summary>The links that stand up. The target of each one (<c>TargetEntityId</c>) is the PDF.</summary>
    internal static IQueryable<EntityLink> LiveLinksToPdfs(this MeepleAiDbContext db) =>
        db.EntityLinks
            .IgnoreQueryFilters()
            .Where(link => !link.IsDeleted
                           && link.SourceEntityType == MeepleEntityType.Game
                           && link.TargetEntityType == MeepleEntityType.KbCard
                           && db.PdfDocuments.Any(pdf =>
                               pdf.Id == link.TargetEntityId
                               && link.OwnerUserId == pdf.UploadedByUserId
                               && (db.PrivateGames.Any(g => g.Id == link.SourceEntityId
                                                            && !g.IsDeleted
                                                            && g.OwnerId == pdf.UploadedByUserId)
                                   || db.SharedGames.Any(s => s.Id == link.SourceEntityId && !s.IsDeleted))));

    /// <summary>
    /// Among <paramref name="pdfIds"/>, the PDFs that a game other than <paramref name="exceptGameId"/> uses: the game
    /// that is letting go of the PDF does not keep it alive. The link counts as a game, not as a row: a game can
    /// reach the same PDF with several link types.
    /// </summary>
    internal static IQueryable<Guid> PdfsUsedByOtherGames(
        this MeepleAiDbContext db, IReadOnlyCollection<Guid> pdfIds, Guid exceptGameId) =>
        db.LiveLinksToPdfs()
            .Where(link => pdfIds.Contains(link.TargetEntityId) && link.SourceEntityId != exceptGameId)
            .Select(link => link.TargetEntityId)
            .Distinct();
}
