namespace Api.BoundedContexts.GameManagement.Domain.ValueObjects;

[Flags]
public enum GameBookRole
{
    None = 0,
    Tutorial = 1,
    RulesReference = 2,
    Narrative = 4,
    Encounter = 8,
    Lore = 16,
    Setup = 32,
}
