using Api.Infrastructure.Entities.SharedGameCatalog;
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
    public SharedGameEntity? Game { get; set; }
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
    public string? DisputesJson { get; set; } // List<RuleDisputeEntry> serialized
    public string? SetupChecklistJson { get; set; } // SetupChecklistData serialized

    // Content
    public string? Notes { get; set; }

    // Turn phase configuration (added by Issue #2097 / ADR-060 schema audit)
    public string? PhaseNamesJson { get; set; } // string[] serialized as jsonb
    public int CurrentPhaseIndex { get; set; }
    public int TurnAdvancePolicy { get; set; } // TurnAdvancePolicy enum: 0=Manual, 1=AllPlayersConfirm, 2=ActivePlayerConfirms

    // Snapshot debounce state (added by Issue #2097 / ADR-060 schema audit)
    public string? SnapshotTriggerConfigJson { get; set; } // SnapshotTriggerConfig serialized as jsonb
    public DateTime? LastSnapshotTimestamp { get; set; }

    // AI Integration
    public int AgentMode { get; set; } // AgentSessionMode enum: 0=None,1=Assistant,2=GameMaster
    // chat_session_id column is retained in DB (nullable, all rows null) — domain property removed per ADR-083 SP0.

    // ADR-083 SP0: id of the SessionTracking.Session companion (cross-BC correlation bridge).
    public Guid? TrackingSessionId { get; set; }

    // Optimistic concurrency via PostgreSQL's xmin system column (Issue #2305).
    // Postgres assigns xmin = transaction-id-of-last-write per row; EF reads back via the
    // xid type-mapped uint property. Server-owned: NO mapper assignment, NO client default,
    // NO trigger maintenance.
    public uint Xmin { get; set; }

    // Navigation Properties
    public ICollection<SessionPlayerEntity> Players { get; set; } = new List<SessionPlayerEntity>();
    public ICollection<SessionTeamEntity> Teams { get; set; } = new List<SessionTeamEntity>();
    public ICollection<LiveRoundScoreEntity> RoundScores { get; set; } = new List<LiveRoundScoreEntity>();
    public ICollection<LiveTurnRecordEntity> TurnRecords { get; set; } = new List<LiveTurnRecordEntity>();
    // #2570 SP3 T2: per-session diary entries (append-only)
    public ICollection<LiveSessionDiaryEntryEntity> DiaryEntries { get; set; } = new List<LiveSessionDiaryEntryEntity>();
}
