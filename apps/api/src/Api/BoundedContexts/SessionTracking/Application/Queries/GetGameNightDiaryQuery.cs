using Api.SharedKernel.Application.Interfaces;
using DiaryEventDto = Api.BoundedContexts.SessionTracking.Application.DTOs.SessionEventDto;

namespace Api.BoundedContexts.SessionTracking.Application.Queries;

/// <summary>
/// Session Flow v2.1 — T9.
/// Reads the unified diary for an entire <c>GameNightEvent</c>, unioning events
/// from every session attached to that night. Useful for the post-night recap
/// view ("Tutto quello che è successo stasera").
/// </summary>
/// <param name="GameNightEventId">The parent game night envelope id.</param>
/// <param name="EventTypes">Optional whitelist of event types.</param>
/// <param name="Since">Optional inclusive lower bound on <c>Timestamp</c>.</param>
/// <param name="Limit">Maximum number of rows to return (default 500 — game nights are larger than single sessions).</param>
public sealed record GetGameNightDiaryQuery(
    Guid GameNightEventId,
    IReadOnlyList<string>? EventTypes,
    DateTime? Since,
    int Limit = 500
) : IQuery<IReadOnlyList<DiaryEventDto>>;
