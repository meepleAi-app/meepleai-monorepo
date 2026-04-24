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
/// Command to bulk-import BoardGameGeek mechanic tags for a shared game
/// (ADR-051 Sprint 1 / Task 22). Tags feed golden-claim seeding and scoring.
/// </summary>
/// <remarks>
/// <para>
/// Semantics are additive/upsert: existing tags not present in <see cref="Tags"/> are left
/// untouched, and new tags are appended. Duplicate (shared_game_id, name) pairs are deduped
/// at the repository layer.
/// </para>
/// <para>
/// The return value is the number of tags submitted by the caller (i.e. <c>Tags.Count</c>)
/// rather than an insert delta — the underlying repository does not surface a count of newly
/// inserted rows vs pre-existing ones, so the command contract reports "how many tags were
/// requested for upsert".
/// </para>
/// </remarks>
/// <param name="SharedGameId">The shared game the tag batch belongs to.</param>
/// <param name="Tags">The batch of BGG tags to upsert. May be empty (no-op).</param>
internal sealed record ImportBggTagsCommand(
    Guid SharedGameId,
    IReadOnlyList<BggTagDto> Tags
) : ICommand<int>;
