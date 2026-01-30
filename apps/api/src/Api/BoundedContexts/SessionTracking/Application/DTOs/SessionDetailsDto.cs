namespace Api.BoundedContexts.SessionTracking.Application.DTOs;

public record SessionDetailsDto
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public Guid? GameId { get; init; }
    public string SessionCode { get; init; } = string.Empty;
    public string SessionType { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime SessionDate { get; init; }
    public string? Location { get; init; }
    public DateTime? FinalizedAt { get; init; }
    public List<ParticipantDto> Participants { get; init; } = [];
    public List<ScoreEntryDto> Scores { get; init; } = [];
    public List<PlayerNoteDto> Notes { get; init; } = [];
}

public record PlayerNoteDto
{
    public Guid Id { get; init; }
    public Guid ParticipantId { get; init; }
    public string NoteType { get; init; } = string.Empty;
    public string? TemplateKey { get; init; }
    public string Content { get; init; } = string.Empty;
    public bool IsHidden { get; init; }
    public DateTime CreatedAt { get; init; }
}
