# Consumer Idempotency Audit — Issue #1535 Phase B Pre-Cutover Gate

**Date:** 2026-06-06
**Issue:** [#1535](https://github.com/meepleAi-app/meepleai-monorepo/issues/1535) — Task 0
**Verdict:** ⚠️ **BLOCKED** — Phase B cutover not safe until 3 cross-cutting fixes + 15+ P0 remediations land
**Scope:** `apps/api/src/Api/BoundedContexts/**/Application/EventHandlers/*.cs` + `WorkflowIntegration/Application/IntegrationEventHandlers/*.cs`
**Methodology:** static analysis via `code-analyzer` subagent reviewing 99 handler classes (the 64-file count understated multi-class files — `SharedGameCatalogAuditEventHandler.cs` declares 7 classes, `QueueStreamEventHandlers.cs` 9, `GameNightN8nEventHandlers.cs` 3, `*StreamEventHandler` 5, etc.)
**Companion docs:** [spec](../docs/superpowers/specs/2026-06-06-issue-1535-event-outbox-design.md) · [plan](../docs/superpowers/plans/2026-06-06-issue-1535-event-outbox.md) · [kickoff](2026-06-06-issue-1535-event-outbox-kickoff.md)

---

## Executive summary

### Methodology

Every `INotificationHandler<TEvent : IDomainEvent>` was categorized into one of:
- ✅ **No-op vestigial** — `await Task.CompletedTask` only (post-#1534 dead code, removed in follow-up cleanup)
- ✅ **Cache invalidation only** — chiama solo `IHybridCacheService.RemoveByTagAsync` (idempotent by definition)
- ✅ **DB write idempotent** — UPSERT, soft-delete con guard, INSERT con UNIQUE, state-convergent SET
- ⚠️ **Broadcast SignalR/SSE** — FE-side dedup needed (payload manca `EventId`)
- ⚠️ **Domain reaction** — raise altro `IMediator.Send(Command)`; idempotency depends su target
- ❌ **DB write NON-idempotent** — counter `++`, append senza dedup, new row `Guid.NewGuid()` ogni call
- ❌ **External HTTP / Email / Slack / LLM** — no remote dedup nel payload (n8n, Slack, SMTP)

### Verdict counts

| Category | Files | Phase B Status |
|---|---|---|
| ✅ No-op vestigial | 44 | ✅ safe |
| ✅ Cache invalidation only | 10 (15+ events) | ✅ safe |
| ✅ DB write idempotent (explicit guards) | 19 | ✅ safe |
| ⚠️ Broadcast SignalR / SSE (FE dedup) | 15+ events | ⚠️ FE render duplicate (acceptable degradation) |
| ⚠️ Domain reaction (raise commands) | 5 | ⚠️ downstream propagation risk |
| ❌ DB write NON-idempotent (counter/append/new-row) | 11 | 🔴 **MUST FIX** |
| ❌ External HTTP / Email / Slack / LLM | 24 + 22 UserNotifications | 🔴 **MUST FIX** |

**Total handlers requiring outbox-aware idempotency**: ~57. Vast majority (~30) concentrated in `UserNotifications/*NotificationHandler.cs` and share **a single root cause** — `INotificationDispatcher.DispatchAsync` does NOT dedup by EventId.

---

## Critical findings (P0 BLOCKERs)

### Cross-cutting fixes (3 changes cover ~30 BLOCKERs)

#### CF-1 — `INotificationDispatcher` idempotency (covers 22 UserNotifications handlers + 5 inline notification creators)

**Evidence:** `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Services/NotificationDispatcher.cs:50`

```csharp
var notification = new Notification(
    id: Guid.NewGuid(),                   // ❌ NEW id every call
    userId: message.RecipientUserId,
    type: message.Type,
    // ...
    correlationId: correlationId);        // NOT same as SourceEventId
await _notificationRepository.AddAsync(notification, ct);   // ❌ insert duplicate row on re-fire

// Step 3-5: enqueue NotificationQueueItem per channel (Email, Slack DM, Slack team)
// → NotificationQueueItem.Create() ALSO generates new IDs without EventId dedup
```

Plus inline (bypassing dispatcher):
- `KnowledgeBase/CircuitBreakerStateChangedEventHandler.cs:79-91`
- `KnowledgeBase/ModelDeprecatedNotificationHandler.cs:79-91`
- `KnowledgeBase/ModelDeprecatedAutoFallbackHandler.cs:130-145`
- `GameManagement/GameSessionTerminatedEventHandler.cs:37`

**Remediation:**
1. Add `SourceEventId` (nullable Guid) to `NotificationMessage`.
2. Add `SourceEventId UNIQUE` index on `Notification` aggregate.
3. Add `SourceEventId UNIQUE` composite (`ChannelType, RecipientUserId, SourceEventId`) on `NotificationQueueItem`.
4. In `NotificationDispatcher.DispatchAsync`: short-circuit if `SourceEventId is not null && _notificationRepository.ExistsBySourceEventIdAsync(eventId)` returns true.
5. Refactor 22 `*NotificationHandler` callers + 5 inline creators to propagate `notification.EventId` into `NotificationMessage.SourceEventId`.

**Estimated effort:** 3-4gg (1 migration + 1 dispatcher refactor + ~27 caller updates + ~30 unit test updates).

#### CF-2 — Financial / counter columns get `SourceEventId UNIQUE` (covers 5 BLOCKERs)

**Evidence:**
- `BusinessSimulations/Infrastructure/Services/LedgerTrackingService.cs:66` — **financial double-counting**
- `DocumentProcessing/Infrastructure/Services/ProcessingMetricsService.cs:52-63` — ETA average corruption
- `GameManagement/Application/EventHandlers/LiveSessionCompletedEventHandler.cs:103` — duplicate PlayRecord
- `GameManagement/Application/EventHandlers/SessionPausedAutoSnapshotHandler.cs:59` — duplicate SessionSnapshot
- `UserLibrary/Application/EventHandlers/CreateProposalMigrationOnApprovalHandler.cs:104` — duplicate ProposalMigration

**Remediation:**
1. Migration `AddSourceEventIdToCounterTables`: add `SourceEventId UUID UNIQUE NULL` (nullable for backfill) to `LedgerEntry`, `ProcessingMetric`, `PlayRecord`, `SessionSnapshot`, `ProposalMigration`.
2. Update factory methods to accept + persist `SourceEventId`.
3. Update handler callers to pass `domainEvent.EventId`.
4. Wrap insertion in `try { Add; SaveChanges; } catch (DbUpdateException ex) when (ex.IsUniqueViolation()) { /* skip */ }` or use `INSERT … ON CONFLICT DO NOTHING` raw SQL.

**Estimated effort:** 2-3gg (1 migration + 5 factory updates + 5 handler updates + tests).

#### CF-3 — Reaction handlers no-counter / pre-check (covers 4 BLOCKERs)

**Evidence:**
- `KnowledgeBase/Application/EventHandlers/Shared/AgentStateMapper.cs:64` — `currentTurn + 1` (driven by `TurnAdvancedEventHandler.cs:63`)
- `AgentMemory/Application/EventHandlers/OnSessionCompletedUpdateStatsHandler.cs:81,99,115,119` — `++` increments on PlayerMemory + GroupMemory
- `KnowledgeBase/Application/EventHandlers/GenerateAgentSummaryHandler.cs:73` — LLM call ($)
- `AgentMemory/Application/EventHandlers/OnDisputeOverriddenAddHouseRuleHandler.cs:66` — duplicate `AddHouseRule`

**Remediation:**
- `AgentStateMapper.UpdateTurn`: change signature to accept target turn number from event payload (e.g., `turnEvent.NewTurnNumber`); deprecate `+ 1` pattern.
- `OnSessionCompletedUpdateStatsHandler`: add `LastProcessedEventId UUID NULL` to `PlayerMemory` + `GroupMemory`; early-exit guard at top of `HandleEventAsync`.
- `GenerateAgentSummaryHandler:73`: add pre-flight `if (pauseSnapshot.Summary is not null) return;` (cheap read; avoids LLM cost).
- `OnDisputeOverriddenAddHouseRuleHandler`: dedup by `(GameId, OwnerId, RuleText.Trim().ToLower())` OR persist `LastProcessedEventId` per memory aggregate.

**Estimated effort:** 2-3gg (4 file changes + migration for `LastProcessedEventId` columns + tests).

### Isolated P0 BLOCKERs (handle individually, ~6 issues)

| # | File:Line | Side Effect | Remediation |
|---|---|---|---|
| 1 | `Administration/EventHandlers/ChannelDispatchHandler.cs:75` | Slack webhook + Email per AlertFiredEvent | Persist `last_dispatched_event_id` per `AlertChannel` aggregate |
| 2 | `Administration/EventHandlers/HealthStatusChangedEventHandler.cs:53` | Slack alert | Dedup on `(ServiceName, OccurredAt)` window |
| 3 | `Authentication/EventHandlers/AccessRequestApprovedEventHandler.cs:33` | `SendInvitationCommand` → DB row + email | Pre-check `accessRequest.InvitationId != null` |
| 4 | `Authentication/EventHandlers/AccessRequestCreatedEventHandler.cs:32` | Slack alert | Track last EventId on AccessRequest |
| 5 | `Authentication/EventHandlers/AccountLockedEventHandler.cs:80` | Account-locked email | Persist `LastLockoutEventId` on User |
| 6 | `GameManagement/EventHandlers/GameNightInvitationRespondedHandler.cs:107` | RSVP confirmation email | Persist `RsvpConfirmationSentAt` on `GameNightInvitation` |

### n8n webhook handlers (3 BLOCKERs, cross-system contract change)

- `WorkflowIntegration/EventHandlers/GameNightN8nEventHandlers.cs:27,53,78` — 3 separate handlers send aggregate IDs but NO `EventId` nel payload JSON.
- `IN8nWebhookClient.TriggerWorkflowAsync(webhookPath, object payload)` — generic signature; caller chooses payload shape.

**Remediation:**
1. Modify 3 handlers to include `domainEventId = notification.EventId` in the payload object.
2. **Cross-system contract change**: n8n workflows on the receiving side MUST dedup by `domainEventId` (Set node + IF condition). Document in `docs/for-developers/integrations/n8n-idempotency-contract.md`.
3. Estimated effort: 1gg BE + 1gg n8n workflow audit (DevOps).

### Largest single data-footprint risk

**`SharedGameCatalog/EventHandlers/ShareRequestApprovedDocumentHandler.cs:33`** → `ShareRequestDocumentService.CopyDocumentsToSharedGame:192,207` — creates new R2 blob paths + new `PdfDocument` rows on every fire. Re-fire would **duplicate megabytes of R2 storage + spurious PdfDocument index entries**.

**Remediation:** track `CopiedToSharedGameAt` timestamp on `ShareRequest` aggregate; pre-check + early-exit if non-null.

---

## ⚠️ Domain reaction concerns (5 handlers; downstream propagation)

| File:Line | Risk |
|---|---|
| `GameManagement/EventHandlers/AutoCreateAgentOnPdfReadyHandler.cs:108,167,171` | Internal guards present at line 108, 204. **PITFALL**: `RecordUsageAsync(TierAction.CreateAgent)` at line 167 fires AFTER agent creation — re-fire would have early-exited at 108. Verify with integration test. Recommend moving inside same tx OR dedup via tier-usage event log. |
| `KnowledgeBase/EventHandlers/AutoCreateAgentOnPdfReadyHandler.cs:101` | `CreateGameAgentCommand` throws `ConflictException` on re-attempt → catch emits `AutoAgentCreationFailedEvent` ⇒ spurious "AGENT_CREATION_FAILED" admin notification on re-fire. **Remediation:** distinguish "already exists" from "creation failure" in catch block. |
| `Administration/EventHandlers/RagBackupOnIndexedEventHandler.cs:41` | `IncrementalRagBackupCommand` → redundant S3 upload (likely overwrite; metadata may grow). **Remediation:** S3 upload idempotency (same key = overwrite is OK; metadata table needs dedup). |
| `KnowledgeBase/EventHandlers/VectorDocumentIndexedEventHandler.cs:67` | Re-publishes `VectorDocumentReadyIntegrationEvent` ⇒ amplifies non-idempotent downstream consumers. **Remediation:** addressed transitively by fixing downstream. |
| `KnowledgeBase/EventHandlers/TurnAdvancedEventHandler.cs:63` | See CF-3 |

---

## ⚠️ Broadcast SignalR / SSE (15+ events; FE-side dedup contract)

Payloads carry aggregate IDs but NOT `EventId` ⇒ FE cannot dedup reliably. Re-fire = double UI render.

**Files affected:**
- `Administration/EventHandlers/DashboardStreamEventHandler.cs:42,54,68,82,103` (SSE × 5 events)
- `GameManagement/EventHandlers/DisputeResolvedSignalRHandler.cs:30`
- `GameManagement/EventHandlers/SessionPausedSignalRHandler.cs:30`
- `GameManagement/Commands/GameNight/StructuredDisputeResolvedSignalRHandler.cs:30`
- `DocumentProcessing/EventHandlers/QueueStreamEventHandlers.cs:36,60,84,126,170,194,219,243` (SSE × 8 classes)
- `DocumentProcessing/EventHandlers/PrivatePdfAssociatedEventHandler.cs:99` (SSE, post DB-guard — only fires on actual job creation)

**Remediation (P2 — accept degraded duplicate UI render as Phase B trade-off):**
1. Add `eventId` to every SSE/SignalR payload.
2. Document FE-side dedup contract in `apps/web/CLAUDE.md`: "ignore SSE/SignalR message if `seen.has(eventId)` (LRU map, size 100, 60s TTL)".
3. Phase B can ship without FE changes (duplicate render is UX-degradation, not data loss).

**Estimated effort:** 1gg BE payload changes + 1gg FE dedup wrapper.

---

## ✅ PASS list (consumer safe for Phase B — no changes required)

### Vestigial audit-hook overrides (44 files)

Authentication: `EmailChangedEventHandler`, `OAuthAccountLinkedEventHandler`, `OAuthAccountUnlinkedEventHandler`, `OAuthTokensRefreshedEventHandler`, `PasswordChangedEventHandler`, `PasswordResetEventHandler`, `RoleChangedEventHandler`, `SessionRevokedEventHandler`, `TwoFactorEnabledEventHandler`, `TwoFactorDisabledEventHandler`.

GameManagement: `GameCreatedEventHandler`, `GameLinkedToBggEventHandler`, `GameSession{Abandoned,Completed,Created,Paused,Resumed,Started}EventHandler`, `GameUpdatedEventHandler`, `PlayerAddedToSessionEventHandler`.

KnowledgeBase: `AgentActivatedEventHandler`, `AgentConfiguredEventHandler`, `AgentCreatedEventHandler`, `AgentDeactivatedEventHandler`, `AgentInvokedEventHandler`, `ChatThreadCreatedEventHandler`, `Message{Added,Deleted,Updated}EventHandler`, `Thread{Closed,Reopened}EventHandler`, `VectorDocumentMetadataUpdatedEventHandler`, `VectorDocumentSearchedEventHandler`.

SharedGameCatalog: 7 audit-only handlers in `SharedGameCatalogAuditEventHandler.cs`.

SystemConfiguration: `ConfigurationCreatedEventHandler`, `ConfigurationDeletedEventHandler`, `UserRateLimitOverride{Created,Removed}EventHandler`.

UserLibrary: `PrivatePdfRemovedEventHandler`.

WorkflowIntegration: `N8nConfiguration{Created,Tested,Updated}EventHandler`, `WorkflowErrorLoggedEventHandler`, `WorkflowRetriedEventHandler`, `IntegrationEventHandlers/GameCreatedIntegrationEventHandler`.

SharedGameCatalog: `PdfReadyForProcessingEventHandler`.

### Cache invalidation only (10 files, 15+ events)

`Administration/{DashboardCacheInvalidation, UserActivityCacheInvalidation, StagingAllowlistCacheInvalidator, ProviderKeyRotated}EventHandler`; `KnowledgeBase/PdfMetadataChangedCacheInvalidationHandler`; `SharedGameCatalog/{AgentDefinitionChangedForCatalogAggregates, ToolkitChangedForCatalogAggregates, MechanicMetricsRecalculatedCacheInvalidation, SessionCompletedForContributors}Handler`; `SystemConfiguration/RateLimitConfigUpdatedEventHandler`; `UserLibrary/GamebookCacheInvalidationHandler`.

⚠️ **MINOR**: 2 files (`DashboardCacheInvalidationEventHandler`, `UserActivityCacheInvalidationEventHandler`) increment Prometheus counter `Add(1)` per call — double-fire = metric inflation. Accept as minor metric corruption (decision: not blocker).

### Idempotent guarded writes / state-convergent (19 files)

`GameManagement/SessionStartedHandler` (documented idempotent on Published state); `GameToolkit/CreateDefaultToolkitWhenGameAddedHandler` (`ExistsDefaultAsync` guard); `SharedGameCatalog/{CatalogSeedApproved, SharedGamePdfUploaded, DocumentApprovedForRag, PdfCoverGenerated, VectorDocumentIndexedForKbFlag, ShareRequestRejectedDocument, BadgeEvaluationOnApproval}Handler`; `KnowledgeBase/{UpdateSnapshotSummary, ModelDeprecatedAutoFallback, SessionFinalized, ScoreUpdated, GamePhaseChanged}EventHandler`; `DocumentProcessing/{PrivatePdfAssociated, VectorDocumentReadyState, PdfDeleted}EventHandler`; `UserLibrary/GameRemovedFromLibraryCustomCoverHandler`.

---

## Phase B cutover decision

### ❌ NOT SAFE TO CUTOVER YET

The original plan (`docs/superpowers/plans/2026-06-06-issue-1535-event-outbox.md`) assumed consumer idempotency would be largely natural. The audit reveals **3 cross-cutting + 15 isolated BLOCKER issues** representing ~10-14gg additional work.

### Revised effort estimate

| Plan phase | Original estimate | Revised estimate | Notes |
|---|---|---|---|
| Phase 1 Foundation (T0–T2) | 4gg | 4gg | unchanged |
| **Pre-Phase B remediation (NEW)** | – | **+10-14gg** | CF-1 (3-4gg) + CF-2 (2-3gg) + CF-3 (2-3gg) + 6 isolated (2-3gg) + 3 n8n (1-2gg) |
| Phase 2 Hybrid dispatch (T3–T5) | 4gg | 4gg | unchanged (Hybrid mode is safe by definition — double-publish but consumers behave today) |
| Phase 3 Observability (T6) | 2gg | 2gg | unchanged |
| Phase 4 Acceptance + Hybrid deploy (T7–T8) | 3gg | 3gg | unchanged |
| Phase 5 Cutover + cleanup (T9–T10) | 2gg | 2gg | unchanged (T10 cleanup still valid) |
| **Total** | **15gg** | **25-29gg** | +66% scope expansion |

### Workflow recommendation

**Sequence:**
1. **PR #1935 (current)** — ship doc only, no production code. *Already shipped.*
2. **PR remediation-CF1** — `INotificationDispatcher` idempotency + 22 caller updates. ~3-4gg. Independent of #1535 (improves current `Hybrid` behavior even WITHOUT outbox).
3. **PR remediation-CF2** — Counter columns SourceEventId. ~2-3gg. Independent of #1535.
4. **PR remediation-CF3** — Reaction handlers no-counter. ~2-3gg. Independent of #1535.
5. **6 PRs isolated BLOCKERs** — 1 per handler, ~3-4gg cumulative. Independent.
6. **PR n8n contract change** — payload + workflows. ~1-2gg. Independent.
7. **PR #1535 Phase 2 (T3-T5)** — outbox implementation + Hybrid mode. ~4gg.
8. **PR #1535 Phase 3 (T6)** — observability. ~2gg.
9. **PR #1535 Phase 4 (T7-T8)** — acceptance + staging Hybrid soak 24h. ~3gg.
10. **PR #1535 Phase 5 (T9-T10)** — cutover + cleanup. ~2gg.

**Rationale for sequence:** steps 2-6 are **independently shippable** — they improve idempotency of the CURRENT in-process MediatR dispatch model (each is a standalone bug fix). They are pre-requisite for #1535 Phase B but don't depend on #1535 being shipped.

### Compromise: ship Hybrid permanently?

Alternative path: keep `EventDispatch:Mode = "Hybrid"` indefinitely (PR #1935 Phase A only). Pros: Phase B cutover doesn't ship → no BLOCKER pressure. Cons: ❌ doesn't solve original issue (event dispatch still inside `SaveChangesAsync`, still subject to rollback race). REJECTED.

---

## Recommendations

### Immediate (within current PR cycle)

1. **Update issue #1535 with this finding** — Phase B requires +10-14gg pre-work; revise milestone if any.
2. **Open follow-up issues** for the 3 cross-cutting fixes + 6 isolated BLOCKERs (9 net-new issues, can be parallelized).
3. **Update plan doc** Task 0 → "BLOCKER count + remediation outline"; insert "Phase 1.5 — Consumer idempotency remediation" between Phase 1 and Phase 2.
4. **No code changes** in this audit cycle — all findings tracked for parallel implementation.

### Phase B gate (revised acceptance criterion)

> Phase B (cutover from Hybrid to OutboxOnly) MUST NOT merge until:
> - CF-1 (NotificationDispatcher idempotency) shipped + deployed to staging 24h
> - CF-2 (Counter columns SourceEventId) shipped + deployed to staging 24h
> - CF-3 (Reaction handlers no-counter) shipped + deployed to staging 24h
> - 6 isolated BLOCKERs shipped (Slack/email/RSVP)
> - 3 n8n handlers + workflow contract updated

### Acceptable Phase B residuals (downgraded)

The following can be deferred AFTER Phase B without blocking cutover:
- 15+ Broadcast SignalR/SSE handlers (FE dedup contract — UX degradation, not data corruption)
- 4 ⚠️ Domain reaction handlers in `KnowledgeBase/AutoCreateAgentOnPdfReadyHandler.cs`, `Administration/RagBackupOnIndexedEventHandler.cs`, `KnowledgeBase/VectorDocumentIndexedEventHandler.cs` (transitively covered by downstream fixes)
- ⚠️ MINOR Prometheus counter inflation in 2 cache invalidation handlers

---

## References

- Issue [#1535](https://github.com/meepleAi-app/meepleai-monorepo/issues/1535) — parent
- PR #1532 — SP5 S1 audit schema + outbox (mitigation predecessor)
- PR #1788 — DomainEventAuditHandler dual-path consolidation (`DomainEventHandlerBase` audit hooks now DEAD)
- PR #1934 — RotateProviderKeyCommand (#1859), concrete case rejected `[AtomicAudit]` for Redis side-effect
- Memo [[feedback_atomic_audit_and_external_side_effects]]
- Memo [[feedback_audit_metadata_exact_name_filter]]
