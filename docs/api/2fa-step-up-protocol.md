# 2FA Step-Up Protocol — Wire-Format Contract

> **SP5 Admin Security S3** — handoff contract for the frontend. The backend ships behind the
> `TwoFactorStrictMode` config flag (**default OFF at merge**); the FE work below is only exercised
> once an environment flips the flag. Until then every endpoint here is reachable but the enforcement
> 401 is never raised, so the current FE (which redirects to `/login` on any 401) keeps working.

## 1. Purpose

Strict 2FA enforcement gates four sensitive admin commands (`ChangeUserRole`, `DeleteUser`,
`SuspendUser`, `ImpersonationStart`) on a **recent** TOTP verification, tracked per session via
`LastTotpVerifiedAt`. Two HTTP surfaces make this work:

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

| `subcode` | Meaning | FE action |
|-----------|---------|-----------|
| `step_up_required` | 2FA enabled, but last TOTP verification is stale/absent for this command's `MaxAgeMinutes` | Open the **step-up modal** → POST Flow B → retry original request |
| `enroll_required` | The account has no 2FA at all (hard block, D-S3-5) | Route to the **2FA enrollment** flow |
| `locked_out` | Too many failed step-up attempts | Show a **retry-after toast**; `retryAfterSeconds` carries the wait |

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
> already-accepted code returns `401 two_factor_failed`. Do not auto-retry with the same code.

**Responses:**

| Status | Body | When |
|--------|------|------|
| `200 OK` | `{ "success": true, "lastTotpVerifiedAt": "2026-05-26T16:02:10.974Z" }` | Code valid → session recency refreshed (ISO-8601 UTC) |
| `401 Unauthorized` | `{ "error": "two_factor_failed", "message": "Invalid or expired verification code." }` | Code invalid/expired, or the session vanished mid-request |
| `429 Too Many Requests` | `{ "error": "two_factor_locked_out", "message": "Too many failed attempts. Try again later.", "retryAfterSeconds": 900 }` | Account is locked after repeated failures |

**Example:**

```bash
curl -X POST https://api.meepleai.app/api/v1/auth/2fa/step-up \
  -H "Content-Type: application/json" \
  -b "session=<auth-cookie>" \
  -d '{"code":"123456"}'

# 200 → { "success": true, "lastTotpVerifiedAt": "2026-05-26T16:02:10.974Z" }
# wrong/expired/reused code → 401 { "error": "two_factor_failed", "message": "Invalid or expired verification code." }
# after 5 failures in 5 min → 429 { "error": "two_factor_locked_out", "retryAfterSeconds": 900 }
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
- The step-up handler performs a read-only `IsLockedOutAsync` pre-check to return the distinct `429`
  before consuming an attempt; `retryAfterSeconds` is a fixed client hint (900s) — the Redis key TTL
  is the authoritative wait.

---

## 6. Audit events

| Event | Emitted when | Attribution during impersonation |
|-------|--------------|----------------------------------|
| `TwoFactorRequired` | Flow A blocks a command | `user_id` = acting admin (EffectiveActor) |
| `TwoFactorStepUp` | Flow B succeeds | `user_id` = EffectiveActor |
| `TwoFactorStepUpLockout` | Flow B blocked by lockout | `user_id` = EffectiveActor |

---

## 7. Known gaps & future alignment

> Documented honestly so the FE plans around the **current** behavior, not the original design intent.

1. **Error-shape asymmetry between the two flows.** Flow A uses `error: "two_factor_required"` +
   `subcode`; Flow B uses top-level `error: "two_factor_failed"` / `"two_factor_locked_out"` and
   returns `429` (not `401`) for lockout. The FE must therefore handle two shapes. The original plan
   (T7) envisaged Flow B reusing `two_factor_required` + `subcode`; the implementation diverged. See
   §8 for the rationale and the open decision.
2. **No `503` for TOTP-store-down.** The plan (D-S3-4) specified a `503 two_factor_unavailable` when
   the secret store / Redis is unreachable. The current implementation lets that surface as a generic
   `500`. Tracked as a follow-up. **FE rule:** treat any unexpected `5xx` as a generic, retryable
   error (show an error toast) — **not** as a step-up or enroll signal; never loop the step-up modal
   on a `5xx`.
3. **Minimal 200 body.** Flow B returns `{ success, lastTotpVerifiedAt }`, not the full
   `SessionStatusResponse` the plan mentioned. The FE only needs the timestamp to know the block is
   cleared.

## 8. Open decision (for review)

Should Flow B be aligned to the enforcement vocabulary before the strict flip?

- **Option A — keep as built:** distinct shapes are arguably honest (enforcement says *"you must
  step up"*; step-up says *"your code was wrong"* — different semantics). Document both; FE handles two.
- **Option B — unify:** Flow B returns `401 { error: "two_factor_required", subcode: "invalid_code" }`
  and `429 { … subcode: "locked_out" }`, plus the `503`. One vocabulary, smaller FE surface, but a
  code change to the already-merged-on-branch T5 endpoint.

This document describes **Option A (as built)**. The decision is deferred to the T8 acceptance review.

---

## 9. Backward compatibility

The FE currently redirects to `/login` on **any** 401. With `TwoFactorStrictMode=OFF` (merge default)
Flow A never fires, so nothing changes. When an environment flips the flag *before* the FE step-up
modal ships, blocked admins are bounced to login (a safe degrade, not a data risk) until the FE
consumes the `WWW-Authenticate: TOTP-StepUp` header / `subcode`.

---

**References:** plan `docs/superpowers/plans/2026-05-26-sp5-admin-security-s3-strict-2fa.md` ·
decisions `audits/2026-05-26-s3-three-amigos-kickoff.md` (D-S3-2, D-S3-4, D-S3-4b, D-S3-5).
