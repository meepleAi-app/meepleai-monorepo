namespace Api.Infrastructure.Entities.GameManagement;

/// <summary>
/// Infrastructure entity for DiaryEntry value object.
/// #2570 SP3 T2: Flattened table for per-session diary entries in live sessions.
/// Table: live_session_diary_entries (FK → live_game_sessions, cascade delete).
/// Diary entries are append-only; no UPDATE or DELETE via the domain model.
/// </summary>
public class LiveSessionDiaryEntryEntity
{
    public Guid Id { get; set; }
    public Guid LiveGameSessionId { get; set; }
    public Guid AuthorId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string Text { get; set; } = default!;

    // Navigation Properties
    public LiveGameSessionEntity LiveGameSession { get; set; } = default!;
}
