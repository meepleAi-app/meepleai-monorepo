using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameToolkit.Application.Commands.YankToolkitVersion;

/// <summary>
/// Yanks (soft-deletes) a previously published <c>ToolkitVersion</c>
/// (issue #822 — Phase 5 PR-2 / spec-panel 2026-05-18 §1 + §4).
/// </summary>
/// <param name="ToolkitId">Parent toolkit id (route).</param>
/// <param name="VersionId">Version row to yank (route).</param>
/// <param name="ViewerId">
/// Authenticated caller. Must equal <c>GameToolkit.CreatedByUserId</c>
/// (server-side ownership enforcement) — non-owner caller returns 403.
/// </param>
/// <param name="Reason">
/// Free-text yank rationale (1-500 chars, required for audit). Stored on
/// the version row's <c>YankReason</c> column.
/// </param>
/// <remarks>
/// <para>
/// Returns <c>null</c> when the toolkit or the version is missing or when
/// the version is not a child of the supplied toolkit (endpoint → 404).
/// Throws <c>ForbiddenException</c> if the caller is not the owner.
/// Throws <c>ConflictException</c> if the version is already yanked.
/// </para>
/// <para>
/// Cascade rule (spec-panel §1): when this yank leaves the toolkit with zero
/// non-yanked versions, <c>GameToolkit.IsPublished</c> flips to <c>false</c>
/// in the SAME transaction so the marketplace surface stops showing a
/// "published but no versions" half-state.
/// </para>
/// </remarks>
internal sealed record YankToolkitVersionCommand(
    Guid ToolkitId,
    Guid VersionId,
    Guid ViewerId,
    string Reason) : ICommand<YankedToolkitVersionResponse?>;

/// <summary>Wire response for the yank endpoint (200 OK body).</summary>
internal sealed record YankedToolkitVersionResponse(
    Guid Id,
    Guid ToolkitId,
    string VersionNumber,
    DateTime YankedAt,
    string Reason,
    bool ToolkitAutoUnpublished);
