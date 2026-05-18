using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameToolkit.Application.Commands.PublishToolkitVersion;

/// <summary>
/// Publishes a new <c>ToolkitVersion</c> for an existing toolkit
/// (issue #822 — Phase 5 PR-2 / spec-panel 2026-05-18 §3).
/// </summary>
/// <param name="ToolkitId">Parent toolkit id.</param>
/// <param name="ViewerId">
/// Authenticated caller. Must equal <c>GameToolkit.CreatedByUserId</c>
/// (server-side ownership enforcement) — non-owner caller returns 403.
/// </param>
/// <param name="VersionNumber">
/// Owner-input semver string (regex <c>^\d+\.\d+\.\d+$</c>). Must be strictly
/// greater than the latest non-yanked version. Duplicate values (including
/// yanked ones) return 409 Conflict — version numbers are permanently retired
/// per spec-panel §1.
/// </param>
/// <param name="Changelog">
/// Optional human-readable changelog (max 4000 chars).
/// </param>
/// <remarks>
/// Returns <c>null</c> when the toolkit does not exist (endpoint → 404).
/// Throws <c>ForbiddenException</c> when the caller is not the owner.
/// Throws <c>ConflictException</c> on duplicate or non-monotonic version.
/// On success, raises <c>ToolkitVersionPublishedEvent</c> via the aggregate.
/// </remarks>
internal sealed record PublishToolkitVersionCommand(
    Guid ToolkitId,
    Guid ViewerId,
    string VersionNumber,
    string? Changelog) : ICommand<PublishedToolkitVersionResponse?>;

/// <summary>Wire response for the publish endpoint (201 Created body).</summary>
internal sealed record PublishedToolkitVersionResponse(
    Guid Id,
    Guid ToolkitId,
    string VersionNumber,
    string? Changelog,
    DateTime PublishedAt,
    Guid PublishedBy);
