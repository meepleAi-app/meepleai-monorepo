namespace Api.BoundedContexts.UserLibrary.Application;

internal static class HandSlotConstants
{
    public static readonly HashSet<string> ValidSlotTypes =
        new(StringComparer.OrdinalIgnoreCase) { "toolkit", "game", "session", "ai" };

    public static readonly HashSet<string> ValidEntityTypes =
        new(StringComparer.OrdinalIgnoreCase) { "toolkit", "game", "session", "agent" };

    public static readonly IReadOnlyDictionary<string, string[]> SlotEntityMap =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["toolkit"] = ["toolkit"],
            ["game"]    = ["game"],
            ["session"] = ["session"],
            ["ai"]      = ["agent"]
        };

    /// <summary>All valid slot types in display order.</summary>
    public static readonly string[] AllSlotTypes = ["toolkit", "game", "session", "ai"];
}
