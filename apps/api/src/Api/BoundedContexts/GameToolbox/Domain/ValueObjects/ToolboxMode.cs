namespace Api.BoundedContexts.GameToolbox.Domain.ValueObjects;

/// <summary>
/// Defines the operational mode of a Toolbox.
/// </summary>
public enum ToolboxMode
{
    /// <summary>
    /// Tools operate independently with shared context.
    /// </summary>
    Freeform = 0,

    /// <summary>
    /// Tools activate/deactivate based on the current phase.
    /// </summary>
    Phased = 1
}
