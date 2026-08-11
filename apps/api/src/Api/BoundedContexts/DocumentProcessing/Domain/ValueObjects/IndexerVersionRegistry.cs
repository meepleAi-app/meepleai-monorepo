// apps/api/src/Api/BoundedContexts/DocumentProcessing/Domain/ValueObjects/IndexerVersionRegistry.cs
using System.Diagnostics.CodeAnalysis;

namespace Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;

/// <summary>
/// Code-resident registry of pipeline indexer versions per design doc D-B.
/// Issue #1673.
/// </summary>
/// <remarks>
/// <para>
/// <b>Aggiungere una nuova versione</b>: introdurre la `static readonly IndexerVersion Vx_y`,
/// includerla in <see cref="All"/>, aggiornare <see cref="Current"/>. NIENTE breaking change
/// per <see cref="Legacy"/> (v0 resta marker di backfill).
/// </para>
/// <para>
/// <b>Deprecation policy</b> (OQ-2 risolta dal spec-panel review PR #1790): le versioni
/// storiche restano nel registry per ≥18 mesi post-supersession da una versione più recente.
/// Oltre quel termine il code-resident slot può essere riciclato.
/// </para>
/// </remarks>
internal static class IndexerVersionRegistry
{
    /// <summary>
    /// Marker per documenti pre-versioning ingeriti prima dell'introduzione del versioning.
    /// Non selectable: serve solo a non lasciare la colonna nullable per le righe storiche.
    /// </summary>
    public static readonly IndexerVersion Legacy =
        new("v0", "v0 (legacy pre-versioning)", IsSelectable: false);

    /// <summary>
    /// Pipeline flat (pre heading-aware). Non più <see cref="Current"/> dopo l'epic #3266,
    /// ma resta nel registry per la deprecation policy (≥18 mesi) e selezionabile da `/reindex`.
    /// </summary>
    public static readonly IndexerVersion V1_0 =
        new("v1.0", "v1.0 — flat chunking pipeline", IsSelectable: true);

    /// <summary>
    /// Pipeline heading-aware (pre coordinate-aware). Non più <see cref="Current"/> dopo l'epic
    /// #3403 SP-E, ma resta nel registry per la deprecation policy (≥18 mesi) e selezionabile da `/reindex`.
    /// SP3 #3269 (epic #3266): SP1/SP2 heading-aware.
    /// </summary>
    public static readonly IndexerVersion V1_1 =
        new("v1.1", "v1.1 — heading-aware chunking", IsSelectable: true);

    /// <summary>
    /// Versione corrente della pipeline. Equivale al comportamento di default quando
    /// `reindexDocument` viene chiamato senza `IndexerVersion` esplicito.
    /// SP-E #3409 (epic #3403): pipeline coordinate-aware — l'estrazione Unstructured emette le bbox
    /// normalizzate (SP-B) persistite in <c>bounding_boxes_json</c> per il region grounding. Un
    /// re-extract del corpus a questa versione ri-popola le coordinate (le regions oggi sono null).
    /// </summary>
    public static readonly IndexerVersion Current =
        new("v1.2", "v1.2 — coordinate-aware (region grounding)", IsSelectable: true);

    public static IReadOnlyList<IndexerVersion> All { get; } = [Legacy, V1_0, V1_1, Current];

    /// <summary>
    /// Restituisce la lista delle versioni effettivamente invocabili da `/reindex`.
    /// </summary>
    public static IReadOnlyList<IndexerVersion> Selectable { get; } =
        All.Where(v => v.IsSelectable).ToArray();

    public static bool TryGet(string? version, [NotNullWhen(true)] out IndexerVersion? result)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            result = null;
            return false;
        }

        result = All.FirstOrDefault(v => string.Equals(v.Version, version, StringComparison.Ordinal));
        return result is not null;
    }

    public static bool IsSelectable(string? version) =>
        TryGet(version, out var v) && v.IsSelectable;
}
