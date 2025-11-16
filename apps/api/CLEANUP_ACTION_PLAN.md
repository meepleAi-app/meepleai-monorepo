# API Cleanup Action Plan

Priority-ordered tasks based on the Legacy Code Analysis

---

## PRIORITY 1: CRITICAL (Do First)

### Task 1.1: Remove LlmService (EASIEST - 20 min)
**File**: `/apps/api/src/Api/Services/LlmService.cs`  
**Status**: Unused (700 lines) - replaced by HybridLlmService  
**Steps**:
```bash
# 1. Verify no references
grep -r "LlmService\b" /apps/api/src --include="*.cs" | grep -v "LlmService.cs:" | grep -v "HybridLlmService" | grep -v "ILlmService" | grep -v "OllamaLlmService"

# 2. Delete file
rm /apps/api/src/Api/Services/LlmService.cs

# 3. Run tests to verify no breakage
dotnet test

# 4. Commit
git commit -m "refactor: Remove unused LlmService (replaced by HybridLlmService)"
```

**Evidence**:
- ApplicationServiceExtensions.cs line 108: "ILlmService now registered in KnowledgeBaseServiceExtensions as HybridLlmService"
- LlmService is NOT registered anywhere in DI
- HybridLlmService fully replaces it

---

### Task 1.2: Migrate ChatService (CRITICAL - 4-6 hours)
**Files Affected**: 
- `/apps/api/src/Api/Services/ChatService.cs` (MARKED OBSOLETE)
- `/apps/api/src/Api/Routing/ChatEndpoints.cs` (5 endpoints)
- `/apps/api/src/Api/Routing/AiEndpoints.cs` (4 endpoints)
- `/apps/api/src/Api/Routing/GameEndpoints.cs` (1 endpoint)

**Current Problem**:
```csharp
// ❌ ChatEndpoints.cs line 19 - Using obsolete service
group.MapGet("/chats", async (HttpContext context, ChatService chatService, ...
```

**Migration Pattern**:
```csharp
// ✅ BEFORE (Legacy)
group.MapGet("/chats", async (HttpContext context, ChatService chatService, ..., CancellationToken ct) =>
{
    var chats = await chatService.GetUserChatsAsync(session.User.Id, 50, ct);
    // ... map response
});

// ✅ AFTER (CQRS - if handler exists)
group.MapGet("/chat-threads", async (HttpContext context, IMediator mediator, ..., CancellationToken ct) =>
{
    var query = new GetUserChatsQuery(session.User.Id, 50);
    var result = await mediator.Send(query, ct);
    // ... map response
});
```

**Endpoints to Fix**:
1. ChatEndpoints.cs:19 - `/chats` (GET all)
2. ChatEndpoints.cs:41 - `/chats/{chatId}` (GET one)
3. ChatEndpoints.cs:75 - `/chats` (POST create)
4. ChatEndpoints.cs:114 - `/chats/{chatId}` (DELETE)
5. ChatEndpoints.cs:141,176 - Message update/delete
6. AiEndpoints.cs:28 - `/agents/qa`
7. AiEndpoints.cs:222,346 - `/agents/explain`
8. AiEndpoints.cs:539 - `/agents/setup`
9. AiEndpoints.cs:716 - `/agents/chess`
10. GameEndpoints.cs - Game agents endpoint

**Action**:
1. Check if CQRS query handlers exist for chat operations
2. If not, create them (estimated 2-3 hours)
3. Migrate endpoints to use IMediator (estimated 1-2 hours)
4. Verify ChatService no longer used
5. Delete ChatService.cs (or keep with [Obsolete] if needed elsewhere)
6. Run integration tests

---

### Task 1.3: Refactor AdminEndpoints DbContext Usage (CRITICAL - 4-6 hours)
**File**: `/apps/api/src/Api/Routing/AdminEndpoints.cs`  
**Problem**: 17 direct DbContext operations violate DDD architecture

**Violations Found**:
```csharp
// Line 207-213
var user = await db.Set<UserEntity>().FindAsync([session.User.Id], ct);
if (user == null) return Results.NotFound(...);
user.DisplayName = displayName;
user.Email = email;
await db.SaveChangesAsync(ct);  // ❌ Persistence logic in endpoint
```

**Similar patterns at**: Lines 173, 185, 195, 235, 248, 257, 88 (FirstOrDefaultAsync), 106

**Migration Path**:
```csharp
// ❌ BEFORE (Anti-pattern)
group.MapPut("/admin/users/{userId:guid}", async (Guid userId, ..., MeepleAiDbContext db, ...) =>
{
    var user = await db.Set<UserEntity>().FindAsync([session.User.Id], ct);
    user.DisplayName = displayName;
    await db.SaveChangesAsync(ct);
});

// ✅ AFTER (DDD)
group.MapPut("/admin/users/{userId:guid}", async (Guid userId, ..., IMediator mediator, ...) =>
{
    var command = new UpdateUserProfileCommand(userId, displayName, email);
    var result = await mediator.Send(command, ct);
    return Results.Ok(result);
});
```

**Steps**:
1. Identify all user/admin operations needing handler commands
2. Create Administration bounded context handlers (may already exist partially)
3. Migrate 10+ endpoints to use IMediator
4. Remove MeepleAiDbContext from AdminEndpoints parameters
5. Run tests

---

## PRIORITY 2: MEDIUM (Fix Next Sprint)

### Task 2.1: Migrate AiRequestLogService (2-3 hours)
**Problem**: Direct service injection in endpoints (lines 29, 347, 540, 717)

**Files**: `/apps/api/src/Api/Routing/AiEndpoints.cs`

**Fix**:
- Move logging to middleware or handler
- Remove AiRequestLogService injection from endpoints
- Use ILogger<Program> instead

---

### Task 2.2: Remove Duplicate Service Registrations (15 min)
**File 1**: `/apps/api/src/Api/Extensions/ApplicationServiceExtensions.cs` lines 193-194
```csharp
services.AddScoped<IAdminStatsService, AdminStatsService>();
services.AddScoped<AdminStatsService>();  // ❌ Duplicate
```

**File 2**: `/apps/api/src/Api/Extensions/AuthenticationServiceExtensions.cs` lines 110-111
```csharp
services.AddScoped<IAlertingService, AlertingService>();
services.AddScoped<AlertingService>();  // ❌ Duplicate
```

**Fix**: Keep only interface registration (first line)

---

### Task 2.3: Consolidate Error Handling (2-3 hours)
**Issue**: Mixed error handling patterns across endpoints

**Standardize On**:
1. Validation exceptions → BadRequest (400)
2. Domain exceptions → specific HTTP codes (400/401/403/404)
3. Unexpected errors → caught by middleware, logged, return 500

**Remove**: Endpoint-level try-catch blocks (except for validation)

---

## PRIORITY 3: LOW (Nice to Have)

### Task 3.1: Remove Empty/Stub Files
**Files**:
- CacheMetricsRecorder.cs (0 lines)
- DynamicTtlStrategy.cs (0 lines) 
- IExportFormatter.cs (0 lines)
- Others with no implementation

**Action**: Verify unused, then delete

---

### Task 3.2: Verify OllamaLlmService
**File**: `/apps/api/src/Api/Services/OllamaLlmService.cs` (152 lines)

**Action**:
1. Check if registered in DI
2. Check if actually used
3. If not used, delete (safe cleanup)

---

## Implementation Checklist

### Week 1 (CRITICAL ONLY)
- [ ] Remove LlmService (20 min)
- [ ] Migrate ChatService endpoints (4-6 hours)
- [ ] Refactor AdminEndpoints (4-6 hours)
- [ ] Run full test suite
- [ ] Create PR + code review

### Week 2 (MEDIUM)
- [ ] Migrate AiRequestLogService (2-3 hours)
- [ ] Remove duplicate registrations (15 min)
- [ ] Consolidate error handling (2-3 hours)
- [ ] Verify no regressions
- [ ] Create PR + code review

### Week 3+ (LOW PRIORITY)
- [ ] Remove empty files (30 min)
- [ ] Verify OllamaLlmService (30 min)
- [ ] Service organization improvements

---

## Verification Steps

After each task, run:

```bash
# 1. Build
dotnet build

# 2. Run tests
dotnet test

# 3. Check for broken references
grep -r "ChatService\|LlmService" src/Api --include="*.cs" | grep -v "Obsolete\|HybridLlmService"

# 4. Run linter
dotnet format --verify-no-changes --verbosity diagnostic

# 5. Check test coverage remains >90%
dotnet test /p:CollectCoverage=true
```

---

## Related Documentation

- See `LEGACY_CODE_ANALYSIS.md` for detailed findings
- KnowledgeBase bounded context: `/apps/api/src/Api/BoundedContexts/KnowledgeBase/`
- Administration bounded context: `/apps/api/src/Api/BoundedContexts/Administration/`

---

## Estimated Total Effort

- **Critical (Week 1)**: 8-12 hours
- **Medium (Week 2)**: 4-6 hours  
- **Low (Week 3+)**: 1-2 hours
- **Total**: 13-20 hours across 3 weeks

**Recommended Pace**: 1 critical task per week to minimize risk
