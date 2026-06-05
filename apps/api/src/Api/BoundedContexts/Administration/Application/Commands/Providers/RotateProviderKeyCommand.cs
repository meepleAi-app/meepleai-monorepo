using Api.BoundedContexts.Administration.Application.Attributes;
using Api.BoundedContexts.Administration.Application.Behaviors;
using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Authentication.Application.Attributes;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Commands.Providers;

/// <summary>
/// Rotates a provider's API key. The handler runs a pre-flight probe against the new key, then
/// (in a single atomic audit transaction) deactivates the current active credential row and
/// inserts a new one with the encrypted ciphertext + masked fingerprint. The
/// <see cref="AuditableActionAttribute"/> captures the before/after snapshot at audit Level 3
/// (CONFIRM-typed destructive); <see cref="AtomicAuditAttribute"/> guarantees the audit row
/// commits together with the mutation. <see cref="RequireTwoFactorAttribute"/> forces strict
/// step-up 2FA (max age 5 min) even when the global StrictMode flag is OFF — provider key
/// rotation is too dangerous to ship in shadow mode.
///
/// Issue #1859. Authorised: superadmin only (handler-level guard, see
/// <c>RotateProviderKeyCommandHandler</c>). Rate-limit guard: 1 rotation per provider per 24h.
/// </summary>
[AuditableAction("ProviderKeyRotated", "provider_credentials", Level = 3)]
[AtomicAudit]
[RequireTwoFactor(
    MaxAgeMinutes = 5,
    ForceStrict = true,
    Reason = "Provider key rotation requires fresh 2FA")]
internal sealed record RotateProviderKeyCommand(
    string ProviderName,
    string NewApiKey,
    string ConfirmedProviderName,
    Guid RequestingUserId) : IRequest<RotateProviderKeyResponseDto>;
