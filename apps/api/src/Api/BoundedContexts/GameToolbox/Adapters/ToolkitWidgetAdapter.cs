namespace Api.BoundedContexts.GameToolbox.Adapters;

/// <summary>
/// Maps existing GameToolkit widget type names to ToolboxTool display info.
/// Provides a static lookup so the frontend can show user-friendly names
/// when listing available tools from the legacy widget catalogue.
/// </summary>
internal static class ToolkitWidgetAdapter
{
    /// <summary>
    /// Display metadata for a mapped widget type.
    /// </summary>
    internal record WidgetDisplayInfo(string ToolType, string DisplayName, string Category);

    private static readonly Dictionary<string, WidgetDisplayInfo> Mappings =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["RandomGenerator"] = new("DiceRoller", "Dice Roller", "Adapted"),
            ["TurnManager"] = new("TurnManager", "Turn Manager", "Adapted"),
            ["ScoreTracker"] = new("ScoreTracker", "Score Tracker", "Adapted"),
            ["ResourceManager"] = new("ResourceManager", "Resource Manager", "Adapted"),
            ["NoteManager"] = new("Notes", "Notes", "Adapted"),
            ["Whiteboard"] = new("Whiteboard", "Whiteboard", "Adapted"),
        };

    /// <summary>
    /// Attempts to map a legacy widget type name to a ToolboxTool type.
    /// Returns null if the widget type is not recognized.
    /// </summary>
    public static WidgetDisplayInfo? Map(string widgetTypeName)
    {
        return Mappings.GetValueOrDefault(widgetTypeName);
    }

    /// <summary>
    /// Returns all known widget-to-tool mappings.
    /// </summary>
    public static IReadOnlyCollection<WidgetDisplayInfo> GetAll()
    {
        return Mappings.Values;
    }

    /// <summary>
    /// Checks whether a given widget type name has a known mapping.
    /// </summary>
    public static bool IsSupported(string widgetTypeName)
    {
        return Mappings.ContainsKey(widgetTypeName);
    }
}
