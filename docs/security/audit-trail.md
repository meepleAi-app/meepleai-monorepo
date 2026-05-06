# Audit Trail

> **Last Updated**: 2026-05-06 (authored from current code state — this doc never existed in git history)
> **Source**: Issue #3691 "Audit Log System"
> **Bounded Context**: `Administration` (with cross-BC consumers)
> **Authoring source files**:
> - `apps/api/src/Api/BoundedContexts/Administration/Domain/Entities/AuditLog.cs`
> - `apps/api/src/Api/BoundedContexts/Administration/Application/Behaviors/AuditLoggingBehavior.cs`
> - `apps/api/src/Api/BoundedContexts/Administration/Application/Attributes/AuditableActionAttribute.cs`
> - `apps/api/src/Api/BoundedContexts/Administration/Infrastructure/Scheduling/AuditLogRetentionJob.cs`
> - `apps/api/src/Api/Routing/AdminAuditLogEndpoints.cs`
> - `apps/api/src/Api/Services/AuditService.cs`

## Overview

MeepleAI uses a **single, append-only audit log** for tracking administrative and privacy-sensitive actions. Audit is implemented as a **MediatR pipeline behavior** that automatically intercepts commands decorated with `[AuditableAction]` — no manual logging code in handlers.

**Design principles**:
- ✅ **Automatic**: decorate the command, get audit for free
- ✅ **Resilient**: audit failures **never** break business operations
- ✅ **Filterable**: structured fields (Action, Resource, Result, dates)
- ✅ **Exportable**: CSV/JSON download for compliance reporting
- ✅ **Bounded retention**: 90-day default cleanup via Quartz job

---

## Architecture

### Domain layer

The `AuditLog` aggregate root (`Administration/Domain/Entities/AuditLog.cs`) holds an immutable record of one auditable event. Fields are populated only at construction; setters are private.

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `Id` | `Guid` | ✅ | Primary key (inherited from `AggregateRoot<Guid>`) |
| `UserId` | `Guid?` | ⚠️ | Acting user. Null for system-initiated events (e.g. retention job) |
| `Action` | `string` | ✅ | Discriminator (e.g. `"UserImpersonate"`, `"DeleteUser"`) |
| `Resource` | `string` | ✅ | Target resource type (e.g. `"User"`, `"Service"`, `"FeatureFlag"`) |
| `ResourceId` | `string?` | ⚠️ | Target resource id (extracted from `Id`/`UserId`/`TargetUserId` properties) |
| `Result` | `string` | ✅ | `"Success"` \| `"Error"` \| `"Denied"` |
| `Details` | `string?` | ⚠️ | JSON metadata (command properties, error info) |
| `IpAddress` | `string?` | ⚠️ | Client IP from `HttpContext.Connection.RemoteIpAddress` |
| `UserAgent` | `string?` | ⚠️ | `User-Agent` header |
| `CreatedAt` | `DateTime` | ✅ | UTC timestamp set at entity construction |

The repository interface lives at `Administration/Domain/Repositories/IAuditLogRepository.cs`; EF Core implementation at `Administration/Infrastructure/Persistence/AuditLogRepository.cs`.

> **Note**: a separate, narrower audit aggregate exists in the `SharedGameCatalog` BC (`MechanicStatusAudit`) for tracking moderation status of game mechanics. It is **not** unified with the main `AuditLog`; the two serve different domains.

### Application layer — pipeline behavior

`AuditLoggingBehavior<TRequest,TResponse>` is registered as a MediatR `IPipelineBehavior` and runs around every command. Its decision tree:

```
Request arrives at handler
   │
   ▼
Has [AuditableAction]? ──no──> next() (no audit overhead)
   │ yes
   ▼
Extract context: userId, email, ipAddress, userAgent (from HttpContext)
Extract resourceId: try Id → UserId → TargetUserId properties
   │
   ▼
   ┌──────────────┐
   │  next()      │
   └──────────────┘
        │
   ┌────┴────┐
   ▼         ▼
success   exception
   │         │
   ▼         ▼
Log "Success"  Log "Error" (then re-throw)
```

**Sensitive property filter**: when serializing command properties into `Details`, properties named `Password`, `Token`, `Secret`, or `ApiKey` are excluded from the metadata JSON.

### Application layer — `[AuditableAction]` attribute

```csharp
[AuditableAction("UserImpersonate", "User", Level = 2)]
internal sealed record ImpersonateUserCommand(Guid UserId) : IRequest<Result>;
```

| Property | Meaning |
|----------|---------|
| `Action` | Free-text action discriminator (the same value goes into `AuditLog.Action`) |
| `Resource` | Resource type discriminator |
| `Level` | UI confirmation level: `1` = warning modal · `2` = typed `CONFIRM` required |

The `Level` is a hint to the frontend; backend audit logging is uniform regardless of level.

### Infrastructure layer — retention

`AuditLogRetentionJob` (Quartz.NET, `[DisallowConcurrentExecution]`) deletes entries older than `DefaultRetentionDays = 90`. Schedule is configured in the Quartz pipeline registration (see `Infrastructure` setup); job logs deleted count + cutoff date for observability.

> ⚠️ **Compliance note**: 90 days is the default. If a regulatory regime (GDPR Art. 30, SOC 2, etc.) requires longer retention, raise the constant or shift to a configurable value before that retention regime applies.

---

## Currently audited commands

The following commands are decorated with `[AuditableAction]` (verified 2026-05-06 via `grep -rln "\[AuditableAction" apps/api/src/Api/BoundedContexts/`):

### Administration BC
- `ChangeUserRoleCommand` — role change (Admin/Editor/User)
- `DeleteUserCommand` — admin-initiated user deletion
- `SuspendUserCommand` — temporary user suspension
- `UpdateUserAiConsentCommand` — AI consent flag changes
- `UpdateUserTierCommand` — subscription tier changes

### Authentication BC (privacy-sensitive)
- `DeleteOwnAccountCommand` — self-service account deletion (GDPR right-to-erasure)
- `ExportUserDataCommand` — self-service data export (GDPR portability)

### KnowledgeBase BC
- `DeleteUserLlmDataCommand` — purge of user-generated LLM artifacts

> **Adding a new auditable command**: decorate the command class with `[AuditableAction("VerbNoun", "ResourceName", Level = 1|2)]`. No other code changes required — the pipeline behavior picks it up via reflection.

---

## API endpoints

### Admin endpoints (`/admin/audit-log`)
> Authorization: `RequireAdminSession()` middleware on every endpoint.

#### `GET /admin/audit-log/`
List audit entries with pagination and filtering.

Query parameters: `limit` (default 50), `offset` (default 0), `adminUserId`, `action`, `resource`, `result`, `startDate`, `endDate`.

Returns `AuditLogListResult { Entries, TotalCount, Limit, Offset }`.

#### `GET /admin/audit-log/export`
Export filtered entries as a downloadable file (CSV/JSON depending on `ExportAuditLogsQueryHandler` content type).

Query parameters: same as list endpoint, minus pagination.

Returns `IResult` with `File(content, contentType, fileName)`.

#### `GET /admin/users/{userId:guid}/audit-log`
Per-user view (issue #124). Filters server-side by `AdminUserId = userId`.

### User self-service (`/users/me/activity`)
Located in `UserActivityEndpoints.cs`, this lets a user view their own audit entries (filtered to events they performed). Useful for transparency / GDPR Art. 15 (right of access).

---

## Event schema in storage

### `AuditLog` table (EF Core via `MeepleAiDbContext.AuditLogs`)
The default EF Core convention maps the entity directly: column names match property names (PascalCase → SQL identifier). `Details` is a `text`/`nvarchar(MAX)` column holding JSON.

### `Details` JSON shape (success path)
```json
{
  "confirmationLevel": 2,
  "adminEmail": "admin@example.com",
  "commandType": "DeleteUserCommand",
  "Reason": "spam-account",
  "TargetUserId": "9c651e4e-..."
}
```

### `Details` JSON shape (error path)
```json
{
  "confirmationLevel": 2,
  "adminEmail": "admin@example.com",
  "commandType": "DeleteUserCommand",
  "errorType": "NotFoundException",
  "errorMessage": "User not found"
}
```

> Properties named `Password`, `Token`, `Secret`, `ApiKey` are **excluded** from the JSON before serialization (defense in depth — these should not be in command DTOs in the first place, but this is a backstop).

---

## Privacy considerations

### PII handled by the audit log
- **`UserId`** (link to acting user) — required for accountability
- **`IpAddress`** — required for forensic value, but is PII under GDPR
- **`UserAgent`** — fingerprintable, considered PII in some regimes
- **`Details.adminEmail`** — admin's email captured at audit time

### Retention vs erasure
- Default retention is **90 days** (Quartz job). Most regimes consider this proportionate for security audit logs.
- **GDPR right-to-erasure (Art. 17)**: when a user requests account deletion (via `DeleteOwnAccountCommand`), audit entries about *them* are **NOT** automatically purged — they are retained for accountability of the admin/system actions. The user's PII (email) is captured in `Details` JSON but the user record itself can be deleted independently. **Gap**: there is currently no automated process to redact PII fields in audit entries when the source user is deleted; this should be evaluated against the legitimate-interest balance per GDPR Art. 6(1)(f).

### IP address handling
`IpAddress` is captured directly as a string. There is currently **no anonymization step** (e.g. truncating the last octet for IPv4). Consider adding if regulatory regime tightens.

---

## Resilience pattern

The `AuditLoggingBehavior.LogAuditEntryAsync` method has a deliberate `catch (Exception ex)` (with `#pragma warning disable CA1031`) that swallows audit-write failures and logs them via `ILogger`. This is intentional:

> *"Audit logging failures never break business operations."*
> — `AuditLoggingBehavior.cs:14`

This means: if the audit DB is down, an admin can still delete a user. The trade-off is **availability over auditability**. In a high-assurance environment this trade-off may need to flip; that would be a deliberate change to the catch handler.

---

## Operations

### Manual queries

Read entries directly from PostgreSQL (e.g. for incident response):
```sql
SELECT "Id", "UserId", "Action", "Resource", "Result", "CreatedAt"
FROM "AuditLogs"
WHERE "CreatedAt" > NOW() - INTERVAL '24 hours'
  AND "Result" = 'Error'
ORDER BY "CreatedAt" DESC
LIMIT 100;
```

### Retention job control
The Quartz job runs on the schedule defined in the Quartz registration (see `Program.cs` / `Infrastructure` setup). To change retention, modify `AuditLogRetentionJob.DefaultRetentionDays`. To pause: unregister the job in Quartz config.

### Monitoring
Audit-write failures are logged at `LogLevel.Error` with the message:
```
Failed to write audit log for action {Action} on {Resource} by admin {AdminUserId}
```
Set up an alert on this log line in your observability stack — sustained audit failures indicate either DB issues or a broken contract somewhere in the pipeline.

---

## Future work

Documented gaps as of 2026-05-06 (open for follow-up issues):

1. **PII redaction on user deletion**: when a user is deleted via GDPR erasure, their email persisted in `AuditLog.Details` is not redacted. Decide policy and implement if needed.
2. **Configurable retention**: `DefaultRetentionDays = 90` is a const. Move to configuration if compliance requires per-environment tuning.
3. **IP anonymization**: no truncation/hashing; raw IP stored. Evaluate if regimes evolve.
4. **Tamper-evident hash chain**: current schema is append-only by convention but has no cryptographic chaining (Merkle, hash-of-prev). Consider for high-assurance contexts.
5. **Cross-BC audit unification**: `MechanicStatusAudit` (SharedGameCatalog) is separate. Decide whether to unify under a single audit aggregate or keep BC-specific audits.
6. **Standardized export format**: confirm `ExportAuditLogsQueryHandler` produces stable CSV/JSON schema with documented columns; add a header version field for future schema evolution.
7. **Audit of audit access**: reads of `/admin/audit-log` are not themselves audited. For high-assurance regimes, consider auditing audit reads (recursive but bounded).

---

## Related Documentation

- [SECURITY.md](../../SECURITY.md) - Top-level security policy
- [`docs/security/oauth-security.md`](./oauth-security.md) - Authentication audit context
- [`apps/api/src/Api/BoundedContexts/Administration/README.md`](../../apps/api/src/Api/BoundedContexts/Administration/README.md) - Administration BC overview
- Issue #3691 - Audit Log System (origin)
- Issue #4652 - Admin Dashboard extension (added user info to DTO)
- Issue #124 - Per-user audit log endpoint
