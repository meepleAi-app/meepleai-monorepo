# D-4 Spike — AuditLog canonical entity decision

**Date:** 2026-05-25
**Status:** decision
**Scope:** disambiguate `Administration/AuditLogEntity` vs `SecurityAudit/AuditLogEntity` (spec §10 D-4)

---

## Findings

### Administration/AuditLogEntity

**Entity:** `Api.Infrastructure.Entities.AuditLogEntity`  
**Domain entity:** `Api.BoundedContexts.Administration.Domain.Entities.AuditLog`  
**Table:** `audit_logs` (created in `20260410113220_Initial`)

**Schema:**
```
Id (Guid), UserId (Guid?), Action (string), Resource (string),
ResourceId (string?), Result (string), Details (string?),
IpAddress (string?), UserAgent (string?), CreatedAt (DateTime)
```

**Writes:**

| Call site | File | Mechanism |
|-----------|------|-----------|
| `AuditService.LogAsync()` → `_db.AuditLogs.Add(entity)` | `Services/AuditService.cs:48` | Direct DbSet — injected into TotpService, PdfRetrievalEndpoints, AccountLockedEventHandler, AccountUnlockedEventHandler, AuditLoggingBehavior (pipeline behavior), UpdateRuleSpecCommandHandler, UpdateMessageCommandHandler, DeleteMessageCommandHandler, MechanicRecalcBackgroundService, CancelRecalcJobHandler, EnqueueRecalculateAllMechanicMetricsHandler (11 call sites) |
| `AuditLogRepository.AddAsync()` → `DbContext.AuditLogs.AddAsync()` | `Administration/Infrastructure/Persistence/AuditLogRepository.cs:62` | Repository — called from SuspendUserCommandHandler, UnsuspendUserCommandHandler, ImpersonateUserCommandHandler, EndImpersonationCommandHandler, ActivatePromptVersionCommandHandler, SendUserEmailCommandHandler, RestartServiceCommandHandler, AddStagingAllowlistEntryCommandHandler, RemoveStagingAllowlistEntryCommandHandler |
| `_dbContext.AuditLogs.Add(auditLog)` | `SharedKernel/Application/EventHandlers/DomainEventHandlerBase.cs:113` | Direct DbSet — base class for all domain event handlers |
| `_dbContext.Set<AuditLog>().Add(auditLog)` | `DatabaseSync/Application/Commands/SyncTableDataHandler.cs:209` | Direct Set — uses `Administration.Domain.Entities.AuditLog` |
| `_dbContext.Set<AuditLog>().Add(auditLog)` | `DatabaseSync/Application/Commands/ApplyMigrationsHandler.cs:121` | Direct Set — uses `Administration.Domain.Entities.AuditLog` |

**Reads:**

| Call site | File | Mechanism |
|-----------|------|-----------|
| `GetAuditLogsQueryHandler` | `Administration/Application/Queries/GetAuditLogsQueryHandler.cs:25` | `_db.AuditLogs.AsNoTracking()` — used by `AdminAuditLogEndpoints` (GET `/admin/audit-log/`, GET `/admin/audit-log/export`, GET `/admin/users/{userId}/audit-log`) |
| `ExportAuditLogsQueryHandler` | `Administration/Application/Queries/ExportAuditLogsQueryHandler.cs:26` | `_db.AuditLogs.AsNoTracking()` — CSV export endpoint |
| `AuditLogRepository.GetByUserIdAsync()` | `Administration/Infrastructure/Persistence/AuditLogRepository.cs:36` | `DbContext.AuditLogs.AsNoTracking().Where(...)` — called by `GetUserActivityQueryHandler`, `GetUserRoleHistoryQueryHandler` |
| `AuditLogRepository.GetEmailSentLogsAsync()` | `Administration/Infrastructure/Persistence/AuditLogRepository.cs:88` | `DbContext.AuditLogs.AsNoTracking().Where(a => a.Action == "email_sent")` — called by `GetSentEmailsQueryHandler` |
| `AuditLogRepository.GetByResourceAsync()` | `Administration/Infrastructure/Persistence/AuditLogRepository.cs:47` | `DbContext.AuditLogs.AsNoTracking().Where(...)` |

**Migration:** `20260410113220_Initial` — `audit_logs` table created at app genesis.

**Active consumers:** YES — heavily used across 5+ BCs (Administration, SharedKernel, DatabaseSync, Authentication, GameManagement, KnowledgeBase, SharedGameCatalog).

---

### SecurityAudit/AuditLogEntity

**Entity:** `Api.BoundedContexts.SecurityAudit.Infrastructure.Entities.AuditLogEntity`  
**Domain entity:** none (persistence model only — no separate domain entity)  
**Table:** `security_audit_logs` (created in `20260506215626_AuthSecurityFixesSchemaPrep`)

**Schema:**
```
Id (Guid), ActorUserId (Guid?), TargetUserId (Guid?), EventType (string 128),
IpAddress (string? 64), UserAgent (string? 512), Timestamp (DateTime),
Metadata (string?), CorrelationId (string? 128)
```

**DbContext property:** `SecurityAuditLogs` (line 103 of `MeepleAiDbContext.cs`)

**Writes:**

| Call site | File | Mechanism |
|-----------|------|-----------|
| `AuditLogger.LogAsync()` → `dbContext.Set<AuditLogEntity>().Add(entry)` | `SecurityAudit/Infrastructure/Services/AuditLogger.cs:79` | Isolated child scope (fresh `MeepleAiDbContext` via `IServiceScopeFactory`) — ensures audit survives caller transaction rollback. Called via `IAuditLogger` from: `LoginCommandHandler` (4 call sites: success, failure, lockout, 2FA prompt), `RegisterCommandHandler` (1 call site), `UpdateGameCategoryCommand`/`DeleteGameCategoryCommand`/`CreateGameCategoryCommand` (shared-games admin taxonomy #1440), `SharedGameCatalog` mechanic validation handlers. Total: ~8 handlers |

**Reads:** NONE — no handler, query, endpoint, or service reads from `SecurityAuditLogs` or `Set<SecurityAudit.Infrastructure.Entities.AuditLogEntity>()`. The `SecurityAuditLogs` DbSet property exists in `MeepleAiDbContext` but has zero consumers outside of `AuditLogger` itself (write-only path).

**Migration:** `20260506215626_AuthSecurityFixesSchemaPrep` — `security_audit_logs` table created 2026-05-06 (auth security hardening sprint).

**Active consumers (reads):** NO — write-only, no query layer, no admin endpoint, no export.

**Active consumers (writes):** YES — `IAuditLogger` is injected in Authentication BC handlers and SharedGameCatalog admin commands.

---

## Summary comparison

| Dimension | Administration `audit_logs` | SecurityAudit `security_audit_logs` |
|-----------|----------------------------|--------------------------------------|
| Schema purpose | Admin action trail: action / resource / result | Security event log: actor / target / eventType / correlationId |
| Write volume | High — 20+ call sites, pipeline behavior | Moderate — 8 call sites, auth + taxonomy |
| Read consumers | 5 query handlers, 3 admin endpoints, CSV export | **None** |
| Migration | Initial (2026-04-10) | AuthSecurityFixes (2026-05-06) |
| Scope | Cross-BC (SharedKernel base class writes here) | Auth BC + SharedGameCatalog admin commands |
| Semantics | General admin/operational audit trail | Immutable security event log (I10 prep) |
| Transaction isolation | Participates in caller UoW | **Isolated child scope** — survives rollback |
| Read endpoint for SP5 admin console | Already exists (GET /admin/audit-log/) | Missing — would need to be built |

---

## Decision

**Canonical:** `both — complementary, not duplicates`

**Rationale:**

These two entities are NOT duplicates of each other — they serve fundamentally different purposes:

1. **`audit_logs` (Administration):** A broad operational audit trail recording admin actions with `action/resource/result` semantics. Used by the existing admin console (`/admin/audit-log/` endpoints), user activity timeline, role history, sent-emails log, and the `DomainEventHandlerBase` (cross-cutting). This is the table the SP5 admin console spec is already reading from. It is deeply embedded in the codebase (20+ write sites, 5 read handlers, 3 HTTP endpoints).

2. **`security_audit_logs` (SecurityAudit):** A security-specific immutable event log with `actor/target/eventType/correlationId` semantics. Introduced in the I10 auth hardening sprint (2026-05-06). Uses an isolated write scope to guarantee audit durability independent of business transaction outcomes. This is a purpose-built security audit table aligned with OWASP A09 recommendations. Currently write-only — no read layer exists yet.

**Why NOT merge:**
- The schema semantics are intentionally different: `action/resource/result` vs `eventType/actor/target/correlationId`. Merging would require a nullable union of both column sets, producing a wide table with significant nullability for every row.
- The write isolation contract of `SecurityAudit.AuditLogger` (child scope, swallowed exceptions) is architecturally important and would conflict with the UoW-based write path used by `Administration`.
- The `DomainEventHandlerBase` writes to `audit_logs` for ALL domain events cross-BC — changing this would be a wide refactor with high blast radius.
- Both tables are already in production (both have applied migrations). A merge would require a data migration script.

**The original spec concern was valid:** The Administration entity IS the one "effectively used" by the admin console and is not a dead branch. The SecurityAudit entity is ALSO alive (writes active, reads pending). The spec's "SecurityAudit MIGHT be a dead branch" hypothesis is partially correct only for the read side — the write side is active and intentional.

**What this means for SP5 S1 scope:**
- Tasks reading from `audit_logs` (Administration) for the admin console: proceed as planned.
- The `security_audit_logs` read layer (admin security event viewer) is **out-of-scope for S1** — it requires a new query handler + endpoint that does not exist yet. If SP5 S1 spec includes a security audit log viewer, that is a net-new feature, not a wiring task.

---

## Deprecation plan

**Delete:** Nothing. Both entities are active and serve different purposes.

**Migrate data:** No. Two separate tables with different schemas; no overlap in the data they store.

**Rename for clarity (recommended, not blocking):**
- Consider renaming `Api.Infrastructure.Entities.AuditLogEntity` to `AdminAuditLogEntity` to reduce the class-name collision at the C# level. This is a refactor task, not part of S1.
- The namespace collision (`Administration` vs `SecurityAudit` both having a class named `AuditLogEntity`) is the root cause of the D-4 ambiguity. The fix is a rename codemod, not a merge.

**Communication:** No BC contract changes — both entities remain unchanged. Team note: the ambiguity is purely nominal (same class name, different namespaces). The codebase already handles this correctly via fully-qualified names where needed (e.g., `DatabaseSync` handlers import `Administration.Domain.Entities`).

**S1 gate impact:** D-4 is resolved. Tasks 1-4 of S1 can proceed using `Administration/AuditLogEntity` (→ `audit_logs`) as the canonical table for the admin audit log viewer. The `SecurityAudit` BC is a parallel system; do not read from it in S1 scope.
