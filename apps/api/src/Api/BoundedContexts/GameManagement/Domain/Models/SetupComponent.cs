namespace Api.BoundedContexts.GameManagement.Domain.Models;

/// <summary>
/// Represents a physical component needed for game setup.
/// Mutable JSONB payload class for LiveGameSession serialization.
/// </summary>
public sealed class SetupComponent
{
    public string Name { get; set; }
    public int Quantity { get; set; }
    public bool Checked { get; set; }

    public SetupComponent(string name, int quantity)
    {
        Name = !string.IsNullOrWhiteSpace(name)
            ? name
            : throw new ArgumentException("Name required", nameof(name));
        Quantity = quantity > 0
            ? quantity
            : throw new ArgumentException("Quantity must be positive", nameof(quantity));
        Checked = false;
    }
}
