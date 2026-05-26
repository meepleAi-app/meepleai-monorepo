# 2FA Step-Up Protocol — Wire-Format Contract

> **SP5 Admin Security S3** — handoff contract for the frontend. The backend ships behind the
> `TwoFactorStrictMode` config flag (**default OFF at merge**); the FE work below is only exercised
> once an environment flips the flag. Until then every endpoint here is reachable but the enforcement
> 401 is never raised, so the current FE (which redirects to `/login` on any 401) keeps working.

## 1. Purpose

Strict 2FA enforcement gates four sensitive admin commands (`ChangeUserRole`, `DeleteUser`,
`SuspendUser`, `ImpersonationStart`) on a **recent** TOTP verification, tracked per session via
`LastTotpVerifiedAt`. Two HTTP surfaces make this work, and they **share one error vocabulary** —
the same `two_factor_required` + `subcode` body (with the same `WWW-Authenticate` header) is
returned whether a command was blocked by the enforcement filter or the step-up attempt itself
failed. The FE only has to learn one error shape.

| Flow | Trigger | Outcome |
|------|---------|---------|
| **A — Enforcement** | A `[RequireTwoFactor]` command is sent with stale/absent/disabled 2FA | `401` telling the FE *what* to do (`subcode`) |
| **B — Step-up** | `POST /api/v1/auth/2fa/step-up` with a fresh TOTP code | `200` refreshes `LastTotpVerifiedAt`, unblocking Flow A |

The FE loop: *call command → get Flow-A 401 → open the right UI → on `step_up_required`, POST step-up
(Flow B) → on 200, retry the original command.*

---

## 2. Flow A — Enforcement rejection

Raised by `TwoFactorEnforcementBehavior` and mapped by `ApiExceptionHandlerMiddleware`.

**Status:** `401 Unauthorized`
**Header:** `WWW-Authenticate: TOTP-StepUp realm="meepleai-admin"` — lets the FE distinguish a 2FA
block from an ordinary session-expired 401 *without parsing the body*.

**Body:**

```json
{
  "error": "two_factor_required",
  "subcode": "step_up_required",
  "message": "Two-factor re-verification required for this action.",
  "retryAfterSeconds": null,
  "correlationId": "0HN...",
  "timestamp": "2026-05-26T16:02:10.974Z"
}
```

**Subcode → FE action:**

| `subcode` | Emitted by | Meaning | FE action |
|-----------|-----------|---------|-----------|
| `step_up_required` | Flow A | 2FA enabled, but last TOTP verification is stale/absent for this command's `MaxAgeMinutes` | Open the **step-up modal** → POST Flow B → retry original request |
| `enroll_required` | Flow A | The account has no 2FA at all (hard block, D-S3-5) | Route to the **2FA enrollment** flow |
| `locked_out` | Flow A *or* Flow B | Too many failed step-up attempts | Show a **retry-after toast**; `retryAfterSeconds` carries the wait |
| `invalid_code` | Flow B only | The submitted TOTP/backup code was invalid, expired, or already used | Show an inline "wrong code" error in the step-up modal; let the user retry with a fresh code |

`retryAfterSeconds` is non-null **only** for `locked_out`.

**Staleness thresholds** — `step_up_required` fires when `now − LastTotpVerifiedAt` exceeds the
command's `MaxAgeMinutes`:

| Command | `MaxAgeMinutes` |
|---------|-----------------|
| `ImpersonationStart` | 5 (tighter — privileged escalation, D-S3-7) |
| `ChangeUserRole`, `DeleteUser`, `SuspendUser` | 30 (default) |

---

## 3. Flow B — Step-up endpoint

```
POST /api/v1/auth/2fa/step-up
```

**Auth:** required — active session cookie (`RequireAuthorization`). The verified actor is the
session's **EffectiveActor**: during an impersonation that is the *impersonating admin*, not the
target user. The endpoint does **not** create a new session; it only refreshes the current one's
`LastTotpVerifiedAt`.

**Request body:**

```json
{ "code": "123456" }
```

`code` — the 6-digit TOTP from the authenticator app (or a backup code). Required, max 10 chars.

> **Replay guard:** a TOTP code is single-use (used-code tracking inherited from `VerifyCodeAsync`).
> The FE must submit the **current** code shown by the authenticator; re-submitting an
> already-accepted code returns `401 two_factor_required` / `subcode: invalid_code`. Do not
> auto-retry with the same code.

**Responses:**

| Status | Body | When |
|--------|------|------|
| `200 OK` | `{ "success": true, "lastTotpVerifiedAt": "2026-05-26T16:02:10.974Z" }` | Code valid → session recency refreshed (ISO-8601 UTC) |
| `401 Unauthorized` | `{ "error": "two_factor_required", "subcode": "invalid_code", "message": "Invalid or expired verification code.", "retryAfterSeconds": null, "correlationId": "…", "timestamp": "…" }` + header `WWW-Authenticate: TOTP-StepUp realm="meepleai-admin"` | Code invalid/expired/reused, or the session vanished mid-request |
| `401 Unauthorized` | Same shape with `"subcode": "locked_out"` and `"retryAfterSeconds": 900` + same header | After 5 failed attempts in 5 min — 15-min lockout |
| `503 Service Unavailable` | `{ "error": "two_factor_unavailable", "message": "Two-factor service is temporarily unavailable. Please try again.", "correlationId": "…", "timestamp": "…" }` | TOTP store / rate-limit backend (Redis or encrypted-secret store) is unreachable. Retryable. |

> Both 401 shapes are identical to a Flow-A 401 — the FE's existing 2FA error handler covers them.

**Example:**

```bash
curl -X POST https://api.meepleai.app/api/v1/auth/2fa/step-up \
  -H "Content-Type: application/json" \
  -b "session=<auth-cookie>" \
  -d '{"code":"123456"}'

# 200 → { "success": true, "lastTotpVerifiedAt": "2026-05-26T16:02:10.974Z" }
# wrong / expired / reused code → 401 { "error": "two_factor_required", "subcode": "invalid_code", ... }
# after 5 fails in 5 min       → 401 { ..., "subcode": "locked_out", "retryAfterSeconds": 900 }
# TOTP store unreachable        → 503 { "error": "two_factor_unavailable", ... }
```

---

## 4. Sequence (happy path)

```
FE                          API
│  POST /admin/users/{id}/suspend
├───────────────────────────────────►
│  401 two_factor_required            │  TwoFactorEnforcementBehavior: stale TOTP
│  subcode=step_up_required           │
│◄───────────────────────────────────┤
│  (open step-up modal, collect code) │
│  POST /auth/2fa/step-up {code}      │
├───────────────────────────────────►│  StepUpTwoFactorCommand → VerifyCodeAsync
│  200 {success, lastTotpVerifiedAt}  │  refresh LastTotpVerifiedAt + audit TwoFactorStepUp
│◄───────────────────────────────────┤
│  POST /admin/users/{id}/suspend     │  (retry original)
├───────────────────────────────────►│  recency now fresh → allowed
│  200                                │
│◄───────────────────────────────────┤
```

---

## 5. Rate limiting & lockout

Step-up verification **delegates to the existing `TotpService.VerifyCodeAsync`**, so it shares one
source of truth with login-time `/auth/2fa/verify`:

- **5 attempts / 5 minutes** (Redis token bucket), then **15-minute lockout**.
- Constant-time comparison + replay-attack guard (used-code tracking) are inherited.
- The step-up handler performs a read-only `IsLockedOutAsync` pre-check to surface the distinct
  `locked_out` subcode before consuming an attempt; `retryAfterSeconds: 900` is a fixed client hint
  — the Redis key TTL is the authoritative wait.

---

## 6. Audit events

| Event | Emitted when | Attribution during impersonation |
|-------|--------------|----------------------------------|
| `TwoFactorRequired` | Flow A blocks a command | `user_id` = acting admin (EffectiveActor) |
| `TwoFactorStepUp` | Flow B succeeds | `user_id` = EffectiveActor |
| `TwoFactorStepUpLockout` | Flow B blocked by lockout | `user_id` = EffectiveActor |

---

## 7. FE handling rules

1. **`2xx` vs `4xx` vs `5xx`:** treat any unexpected `5xx` other than `503 two_factor_unavailable`
   as a generic, retryable error toast — **never** loop the step-up modal on a `5xx`. A `503
   two_factor_unavailable` is also retryable, but specifically a 2FA-backend outage; the modal can
   stay open with a "service temporarily unavailable, try again" message.
2. **Minimal 200 body.** Flow B returns `{ success, lastTotpVerifiedAt }`, not the full
   `SessionStatusResponse`. The FE only needs the timestamp to know the block is cleared and the
   blocked original request can be retried.
3. **`WWW-Authenticate` header presence** is sufficient to distinguish a 2FA-related 401 from any
   ordinary session-expired 401 without parsing the body — useful for a global 401 interceptor.

---

## 8. Backward compatibility

The FE currently redirects to `/login` on **any** 401. With `TwoFactorStrictMode=OFF` (merge default)
Flow A never fires, so nothing changes. When an environment flips the flag *before* the FE step-up
modal ships, blocked admins are bounced to login (a safe degrade, not a data risk) until the FE
consumes the `WWW-Authenticate: TOTP-StepUp` header / `subcode`.

---

**References:** plan `docs/superpowers/plans/2026-05-26-sp5-admin-security-s3-strict-2fa.md` ·
decisions `audits/2026-05-26-s3-three-amigos-kickoff.md` (D-S3-2, D-S3-4, D-S3-4b, D-S3-5).
