using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Commands.Invitation;

/// <summary>
/// Command to provision a pending user and send an invitation email in one operation.
/// Creates both the User (Pending status) and InvitationToken entities.
/// Issue #124: Admin invitation flow.
/// </summary>
internal sealed record ProvisionAndInviteUserCommand(
    string Email,
    string DisplayName,
    string Role,
    string Tier,
    string? CustomMessage,
    int ExpiresInDays,
    List<GameSuggestionDto> GameSuggestions,
    Guid InvitedByUserId
) : ICommand<InvitationDto>;
