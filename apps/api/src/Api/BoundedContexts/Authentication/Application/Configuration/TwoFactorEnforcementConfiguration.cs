using Api.Services;

namespace Api.BoundedContexts.Authentication.Application.Configuration;

/// <summary>
/// Compile-time documented configuration keys for the 2FA enforcement subsystem.
/// SP5 Admin Security S3 — D-S3-1 cutover flag.
/// </summary>
internal static class TwoFactorConfigurationKeys
{
    /// <summary>
    /// Boolean flag controlling whether <c>TwoFactorEnforcementBehavior</c> runs in strict
    /// (throws <c>TwoFactorRequiredException</c>) or shadow (logs only) mode. Default
    /// <c>false</c> at deploy — ops explicitly flips to <c>true</c> via admin toggle after
    /// the pre-cutover sweep (D-S3-5) confirms zero admins lack 2FA. See
    /// <c>audits/2026-05-26-s3-three-amigos-kickoff.md</c> §D-S3-1.
    /// </summary>
    public const string StrictMode = "TwoFactor:StrictMode";

    /// <summary>Default for <see cref="StrictMode"/> — strict OFF on first deploy.</summary>
    public const bool StrictModeDefault = false;
}

/// <summary>
/// Typed accessor for the 2FA enforcement runtime configuration. Provides:
/// <list type="bullet">
/// <item>compile-time-safe binding to <see cref="TwoFactorConfigurationKeys.StrictMode"/></item>
/// <item>mockable seam for unit-testing <c>TwoFactorEnforcementBehavior</c> (T4) without
///   needing a full <c>IConfigurationService</c> setup</item>
/// <item>fail-closed semantics (returns the default on any error — matches the existing
///   <c>GetRegistrationModeQueryHandler</c> pattern)</item>
/// </list>
/// SP5 Admin Security S3 — D-S3-1.
/// </summary>
internal interface ITwoFactorEnforcementConfiguration
{
    /// <summary>
    /// Reads the strict-mode flag from the dynamic configuration store. Returns
    /// <see cref="TwoFactorConfigurationKeys.StrictModeDefault"/> when the flag is unset
    /// or the underlying store is unreachable (fail-closed: shadow mode is the safe default
    /// since it never blocks legitimate admin actions).
    /// </summary>
    Task<bool> GetStrictModeAsync(CancellationToken cancellationToken = default);
}

internal sealed class TwoFactorEnforcementConfiguration : ITwoFactorEnforcementConfiguration
{
    private readonly IConfigurationService _configurationService;

    public TwoFactorEnforcementConfiguration(IConfigurationService configurationService)
    {
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
    }

    public async Task<bool> GetStrictModeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var value = await _configurationService.GetValueAsync<bool?>(
                TwoFactorConfigurationKeys.StrictMode,
                TwoFactorConfigurationKeys.StrictModeDefault).ConfigureAwait(false);
            return value ?? TwoFactorConfigurationKeys.StrictModeDefault;
        }
        catch
        {
            // Fail-closed: shadow mode on any read error. Strict mode is opt-in by ops, so
            // an unreachable config store must NOT silently flip behavior to strict.
            return TwoFactorConfigurationKeys.StrictModeDefault;
        }
    }
}
