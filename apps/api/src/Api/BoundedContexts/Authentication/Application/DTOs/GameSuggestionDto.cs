namespace Api.BoundedContexts.Authentication.Application.DTOs;

/// <summary>
/// Data transfer object for game suggestions attached to an invitation.
/// Issue #124: Admin invitation flow.
/// </summary>
public sealed record GameSuggestionDto(
    Guid GameId,
    string Type);
