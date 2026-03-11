# Phase 4: Admin User Management + Audit — Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Admin can change user roles inline, view per-user activity timeline, and access a dedicated cross-user audit log page with filtering, search, and CSV export.

**Architecture:** New `AuditLogEntry` entity in Administration bounded context with event-driven population. Modify `RoleChangedEvent` to include `ChangedById`. New dedicated `PUT /admin/users/{id}/role` endpoint. Frontend: inline role dropdown in user table, activity tab in user detail, dedicated `/admin/monitor/audit` page. Integrates with Epic #124.

**Tech Stack:** .NET 9, EF Core, MediatR, Quartz (retention job), Next.js 16, React 19, TanStack Table

**Spec:** `docs/superpowers/specs/2026-03-11-admin-invite-onboarding-design.md` — Phase 4

**Independent of:** Phases 1-3 (audit system is self-contained)

---

## File Structure

### Backend — New Files
| File | Responsibility |
|------|---------------|
| `Administration/Domain/Entities/AuditLogEntry.cs` | DDD entity |
| `Administration/Domain/Enums/AuditAction.cs` | 26-value enum |
| `Administration/Domain/Repositories/IAuditLogRepository.cs` | Repository interface |
| `Administration/Infrastructure/Repositories/AuditLogRepository.cs` | EF Core implementation |
| `Administration/Infrastructure/Configuration/AuditLogEntryConfiguration.cs` | EF config with indexes |
| `Administration/Application/Commands/ChangeUserRoleCommand.cs` | Dedicated role change |
| `Administration/Application/Queries/GetAuditLogQuery.cs` | Paginated + filtered |
| `Administration/Application/Queries/GetUserAuditLogQuery.cs` | Per-user filtered |
| `Administration/Application/Queries/ExportAuditLogQuery.cs` | CSV export |
| `Administration/Application/EventHandlers/AuditLogEventHandler.cs` | Centralized audit writer |
| `Administration/Infrastructure/Jobs/AuditLogCleanupJob.cs` | Quartz: 90-day retention |
| `Routing/AdminAuditEndpoints.cs` | Audit log endpoints |

### Backend — Modified Files
| File | Change |
|------|--------|
| `Authentication/Domain/Events/RoleChangedEvent.cs` | Add `ChangedById: Guid` property |
| `Authentication/Domain/Entities/User.cs` | `UpdateRole` accepts `changedById` param |
| `AdminUserEndpoints.cs` | Add `PUT /admin/users/{id}/role` |
| `Administration/Infrastructure/DependencyInjection/AdministrationServiceExtensions.cs` | Register audit services |
| `Program.cs` | Map audit endpoints, register Quartz job |

### Frontend — New Files
| File | Responsibility |
|------|---------------|
| `components/admin/users/InlineRoleSelect.tsx` | Dropdown + confirmation modal |
| `components/admin/users/UserActivityTimeline.tsx` | Per-user event timeline |
| `app/admin/(dashboard)/monitor/audit/page.tsx` | Audit log page |
| `components/admin/audit/AuditLogTable.tsx` | Filterable table |
| `components/admin/audit/AuditLogFilters.tsx` | Filter controls |
| `components/admin/audit/AuditLogExport.tsx` | CSV export button |
| `lib/api/clients/auditClient.ts` | API client |
| `lib/api/schemas/audit.schemas.ts` | Zod schemas |

### Frontend — Modified Files
| File | Change |
|------|--------|
| `lib/api/index.ts` | Register auditClient |
| `components/admin/users/activity-table.tsx` | Wire to real API (currently mock data) |
| Admin user detail page | Add "Activity" tab |

### Tests
| File | Scope |
|------|-------|
| `Api.Tests/Administration/Domain/AuditLogEntryTests.cs` | Unit: entity |
| `Api.Tests/Administration/Commands/ChangeUserRoleCommandTests.cs` | Unit: handler |
| `Api.Tests/Administration/Queries/GetAuditLogQueryTests.cs` | Unit: query handler |
| `Api.Tests/Administration/EventHandlers/AuditLogEventHandlerTests.cs` | Unit: event handler |
| `apps/web/__tests__/admin/audit/AuditLogTable.test.tsx` | Frontend: table |
| `apps/web/__tests__/admin/users/InlineRoleSelect.test.tsx` | Frontend: role change |

### Migration
| File | Description |
|------|-------------|
| `Infrastructure/Migrations/{timestamp}_AddAuditLogEntries.cs` | Table + 3 indexes |

---

## Chunk 1: Domain Layer (Entity + Enum + Repository)

### Task 1: Create AuditAction enum

- [ ] **Step 1: Write enum** with 26 values (RoleChanged through RateLimitExceeded)
- [ ] **Step 2: Commit**

### Task 2: Create AuditLogEntry entity

- [ ] **Step 1: Write test**

```csharp
[Trait("Category", "Unit")]
[Trait("BoundedContext", "Administration")]
public class AuditLogEntryTests
{
    [Fact]
    public void Create_WithValidData_ShouldSetAllProperties()
    {
        var entry = AuditLogEntry.Create(
            userId: Guid.NewGuid(),
            actorId: Guid.NewGuid(),
            AuditAction.RoleChanged,
            details: """{"oldRole":"user","newRole":"admin"}""",
            ipAddress: "127.0.0.1",
            userAgent: "Chrome");

        entry.Id.Should().NotBeEmpty();
        entry.Action.Should().Be(AuditAction.RoleChanged);
        entry.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }
}
```

- [ ] **Step 2: Write entity**

```csharp
public sealed class AuditLogEntry : Entity<Guid>
{
    public Guid UserId { get; private set; }       // Subject
    public Guid ActorId { get; private set; }       // Who did it
    public AuditAction Action { get; private set; }
    public string Details { get; private set; } = string.Empty; // JSONB
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private AuditLogEntry() : base() { }

    public static AuditLogEntry Create(
        Guid userId, Guid actorId, AuditAction action,
        string details, string? ipAddress = null, string? userAgent = null)
    {
        return new AuditLogEntry
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ActorId = actorId,
            Action = action,
            Details = details,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            CreatedAt = DateTime.UtcNow,
        };
    }
}
```

- [ ] **Step 3: Run tests + commit**

### Task 3: Create repository + EF config

- [ ] **Step 1: Write IAuditLogRepository** — methods: AddAsync, GetPagedAsync(filters), GetByUserIdAsync, GetForExportAsync
- [ ] **Step 2: Write AuditLogRepository** — EF Core with filter predicates
- [ ] **Step 3: Write EF configuration** with 3 indexes:
  - `IX_AuditLogEntry_CreatedAt` (for retention cleanup)
  - `IX_AuditLogEntry_UserId_CreatedAt` (for per-user queries)
  - `IX_AuditLogEntry_Action` (for event type filtering)
- [ ] **Step 4: Register in DI + commit**

### Task 4: Generate migration

- [ ] **Step 1: `dotnet ef migrations add AddAuditLogEntries`**
- [ ] **Step 2: Verify indexes are in migration**
- [ ] **Step 3: Apply + commit**

---

## Chunk 2: Modify RoleChangedEvent + Create ChangeUserRoleCommand

### Task 5: Add ChangedById to RoleChangedEvent

- [ ] **Step 1: Read RoleChangedEvent.cs** (Authentication/Domain/Events/)
- [ ] **Step 2: Add `ChangedById` property**

```csharp
public Guid ChangedById { get; }

public RoleChangedEvent(Guid userId, Role oldRole, Role newRole, Guid changedById)
{
    UserId = userId;
    OldRole = oldRole.Value;
    NewRole = newRole.Value;
    ChangedById = changedById;
}
```

- [ ] **Step 3: Update User.UpdateRole** to accept and forward `changedById`

```csharp
public void UpdateRole(Role newRole, Guid changedById)
{
    ArgumentNullException.ThrowIfNull(newRole);
    if (Role == newRole) return;
    var oldRole = Role;
    Role = newRole;
    AddDomainEvent(new RoleChangedEvent(Id, oldRole, newRole, changedById));
}
```

- [ ] **Step 4: Fix all callers** of `UpdateRole` (BulkRoleChangeCommandHandler etc.) to pass changedById
- [ ] **Step 5: Run affected tests + commit**

### Task 6: Create ChangeUserRoleCommand

- [ ] **Step 1: Write test**
- [ ] **Step 2: Write command + handler**

```csharp
internal record ChangeUserRoleCommand(Guid UserId, string NewRole, Guid ChangedById) : ICommand<bool>;

internal class ChangeUserRoleCommandHandler : ICommandHandler<ChangeUserRoleCommand, bool>
{
    // Fetch user, validate role, call user.UpdateRole(newRole, changedById), save
}
```

- [ ] **Step 3: Add PUT /admin/users/{id}/role endpoint** to AdminUserEndpoints.cs
- [ ] **Step 4: Run tests + commit**

---

## Chunk 3: Audit Event Handler + Cleanup Job

### Task 7: Create centralized AuditLogEventHandler

- [ ] **Step 1: Write handler** that listens to all domain events and writes AuditLogEntry

```csharp
internal sealed class RoleChangedAuditHandler : INotificationHandler<RoleChangedEvent>
{
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IUnitOfWork _unitOfWork;

    public async Task Handle(RoleChangedEvent notification, CancellationToken ct)
    {
        var entry = AuditLogEntry.Create(
            notification.UserId,
            notification.ChangedById,
            AuditAction.RoleChanged,
            JsonSerializer.Serialize(new { notification.OldRole, notification.NewRole }));

        await _auditLogRepository.AddAsync(entry, ct).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
```

- [ ] **Step 2: Add handlers for other events** (UserInvited, InvitationAccepted, Login, etc.)
- [ ] **Step 3: Test + commit**

### Task 8: Create AuditLogCleanupJob

- [ ] **Step 1: Write Quartz job** — deletes entries older than 90 days

```csharp
[DisallowConcurrentExecution]
internal sealed class AuditLogCleanupJob : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var cutoff = DateTime.UtcNow.AddDays(-90);
        var deleted = await _auditLogRepository.DeleteOlderThanAsync(cutoff, ct);
        _logger.LogInformation("Audit log cleanup: deleted {Count} entries older than {Cutoff}", deleted, cutoff);
    }
}
```

- [ ] **Step 2: Register Quartz schedule** (daily at 3 AM)
- [ ] **Step 3: Commit**

---

## Chunk 4: Audit Endpoints + Query Handlers

### Task 9: Create GetAuditLogQuery

- [ ] **Step 1: Write query** with filter parameters (userId, action, dateFrom, dateTo, actorId, search, limit, offset)
- [ ] **Step 2: Write handler** with EF predicate building
- [ ] **Step 3: Test + commit**

### Task 10: Create ExportAuditLogQuery

- [ ] **Step 1: Write query** returning CSV-ready data
- [ ] **Step 2: Write handler** generating CSV string
- [ ] **Step 3: Commit**

### Task 11: Create AdminAuditEndpoints

- [ ] **Step 1: Write endpoints**
  - `GET /admin/audit-log` → GetAuditLogQuery
  - `GET /admin/audit-log/export` → ExportAuditLogQuery (returns `text/csv`)
  - `GET /admin/users/{id}/audit-log` → GetUserAuditLogQuery
- [ ] **Step 2: Register in Program.cs + commit**

---

## Chunk 5: Frontend

### Task 12: Create audit API client + schemas

- [ ] **Step 1: Write Zod schemas** (AuditLogEntryDto, filter params)
- [ ] **Step 2: Write auditClient** (getAuditLog, getUserAuditLog, exportCsv)
- [ ] **Step 3: Register in API factory + commit**

### Task 13: Create InlineRoleSelect component

- [ ] **Step 1: Write component** — Select dropdown with confirmation modal

```tsx
interface InlineRoleSelectProps {
  userId: string;
  currentRole: string;
  userName: string;
  onRoleChanged?: () => void;
}
```

- [ ] **Step 2: Wire to PUT /admin/users/{id}/role**
- [ ] **Step 3: Test + commit**

### Task 14: Create UserActivityTimeline component

- [ ] **Step 1: Write component** — chronological event list with icons per action type
- [ ] **Step 2: Wire to GET /admin/users/{id}/audit-log**
- [ ] **Step 3: Add as tab in admin user detail page**
- [ ] **Step 4: Test + commit**

### Task 15: Create Audit Log page

- [ ] **Step 1: Write page** at `/admin/monitor/audit/page.tsx`
- [ ] **Step 2: Write AuditLogTable** — filterable table with action badges
- [ ] **Step 3: Write AuditLogFilters** — user picker, action dropdown, date range
- [ ] **Step 4: Write AuditLogExport** — CSV download button
- [ ] **Step 5: Add to admin navigation** in `config/admin-dashboard-navigation.ts`
- [ ] **Step 6: Test + commit**

### Task 16: Wire existing activity-table.tsx to real API

- [ ] **Step 1: Read current mock data** in activity-table.tsx
- [ ] **Step 2: Replace with useQuery** calling auditClient
- [ ] **Step 3: Test + commit**

### Task 17: Full test suite

- [ ] **Step 1: Backend tests**: `dotnet test --filter "BoundedContext=Administration"`
- [ ] **Step 2: Frontend tests**: `pnpm test -- --run`
- [ ] **Step 3: Full build**: both backend and frontend
- [ ] **Step 4: Fix any issues + commit**
