namespace Api.BoundedContexts.GameManagement.Application.DTOs.GameNights;

/// <summary>
/// A guest invited to a game night by email, as the organizer sees it.
/// #4706: these invitations live apart from the member RSVPs, so they were invisible to the organizer.
/// </summary>
/// <remarks>
/// <see cref="Email"/> is the address the organizer typed, returned only to the organizer. The response
/// token is deliberately absent: whoever holds it can answer in the guest's name.
/// <see cref="Status"/> is the <c>GameNightInvitationStatus</c> name (Pending, Accepted, Declined, Expired).
/// </remarks>
public sealed record GameNightGuestInvitationDto(
    Guid Id,
    string Email,
    string Status,
    DateTimeOffset? RespondedAt,
    string? RespondedByName,
    DateTimeOffset ExpiresAt,
    DateTimeOffset CreatedAt);
