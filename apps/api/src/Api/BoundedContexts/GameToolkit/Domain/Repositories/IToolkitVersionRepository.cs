using Api.BoundedContexts.GameToolkit.Domain.Entities;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.GameToolkit.Domain.Repositories;

/// <summary>
/// Repository contract for the <see cref="ToolkitVersion"/> aggregate.
/// Phase 5 schema foundation per issue #822.
/// </summary>
internal interface IToolkitVersionRepository : IRepository<ToolkitVersion, Guid>
{
    /// <summary>
    /// Returns the version history for a toolkit sorted by <c>PublishedAt DESC</c>.
    /// </summary>
    /// <param name="toolkitId">Parent toolkit id.</param>
    /// <param name="includeYanked">
    /// When <c>false</c> (default for marketplace reads), yanked rows are filtered out.
    /// When <c>true</c> (audit / owner-detail), all rows are returned.
    /// </param>
    /// <param name="cancellationToken">Standard cancellation token.</param>
    Task<IReadOnlyList<ToolkitVersion>> GetByToolkitIdAsync(
        Guid toolkitId,
        bool includeYanked = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the latest non-yanked version for a toolkit, or <c>null</c> if there is none.
    /// Used by <c>PublishToolkitVersionCommandHandler</c> for monotonicity checks
    /// and by the marketplace surface to render the "current" version label.
    /// </summary>
    Task<ToolkitVersion?> GetLatestNonYankedAsync(
        Guid toolkitId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns <c>true</c> if a row with the given <c>(toolkitId, versionNumber)</c>
    /// already exists (irrespective of yank state). Used by the publish command
    /// to surface a 409 Conflict before insert rather than relying on the DB
    /// unique-index violation.
    /// </summary>
    Task<bool> ExistsAsync(
        Guid toolkitId,
        string versionNumber,
        CancellationToken cancellationToken = default);
}
