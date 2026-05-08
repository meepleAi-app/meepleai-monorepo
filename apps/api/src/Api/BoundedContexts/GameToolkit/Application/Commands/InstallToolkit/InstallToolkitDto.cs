namespace Api.BoundedContexts.GameToolkit.Application.Commands.InstallToolkit;

/// <summary>
/// Wire response for the toolkit install endpoint
/// (Wave 3 Phase 2, PR #732 §5.3.5 / Issue #805).
/// </summary>
/// <param name="InstallCount">
/// Total cumulative install count after this call. Schema reality v1
/// carryover (Gate B): no <c>ToolkitInstallation</c> entity, so the value
/// is always <c>0</c> until Phase 4 wires the schema.
/// </param>
/// <param name="HasInstalled">
/// Always <c>true</c> on success — the install command is idempotent
/// (PR #732 §5.3.5 Nygard note); subsequent calls return the same flag
/// without raising 409.
/// </param>
internal sealed record InstallToolkitResponse(
    int InstallCount,
    bool HasInstalled);
