namespace Api.BoundedContexts.EntityRelationships.Domain.Enums;

/// <summary>
/// Identifies the type of a MeepleAI entity that can participate in an EntityLink.
/// </summary>
public enum MeepleEntityType
{
    Game = 1,
    Player = 2,
    Session = 3,
    Agent = 4,
    Document = 5,
    ChatSession = 6,
    Event = 7,
    Toolkit = 8
}
