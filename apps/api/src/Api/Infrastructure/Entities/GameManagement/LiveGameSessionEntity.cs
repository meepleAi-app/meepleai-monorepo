using Api.Infrastructure.Entities.Authentication;

namespace Api.Infrastructure.Entities.GameManagement;

/// <summary>
/// Infrastructure entity for LiveGameSession aggregate.
/// Issue #4750: Maps domain LiveGameSession to database table.
/// </summary>
public class LiveGameSessionEntity
{
    public Guid Id { get; set; }

    // Session Identity
    public string SessionCode { get; set; } = default!;

    // Game Association (Optional - free-form sessions have null GameId)
    public Guid? GameId { get; set; }
    public string GameName { get; set; } = default!;
    public GameEntity? Game { get; set; }
    public Guid? ToolkitId { get; set; }

    // Ownership & Permissions
    public Guid CreatedByUserId { get; set; }
    public UserEntity? CreatedByUser { get; set; }
    public int Visibility { get; set; } // 0=Private, 1=Group (PlayRecordVisibility enum)
    public Guid? GroupId { get; set; }

    // Session State
    public int Status { get; set; } // LiveSessionStatus enum: 0=Created,1=Setup,2=InProgress,3=Paused,4=Completed
    public int CurrentTurnIndex { get; set; }

    // Timestamps
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? PausedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? LastSavedAt { get; set; }

    // Issue #216: Server-side timer for live session reliability
    public long TotalPausedDurationMs { get; set; }

    // Configuration (stored as JSON)
    public string ScoringConfigJson { get; set; } = default!;
    public string? GameStateJson { get; set; } // Free-form game state
    public string? TurnOrderJson { get; set; } // List<Guid> serialized

    // Content
    public string? Notes { get; set; }

    // AI Integration
    public int AgentMode { get; set; } // AgentSessionMode enum: 0=None,1=Assistant,2=GameMaster
    public Guid? ChatSessionId { get; set; }

    // Concurrency
    public byte[] RowVersion { get; set; } = default!;

    // Navigation Properties
    public ICollection<SessionPlayerEntity> Players { get; set; } = new List<SessionPlayerEntity>();
    public ICollection<SessionTeamEntity> Teams { get; set; } = new List<SessionTeamEntity>();
    public ICollection<LiveRoundScoreEntity> RoundScores { get; set; } = new List<LiveRoundScoreEntity>();
    public ICollection<LiveTurnRecordEntity> TurnRecords { get; set; } = new List<LiveTurnRecordEntity>();
}
