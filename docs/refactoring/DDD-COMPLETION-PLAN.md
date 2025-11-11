# DDD Refactoring - Path to 100% Completion

**Current Status**: 72% Complete (2025-01-11)
**Target**: 100% DDD Migration
**Estimated Time**: 28-35 hours remaining

---

## 🎯 COMPLETION STRATEGY

### Priority-Based Approach

**Tier 1: Quick Wins** (5-6 hours) → 80% completion
1. WorkflowIntegration (1h) - 90% → 100%
2. Authentication Login/Logout (2h) - 70% → 85%
3. Simple endpoint migrations (2-3h)

**Tier 2: Medium Complexity** (14-18 hours) → 90% completion
4. Authentication 2FA (2h) - 85% → 100%
5. SystemConfiguration (6-8h) - 50% → 100%
6. Administration (8-10h) - 40% → 100%

**Tier 3: Complex** (12-14 hours) → 100% completion
7. KnowledgeBase RAG decomposition - 75% → 100%

**Total**: 31-38 hours across all tiers

---

## 📋 DETAILED EXECUTION PLAN

### Phase 1: WorkflowIntegration → 100% (1 hour)

**Current**: 90% (handlers exist, endpoints use N8nConfigService)

**Tasks**:
1. Check if handlers are complete (10 min)
   ```bash
   cat apps/api/src/Api/BoundedContexts/WorkflowIntegration/Application/Handlers/*.cs
   ```

2. Migrate n8n endpoints to CQRS (30 min)
   - File: `Routing/AdminEndpoints.cs` lines 267-387
   - 4 endpoints: GET, POST, PUT, DELETE `/admin/n8n/*`
   - Replace `n8nService.XxxAsync()` with `mediator.Send(new XxxCommand())`

3. Remove N8nConfigService if unused (10 min)
   ```bash
   grep -r "N8nConfigService" apps/api/src/Api/
   rm apps/api/src/Api/Services/N8nConfigService.cs
   ```

4. Write integration tests (10 min)
   - Test n8n configuration CRUD
   - Test workflow error logging

**Deliverable**: WorkflowIntegration 100% complete

---

### Phase 2: Authentication Login/Logout → 85% (2 hours)

**Current**: 70% (Register done, Login/Logout handlers exist but need 2FA)

**Task 1: Update LoginCommandHandler for 2FA** (45 min)

File: `BoundedContexts/Authentication/Application/Commands/LoginCommandHandler.cs:48-57`

Current:
```csharp
if (user.RequiresTwoFactor())
{
    // TODO: Create temp session for 2FA verification
    return new LoginResponse(RequiresTwoFactor: true, ...);
}
```

Add:
```csharp
if (user.RequiresTwoFactor())
{
    // Create 5-minute temp session for 2FA
    var tempSessionId = Guid.NewGuid();
    var tempToken = SessionToken.Generate(); // Or create TempSessionToken VO

    // TODO: Create ITempSessionRepository or use ISessionRepository
    // For now, store in session table with IsTemp flag
    var tempSession = new Session(
        id: tempSessionId,
        userId: user.Id,
        token: tempToken,
        ipAddress: command.IpAddress,
        userAgent: command.UserAgent,
        expiresAt: _timeProvider.GetUtcNow().AddMinutes(5) // Short TTL
    );

    await _sessionRepository.AddAsync(tempSession, cancellationToken);
    await _unitOfWork.SaveChangesAsync(cancellationToken);

    return new LoginResponse(
        RequiresTwoFactor: true,
        TempSessionToken: tempToken.Value,
        User: null,
        SessionToken: null
    );
}
```

**Task 2: Migrate `/auth/login` endpoint** (30 min)

File: `Routing/AuthEndpoints.cs:74-152`

Replace:
```csharp
var result = await auth.LoginAsync(command, ct);
// + 2FA logic
```

With:
```csharp
var command = new DddLoginCommand(
    Email: payload.Email,
    Password: payload.Password,
    IpAddress: context.Connection.RemoteIpAddress?.ToString(),
    UserAgent: context.Request.Headers.UserAgent.ToString());

var result = await mediator.Send(command, ct);

if (result.RequiresTwoFactor)
{
    // 2FA flow - handler already created temp session
    return Results.Json(new {
        requiresTwoFactor = true,
        sessionToken = result.TempSessionToken,
        message = "Two-factor authentication required"
    });
}

// Normal login - handler already created session
writeSessionCookie(context, result.SessionToken, ...);
var legacyUser = new AuthUser(...);
return Results.Json(new AuthResponse(legacyUser, ...));
```

**Task 3: Migrate `/auth/logout` endpoint** (15 min)

File: `Routing/AuthEndpoints.cs` (search for "logout")

Simple replacement:
```csharp
// Before
await auth.LogoutAsync(sessionToken);

// After
var command = new DddLogoutCommand(SessionToken: sessionToken);
await mediator.Send(command, ct);
```

**Task 4: Test** (30 min)
```bash
cd apps/api && dotnet test --filter "FullyQualifiedName~Authentication"
```

**Deliverable**: Authentication 70% → 85%

---

### Phase 3: Authentication 2FA Handlers → 100% (2 hours)

**Task 1: Implement Enable2FACommandHandler** (45 min)

Create: `BoundedContexts/Authentication/Application/Commands/Enable2FACommandHandler.cs`

Logic (from TotpService):
- Generate TOTP secret
- Generate 10 backup codes
- Verify test code before enabling
- Update user aggregate
- Store encrypted secret

**Task 2: Implement Verify2FACommandHandler** (30 min)

Logic:
- Validate temp session token
- Verify TOTP code OR backup code
- Create full session if valid
- Invalidate temp session

**Task 3: Migrate 2FA endpoints** (30 min)

Endpoints (5 total):
- POST `/auth/2fa/setup`
- POST `/auth/2fa/enable`
- POST `/auth/2fa/verify`
- POST `/auth/2fa/disable`
- GET `/users/me/2fa/status`

**Task 4: Remove AuthService** (15 min)
```bash
grep -r "AuthService" apps/api/src/Api/Routing/ # Should be zero
rm apps/api/src/Api/Services/AuthService.cs
# Remove from DI
```

**Deliverable**: Authentication 100% complete

---

### Phase 4: SystemConfiguration Handlers → 100% (6-8 hours)

**Current**: 50% (domain + repositories exist, NO handlers)

**Needed**: 10-12 CQRS handlers

**Handler List**:
1. GetConfigByKeyQueryHandler ✅ (query exists)
2. GetAllConfigsQueryHandler
3. UpdateConfigValueCommandHandler ✅ (command exists)
4. CreateConfigCommandHandler
5. DeleteConfigCommandHandler
6. ToggleFeatureFlagCommandHandler ✅ (command exists)
7. BulkUpdateConfigsCommandHandler
8. RollbackConfigCommandHandler
9. ExportConfigsQueryHandler
10. ImportConfigsCommandHandler
11. GetConfigHistoryQueryHandler
12. ValidateConfigCommandHandler

**Strategy**: Implement 3-4 handlers per hour by copying from GameManagement pattern

**Template** (from CreateGameCommandHandler):
```csharp
public class GetConfigByKeyQueryHandler : IQueryHandler<GetConfigByKeyQuery, ConfigurationDto?>
{
    private readonly IConfigurationRepository _repository;

    public async Task<ConfigurationDto?> Handle(GetConfigByKeyQuery query, CancellationToken ct)
    {
        var config = await _repository.GetByKeyAsync(query.Key, ct);
        return config != null ? MapToDto(config) : null;
    }

    private static ConfigurationDto MapToDto(SystemConfiguration config)
    {
        return new ConfigurationDto(...);
    }
}
```

**Endpoint Migration** (14 endpoints in AdminEndpoints.cs):
- Search for `/admin/configurations` endpoints
- Replace `configService.XxxAsync()` with `mediator.Send()`

**Cleanup**:
- Remove ConfigurationService.cs (814 lines!)
- Remove FeatureFlagService.cs

**Deliverable**: SystemConfiguration 100% complete

---

### Phase 5: Administration Handlers → 100% (8-10 hours)

**Current**: 40% (domain + repositories exist, NO handlers)

**Needed**: 15-18 CQRS handlers

**Handler Categories**:

**User Management** (6 handlers):
1. CreateUserCommandHandler
2. UpdateUserCommandHandler
3. DeleteUserCommandHandler
4. GetUserByIdQueryHandler (exists?)
5. GetAllUsersQueryHandler
6. UpdateUserRoleCommandHandler

**Statistics** (4 handlers):
7. GetAdminStatsQueryHandler
8. GetChatStatsQueryHandler
9. GetGameStatsQueryHandler
10. ExportStatsCommandHandler

**Alerting** (5 handlers):
11. SendAlertCommandHandler
12. GetAlertsQueryHandler
13. ResolveAlertCommandHandler
14. GetAlertByIdQueryHandler
15. DeleteAlertCommandHandler

**Audit Log** (3 handlers):
16. GetAuditLogsQueryHandler
17. GetAuditLogByIdQueryHandler
18. CreateAuditLogCommandHandler

**Endpoint Migration**:
- `/admin/users` endpoints (8 total)
- `/admin/analytics` endpoints (5 total)
- `/admin/alerts` endpoints (4 total)

**Cleanup**:
- Remove AdminStatsService.cs
- Remove UserManagementService.cs
- Remove AlertingService.cs

**Deliverable**: Administration 100% complete

---

### Phase 6: KnowledgeBase RAG Decomposition → 100% (12-14 hours)

**Current**: 75% (foundation + 6 handlers, but RagService still monolithic)

**Challenge**: RagService.cs is 995 lines with multiple concerns

**Decomposition Strategy**:

**Step 1: Extract Domain Services** (6-8 hours)

1. **VectorSearchDomainService** (2h)
   - Extract from RagService.SearchAsync()
   - Logic: Similarity search, score normalization
   - Lines: ~150

2. **RrfFusionDomainService** (2h)
   - Extract from hybrid search logic
   - Logic: Reciprocal Rank Fusion algorithm
   - Lines: ~100

3. **QualityTrackingDomainService** (1h)
   - Extract from quality metrics (AI-11)
   - Logic: Confidence scoring, quality assessment
   - Lines: ~80

4. **CitationExtractionDomainService** (1h)
   - Extract from citation logic
   - Logic: Page reference extraction
   - Lines: ~60

5. **ContextRetrievalDomainService** (2h)
   - Extract from chunk retrieval
   - Logic: Context windowing, relevance filtering
   - Lines: ~120

**Step 2: Implement Handlers** (4-5 hours)

Use extracted domain services in handlers:
- SearchQueryHandler (already exists, refactor to use domain services)
- AskQuestionQueryHandler (already exists, refactor)
- New handlers as needed

**Step 3: Migrate Endpoints** (1h)

Most RAG endpoints already use KnowledgeBaseEndpoints with CQRS

**Step 4: Remove RagService** (1h)

Verify zero usages and delete (995 lines eliminated!)

**Deliverable**: KnowledgeBase 100% complete, RagService eliminated

---

## 🎯 OPTIMIZED EXECUTION ORDER

### Week 1 (20 hours) - Push to 85%
**Day 1** (4h):
- ✅ WorkflowIntegration → 100% (1h)
- ✅ Authentication Login/Logout → 85% (2h)
- ✅ Quick endpoint migrations (1h)

**Day 2** (4h):
- ✅ Authentication 2FA → 100% (2h)
- ✅ Start SystemConfiguration handlers (2h)

**Day 3-5** (12h):
- ✅ Complete SystemConfiguration → 100% (6h)
- ✅ Start Administration handlers (6h)

**Result**: 5/7 contexts → 100% (71%)

### Week 2 (14 hours) - Push to 100%
**Day 6-7** (8h):
- ✅ Complete Administration → 100%

**Day 8-10** (6h):
- ✅ Start KnowledgeBase RAG decomposition

**Result**: 6/7 contexts → 100% (86%)

### Week 3 (8 hours) - Final Push
**Day 11-13** (8h):
- ✅ Complete KnowledgeBase RAG → 100%

**Day 14** (2h):
- ✅ Final polish and celebration

**Result**: 7/7 contexts → 100% ✅

---

## 🔧 HANDLER IMPLEMENTATION TEMPLATE

### Copy-Paste-Modify Pattern

**From**: `BoundedContexts/GameManagement/Application/Commands/CreateGameCommandHandler.cs`

**Template**:
```csharp
using Api.BoundedContexts.{Context}.Domain.Entities;
using Api.BoundedContexts.{Context}.Domain.ValueObjects;
using Api.BoundedContexts.{Context}.Infrastructure.Persistence;
using Api.BoundedContexts.{Context}.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.{Context}.Application.Commands;

public class {Operation}CommandHandler : ICommandHandler<{Operation}Command, {Operation}Response>
{
    private readonly I{Entity}Repository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider; // If needed

    public {Operation}CommandHandler(
        I{Entity}Repository repository,
        IUnitOfWork unitOfWork,
        TimeProvider? timeProvider = null)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<{Response}> Handle({Operation}Command command, CancellationToken ct)
    {
        // 1. Validate (or let VOs validate)

        // 2. Load existing entity if update/delete
        // var entity = await _repository.GetByIdAsync(command.Id, ct);
        // if (entity == null) throw new DomainException("Not found");

        // 3. Create or modify aggregate
        // var newEntity = new {Entity}(...);
        // OR entity.UpdateXxx(...);

        // 4. Save
        // await _repository.AddAsync(newEntity, ct);
        // OR await _repository.UpdateAsync(entity, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        // 5. Map to DTO and return
        return MapToDto(newEntity);
    }

    private static {Response} MapToDto({Entity} entity)
    {
        return new {Response}(...);
    }
}
```

**Modification Steps**:
1. Replace `{Context}`, `{Operation}`, `{Entity}`, `{Response}` placeholders
2. Adjust constructor dependencies (remove TimeProvider if not needed)
3. Implement business logic in Handle()
4. Implement MapToDto()
5. Add error handling if needed

**Time**: 15-30 minutes per handler (after first few)

---

## 🚀 ACCELERATION TECHNIQUES

### 1. Batch Implementation
Implement all handlers for one context in one session:
- SystemConfiguration: 12 handlers in 6 hours = 30min each
- Administration: 18 handlers in 9 hours = 30min each

### 2. Parallel Work Possible
Different developers can work on different contexts simultaneously:
- Dev A: SystemConfiguration
- Dev B: Administration
- Dev C: KnowledgeBase RAG

### 3. Copy-Paste-Modify
Don't write from scratch - copy GameManagement handlers:
- CreateGameCommandHandler → CreateConfigCommandHandler
- GetGameByIdQueryHandler → GetConfigByIdQueryHandler
- Pattern is identical, just different domain objects

### 4. Test As You Go
Test each handler immediately after implementation:
```bash
dotnet test --filter "FullyQualifiedName~{HandlerName}Tests"
```

Prevents accumulating bugs and debugging later

---

## ✅ COMPLETION CHECKLIST

### Per Context
- [ ] All handlers implemented
- [ ] All endpoints migrated to MediatR
- [ ] All legacy services removed
- [ ] Tests passing (90%+)
- [ ] Documentation updated
- [ ] Commit with clear message

### Per Handler
- [ ] Command/Query defined
- [ ] Handler implemented
- [ ] Repository methods exist
- [ ] DTO mapping complete
- [ ] Unit test written
- [ ] Integration test (if complex)
- [ ] Build successful

### Per Endpoint Migration
- [ ] Handler exists and works
- [ ] Endpoint uses `mediator.Send()`
- [ ] Auth preserved
- [ ] Error handling preserved
- [ ] Response format matches (backward compatibility)
- [ ] Test endpoint manually or with existing E2E tests

---

## 📊 PROGRESS TRACKING

### Update After Each Context
```bash
# Count handlers
find apps/api/src/Api/BoundedContexts -name "*Handler.cs" | wc -l

# Count migrated endpoints
grep -r "mediator.Send" apps/api/src/Api/Routing | wc -l

# Count legacy services
ls apps/api/src/Api/Services/*.cs | wc -l

# Test health
dotnet test --verbosity quiet | grep "Passed\|Failed"
```

### Milestones
- [ ] 3/7 contexts → 100% (WorkflowIntegration)
- [ ] 4/7 contexts → 100% (Authentication)
- [ ] 5/7 contexts → 100% (SystemConfiguration)
- [ ] 6/7 contexts → 100% (Administration)
- [ ] 7/7 contexts → 100% (KnowledgeBase)

---

## 🎯 SUCCESS CRITERIA

### Technical
- [ ] All 7 bounded contexts 100% DDD
- [ ] Zero legacy services in `Services/` directory
- [ ] All endpoints use MediatR (grep should find 160 `mediator.Send`)
- [ ] Test coverage 90%+
- [ ] Build green

### Organizational
- [ ] All patterns documented
- [ ] Architecture diagrams created
- [ ] Migration retrospective written
- [ ] Team training materials ready

---

## 🔍 REFERENCE IMPLEMENTATIONS

### For Handlers
**Source**: `BoundedContexts/GameManagement/Application/`
- CreateGameCommandHandler - Create operation pattern
- UpdateGameCommandHandler - Update operation pattern
- GetGameByIdQueryHandler - Single item query pattern
- GetAllGamesQueryHandler - List query pattern

### For Endpoints
**Source**: `Routing/GameEndpoints.cs`
- Shows complete CQRS migration
- Auth handling examples
- Error mapping examples
- Response formatting

### For Domain Services
**Source**: `BoundedContexts/DocumentProcessing/Domain/Services/`
- PdfTextProcessingDomainService - Pure business logic
- Shows validation, normalization, decision logic

---

## 💡 TIPS FOR FAST COMPLETION

### 1. Start with Reads, Then Writes
- Query handlers are simpler (just read and map)
- Command handlers are more complex (validation, business logic, save)
- Do all queries first, then commands

### 2. Reuse DTOs
- Many contexts can share DTO patterns
- Copy DTO structure from Game/Session DTOs

### 3. Keep Handlers Small
- Average 50-100 lines per handler
- Extract mapping to private methods
- Extract validation to VOs

### 4. Test Incrementally
- Test each handler after implementation
- Don't wait until all handlers done

### 5. Commit Frequently
- One commit per 2-3 handlers
- Easy to revert if issues found

---

## 🎉 CELEBRATION MILESTONES

### 80% Complete
- 3/7 contexts → 100%
- Major infrastructure complete
- Clear path to finish line

### 90% Complete
- 5/7 contexts → 100%
- Only complex RAG decomposition remains
- Victory in sight

### 100% Complete
- All 7 contexts → 100% DDD
- Zero legacy services
- All endpoints CQRS
- **MISSION ACCOMPLISHED!** 🏆

---

**Document Owner**: DDD Migration Team
**Status**: Executable Plan
**Next Action**: Start with WorkflowIntegration (1 hour to 100%)
