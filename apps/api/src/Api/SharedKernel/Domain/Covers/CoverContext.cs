namespace Api.SharedKernel.Domain.Covers;

/// <summary>
/// The UI surfaces for which an admin can assign a distinct game cover
/// (epic #3470, Slice 0 SD1). Each context has a target aspect ratio; the
/// rendered crop is produced from the chosen source with a configurable
/// focal point (SD2).
/// </summary>
public enum CoverContext
{
    /// <summary>Portrait card ~2:3 — catalog grid, list, thumbnail.</summary>
    Card = 0,

    /// <summary>Landscape ~16:9 — detail hero, featured, hub banner.</summary>
    Hero = 1,

    /// <summary>OpenGraph social share ~1.91:1 (1200×630).</summary>
    Social = 2,
}
