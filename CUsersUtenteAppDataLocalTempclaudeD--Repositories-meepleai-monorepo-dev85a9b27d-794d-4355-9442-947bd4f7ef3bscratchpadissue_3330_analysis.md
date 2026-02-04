# Issue #3330: Session Limits Enforcement - Analysis

## Pattern Found: GameLibraryQuotaService

✅ Excellent pattern to replicate for SessionQuotaService

**Key Components**:
1. `IGameLibraryQuotaService` interface (Domain/Services)
2. `GameLibraryQuotaService` implementation (Infrastructure/Services)
3. Uses `IConfigurationService` for dynamic limits
4. Returns `LibraryQuotaResult` with Allowed/Denied states
5. Bypasses checks for Admin/Editor roles

**Architecture**:
- Domain: Interface + Value Objects (QuotaResult, QuotaInfo)
- Infrastructure: Service implementation
- Application: Used in CommandHandlers
- DI Registration: UserLibraryServiceExtensions

## CreateSessionCommandHandler Location

**Path**: `BoundedContexts/SessionTracking/Application/Handlers/CreateSessionCommandHandler.cs`

**Current Logic**:
- Creates session with unique code (retry logic)
- NO quota enforcement currently
- Uses `ISessionRepository` and `IUnitOfWork`

**Injection Point**: Add quota check BEFORE session creation (line ~25)

## SystemConfiguration Pattern

**Table**: `system_configurations`
**Config Keys**: Use pattern like `LibraryLimits:free:MaxGames`
**Service**: `IConfigurationService` for retrieval

## Implementation Plan

### Backend Structure:
```
BoundedContexts/SessionTracking/
├── Domain/
│   └── Services/
│       └── ISessionQuotaService.cs (NEW)
├── Application/
│   ├── Queries/
│   │   ├── GetSessionLimitsQuery.cs (NEW)
│   │   └── GetSessionLimitsQueryHandler.cs (NEW)
│   └── Commands/
│       ├── UpdateSessionLimitsCommand.cs (NEW)
│       └── UpdateSessionLimitsCommandHandler.cs (NEW)
├── Infrastructure/
│   ├── Services/
│   │   └── SessionQuotaService.cs (NEW)
│   └── DependencyInjection/
│       └── SessionTrackingServiceExtensions.cs (UPDATE)
└── Routing/
    └── SessionTrackingEndpoints.cs (UPDATE - add admin endpoints)
```

### Limit Configuration:
```yaml
SessionLimits:Free:MaxSessions: 3
SessionLimits:Normal:MaxSessions: 10
SessionLimits:Premium:MaxSessions: -1  # unlimited
```

### Quota Check Logic:
1. Count active sessions for user: `SELECT COUNT(*) FROM sessions WHERE user_id = @userId AND end_date IS NULL`
2. Get limit from config (with defaults)
3. Compare: current >= limit → Deny
4. Inject in CreateSessionCommandHandler BEFORE session creation
