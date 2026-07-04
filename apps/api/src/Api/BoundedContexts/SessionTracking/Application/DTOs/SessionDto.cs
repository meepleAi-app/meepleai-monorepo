namespace Api.BoundedContexts.SessionTracking.Application.DTOs;

public record SessionDto
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

    /// <summary>
    /// Polymorphic scoring discriminator (mirrors <see cref="Api.BoundedContexts.SessionTracking.Domain.Enums.ScoreType"/>).
    /// Null when the session has not been configured yet.
    /// </summary>
    public string? ScoringType { get; init; }

    /// <summary>
    /// Per-variant scoring payload as raw JSON (PointsScoreData, BinaryWinScoreData,
    /// ObjectivesScoreData, or RankingScoreData). FE deserialises via Zod schema.
    /// </summary>
    public string? ScoreData { get; init; }

    /// <summary>
    /// Turn mode discriminator derived from the game's published GameToolkit TurnTemplate.
    /// Mirrors <see cref="Api.BoundedContexts.GameToolkit.Domain.Enums.TurnOrderType"/>.
    /// Null when the session has no associated game or the game has no published toolkit
    /// with a TurnTemplate configured. Static for the session lifetime — never changes
    /// mid-session (no SignalR event needed). Issue #2483.
    /// </summary>
    public string? TurnOrderType { get; init; }
}

public record ParticipantDto
{
    public Guid Id { get; init; }
    public Guid? UserId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public bool IsOwner { get; init; }
    public int JoinOrder { get; init; }
    public int? FinalRank { get; init; }
    public decimal TotalScore { get; init; }
}

public record ScoreEntryDto
{
    public Guid Id { get; init; }
    public Guid ParticipantId { get; init; }
    public int? RoundNumber { get; init; }
    public string? Category { get; init; }
    public decimal ScoreValue { get; init; }
    public DateTime Timestamp { get; init; }
}

public record SessionSummaryDto
{
    public Guid Id { get; init; }
    public string SessionCode { get; init; } = string.Empty;
    public DateTime SessionDate { get; init; }
    public string? GameName { get; init; }
    public string? GameIcon { get; init; }
    public string ParticipantsNames { get; init; } = string.Empty;
    public string? WinnerName { get; init; }
    public int DurationMinutes { get; init; }
    public string Status { get; init; } = string.Empty;
}

public record ScoreboardDto
{
    public Guid SessionId { get; init; }
    public List<ParticipantScoreDto> Participants { get; init; } = [];
    public Dictionary<int, Dictionary<Guid, decimal>> ScoresByRound { get; init; } = [];
    public Dictionary<string, Dictionary<Guid, decimal>> ScoresByCategory { get; init; } = [];
    public Guid? CurrentLeaderId { get; init; }
}

public record ParticipantScoreDto
{
    public Guid ParticipantId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public decimal TotalScore { get; init; }
    public int CurrentRank { get; init; }
}
