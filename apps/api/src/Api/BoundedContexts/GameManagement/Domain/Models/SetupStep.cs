namespace Api.BoundedContexts.GameManagement.Domain.Models;

/// <summary>
/// Represents a single step in the game setup process.
/// Mutable JSONB payload class for LiveGameSession serialization.
/// </summary>
public sealed class SetupStep
{
    public int Order { get; set; }
    public string Instruction { get; set; }
    public bool Completed { get; set; }

    public SetupStep(int order, string instruction)
    {
        Order = order;
        Instruction = !string.IsNullOrWhiteSpace(instruction)
            ? instruction
            : throw new ArgumentException("Instruction required", nameof(instruction));
        Completed = false;
    }
}
