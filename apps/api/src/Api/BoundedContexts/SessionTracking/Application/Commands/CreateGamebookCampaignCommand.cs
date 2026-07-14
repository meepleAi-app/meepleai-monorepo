using Api.BoundedContexts.SessionTracking.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// #2917 (decision #2759 Option A): optional roster fields. When present, the handler spins up a
/// non-live Session carrying these participants (owner auto-seeded) + guest names. Optional/nullable
/// for backward-compat with callers that only create the campaign.
/// </summary>
public sealed record CreateGamebookCampaignCommand(
    Guid GameId,
    Guid OwnerUserId,
    string Title,
    IReadOnlyList<ParticipantDto>? Participants = null,
    IReadOnlyList<string>? GuestNames = null)
    : IRequest<GamebookCampaignDto>;
