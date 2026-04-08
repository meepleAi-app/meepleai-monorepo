namespace Api.BoundedContexts.GameManagement.Domain.Enums;

/// <summary>
/// Status of a scheduled game session within a game night event.
/// Corrupted: quarantine state for sessions whose persisted status cannot be parsed.
/// </summary>
public enum GameNightSessionStatus
{
    Pending = 0,
    InProgress = 1,
    Completed = 2,
    Skipped = 3,
    Corrupted = 999
}
