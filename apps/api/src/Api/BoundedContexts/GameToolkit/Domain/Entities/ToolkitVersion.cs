using System.Text.RegularExpressions;
using Api.BoundedContexts.GameToolkit.Domain.Events;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.GameToolkit.Domain.Entities;

/// <summary>
/// Aggregate root representing a single published version snapshot of a
/// <see cref="GameToolkit"/>. Phase 5 schema foundation per issue #822.
/// </summary>
/// <remarks>
/// <para>
/// Multiple <c>ToolkitVersion</c> rows form the version history of one
/// <c>GameToolkit</c>. <see cref="VersionNumber"/> is owner-input semver
/// (<c>MAJOR.MINOR.PATCH</c>); the marketplace surface reads the latest
/// non-yanked row's <c>VersionNumber</c> for display.
/// </para>
/// <para>
/// Soft-delete semantics (yank): once yanked, the row stays in the table
/// for audit and installed-user reads but is filtered out of marketplace
/// listings. Re-publishing the same <see cref="VersionNumber"/> is rejected
/// by the unique index <c>(ToolkitId, VersionNumber)</c> — yanked numbers
/// are NOT reusable, by design (spec-panel 2026-05-18 §1).
/// </para>
/// </remarks>
internal sealed class ToolkitVersion : AggregateRoot<Guid>
{
    // ^\d+\.\d+\.\d+$ — strict three-part semver (no pre-release / build metadata).
    // Owners can adopt the broader spec later; v1 keeps the surface conservative
    // so the lexicographic monotonicity check below is unambiguous.
    // 1s timeout per MA0009 (ReDoS hardening) — the regex is linear so 1s is
    // generous but guarantees the analyzer is satisfied even if the call site changes.
    private static readonly Regex SemverRegex = new(
        @"^\d+\.\d+\.\d+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    public Guid ToolkitId { get; private set; }
    public string VersionNumber { get; private set; } = default!;
    public string? Changelog { get; private set; }
    public DateTime PublishedAt { get; private set; }
    public Guid PublishedBy { get; private set; }
    public DateTime? YankedAt { get; private set; }
    public string? YankReason { get; private set; }
    public Guid? YankedBy { get; private set; }

    /// <summary>True when the version has been soft-deleted via <see cref="Yank"/>.</summary>
    public bool IsYanked => YankedAt.HasValue;

    // EF Core parameterless constructor
#pragma warning disable CS8618
    private ToolkitVersion() : base() { }
#pragma warning restore CS8618

    private ToolkitVersion(
        Guid id,
        Guid toolkitId,
        string versionNumber,
        string? changelog,
        Guid publishedBy,
        DateTime publishedAt) : base(id)
    {
        ToolkitId = toolkitId;
        VersionNumber = versionNumber;
        Changelog = changelog;
        PublishedBy = publishedBy;
        PublishedAt = publishedAt;
    }

    /// <summary>
    /// Factory for a brand-new published version.
    /// </summary>
    /// <param name="toolkitId">Parent <see cref="GameToolkit"/> id.</param>
    /// <param name="versionNumber">Semver string (e.g. <c>"1.2.3"</c>).</param>
    /// <param name="changelog">Optional human-readable change description (max 4000 chars).</param>
    /// <param name="publishedBy">User id of the publishing author.</param>
    /// <param name="publishedAt">UTC timestamp of the publish action.</param>
    /// <exception cref="ArgumentException">
    /// When <paramref name="toolkitId"/> or <paramref name="publishedBy"/> is empty,
    /// or <paramref name="versionNumber"/> is not a valid 3-part semver.
    /// </exception>
    public static ToolkitVersion Publish(
        Guid toolkitId,
        string versionNumber,
        string? changelog,
        Guid publishedBy,
        DateTime publishedAt)
    {
        if (toolkitId == Guid.Empty)
            throw new ArgumentException("ToolkitId cannot be empty.", nameof(toolkitId));
        if (publishedBy == Guid.Empty)
            throw new ArgumentException("PublishedBy cannot be empty.", nameof(publishedBy));
        ValidateSemver(versionNumber);

        if (changelog is { Length: > 4000 })
            throw new ArgumentException("Changelog cannot exceed 4000 characters.", nameof(changelog));

        var trimmedChangelog = string.IsNullOrWhiteSpace(changelog) ? null : changelog.Trim();

        var version = new ToolkitVersion(
            id: Guid.NewGuid(),
            toolkitId: toolkitId,
            versionNumber: versionNumber.Trim(),
            changelog: trimmedChangelog,
            publishedBy: publishedBy,
            publishedAt: publishedAt);

        version.AddDomainEvent(new ToolkitVersionPublishedEvent(
            toolkitId: toolkitId,
            versionId: version.Id,
            versionNumber: version.VersionNumber,
            publishedBy: publishedBy));

        return version;
    }

    /// <summary>
    /// Factory used during EF Core migration backfill. Bypasses the domain
    /// event raise so the backfill does not flood the outbox with synthetic
    /// publish events for historical rows.
    /// </summary>
    internal static ToolkitVersion BackfillFromLegacy(
        Guid toolkitId,
        string versionNumber,
        Guid publishedBy,
        DateTime publishedAt)
    {
        // Skip semver regex — backfill provides "0.{int}.0" by construction;
        // the migration writes these rows directly to SQL, so this factory is
        // here for test seeding of legacy-shaped data, not the migration itself.
        if (toolkitId == Guid.Empty)
            throw new ArgumentException("ToolkitId cannot be empty.", nameof(toolkitId));
        if (publishedBy == Guid.Empty)
            throw new ArgumentException("PublishedBy cannot be empty.", nameof(publishedBy));
        if (string.IsNullOrWhiteSpace(versionNumber))
            throw new ArgumentException("VersionNumber cannot be empty.", nameof(versionNumber));

        return new ToolkitVersion(
            id: Guid.NewGuid(),
            toolkitId: toolkitId,
            versionNumber: versionNumber.Trim(),
            changelog: null,
            publishedBy: publishedBy,
            publishedAt: publishedAt);
    }

    /// <summary>
    /// Marks this version as yanked (soft-delete + audit trail). The row
    /// remains in storage; the marketplace surface filters it out via the
    /// <see cref="IsYanked"/> projection.
    /// </summary>
    /// <param name="yankedBy">User id of the yanking author (must equal toolkit owner).</param>
    /// <param name="reason">Free-text rationale (1-500 chars, required for audit).</param>
    /// <param name="yankedAt">UTC timestamp of the yank action.</param>
    /// <exception cref="ConflictException">When already yanked (idempotency boundary).</exception>
    /// <exception cref="ArgumentException">When reason is empty or exceeds 500 chars.</exception>
    public void Yank(Guid yankedBy, string reason, DateTime yankedAt)
    {
        if (IsYanked)
            throw new ConflictException($"ToolkitVersion {VersionNumber} is already yanked.");
        if (yankedBy == Guid.Empty)
            throw new ArgumentException("YankedBy cannot be empty.", nameof(yankedBy));
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Yank reason is required for audit.", nameof(reason));
        if (reason.Length > 500)
            throw new ArgumentException("Yank reason cannot exceed 500 characters.", nameof(reason));

        YankedAt = yankedAt;
        YankedBy = yankedBy;
        YankReason = reason.Trim();

        AddDomainEvent(new ToolkitVersionYankedEvent(
            toolkitId: ToolkitId,
            versionId: Id,
            versionNumber: VersionNumber,
            yankedBy: yankedBy,
            reason: YankReason));
    }

    /// <summary>
    /// Lexicographic strict-greater comparison on three semver parts.
    /// Used by <c>PublishToolkitVersionCommandHandler</c> to enforce monotonic
    /// versioning vs the previous non-yanked version (spec-panel 2026-05-18 §3).
    /// </summary>
    public static bool IsStrictlyGreater(string candidate, string previous)
    {
        ValidateSemver(candidate);
        ValidateSemver(previous);

        var c = candidate.Split('.');
        var p = previous.Split('.');
        for (int i = 0; i < 3; i++)
        {
            var ci = int.Parse(c[i], System.Globalization.CultureInfo.InvariantCulture);
            var pi = int.Parse(p[i], System.Globalization.CultureInfo.InvariantCulture);
            if (ci > pi) return true;
            if (ci < pi) return false;
        }
        return false;  // equal → not strictly greater
    }

    private static void ValidateSemver(string versionNumber)
    {
        if (string.IsNullOrWhiteSpace(versionNumber))
            throw new ArgumentException("VersionNumber cannot be empty.", nameof(versionNumber));
        if (!SemverRegex.IsMatch(versionNumber))
            throw new ArgumentException(
                $"VersionNumber '{versionNumber}' must match semver MAJOR.MINOR.PATCH (e.g. '1.2.3').",
                nameof(versionNumber));
    }
}
