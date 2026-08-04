using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.SharedKernel.Domain.Covers;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services;

/// <summary>
/// Epic #3470 Slice 1d-a / 3b — maps the WINNING cover source to the license / attribution /
/// source-url triple the shared-game DTOs surface (source-neutral <c>Cover*</c> fields). Only
/// sources that carry an attested license emit one: Wikidata (the enrichment pipeline) and
/// Manual (the admin arbitrary-URL path, Slice 3b — copyright-critical: a whitelist-admitted
/// CC-BY/CC-BY-SA image MUST render its credit). When a PDF / BGG / user cover wins — none of
/// which carry a license — or nothing wins, the triple is all-null, so the footer never credits
/// a source over a different image (the source-mislabeling bug Slice 1d-a fixed).
/// </summary>
internal static class CoverAttribution
{
    /// <summary>
    /// Returns the attribution triple for the <paramref name="winningKind"/>, reading from
    /// <paramref name="entity"/>. HTML in the attribution is stripped (DEC-G6-1) for both the
    /// Wikidata and the admin-attested Manual credit. All-null for any source without a license
    /// (PDF / BGG / user) or when no source won.
    /// </summary>
    public static (string? License, string? Attribution, string? SourceUrl) ForWinningSource(
        CoverKind? winningKind,
        SharedGameEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return winningKind switch
        {
            CoverKind.Wikidata => (entity.WikidataCoverLicense,
                                   AttributionTextExtractor.Strip(entity.WikidataCoverAttribution),
                                   entity.WikidataCoverSourceUrl),
            CoverKind.Manual => (entity.ManualCoverLicense,
                                 AttributionTextExtractor.Strip(entity.ManualCoverAttribution),
                                 entity.ManualCoverSourceUrl),
            _ => (null, null, null),
        };
    }
}
