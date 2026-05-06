namespace Api.BoundedContexts.Authentication.Application.Attributes;

/// <summary>
/// Marks a MediatR command as requiring 2FA (TOTP) verification before execution.
/// Q2 2026 Security Review (#186) P1.1: 2FA admin enforcement.
///
/// Shadow mode (initial rollout): the TwoFactorEnforcementBehavior logs warnings
/// when the request comes from an admin without recent TOTP verification, but
/// does NOT block. This allows visibility before strict enforcement.
///
/// Strict mode (subsequent rollout): same behavior, but rejects the command with
/// HTTP 401 if TOTP not recently verified.
///
/// Usage:
/// <code>
/// [RequireTwoFactor]
/// internal record DeleteUserCommand(Guid UserId) : ICommand&lt;UserDto&gt;;
/// </code>
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
internal sealed class RequireTwoFactorAttribute : Attribute
{
    /// <summary>
    /// Maximum age (in minutes) of the most recent TOTP verification on the
    /// session before a fresh challenge is required. Default: 30 minutes for
    /// sensitive admin actions per Q2 review §11 D3.
    /// </summary>
    public int MaxAgeMinutes { get; set; } = 30;

    /// <summary>
    /// Optional: human-readable description of why this command needs 2FA,
    /// surfaced in audit logs and (future) UI prompts.
    /// </summary>
    public string? Reason { get; set; }
}
