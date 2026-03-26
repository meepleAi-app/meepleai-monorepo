namespace Api.BoundedContexts.AgentMemory.Application.DTOs;

/// <summary>
/// DTO representing a game's memory including house rules and notes.
/// </summary>
internal record GameMemoryDto(
    Guid Id,
    Guid GameId,
    Guid OwnerId,
    List<HouseRuleDto> HouseRules,
    List<MemoryNoteDto> Notes);

/// <summary>
/// DTO representing a single house rule.
/// </summary>
internal record HouseRuleDto(
    string Description,
    DateTime AddedAt,
    string Source);

/// <summary>
/// DTO representing a memory note attached to a game.
/// </summary>
internal record MemoryNoteDto(
    string Content,
    DateTime AddedAt);

/// <summary>
/// DTO representing a play group's collective memory.
/// </summary>
internal record GroupMemoryDto(
    Guid Id,
    string Name,
    List<GroupMemberDto> Members,
    GroupPreferencesDto? Preferences,
    GroupStatsDto? Stats);

/// <summary>
/// DTO representing a group member (registered user or guest).
/// </summary>
internal record GroupMemberDto(
    Guid? UserId,
    string? GuestName,
    DateTime JoinedAt);

/// <summary>
/// DTO representing group preferences.
/// </summary>
internal record GroupPreferencesDto(
    TimeSpan? MaxDuration,
    string? PreferredComplexity,
    string? CustomNotes);

/// <summary>
/// DTO representing group play statistics.
/// </summary>
internal record GroupStatsDto(
    int TotalSessions,
    Dictionary<Guid, int> GamePlayCounts,
    DateTime? LastPlayedAt);

/// <summary>
/// DTO representing a player's memory aggregate.
/// </summary>
internal record PlayerMemoryDto(
    Guid Id,
    Guid? GroupId,
    List<PlayerGameStatsDto> GameStats,
    DateTime? ClaimedAt);

/// <summary>
/// DTO representing a player's statistics for a specific game.
/// </summary>
internal record PlayerGameStatsDto(
    Guid GameId,
    int Wins,
    int Losses,
    int TotalPlayed,
    int? BestScore);

/// <summary>
/// DTO representing a claimable guest player.
/// </summary>
internal record ClaimableGuestDto(
    Guid PlayerMemoryId,
    string GuestName,
    Guid? GroupId,
    string? GroupName);
