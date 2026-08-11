namespace Api.BoundedContexts.Authentication.Domain.Enums;

/// <summary>
/// Why a ToS acceptance row was recorded (#2954 F1). Persisted as its string
/// name (never as an int) so growing the enum never requires a DB constraint change.
/// </summary>
public enum TermsAcceptanceContext
{
    /// <summary>Recorded during initial account registration.</summary>
    Registration,

    /// <summary>Recorded when the user re-accepts an updated ToS version.</summary>
    ReConsent,
}
