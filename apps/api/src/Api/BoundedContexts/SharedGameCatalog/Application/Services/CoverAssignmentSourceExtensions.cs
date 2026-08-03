using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.SharedKernel.Domain.Covers;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services;

/// <summary>
/// Epic #3470 — bridges the domain-facing <see cref="CoverAssignmentSource"/>
/// (what an admin pins) to the infrastructure storage-key <see cref="CoverKind"/>
/// (how <see cref="CoverKeyBuilder"/> composes the R2 key). The two enums DO NOT
/// share numeric values (<c>Source.Pdf=0</c> vs <c>Kind.Pdf=1</c>; <c>Kind.User=0</c>
/// has no <c>Source</c> counterpart because L3 is per-user, never a catalog
/// assignment), so a numeric <c>(CoverKind)(int)source</c> cast is silently wrong
/// (a PDF assignment would resolve as the user-custom cover). This explicit,
/// value-by-value switch is the ONLY sanctioned conversion.
/// </summary>
internal static class CoverAssignmentSourceExtensions
{
    public static CoverKind ToCoverKind(this CoverAssignmentSource source) => source switch
    {
        CoverAssignmentSource.Pdf => CoverKind.Pdf,
        CoverAssignmentSource.Bgg => CoverKind.Bgg,
        CoverAssignmentSource.Wikidata => CoverKind.Wikidata,
        CoverAssignmentSource.Manual => CoverKind.Manual,
        _ => throw new ArgumentOutOfRangeException(nameof(source), source, "Unknown cover assignment source."),
    };
}
