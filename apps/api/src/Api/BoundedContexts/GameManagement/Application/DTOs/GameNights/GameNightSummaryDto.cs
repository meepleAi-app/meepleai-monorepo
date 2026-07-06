using Api.BoundedContexts.GameManagement.Domain.Enums;

namespace Api.BoundedContexts.GameManagement.Application.DTOs.GameNights;

/// <summary>
/// Post-finalize summary of a game night — Issue #2702: header, computed MVP + KPIs, and a
/// per-game recap. <c>ShareToken</c> is only populated for the organiser.
/// </summary>
public sealed record GameNightSummaryDto(
    Guid Id,
    string Title,
    GameNightStatus Status,
    DateTimeOffset ScheduledAt,
    string? Location,
    bool IsViewerOrganizer,
    bool IsArchived,
    bool IsShared,
    string? ShareToken,
    GameNightMvpDto? Mvp,
    GameNightSummaryKpisDto Kpis,
    IReadOnlyList<GameNightGameRecapDto> Games);

/// <summary>The night's most valuable player: the winner name with the most session wins.</summary>
public sealed record GameNightMvpDto(string PlayerName, int Wins);

/// <summary>Aggregated night KPIs.</summary>
public sealed record GameNightSummaryKpisDto(
    int TotalGames,
    int CompletedGames,
    int TotalDurationMinutes,
    int WinnersCount);

/// <summary>Response for a generated summary share token — Issue #2702.</summary>
public sealed record GameNightShareLinkDto(string ShareToken);

/// <summary>Per-game recap row.</summary>
public sealed record GameNightGameRecapDto(
    Guid SessionId,
    Guid GameId,
    string GameTitle,
    int PlayOrder,
    GameNightSessionStatus Status,
    Guid? WinnerId,
    string? WinnerName,
    int? DurationMinutes,
    int EventsCount);
