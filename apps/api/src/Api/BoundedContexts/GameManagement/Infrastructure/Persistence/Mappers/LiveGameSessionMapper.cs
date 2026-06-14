using System.Text.Json;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Models;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Infrastructure.Entities.GameManagement;

namespace Api.BoundedContexts.GameManagement.Infrastructure.Persistence.Mappers;

/// <summary>
/// Bidirectional mapper between Domain <see cref="LiveGameSession"/> and Infrastructure
/// <see cref="LiveGameSessionEntity"/>. Owns JSON serialization for jsonb columns
/// (ScoringConfigJson, GameStateJson, TurnOrderJson, DisputesJson, SetupChecklistJson,
/// PhaseNamesJson, SnapshotTriggerConfigJson) and round-trips 4 child collections.
/// Issue #2097 / ADR-060.
/// </summary>
internal static class LiveGameSessionMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static LiveGameSessionEntity ToEntity(LiveGameSession domain)
    {
        ArgumentNullException.ThrowIfNull(domain);

        var entity = new LiveGameSessionEntity
        {
            Id = domain.Id,
            SessionCode = domain.SessionCode,
            GameId = domain.GameId,
            GameName = domain.GameName,
            ToolkitId = domain.ToolkitId,
            CreatedByUserId = domain.CreatedByUserId,
            Visibility = (int)domain.Visibility,
            GroupId = domain.GroupId,
            Status = (int)domain.Status,
            CurrentTurnIndex = domain.CurrentTurnIndex,
            CurrentPhaseIndex = domain.CurrentPhaseIndex,
            TurnAdvancePolicy = (int)domain.TurnAdvancePolicy,
            CreatedAt = domain.CreatedAt,
            StartedAt = domain.StartedAt,
            PausedAt = domain.PausedAt,
            CompletedAt = domain.CompletedAt,
            UpdatedAt = domain.UpdatedAt,
            LastSavedAt = domain.LastSavedAt,
            LastSnapshotTimestamp = domain.LastSnapshotTimestamp,
            ScoringConfigJson = SerializeScoringConfig(domain.ScoringConfig),
            GameStateJson = domain.GameState?.RootElement.GetRawText(),
            TurnOrderJson = JsonSerializer.Serialize(domain.TurnOrder, JsonOptions),
            DisputesJson = domain.Disputes.Count > 0
                ? JsonSerializer.Serialize(domain.Disputes, JsonOptions)
                : null,
            SetupChecklistJson = domain.SetupChecklist != null
                ? JsonSerializer.Serialize(domain.SetupChecklist, JsonOptions)
                : null,
            PhaseNamesJson = domain.PhaseNames.Length > 0
                ? JsonSerializer.Serialize(domain.PhaseNames, JsonOptions)
                : null,
            SnapshotTriggerConfigJson = domain.SnapshotTriggerConfig != null
                ? SerializeSnapshotTriggerConfig(domain.SnapshotTriggerConfig)
                : null,
            Notes = domain.Notes,
            AgentMode = (int)domain.AgentMode,
            ChatSessionId = domain.ChatSessionId,
            // RowVersion is ValueGeneratedOnAddOrUpdate backed by a Postgres trigger
            // (see migration LiveSessionRowVersionTrigger, Issue #2097).
            // On INSERT: pass Array.Empty<byte>() — the trigger overwrites it with clock_timestamp().
            // On UPDATE: pass the stale token from Domain so EF can enforce optimistic concurrency.
            RowVersion = domain.RowVersion.Length > 0 ? domain.RowVersion : Array.Empty<byte>()
        };

        // Child collections — relational tables, not jsonb.
        // SessionPlayerEntity.Color and .Role are stored as enum name strings.
        foreach (var player in domain.Players)
        {
            entity.Players.Add(new SessionPlayerEntity
            {
                Id = player.Id,
                LiveGameSessionId = domain.Id,
                UserId = player.UserId,
                DisplayName = player.DisplayName,
                Color = player.Color.ToString(),
                Role = player.Role.ToString(),
                TeamId = player.TeamId,
                AvatarUrl = player.AvatarUrl,
                IsActive = player.IsActive,
                TotalScore = player.TotalScore,
                CurrentRank = player.CurrentRank,
                JoinedAt = player.JoinedAt
            });
        }

        foreach (var team in domain.Teams)
        {
            entity.Teams.Add(new SessionTeamEntity
            {
                Id = team.Id,
                LiveGameSessionId = domain.Id,
                Name = team.Name,
                Color = team.Color,
                TeamScore = team.TeamScore,
                CurrentRank = team.CurrentRank
            });
        }

        foreach (var score in domain.RoundScores)
        {
            entity.RoundScores.Add(new LiveRoundScoreEntity
            {
                // Deterministic Id derived from the unique tuple
                // (sessionId, playerId, round, dimension) — matches the unique index
                // ix_live_round_scores_session_player_round_dim. This keeps the PK
                // stable across repeated ToEntity calls so EF treats subsequent
                // UpdateAsync as UPDATE rather than INSERT (which would dup-key crash).
                Id = DeterministicScoreId(domain.Id, score.PlayerId, score.Round, score.Dimension),
                LiveGameSessionId = domain.Id,
                PlayerId = score.PlayerId,
                Round = score.Round,
                Dimension = score.Dimension,
                Value = score.Value,
                Unit = score.Unit,
                RecordedAt = score.RecordedAt
            });
        }

        foreach (var record in domain.TurnRecords)
        {
            entity.TurnRecords.Add(new LiveTurnRecordEntity
            {
                // Deterministic Id derived from (sessionId, turnIndex) — matches the
                // unique index ix_live_turn_records_session_turn. Same rationale as
                // RoundScore above: stable PK across re-mapping.
                Id = DeterministicTurnId(domain.Id, record.TurnIndex),
                LiveGameSessionId = domain.Id,
                TurnIndex = record.TurnIndex,
                PlayerId = record.PlayerId,
                StartedAt = record.StartedAt,
                EndedAt = record.EndedAt,
                PhaseIndex = record.PhaseIndex,
                PhaseName = record.PhaseName
            });
        }

        return entity;
    }

    public static LiveGameSession ToDomain(LiveGameSessionEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var scoringConfig = DeserializeScoringConfig(entity.ScoringConfigJson);

        var turnOrder = string.IsNullOrEmpty(entity.TurnOrderJson)
            ? Array.Empty<Guid>()
            : (JsonSerializer.Deserialize<List<Guid>>(entity.TurnOrderJson, JsonOptions) ?? new List<Guid>()).ToArray();

        var disputes = string.IsNullOrEmpty(entity.DisputesJson)
            ? Array.Empty<RuleDisputeEntry>()
            : (JsonSerializer.Deserialize<List<RuleDisputeEntry>>(entity.DisputesJson, JsonOptions)
                ?? new List<RuleDisputeEntry>()).ToArray();

        var setupChecklist = string.IsNullOrEmpty(entity.SetupChecklistJson)
            ? null
            : JsonSerializer.Deserialize<SetupChecklistData>(entity.SetupChecklistJson, JsonOptions);

        var phaseNames = string.IsNullOrEmpty(entity.PhaseNamesJson)
            ? Array.Empty<string>()
            : JsonSerializer.Deserialize<string[]>(entity.PhaseNamesJson, JsonOptions) ?? Array.Empty<string>();

        var snapshotTriggerConfig = string.IsNullOrEmpty(entity.SnapshotTriggerConfigJson)
            ? null
            : DeserializeSnapshotTriggerConfig(entity.SnapshotTriggerConfigJson);

        JsonDocument? gameState = string.IsNullOrEmpty(entity.GameStateJson)
            ? null
            : JsonDocument.Parse(entity.GameStateJson);

        // SessionPlayerEntity.Color and .Role are stored as enum name strings.
        var players = entity.Players.Select(p => new LiveSessionPlayer(
            id: p.Id,
            liveGameSessionId: entity.Id,
            userId: p.UserId,
            displayName: p.DisplayName,
            color: Enum.Parse<PlayerColor>(p.Color, ignoreCase: true),
            role: Enum.Parse<PlayerRole>(p.Role, ignoreCase: true),
            joinedAt: p.JoinedAt,
            avatarUrl: p.AvatarUrl)).ToList();

        // Restore mutable player state (score / rank / team / active) — these aren't ctor params
        foreach (var entityPlayer in entity.Players)
        {
            var domainPlayer = players.First(dp => dp.Id == entityPlayer.Id);
            domainPlayer.UpdateScore(entityPlayer.TotalScore, entityPlayer.CurrentRank);
            if (entityPlayer.TeamId.HasValue) domainPlayer.AssignToTeam(entityPlayer.TeamId.Value);
            if (!entityPlayer.IsActive) domainPlayer.Deactivate();
        }

        var teams = entity.Teams.Select(t => new LiveSessionTeam(
            id: t.Id,
            liveGameSessionId: entity.Id,
            name: t.Name,
            color: t.Color)).ToList();

        // Restore mutable team state (score, rank) — these aren't ctor params.
        // Also rebuild Team._playerIds from the Players' TeamId pointers — the ctor
        // initializes the list empty and the domain reassignment path expects
        // oldTeam._playerIds.Contains(playerId) to be true (LiveSessionTeam.RemovePlayer
        // throws otherwise).
        foreach (var entityTeam in entity.Teams)
        {
            var domainTeam = teams.First(dt => dt.Id == entityTeam.Id);
            domainTeam.UpdateScore(entityTeam.TeamScore, entityTeam.CurrentRank);
        }
        foreach (var entityPlayer in entity.Players)
        {
            if (entityPlayer.TeamId is not Guid teamId) continue;
            var domainTeam = teams.FirstOrDefault(dt => dt.Id == teamId);
            domainTeam?.AddPlayer(entityPlayer.Id);
        }

        var roundScores = entity.RoundScores.Select(s => new RoundScore(
            playerId: s.PlayerId,
            round: s.Round,
            dimension: s.Dimension,
            value: s.Value,
            recordedAt: s.RecordedAt,
            unit: s.Unit)).ToList();

        var turnRecords = entity.TurnRecords.Select(t => new TurnRecord(
            turnIndex: t.TurnIndex,
            playerId: t.PlayerId,
            startedAt: t.StartedAt,
            phaseIndex: t.PhaseIndex,
            phaseName: t.PhaseName,
            endedAt: t.EndedAt)).ToList();

        return LiveGameSession.Reconstitute(
            id: entity.Id,
            sessionCode: entity.SessionCode,
            gameId: entity.GameId,
            gameName: entity.GameName,
            toolkitId: entity.ToolkitId,
            createdByUserId: entity.CreatedByUserId,
            visibility: (PlayRecordVisibility)entity.Visibility,
            groupId: entity.GroupId,
            status: (LiveSessionStatus)entity.Status,
            createdAt: entity.CreatedAt,
            startedAt: entity.StartedAt,
            pausedAt: entity.PausedAt,
            completedAt: entity.CompletedAt,
            updatedAt: entity.UpdatedAt,
            lastSavedAt: entity.LastSavedAt,
            currentTurnIndex: entity.CurrentTurnIndex,
            currentPhaseIndex: entity.CurrentPhaseIndex,
            phaseNames: phaseNames,
            snapshotTriggerConfig: snapshotTriggerConfig,
            lastSnapshotTimestamp: entity.LastSnapshotTimestamp,
            scoringConfig: scoringConfig,
            gameState: gameState,
            notes: entity.Notes,
            agentMode: (AgentSessionMode)entity.AgentMode,
            chatSessionId: entity.ChatSessionId,
            turnAdvancePolicy: (TurnAdvancePolicy)entity.TurnAdvancePolicy,
            rowVersion: entity.RowVersion,
            players: players,
            teams: teams,
            turnOrder: turnOrder,
            roundScores: roundScores,
            turnRecords: turnRecords,
            disputes: disputes,
            setupChecklist: setupChecklist);
    }

    // ── Serialization helpers for types not directly deserializable by STJ ──

    /// <summary>
    /// Intermediate POCO for serializing/deserializing <see cref="SessionScoringConfig"/>
    /// whose constructor takes positional IEnumerable/IDictionary params (no parameterless ctor).
    /// </summary>
    private sealed record ScoringConfigDto(
        List<string> EnabledDimensions,
        Dictionary<string, string> DimensionUnits);

    private static string SerializeScoringConfig(SessionScoringConfig config)
    {
        var dto = new ScoringConfigDto(
            config.EnabledDimensions.ToList(),
            config.DimensionUnits.ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase));
        return JsonSerializer.Serialize(dto, JsonOptions);
    }

    private static SessionScoringConfig DeserializeScoringConfig(string json)
    {
        if (string.IsNullOrEmpty(json))
            return SessionScoringConfig.CreateDefault();

        var dto = JsonSerializer.Deserialize<ScoringConfigDto>(json, JsonOptions);
        if (dto == null) return SessionScoringConfig.CreateDefault();

        return new SessionScoringConfig(dto.EnabledDimensions, dto.DimensionUnits);
    }

    /// <summary>
    /// Intermediate POCO for serializing/deserializing <see cref="SnapshotTriggerConfig"/>
    /// which has read-only collection properties.
    /// </summary>
    private sealed record SnapshotTriggerConfigDto(
        List<SnapshotTrigger> EnabledTriggers,
        int DebounceDurationSeconds,
        int MaxSnapshotsPerMinute);

    private static string SerializeSnapshotTriggerConfig(SnapshotTriggerConfig config)
    {
        var dto = new SnapshotTriggerConfigDto(
            config.EnabledTriggers.ToList(),
            config.DebounceDurationSeconds,
            config.MaxSnapshotsPerMinute);
        return JsonSerializer.Serialize(dto, JsonOptions);
    }

    private static SnapshotTriggerConfig? DeserializeSnapshotTriggerConfig(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;

        var dto = JsonSerializer.Deserialize<SnapshotTriggerConfigDto>(json, JsonOptions);
        if (dto == null) return null;

        return new SnapshotTriggerConfig(dto.EnabledTriggers, dto.DebounceDurationSeconds, dto.MaxSnapshotsPerMinute);
    }

    // ── Deterministic PK helpers for child rows that have no domain-side Id ──
    //
    // RoundScore and TurnRecord are value objects in the Domain (no Id). The Entity tables
    // need PKs. Using Guid.NewGuid() on every ToEntity() call would make every UpdateAsync
    // generate fresh PKs, which causes EF to issue UPDATEs against non-existent rows on
    // the first save, and duplicate-key crashes on subsequent saves. Deriving the PK
    // deterministically from the row's unique tuple keeps it stable across calls so EF's
    // change tracker recognises existing rows. The tuple matches the unique index
    // configured on each child table.

    private static Guid DeterministicScoreId(Guid sessionId, Guid playerId, int round, string dimension)
        => DeterministicGuid($"score:{sessionId:N}:{playerId:N}:{round}:{dimension.ToLowerInvariant()}");

    private static Guid DeterministicTurnId(Guid sessionId, int turnIndex)
        => DeterministicGuid($"turn:{sessionId:N}:{turnIndex}");

    private static Guid DeterministicGuid(string seed)
    {
        Span<byte> hash = stackalloc byte[32];
        System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(seed), hash);
        return new Guid(hash[..16]);
    }
}
