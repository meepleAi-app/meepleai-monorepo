namespace Api.BoundedContexts.Authentication.Application.DTOs;

/// <summary>
/// Data transfer object for invitation information.
/// Issue #124: User invitation system.
/// </summary>
public sealed record InvitationDto(
    Guid Id,
    string Email,
    string Role,
    string Status,
    DateTime ExpiresAt,
    DateTime CreatedAt,
    DateTime? AcceptedAt,
    Guid InvitedByUserId,
    bool EmailSent = true,
    List<GameSuggestionDto>? GameSuggestions = null,
    // Raw token only populated on creation; not stored in DB (only hash persisted).
    // Use to construct the accept-invite URL immediately after sending.
    string? Token = null);
