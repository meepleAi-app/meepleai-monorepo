using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameToolkit.Application.Commands.InstallToolkit;

/// <summary>
/// Command for the toolkit install action
/// (Wave 3 Phase 2, PR #732 §5.3.5 / Issue #805).
/// </summary>
/// <param name="ToolkitId">Toolkit aggregate id.</param>
/// <param name="ViewerId">
/// Authenticated caller — the user installing the toolkit. Used for
/// idempotency (PR #732 §5.3.5 Nygard note): subsequent calls by the
/// same viewer return the current state instead of raising 409.
/// </param>
/// <remarks>
/// Returns null when the toolkit is missing or yanked (endpoint maps to
/// 404). Otherwise returns <see cref="InstallToolkitResponse"/> with the
/// post-install state. Side-effect: invalidates the
/// <c>discover:popularAgents</c> Redis cache so the popularity rail
/// reflects the new install on the next read.
/// </remarks>
internal sealed record InstallToolkitCommand(
    Guid ToolkitId,
    Guid ViewerId) : ICommand<InstallToolkitResponse?>;
