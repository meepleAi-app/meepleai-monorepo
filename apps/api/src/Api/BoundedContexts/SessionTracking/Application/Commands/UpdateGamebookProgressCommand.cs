using Api.BoundedContexts.SessionTracking.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// Advances or navigates the per-book progress marker for a gamebook campaign.
/// C2 (2026-05-19): now scoped by <paramref name="GameBookId"/> to support
/// campaigns spanning multiple books (one progress row per (campaign, book) pair).
/// </summary>
public sealed record UpdateGamebookProgressCommand(
    Guid CampaignId,
    Guid CallerUserId,
    Guid GameBookId,
    int CurrentParagraph)
    : IRequest<GamebookCampaignDto>;
