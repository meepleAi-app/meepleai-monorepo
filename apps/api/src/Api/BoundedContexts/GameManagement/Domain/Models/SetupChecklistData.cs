namespace Api.BoundedContexts.GameManagement.Domain.Models;

/// <summary>
/// Aggregates components and steps for a player-count-specific game setup checklist.
/// Mutable JSONB payload class for LiveGameSession serialization.
/// </summary>
public sealed class SetupChecklistData
{
    public int PlayerCount { get; set; }
    public List<SetupComponent> Components { get; set; }
    public List<SetupStep> SetupSteps { get; set; }

    public SetupChecklistData(int playerCount, List<SetupComponent> components, List<SetupStep> setupSteps)
    {
        PlayerCount = playerCount > 0
            ? playerCount
            : throw new ArgumentException("PlayerCount must be positive", nameof(playerCount));
        Components = components ?? new List<SetupComponent>();
        SetupSteps = setupSteps ?? new List<SetupStep>();
    }

    /// <summary>
    /// Toggles the Checked state of a component at the given index.
    /// </summary>
    public void ToggleComponent(int index)
    {
        if (index < 0 || index >= Components.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        Components[index].Checked = !Components[index].Checked;
    }

    /// <summary>
    /// Marks a setup step at the given index as completed.
    /// </summary>
    public void CompleteStep(int index)
    {
        if (index < 0 || index >= SetupSteps.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        SetupSteps[index].Completed = true;
    }
}
