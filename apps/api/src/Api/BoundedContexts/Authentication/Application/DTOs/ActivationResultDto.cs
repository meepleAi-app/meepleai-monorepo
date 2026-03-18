namespace Api.BoundedContexts.Authentication.Application.DTOs;

/// <summary>
/// Result of activating an invited user account.
/// Contains the session token for immediate login and onboarding flag.
/// </summary>
internal sealed record ActivationResultDto(string SessionToken, bool RequiresOnboarding);
