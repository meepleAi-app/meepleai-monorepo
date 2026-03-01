using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.EntityRelationships.Application.Commands;

/// <summary>
/// Command to import BGG expansion and reimplements EntityLinks
/// for a given SharedGame (Issue #5141).
///
/// The handler delegates to IBggExpansionImporter which:
/// - Fetches the BGG XML API
/// - Extracts boardgameexpansion / boardgameimplementation links
/// - Persists new EntityLinks (IsBggImported=true) skipping duplicates (BR-08)
/// </summary>
internal sealed record ImportBggExpansionsCommand(
    Guid SharedGameId,
    Guid AdminUserId
) : ICommand<int>;
