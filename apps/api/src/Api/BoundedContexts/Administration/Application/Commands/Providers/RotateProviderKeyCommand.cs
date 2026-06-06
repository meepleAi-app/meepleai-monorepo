using Api.BoundedContexts.Administration.Application.Attributes;
using Api.BoundedContexts.Administration.Application.Behaviors;
using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Authentication.Application.Attributes;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Commands.Providers;

/// <summary>
/// Rotates a provider's API key. The handler runs a pre-flight probe against the new key, then
/// deactivates the current active credential row and inserts a new one with the encrypted
/// ciphertext + masked fingerprint. The <see cref="AuditableActionAttribute"/> captures the
/// before/after snapshot at audit Level 3 (CONFIRM-typed destructive). The
/// <see cref="RequireTwoFactorAttribute"/> forces strict step-up 2FA (max age 5 min) even when
/// the global StrictMode flag is OFF — provider key rotation is too dangerous to ship in
/// shadow mode.
///
/// Audit redaction: the property is named <c>ApiKey</c> so it matches the exact-string
/// exclusion filter in <c>AuditLoggingBehavior.BuildMetadata</c> (line 351) — it is never
/// serialized into <c>audit_outbox.Details</c>. The DTO on the wire (<see cref="RotateProviderKeyRequestDto"/>)
/// still names it <c>NewApiKey</c> for FE-side clarity; the endpoint maps body.NewApiKey →
/// command.ApiKey at construction time.
///
/// Audit atomicity: <c>[AtomicAudit]</c> is intentionally NOT used here even though the command
/// is destructive. Reason — <c>ProviderCredential.Create</c> raises a
/// <c>ProviderKeyRotatedEvent</c> dispatched inside <c>SaveChangesAsync</c> via
/// <c>MediatR.Publish</c>, and the event handler broadcasts on Redis pub/sub (cross-pod cache
/// invalidation). This broadcast is observable external side-effect that cannot be undone if
/// the outer audit transaction rolls back. The <see cref="AtomicAuditAttribute"/> doc-comment
/// (lines 24-33) explicitly forbids this combination. Best-effort audit (the default
/// non-atomic path) is acceptable here: a missing audit row is recoverable from logs, but a
/// spurious cache invalidation followed by a rolled-back rotation is not.
///
/// Issue #1859. Authorised: superadmin only (handler-level guard, see
/// <c>RotateProviderKeyCommandHandler</c>). Rate-limit guard: 1 rotation per provider per 24h.
/// </summary>
[AuditableAction("ProviderKeyRotated", "provider_credentials", Level = 3)]
[RequireTwoFactor(
    MaxAgeMinutes = 5,
    ForceStrict = true,
    Reason = "Provider key rotation requires fresh 2FA")]
internal sealed record RotateProviderKeyCommand(
    string ProviderName,
    string ApiKey,
    string ConfirmedProviderName,
    Guid RequestingUserId) : IRequest<RotateProviderKeyResponseDto>;
