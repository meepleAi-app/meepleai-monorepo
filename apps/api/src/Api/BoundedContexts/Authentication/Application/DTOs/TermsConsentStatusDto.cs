namespace Api.BoundedContexts.Authentication.Application.DTOs;

/// <summary>
/// Read model describing a user's ToS acceptance status (#2954 F1).
/// NeedsReAcceptance is computed but intentionally NOT enforced by any gate in this scope.
/// </summary>
public sealed record TermsConsentStatusDto(
    string CurrentVersion,
    string? AcceptedVersion,
    DateTime? AcceptedAt,
    bool NeedsReAcceptance);
