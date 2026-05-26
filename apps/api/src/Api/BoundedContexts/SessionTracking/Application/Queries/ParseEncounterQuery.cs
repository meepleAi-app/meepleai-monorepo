using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SessionTracking.Application.Queries;

/// <summary>
/// Issue #1520: extract a structured encounter cheatsheet (enemies, options,
/// win/loss conditions) from a previously uploaded+segmented gamebook photo
/// segment.
///
/// Mirror of <see cref="TranslateGamebookSegmentQuery"/> in terms of ownership
/// + photo+segment lookup contract, but synchronous (no SSE) and ephemeral
/// (no persistence — the result is not cached server-side per §9.1 of the
/// runthrough spec).
///
/// <para><b>Ownership:</b> enforced via <see cref="Api.BoundedContexts.SessionTracking.Application.Services.ICampaignOwnershipGuard"/>.</para>
/// <para><b>Side effects:</b> none (pure query).</para>
/// </summary>
public sealed record ParseEncounterQuery(
    Guid CampaignId,
    Guid PhotoId,
    int ParagraphNumber,
    Guid CallerUserId,
    Guid GameBookId) : IQuery<EncounterCheatsheetDto>;
