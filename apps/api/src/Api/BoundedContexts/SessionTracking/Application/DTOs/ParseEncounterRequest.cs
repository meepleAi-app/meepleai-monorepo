namespace Api.BoundedContexts.SessionTracking.Application.DTOs;

/// <summary>
/// Issue #1520 — request body for <c>POST /gamebook/campaigns/{campaignId}/photos/{photoId}/encounter-parse</c>.
/// <c>campaignId</c> and <c>photoId</c> are supplied via the route, the caller's
/// user id from the authenticated session; only paragraph + book remain in the
/// body.
/// </summary>
public sealed record ParseEncounterRequest(
    int ParagraphNumber,
    Guid GameBookId);
