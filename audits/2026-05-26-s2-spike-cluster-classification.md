# SP5 S2 — T0 Spike: Legacy Inventory + Principal Refactor Blueprint

**Date:** 2026-05-26
**Branch:** `feature/sp5-admin-security-s2-impersonate`
**Output of:** T0 step 1-3 of `docs/superpowers/plans/2026-05-25-sp5-admin-security-s2-impersonate.md`
**Predecessor reading:** `audits/2026-05-25-s2-three-amigos-kickoff.md`

---

## ⚠️ Major discovery — impersonation is already live

The codebase already ships an impersonation system (issues **#3349 / #2890**). The three-amigos doc from 2026-05-25 was authored under a greenfield assumption; **this spike supersedes that assumption** with the legacy inventory and refactor delta below.

**User decision (2026-05-26):** **Refactor in-place + tightening** — keep all six S2 decisions (D-S2-1..6) including the tighter eligibility (superadmin only), and dismantle the legacy direct-`_auditLogRepository.AddAsync` paths in favour of `[AuditableAction]` + `[AtomicAudit]` going through the S1 outbox.

The kickoff doc and the plan have been updated in this same commit to reflect the legacy-refactor framing.

---

## 1. Legacy inventory

### 1.1 Domain — Commands and handlers (Administration BC)

| File | Shape | Notes |
|------|-------|-------|
| `apps/api/src/Api/BoundedContexts/Administration/Application/Commands/ImpersonateUserCommand.cs` | `record (Guid TargetUserId, Guid AdminUserId, string Reason)` + `[RequireTwoFactor]` | No `[AuditableAction]`, no `[AtomicAudit]`. Returns `ImpersonateUserResponseDto`. |
| `.../ImpersonateUserCommandHandler.cs` | 1. Authz: caller `admin` OR `superadmin`. 2. Target eligibility: not `admin`/`superadmin`, not suspended. 3. Spawns new session via `CreateSessionCommand(UserId=target, IpAddress="impersonated", UserAgent="Admin Impersonation by ...")`. 4. Writes **2 audit_logs rows** directly via `IAuditLogRepository.AddAsync`. 5. SaveChangesAsync. | The two manual audit rows are: `action=impersonate_user_started userId=admin resourceId=targetId` + `action=impersonated_by_admin userId=target resourceId=adminId`. |
| `.../EndImpersonationCommand.cs` | `record (Guid SessionId, Guid AdminUserId)` | Returns `bool`. |
| `.../EndImpersonationCommandHandler.cs` | 1. Reads session by id. 2. Revokes via `RevokeSessionCommand`. 3. Writes 1 manual audit row `impersonate_user_ended`. | Same legacy audit anti-pattern. |
| `.../Validators/ImpersonateUserCommandValidator.cs` | FluentValidation | Confirms `Reason.MinimumLength`. |
| `.../Validators/EndImpersonationCommandValidator.cs` | FluentValidation | Trivial. |

### 1.2 Routing — Endpoints

`apps/api/src/Api/Routing/Admin/AdminUserActivityEndpoints.cs`:
- `POST /admin/users/{userId:guid}/impersonate` → `HandleImpersonateUser` (lines 620–666). Gate: `RequireSuperAdminSession()` (line 628) — **tighter than the command handler accepts**: the endpoint requires superadmin, the command allows admin too. The current effective gate is therefore superadmin. **This already matches D-S2-1.** The command-handler's `admin OR superadmin` check is dead defence — to be removed in T3 for clarity.
- `POST /admin/impersonation/end` → `HandleEndImpersonation` (lines 668–697). Gate: `RequireAdminSession()` (any admin) — the admin who ended it can be a different admin than the one who started.

### 1.3 Persistence — Session creation

`CreateSessionCommand` (in the Authentication BC) is the same primitive used by login. `ImpersonateUserCommandHandler` calls it with:
- `UserId = command.TargetUserId` (the subject — same as a regular login)
- `IpAddress = "impersonated"` (literal string used as a hack signal — there is currently NO column on `UserSessionEntity` that says "this is an impersonation")
- `UserAgent = $"Admin Impersonation by {adminUser.DisplayName ?? adminUser.Email.Value}"`

Effect on `UserSessionEntity`: **a regular session row, indistinguishable from a real user login except by the magic-string IpAddress.** No `ImpersonatedByUserId`, no `ImpersonatedUntil`. The session expires with the regular `ExpiresAt` (configured globally — typically 7 days for a user session, see SessionExpiration config).

### 1.4 Audit trail (legacy paths writing into `audit_logs`)

Three direct `_auditLogRepository.AddAsync` paths bypass the S1 outbox:

| Source | Action | `user_id` | `resource_id` | `impersonated_user_id` |
|--------|--------|-----------|---------------|------------------------|
| `ImpersonateUserCommandHandler` row #1 | `impersonate_user_started` | admin | target | (null — S1 column not populated) |
| `ImpersonateUserCommandHandler` row #2 | `impersonated_by_admin` | target | admin | (null) |
| `EndImpersonationCommandHandler` | `impersonate_user_ended` | admin | target | (null) |

This is the path duplication called out by **issue #1534**. S2 refactor closes this gap for the impersonation-specific actions; the rest of #1534 (DomainEventHandlerBase parallel audit) remains a separate follow-up.

---

## 2. Gap analysis vs S2 design

| Decision | Legacy state | S2 target | Refactor needed |
|----------|--------------|-----------|-----------------|
| **D-S2-1** Eligibility | Endpoint gate `RequireSuperAdminSession` (✅). Command handler accepts admin OR superadmin (defence-in-depth dead code). Target gate: not admin/superadmin, not suspended (✅). No `cannot_impersonate_self` explicit guard. No `target_account_ineligible` for demo accounts. | Solo superadmin can call; explicit guards on self/peer/superior/suspended/demo/banned. | Remove the `adminLevel < 3` path in `ImpersonateUserCommandHandler:65-68` (dead defence); add explicit self-impersonate guard; add demo/banned check. |
| **D-S2-2** Dual principal | `SessionStatusDto.User: UserDto?` (mono-principal); 100+ active call sites of `session.User.X` (see §3). | `Principal { Subject, Actor? }` record replaces `User`. | Wave 1 codemod (~355 occurrences total in 79 files); Wave 2 semantic disambiguation (15 call sites use `EffectiveActor`). |
| **D-S2-3** Audit attribution | 3 direct `AddAsync` rows, none populating `impersonated_user_id`. | `[AuditableAction("ImpersonationStarted", "Session")] [AtomicAudit]` → 1 outbox row per command; `audit_logs.user_id=subject, impersonated_user_id=actor`. | Replace direct `AddAsync` with attribute-driven flow in both handlers; remove manual audit writes; let S1's behavior populate the row. |
| **D-S2-4** Auto-expiry 15min | No cap; session has standard `ExpiresAt` (~7d). | Hard cap 15min, configurable via `SystemConfiguration.ImpersonationMaxDurationMinutes`. | New column `ImpersonatedUntil`; middleware verifies on every request. Existing legacy sessions are NOT yet expiry-capped — at most a handful exist; documented as accepted carry-over. |
| **D-S2-5** Revoke kill-switch | No superadmin revoke API distinct from `EndImpersonationCommand`. | New `RevokeImpersonationCommand` callable only by superadmin (kill-switch). | Add the command, endpoint, and invalidate-on-read middleware check on `RevokedAt`. |
| **D-S2-6** 2FA on start | `[RequireTwoFactor]` already on `ImpersonateUserCommand` (✅). | Same — keep shadow mode. | No change needed; the attribute is preserved on the new `ImpersonationStartCommand`. |

---

## 3. Call-site inventory by cluster (100 concrete lines)

Grep over `apps/api/src` for `session\.User\.(Id|Role|Email|Tier|DisplayName|IsAdmin)` yields **100 concrete lines** (the original three-amigos estimate of "39 call sites" was off — the true denominator is larger because logging statements also touch `session.User.Id`). Pattern-of-extraction occurrences (`if session?.User == null`, `is SessionStatusDto { ... }`) are excluded here — codemod target Wave 1, ~355 occurrences across 79 files (see §4).

### Cluster A — Audit/log attribution → **EffectiveActor (Actor ?? Subject)**

These are admin command IDs and log-statement subjects that must reflect the **real admin** under impersonation.

| File:Line | Snippet | Why Actor |
|-----------|---------|-----------|
| `Routing/AdminAbTestEndpoints.cs:35` | `CreatedBy: session.User.Id` (CreateAbTestCommand) | Admin owns the AB test creation |
| `Routing/AdminAbTestEndpoints.cs:45,165,179` | log + `EvaluatorId` | Admin action attribution |
| `Routing/Admin/AdminUserTierLevelBadgesEndpoints.cs:199,226,248` | log "by admin" | Admin action |
| `Routing/Admin/AdminUserTierEndpoints.cs:233,255,277,302` | log "by admin" | Admin action |
| `Routing/Admin/AdminUserInvitationEndpoints.cs:103,133,159,210,245,295,317` | Command IDs (Send/BulkSend/Resend/Revoke/Invite) | Admin attribution (`AdminUserId`, `InvitedByUserId`) |
| `Routing/Admin/AdminUserCrudEndpoints.cs:196` | log | Admin |
| `Routing/Admin/AdminUserBulkEndpoints.cs:245,274,310,312` | `requesterId`, `UnlockAccountCommand`, log | Admin |
| `Routing/Admin/AdminUserActivityEndpoints.cs:371,400,436,438,538,563,599,604,641,647,653,683,690` | `requesterId`, command IDs, logs (incl. existing impersonation logs) | Admin |
| `Routing/Admin/AdminUserActivityDetailEndpoints.cs:260,282,307,343,348,385,391,397,427,434` | log | Admin |
| `Routing/AdminTestResultEndpoints.cs:48,57,167,178` | `ExecutedBy`, command IDs, log | Admin |
| `Routing/AdminMiscEndpoints.cs:34` | log "by admin" | Admin |
| `Routing/WorkflowEndpoints.cs:92` | log "Admin creating n8n config" | Admin |
| `Routing/FeatureFlagEndpoints.cs:246,325,382` | log feature flag changes | Admin |
| `Routing/RagPipelineAdminEndpoints.cs:121,161,193,286,336` | log RAG admin ops | Admin |
| `Routing/RagExecutionAdminEndpoints.cs:63,160` | log | Admin |
| `Routing/RagDashboardEndpoints.cs:156,181` | `UserId =` in admin command | Admin |

**Cluster A total: 64 lines.**

### Cluster B — Authorization (privilege gate) → **EffectiveActor**

Privilege/role checks that gate sensitive operations.

| File:Line | Snippet | Why Actor |
|-----------|---------|-----------|
| `Extensions/SessionValidationExtensions.cs:109` | `Enum.TryParse<UserRole>(session.User.Role, ...)` | Central authz helper — must read the real role, never the impersonated one (escalation/de-escalation prevention) |
| `Filters/RequireFeatureFilter.cs:58,62` | `userRepository.GetByIdAsync(session.User.Id)` | Combined role+tier feature gate |
| `Routing/Pdf/PdfUploadEndpoints.cs:198` | `session.User.Role == "Admin"` for priority override | Admin-only privilege |
| `Routing/RuleSpecEndpoints.cs:524` | `session.User.Role == "Admin"` for comment delete privilege | Admin-only |

**Cluster B total: 5 lines.**

### Cluster C — Resource ownership / "own data" → **Subject**

Endpoints where the user (or their impersonator-as-subject) is operating on their own resources.

| File:Line | Snippet | Why Subject |
|-----------|---------|-------------|
| `Routing/UserProfileEndpoints.cs:73,117,168,209,274,313,370,563,601` | log "for user {UserId}", profile/preferences | Subject's own profile |
| `Routing/UserLlmDataEndpoints.cs:96` | log own-data deletion | Subject |
| `Routing/UserAiConsentEndpoints.cs:39,81` | log consent | Subject (admin impersonating Bob updates Bob's consent) |
| `Routing/DeviceEndpoints.cs:32,76,80` | log devices | Subject's own devices |
| `Routing/ShareLinkEndpoints.cs:82,122` | `UserId: session.User.Id` (Create/Delete) | Subject creates/deletes their share link |
| `Routing/GameEndpoints.cs:979` | `userId = session.User.Id` | Resource ownership context |
| `Routing/PhotoIngestionEndpoints.cs:194` | `return session.User.Id` (uploader id) | Subject uploads their photo |
| `Routing/Pdf/PdfUploadEndpoints.cs:419` | log "userId {X} by {Y}" | Subject |
| `Routing/WorkflowEndpoints.cs:240` | `UserId =` (workflow request) | Subject's own workflow |
| `Routing/RuleSpecEndpoints.cs:580` | `session.User.Email ?? "unknown@user.com"` (comment author) | Subject |
| `BoundedContexts/Authentication/Application/Queries/GetSessionStatusQueryHandler.cs:52` | `session.User.Email` (response DTO) | Subject — own session info |

**Cluster C total: ~22 lines.**

### Cluster D — Rate-limit / quota → **Subject**

Quotas apply to the user being acted-as. An impersonator must NOT bypass their target's rate limits.

| File:Line | Snippet | Why Subject |
|-----------|---------|-------------|
| `Filters/NotificationRateLimitFilter.cs:117` | `$"notifications:{session.User.Id}"` | Subject's notification quota |
| `Middleware/RateLimitingMiddleware.cs:68,69` | `role`, `rateKey` | Subject — tier-bucket classification + per-user key |
| `Routing/ChatSessionEndpoints.cs:118,234,235` | `GetLimit(session.User.Tier, session.User.Role)` | Subject's chat tier quota |

**Cluster D total: 6 lines.**

### Special / context-dependent

| File:Line | Snippet | Resolution |
|-----------|---------|------------|
| `BoundedContexts/Administration/Application/Behaviors/AuditLoggingBehavior.cs:302-303` | `userId = session.User.Id.ToString()` + `email = session.User.Email` populating `AuditOutboxPayload` | **Stays Subject (Principal.Subject)**. The behavior also reads `ImpersonatedUserId` from a new field (T3); the payload contract D-S2-3 is `user_id=subject, impersonated_user_id=actor`. |
| `Routing/AuthenticationEndpoints.cs:42` | Comment only | N/A — purely documentation. |
| `Routing/Admin/AdminUserBulkEndpoints.cs:244,273` | Comments only | N/A. |

**Special total: 5 lines (2 functional — AuditLoggingBehavior:302-303; the other 3 are comments/docstrings).**

### Tally

| Cluster | Functional lines | Wave 1 codemod target | Wave 2 disambiguation |
|---------|------------------|----------------------|----------------------|
| A — Audit/log attribution | 64 | `session.User` → `session.Principal.Subject` (intermediate state) | → `session.Principal.EffectiveActor` |
| B — Authorization | 5 | (same as A intermediate) | → `session.Principal.EffectiveActor` |
| C — Resource ownership | 22 | (same as A intermediate) | (none — Subject is correct semantics) |
| D — Rate-limit/quota | 6 | (same as A intermediate) | (none — Subject is correct) |
| Special | 2 | Audit behavior: stays Subject (already correct intent) | (none) |
| **Total** | **99** functional lines | 100% mechanical | **69 lines** need Wave 2 actor disambiguation |

Plus the **~290 pattern-of-extraction** occurrences (null checks, pattern matching, type references) — all handled by Wave 1's mechanical rename only.

> **Note on counting:** the grep over `session\.User\.(Id|Role|Email|Tier|DisplayName|IsAdmin)` returned 100 lines, of which 1 is a pure-comment line (`AuthenticationEndpoints.cs:42`) — net 99 functional. An earlier draft of this spike under-counted Cluster A by 6 (manual aggregation error); corrected on 2026-05-26 during review.

---

## 4. Codemod plan

### Wave 1 — Mechanical rename (safe, large blast radius)

**Target:** all 79 files, ~349 occurrences total (95 functional + 294 extraction).

**Substitution rules (in order):**

| Match | Replace | Rationale |
|-------|---------|-----------|
| `SessionStatusDto.User` (type-ref usages) | `SessionStatusDto.Principal` | Type-level rename |
| `value is SessionStatusDto { IsValid: true, User: not null }` | `value is SessionStatusDto { IsValid: true, Principal: not null }` | Pattern match update |
| `session?.User == null` / `session.User == null` | `session?.Principal == null` / `session.Principal == null` | Null check update |
| `session.User.X` | `session.Principal.Subject.X` | Functional access — defaults to Subject (Wave 2 selectively switches A+B to EffectiveActor) |
| `session!.User!.X` | `session!.Principal!.Subject.X` | Same, with bang-bang variant |

**Execution:** IDE Roslyn refactor preferred over `sed`:
1. In `SessionDto.cs`, rename property `User` → `Principal` (manual edit to introduce the `Principal` record type first).
2. After the rename, the compiler highlights all 79 broken files. Each is a mechanical fix.
3. CI green = Wave 1 done.

**Risk:** any consumer that did **destructured access** like `var (id, role, email) = (session.User.Id, session.User.Role, session.User.Email);` — not seen in grep but possible — must be fixed manually.

### Wave 2 — Semantic disambiguation (Clusters A+B)

**Target:** 63 lines across the files listed in §3 clusters A and B.

**Substitution after Wave 1:**

```csharp
// Wave 1 left:
var command = new CreateAbTestCommand(CreatedBy: session.Principal.Subject.Id, ...);

// Wave 2 (Cluster A — audit attribution):
var command = new CreateAbTestCommand(CreatedBy: session.Principal.EffectiveActor.Id, ...);
```

**Strategy:** manual review per cluster (driven by §3 tables). PR-reviewable diff because each change is annotated by the cluster table. **No automated script for Wave 2** — too easy to over-apply.

### Wave 3 — Legacy impersonation handler dismantling (Refactor in-place per user decision)

**Files touched (deletions):**
- `apps/api/src/Api/BoundedContexts/Administration/Application/Commands/ImpersonateUserCommand.cs` → **renamed** to `ImpersonationStartCommand.cs` with `[AuditableAction("ImpersonationStarted", "Session", Level=2)]` + `[AtomicAudit]`. `[RequireTwoFactor]` kept (D-S2-6).
- `ImpersonateUserCommandHandler.cs` → **rewritten** to: (a) create `UserSessionEntity` with new impersonation fields, (b) NO manual `AddAsync` (the behavior writes the outbox row), (c) tighter eligibility (drop `admin` from caller, add self/demo/banned guards).
- `EndImpersonationCommand.cs` + handler → `ImpersonationEndCommand` + handler with `[AuditableAction("ImpersonationEnded", "Session", Level=1)]`. No manual audit row.
- **New:** `RevokeImpersonationCommand.cs` + handler with `[AuditableAction("ImpersonationRevoked", "Session", Level=2)] [AtomicAudit]`.
- **New:** `AdminImpersonationEndpoints.cs` consolidates start/end/revoke/active. The old impersonate route on `AdminUserActivityEndpoints` is **redirected** to the new endpoint (HTTP 308 Permanent Redirect) for one release cycle, then removed in a follow-up — keeps FE backward compat.

**Migration consideration:** legacy `audit_logs` rows with `action ∈ {impersonate_user_started, impersonated_by_admin, impersonate_user_ended}` are **left in place** (audit log is append-only). New rows after S2 ships use `action ∈ {ImpersonationStarted, ImpersonationEnded, ImpersonationRevoked, ImpersonationAutoEnded}` and carry `impersonated_user_id` populated. Forensic queries that span the cutover must `UNION ALL` both action vocabularies — documented in the kickoff doc's "Open follow-ups".

---

## 5. Risks and edge cases (Nygard)

- **In-flight legacy sessions at cutover.** A session created by the legacy code path before S2 deploy has no `ImpersonatedByUserId` set. The new middleware check `if session.ImpersonatedByUserId is not null` simply doesn't fire on them — they continue to behave as before until the standard ExpiresAt. Acceptable.
- **The hack-signal `IpAddress="impersonated"` becomes redundant.** Cleanup is a separate non-blocking follow-up (or: include it in T3 step "rewrite handler" if effort permits).
- **`RequireAdminSession` on `/admin/impersonation/end` allows ANY admin to end an impersonation.** D-S2 doesn't tighten this. Keep current behaviour (any admin can clean up a forgotten session). Distinction with `RevokeImpersonationCommand`: revoke is superadmin-only and audited as a kill-switch action.
- **`SessionStatusDto` is `internal`** but the wire JSON is public. The Wave 1 codemod leaves the wire format unchanged — the response shape is built by `GetSessionStatusQueryHandler` and projected to `SessionStatusResponse`. Keep that projection backward-compatible (`user` field stays; `actor` field added). See T1 step 5 in the plan.

---

## 6. Deliverables of T0 (this spike)

- ✅ §1 Legacy inventory (commands, handler, endpoints, audit pattern)
- ✅ §2 Gap analysis against the 6 S2 decisions
- ✅ §3 Cluster classification of all 95 functional + ~294 mechanical occurrences (100 lines total checked)
- ✅ §4 Codemod plan in 3 waves
- ✅ §5 Risks and edge cases for the refactor
- ✅ Kickoff doc + plan amended in same commit (PR-internal coherence)

**T0 conclusion:** the refactor is well-bounded. Wave 1 is mechanical (low risk, high blast radius — protected by the 17k+ regression suite). Wave 2 is human-reviewable (69 lines, clear cluster annotations). Wave 3 is the meat — the legacy handler dismantling — and it's contained to 4 files in the Administration BC.

Effort revised: **3-5 giorni** (incluso codemod) — unchanged from the kickoff doc estimate.

**Next task:** T1 — Migration + `Principal` DTO (per the plan).
