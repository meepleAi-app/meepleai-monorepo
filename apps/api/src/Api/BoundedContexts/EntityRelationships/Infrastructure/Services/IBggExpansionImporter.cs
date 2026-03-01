namespace Api.BoundedContexts.EntityRelationships.Infrastructure.Services;

/// <summary>
/// Service that fetches expansion and reimplements relationships from BGG XML API
/// and persists them as EntityLinks with IsBggImported=true.
///
/// Issue #5141 (Epic A — EntityRelationships)
/// </summary>
public interface IBggExpansionImporter
{
    /// <summary>
    /// Imports BGG expansion/reimplements links for a SharedGame identified by its internal ID.
    ///
    /// - Fetches the BGG XML API: thing?id={bggId}&amp;type=boardgame
    /// - Extracts boardgameexpansion → EntityLinkType.ExpansionOf
    /// - Extracts boardgamereimplements → EntityLinkType.Reimplements
    /// - Looks up each referenced BGG ID in the local SharedGame catalog
    /// - Creates EntityLinks (IsBggImported=true, IsAdminApproved=true, Scope=Shared)
    ///   only when the target game exists locally (skips unknown BGG IDs)
    /// - Idempotent: skips links that already exist (BR-08)
    /// </summary>
    /// <param name="sharedGameId">Internal MeepleAI SharedGame ID.</param>
    /// <param name="adminUserId">Admin user performing the import.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of new EntityLinks created.</returns>
    Task<int> ImportExpansionsAsync(
        Guid sharedGameId,
        Guid adminUserId,
        CancellationToken cancellationToken = default);
}
