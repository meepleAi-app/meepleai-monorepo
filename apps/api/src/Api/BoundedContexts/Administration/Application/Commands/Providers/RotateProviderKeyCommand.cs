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
/// Audit atomicity: <c>[AtomicAudit]</c> is RESTORED here at #1535 T10 cleanup. Reason —
/// <c>ProviderCredential.Create</c> raises a <c>ProviderKeyRotatedEvent</c>. Pre-#1535, the
/// event handler's Redis pub/sub broadcast fired INSIDE <c>SaveChangesAsync</c>, so an outer
/// audit rollback could leave a spurious cache invalidation in flight (the original
/// <c>AtomicAudit + external side-effects forbidden</c> constraint). Post-#1535 (commits
/// <c>23dc88727</c> Phase A + T10 cleanup), the event flows through the post-commit outbox:
/// if the outer audit transaction rolls back, the outbox row is rolled back too — the
/// broadcast never fires. The original forbidden combination is now safe IN OUTBOXONLY MODE.
///
/// ⚠ Hybrid rollback caveat: flipping DomainEventOutbox:Mode back to Hybrid (the documented
/// rollback path) re-enables the inline MediatR.Publish race for this command. Treat the
/// rollback as a short-term incident-response measure; if production needs to stay on Hybrid
/// for an extended window, gate the rotation endpoint behind a feature flag until OutboxOnly
/// is restored.
///
/// Issue #1859 + #1535. Authorised: superadmin only (handler-level guard, see
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
    string ApiKey,
    string ConfirmedProviderName,
    Guid RequestingUserId) : IRequest<RotateProviderKeyResponseDto>;
