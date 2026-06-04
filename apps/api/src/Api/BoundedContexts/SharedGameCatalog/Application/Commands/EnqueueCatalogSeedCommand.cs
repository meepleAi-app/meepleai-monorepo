using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Command to enqueue a single catalog seed draft for asynchronous provider fetch.
/// Spec: 2026-06-04-admin-catalog-seed-design.md §4.2 / §6.
/// Issue #1903 — M4.2.
/// </summary>
/// <remarks>
/// At least one of <paramref name="BggId"/>, <paramref name="WikidataQid"/>, or
/// <paramref name="SearchTermInput"/> must be supplied (enforced by validator).
/// If a draft already exists for the given BggId, the command is idempotent and
/// returns <c>IsDuplicate=true</c> without inserting a new row.
/// </remarks>
internal sealed record EnqueueCatalogSeedCommand(
    int? BggId,
    string? WikidataQid,
    string? SearchTermInput,
    Guid CreatedByUserId) : ICommand<EnqueueCatalogSeedResult>;

/// <summary>
/// Result of enqueueing a single catalog seed draft.
/// </summary>
/// <param name="Id">The draft id (existing or newly created).</param>
/// <param name="BggId">Echo of the BGG id (may be null for Wikidata-/search-only seeds).</param>
/// <param name="Status">Current status of the draft. New drafts start as <c>Pending</c>.</param>
/// <param name="IsDuplicate">True when an existing draft was found for the given BggId.</param>
internal sealed record EnqueueCatalogSeedResult(Guid Id, int? BggId, string Status, bool IsDuplicate);
