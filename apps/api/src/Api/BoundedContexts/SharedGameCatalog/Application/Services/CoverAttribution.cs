using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.SharedKernel.Domain.Covers;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services;

/// <summary>
/// Epic #3470 Slice 1d-a — maps the WINNING cover source to the license / attribution /
/// source-url triple the shared-game DTOs surface. Only sources that carry a license
/// emit one: today that is Wikidata (the DTOs expose Wikidata attribution fields only).
/// When a PDF / BGG / user cover wins — or nothing wins — the triple is all-null, so the
/// footer never credits Wikidata over a non-Wikidata image (the source-mislabeling bug
/// this fixes). Manual-cover attribution surfacing is deferred: no DTO fields exist yet.
/// </summary>
internal static class CoverAttribution
{
    /// <summary>
    /// Returns the attribution triple for the <paramref name="winningKind"/>, reading
    /// from <paramref name="entity"/>. HTML in the Wikidata attribution is stripped
    /// (DEC-G6-1). All-null for any non-Wikidata winner or when no source won.
    /// </summary>
    public static (string? License, string? Attribution, string? SourceUrl) ForWinningSource(
        CoverKind? winningKind,
        SharedGameEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return winningKind == CoverKind.Wikidata
            ? (entity.WikidataCoverLicense,
               AttributionTextExtractor.Strip(entity.WikidataCoverAttribution),
               entity.WikidataCoverSourceUrl)
            : (null, null, null);
    }
}
