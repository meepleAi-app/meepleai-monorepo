namespace Api.BoundedContexts.GameToolkit.Domain.Enums;

/// <summary>
/// Zones where cards can be located during gameplay.
/// </summary>
public enum CardZone
{
    DrawPile = 0,
    DiscardPile = 1,
    PlayerHand = 2,
    TableArea = 3
}
