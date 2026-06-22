namespace Api.Infrastructure.Entities.GameToolkit;

/// <summary>
/// Persistence entity for the <c>ToolkitVersion</c> aggregate
/// (issue #822 — Phase 5 schema foundation for marketplace versioning).
/// </summary>
/// <remarks>
/// One <see cref="GameToolkitEntity"/> has many <see cref="ToolkitVersionEntity"/>
/// rows forming a publish-history. The unique index
/// <c>(ToolkitId, VersionNumber)</c> enforces "no version-number reuse" — even
/// after a yank, that number is permanently retired (spec-panel 2026-05-18 §1).
/// </remarks>
public class ToolkitVersionEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ToolkitId { get; set; }

    /// <summary>Owner-input semver string (e.g. "1.2.3"). Max 50 chars.</summary>
    public string VersionNumber { get; set; } = default!;

    /// <summary>Optional human-readable changelog. Max 4000 chars.</summary>
    public string? Changelog { get; set; }

    public DateTime PublishedAt { get; set; }
    public Guid PublishedBy { get; set; }

    // Yank audit (soft-delete)
    public DateTime? YankedAt { get; set; }
    public string? YankReason { get; set; }
    public Guid? YankedBy { get; set; }

    // Concurrency token — mirrors GameToolkitEntity.RowVersion.
    public byte[] RowVersion { get; set; } = default!;

    // Navigation
    public GameToolkitEntity? Toolkit { get; set; }
}
