using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Infrastructure.Entities.UserLibrary;

namespace Api.Infrastructure.Entities.GameToolkit;

/// <summary>
/// Persistence entity for GameToolkit aggregate.
/// Tool configs stored as JSONB columns for flexibility.
/// </summary>
public class GameToolkitEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? GameId { get; set; }
    public Guid? PrivateGameId { get; set; }
    public string Name { get; set; } = default!;

    /// <summary>
    /// Legacy integer version counter (pre-#1144). Retained for unchanged
    /// runtime callers (composite uniqueness index <c>(GameId, Version)</c>)
    /// until the cleanup PR scheduled in spec §13 lands.
    /// </summary>
    /// <remarks>
    /// Issue #1144 / spec D-5: marketplace surface reads <see cref="VersionSemver"/>.
    /// Reads remain fully supported. The setter is marked <see cref="ObsoleteAttribute"/>
    /// so any new write path that mutates <see cref="Version"/> without also
    /// updating <see cref="VersionSemver"/> in the same <c>SaveChangesAsync</c>
    /// call surfaces a build-time warning (escalated to error by the
    /// repository-scan drift-prevention test).
    /// </remarks>
    public int Version
    {
        get => _version;
#pragma warning disable S1133 // Removal scheduled in spec §13 cleanup PR (post-marketplace migration).
        [Obsolete("Issue #1144 / spec D-5: writes MUST also update VersionSemver in the same SaveChangesAsync. Removal scheduled per spec §13.")]
#pragma warning restore S1133
        set => _version = value;
    }
    private int _version = 1;
    public Guid CreatedByUserId { get; set; }
    public bool IsPublished { get; set; }
    public bool OverridesTurnOrder { get; set; }
    public bool OverridesScoreboard { get; set; }
    public bool OverridesDiceSet { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // JSONB columns for tool configs
    public string? DiceToolsJson { get; set; }
    public string? CardToolsJson { get; set; }
    public string? TimerToolsJson { get; set; }
    public string? CounterToolsJson { get; set; }
    public string? UserDicePresetsJson { get; set; }

    // JSONB columns for templates
    public string? ScoringTemplateJson { get; set; }
    public string? TurnTemplateJson { get; set; }
    public string? StateTemplate { get; set; }
    public string? AgentConfig { get; set; }

    // Template marketplace
    public int TemplateStatus { get; set; }
    public bool IsTemplate { get; set; }
    public string? ReviewNotes { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }

    // Issue #1144 / spec §5.2 — Stage 3 marketplace extension fields
    /// <summary>Free-form author description. Nullable; falls back to a synthetic preview when null. Max 2000 chars.</summary>
    public string? Description { get; set; }
    /// <summary>SPDX-like license string (e.g. "CC BY-SA 4.0"). Nullable. Max 200 chars.</summary>
    public string? License { get; set; }
    /// <summary>SemVer-shaped version string (e.g. "2.0.0"). Source of truth for the marketplace surface. Default "0.1.0". Max 50 chars.</summary>
    public string VersionSemver { get; set; } = "0.1.0";

    // Concurrency
    public byte[] RowVersion { get; set; } = default!;

    // Navigation properties
    public SharedGameEntity? Game { get; set; }
    public PrivateGameEntity? PrivateGame { get; set; }
}
