namespace Api.BoundedContexts.Authentication.Application.DTOs;

/// <summary>
/// Data transfer object for a single invitation item in a batch operation.
/// Issue #124: Admin invitation flow — batch provisioning.
/// </summary>
public sealed record BatchInvitationItemDto(
    string Email,
    string DisplayName,
    string Role,
    string Tier,
    string? CustomMessage,
    int ExpiresInDays,
    List<GameSuggestionDto> GameSuggestions);
