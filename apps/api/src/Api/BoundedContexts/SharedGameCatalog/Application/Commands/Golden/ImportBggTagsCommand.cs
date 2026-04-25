using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.Golden;

/// <summary>
/// Data-transfer record for a single BoardGameGeek tag imported into the golden set
/// (ADR-051 Sprint 1 / Task 22). Bound constraints match the persisted entity
/// <see cref="Api.BoundedContexts.SharedGameCatalog.Domain.Entities.MechanicGoldenBggTag"/>:
/// name 1..200, category 1..100.
/// </summary>
/// <param name="Name">Tag name (e.g. "Worker Placement").</param>
/// <param name="Category">Tag category (e.g. "mechanic", "theme").</param>
internal sealed record BggTagDto(string Name, string Category);

/// <summary>
/// Result of <see cref="ImportBggTagsCommand"/> (ADR-051 Sprint 2 / Task 17). Splits the
/// submitted batch into newly inserted rows and rows skipped due to either an existing
/// <c>(shared_game_id, name)</c> pair or a within-batch duplicate.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Inserted"/> + <see cref="Skipped"/> equals <c>command.Tags.Count</c> for every
/// non-empty batch — the BGG importer UI uses this split to render "Imported N tags
/// (M skipped as duplicates)" telemetry without round-tripping back to the server.
/// </para>
/// <para>
/// Empty submissions return <c>(0, 0)</c>.
/// </para>
/// </remarks>
internal sealed record BggImportResult(int Inserted, int Skipped);

/// <summary>
/// Command to bulk-import BoardGameGeek mechanic tags for a shared game
/// (ADR-051 Sprint 1 / Task 22, refined in Sprint 2 / Task 17 to surface
/// inserted/skipped counts). Tags feed golden-claim seeding and scoring.
/// </summary>
/// <remarks>
/// <para>
/// Semantics are additive/upsert: existing tags not present in <see cref="Tags"/> are left
/// untouched, and new tags are appended. Duplicate (shared_game_id, name) pairs are deduped
/// at the repository layer — both against existing rows and within the same batch.
/// </para>
/// <para>
/// The result is a <see cref="BggImportResult"/> splitting the submitted batch into new
/// inserts and skipped duplicates, so the importer UI can render meaningful feedback.
/// </para>
/// </remarks>
/// <param name="SharedGameId">The shared game the tag batch belongs to.</param>
/// <param name="Tags">The batch of BGG tags to upsert. May be empty (no-op).</param>
internal sealed record ImportBggTagsCommand(
    Guid SharedGameId,
    IReadOnlyList<BggTagDto> Tags
) : ICommand<BggImportResult>;
