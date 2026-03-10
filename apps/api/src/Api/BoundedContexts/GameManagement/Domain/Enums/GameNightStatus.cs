namespace Api.BoundedContexts.GameManagement.Domain.Enums;

/// <summary>
/// Status of a game night event.
/// Issue #42: GameNightEvent + GameNightRsvp domain entities.
/// </summary>
public enum GameNightStatus
{
    Draft = 0,
    Published = 1,
    Cancelled = 2,
    Completed = 3
}
