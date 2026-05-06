# 2FA Admin Enforcement — Design

> **Origin**: Q2 2026 Security Review (#186) P1.1
> **Status**: Phase A (shadow mode) — landed 2026-05-06
> **Bounded Context**: Authentication (cross-cutting via MediatR pipeline)

## Problem

Q2 2026 Security Review §3.5 BC tier-1 review flagged **2FA enforcement for admin role** as a partial control:
- TOTP availability ✅ (TotpService present, fully featured)
- TOTP enforcement ⚠️ — admins can perform sensitive actions without recent TOTP verification

A compromised admin password (phishing, credential stuffing) currently grants full admin capability without a second factor. Spec-panel review identified this as the highest-value P1 item (Kill Chain 1 in §0.3).

## Approach — phased rollout

Three-phase delivery to minimize disruption while maximizing observability:

### Phase A — Shadow mode (this PR)

- Add `[RequireTwoFactor]` attribute (mirrors `[AuditableAction]` pattern)
- Add `TwoFactorEnforcementBehavior` MediatR pipeline behavior
- Decorate top 4 sensitive admin commands
- Behavior **logs WARN/INFO** but does NOT block

**Goal**: collect telemetry on actual usage. How often do admins hit decorated commands? How many already have 2FA enabled? Tune attribute defaults before strict mode.

### Phase B — Strict mode (next PR)

- Add session schema field `LastTotpVerifiedAt`
- Replace shadow-mode warnings with rejection (HTTP 401 + `TwoFactorRequiredException`)
- Login flow: capture TOTP verification timestamp on session creation
- Re-challenge endpoint: `POST /api/v1/auth/2fa/challenge` to refresh `LastTotpVerifiedAt`
- Frontend: prompt + step-up modal when 401 received with `WWW-Authenticate: TOTP` header

**Goal**: actual enforcement.

### Phase C — Hardening (Q3)

- Rate limit failed TOTP attempts (5/min/IP, with audit log)
- Recovery code audit (10× single-use, hashed, regenerable)
- Admin without TOTP enrolled → forced enrollment on first admin login
- Integration test matrix `[admin/user] × [totp on/off] × [valid/invalid]`

## Architecture

```
┌──────────────────────────────────────────────────────┐
│  HTTP request → admin endpoint                       │
│       │                                              │
│       ▼                                              │
│  RequireAdminSession() middleware (existing)         │
│       │                                              │
│       ▼                                              │
│  IMediator.Send(command)                             │
│       │                                              │
│       ▼                                              │
│  ValidationBehavior            (existing)            │
│       ▼                                              │
│  AuditLoggingBehavior          (existing, #3691)     │
│       ▼                                              │
│  TwoFactorEnforcementBehavior  ← NEW (this PR)       │
│       │                                              │
│       ├─ no [RequireTwoFactor] → next()              │
│       │                                              │
│       ├─ no session in HttpContext → next()          │
│       │                                              │
│       ├─ user.IsTwoFactorEnabled = false             │
│       │     → log WARN, next() (Phase A)             │
│       │     → throw 401   (Phase B)                  │
│       │                                              │
│       └─ user.IsTwoFactorEnabled = true              │
│             → check LastTotpVerifiedAt (Phase B)     │
│             → log INFO, next() (Phase A — recency    │
│                                  check pending      │
│                                  schema update)      │
│       ▼                                              │
│  Handler executes                                    │
└──────────────────────────────────────────────────────┘
```

## Decorated commands (Phase A)

Top 4 sensitive admin actions, ranked by blast radius:

| Command | Blast radius | `Reason` value |
|---------|--------------|----------------|
| `ImpersonateUserCommand` | HIGH — full target-user session | "Impersonation grants full target-user session; HIGH RISK action." |
| `DeleteUserCommand` | HIGH — irreversible | "Irreversible destruction of user data; must be 2FA-guarded." |
| `ChangeUserRoleCommand` | MEDIUM — privilege escalation | "Role escalation grants new privileges and must be guarded." |
| `SuspendUserCommand` | MEDIUM — account lockout | "User suspension blocks login and must be 2FA-guarded." |

**Not yet decorated** (deferred to Phase B/C, low blast radius or read-only):
- `BulkPasswordResetCommand` (mass action — should be decorated in Phase B)
- `BulkRoleChangeCommand` (mass action — Phase B)
- `BulkImportUsersCommand` (Phase B)
- `EndImpersonationCommand` (return to admin — likely safe to skip)

## Default attribute values

```csharp
[RequireTwoFactor(MaxAgeMinutes = 30, Reason = "...")]
```

- `MaxAgeMinutes = 30`: per Q2 review §11 D3 ("30min for sensitive, 8h for read")
- `Reason`: human-readable, surfaced in audit logs and (Phase B+) UI prompts

## Telemetry questions for Phase A → Phase B transition

Before flipping to strict mode, the shadow-mode logs must answer:

1. **Adoption rate**: of admin requests hitting decorated commands, what % come from users with `IsTwoFactorEnabled = true`?
2. **Enrollment gap**: how many distinct admin users hit a decorated command without 2FA enabled? (These would be blocked in Phase B — need migration plan.)
3. **Frequency**: are decorated commands invoked frequently enough that 30min step-up window would create UX friction?

Decision rule: when adoption ≥ 90% of decorated invocations, proceed to Phase B.

## Failure modes considered

| Mode | Phase A handling | Phase B handling |
|------|------------------|------------------|
| User without 2FA invokes decorated cmd | log WARN, allow | reject 401, redirect to enrollment |
| Session has 2FA but expired recency window | log INFO, allow | reject 401, prompt step-up |
| No session in context (internal call, test) | log DEBUG, allow | allow (whitelisted internal context) |
| TOTP service down | n/a (no verification yet) | fail-closed: reject all decorated cmds |
| Lost TOTP device | n/a | recovery code (Phase C) |

## References

- Q2 2026 Security Review §11 P1.1 — SMART acceptance criteria
- `apps/api/src/Api/Services/TotpService.cs` — existing TOTP API
- `apps/api/src/Api/BoundedContexts/Administration/Application/Behaviors/AuditLoggingBehavior.cs` — pattern reference
- `docs/security/totp-vulnerability-analysis.md` — TOTP threat model
- `docs/security/audit-trail.md` — audit-log integration
