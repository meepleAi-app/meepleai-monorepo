namespace Api.BoundedContexts.GameToolkit.Domain.Enums;

/// <summary>
/// How turn order is determined in a game session.
/// </summary>
public enum TurnOrderType
{
    RoundRobin = 0,
    Custom = 1,
    Free = 2
}
