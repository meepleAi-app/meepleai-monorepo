namespace Api.BoundedContexts.Authentication.Application.DTOs;

/// <summary>
/// Result of a batch invitation operation containing succeeded and failed items.
/// Issue #124: Admin invitation flow — batch provisioning.
/// </summary>
public sealed record BatchInvitationResultDto(
    List<InvitationDto> Succeeded,
    List<BatchInvitationError> Failed);

/// <summary>
/// Represents a single failed invitation in a batch operation.
/// </summary>
public sealed record BatchInvitationError(string Email, string Error);
