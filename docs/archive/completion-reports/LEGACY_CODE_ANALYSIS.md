# Legacy Code Analysis Report - apps/api

## Executive Summary

**Analysis Date**: 2025-11-16  
**Scope**: Full apps/api directory  
**Severity**: MEDIUM - Multiple cleanup opportunities identified  
**Impact**: ~70+ files with legacy patterns; ~17 direct DbContext usages in endpoints

---

## 1. SERVICES DIRECTORY ANALYSIS

### Current State
- **Total Service Classes**: 45 service classes (implementation + interfaces)
- **Location**: `/home/user/meepleai-monorepo/apps/api/src/Api/Services/`

### Expected vs Actual
**According to CLAUDE.md, only 4 services should exist:**
- ✅ ConfigurationService (805 lines) - VALID
- ✅ AdminStatsService (411 lines) - VALID  
- ✅ AlertingService - VALID
- ✅ RagService (995 lines) - VALID

**Reality**: 40+ additional services exist, but many are legitimate infrastructure/domain services

### Critical Finding: Obsolete Services Still in Use

#### ChatService (CRITICAL)
- **Path**: `/home/user/meepleai-monorepo/apps/api/src/Api/Services/ChatService.cs`
- **Status**: MARKED [Obsolete] but STILL ACTIVELY USED
- **Lines**: 438 lines
- **Obsolescence Note** (line 14):
  ```csharp
  /// LEGACY SERVICE - Use KnowledgeBase bounded context CQRS handlers instead.
  /// This service is deprecated and will be removed in a future version.
  /// See: KnowledgeBaseEndpoints.cs for new DDD/CQRS endpoints using /chat-threads.
  [Obsolete("Use CQRS handlers in KnowledgeBase bounded context instead...")]
  ```

**Usage Locations** (6 active injection points):
- `/home/user/meepleai-monorepo/apps/api/src/Api/Routing/ChatEndpoints.cs:19,41,75,114,141,176`
  - Line 19: `MapGet("/chats", async (HttpContext context, ChatService chatService...`
  - Line 41: `MapGet("/chats/{chatId:guid}", async (Guid chatId, HttpContext context, ChatService chatService...`
  - Line 75: `MapPost("/chats", async (CreateChatRequest? request, HttpContext context, ChatService chatService...`
  - Line 114: `MapDelete("/chats/{chatId:guid}", async (Guid chatId, HttpContext context, ChatService chatService...`
  - Line 141,176: Message update/delete endpoints

- `/home/user/meepleai-monorepo/apps/api/src/Api/Routing/AiEndpoints.cs:28,222,346,539,716`
  - Line 28: `/agents/qa` endpoint
  - Line 222: `/agents/explain` endpoint
  - Line 346: Stream explain endpoint
  - Line 539: `/agents/setup` endpoint (CQRS mixed with legacy service)
  - Line 716: `/agents/chess` endpoint (CQRS mixed with legacy service)

- `/home/user/meepleai-monorepo/apps/api/src/Api/Routing/GameEndpoints.cs`
  - Game agents endpoint uses ChatService

**Severity**: CRITICAL - Using deprecated service in multiple endpoints

---

### Unused/Underutilized Services

#### 1. LlmService (700 lines)
- **Path**: `/home/user/meepleai-monorepo/apps/api/src/Api/Services/LlmService.cs`
- **Status**: UNUSED - Replaced by HybridLlmService
- **Registration**: NOT registered in DI container (commented out in ApplicationServiceExtensions.cs:108)
- **Note**: ApplicationServiceExtensions.cs line 108:
  ```csharp
  // ISSUE-958: ILlmService now registered in KnowledgeBaseServiceExtensions as HybridLlmService
  // (Removed old LlmService registration to prevent duplicate)
  ```
- **Severity**: MEDIUM - Dead code that can be removed after cleanup

#### 2. OllamaLlmService (152 lines)
- **Path**: `/home/user/meepleai-monorepo/apps/api/src/Api/Services/OllamaLlmService.cs`
- **Status**: NOT registered, possibly unused
- **Severity**: LOW - Potentially dead code

#### 3. AiRequestLogService (Injected directly in endpoints)
- **Path**: `/home/user/meepleai-monorepo/apps/api/src/Api/Services/AiRequestLogService.cs`
- **Usage**: Direct injection in AiEndpoints (lines 29, 347, 540, 717)
- **Severity**: MEDIUM - Legacy pattern (service injection in endpoint instead of CQRS)

---

### Service Injection Antipattern in Endpoints

#### Problem Pattern
Endpoints are mixing IMediator (DDD) with direct service injection (legacy pattern):

**AiEndpoints.cs line 24-37** (QA Endpoint):
```csharp
group.MapPost("/agents/qa", async (
    QaRequest req,
    HttpContext context,
    IRagService rag,
    ChatService chatService,           // ← Legacy injection
    AiRequestLogService aiLog,         // ← Legacy injection
    IResponseQualityService qualityService,
    IMediator mediator,                // ← CQRS (correct)
    IOptions<FollowUpQuestionsConfiguration> followUpConfig,
    MeepleAiDbContext dbContext,       // ← Direct DbContext in endpoint
    ILogger<Program> logger,
    bool bypassCache = false,
    bool generateFollowUps = true,
    CancellationToken ct = default) =>
```

**Count**: 20+ legacy service injections across routing files

---

## 2. ENDPOINT PATTERNS - LEGACY vs CQRS

### Direct Service Injection (Legacy - Should Be Removed)

#### Critical Violations

**1. Direct DbContext Usage in Endpoints** (17 instances)
- Location: `/home/user/meepleai-monorepo/apps/api/src/Api/Routing/AdminEndpoints.cs`
- Examples:
  ```csharp
  var user = await db.Set<UserEntity>().FindAsync([session.User.Id], ct);
  await db.SaveChangesAsync(ct);
  ```
- **Severity**: CRITICAL - Violates DDD architectural principle
- **Lines**: AdminEndpoints.cs contains multiple DbContext operations

**2. Service Injection Instead of IMediator**

**ChatEndpoints.cs** (Lines 19-176):
```csharp
// ❌ WRONG: Direct service injection
group.MapGet("/chats", async (HttpContext context, ChatService chatService, ...

// Should be ✅ CORRECT: IMediator
group.MapGet("/chats", async (HttpContext context, IMediator mediator, ...
    var query = new GetUserChatsQuery(session.User.Id);
    var chats = await mediator.Send(query, ct);
```

**AuthEndpoints.cs** (Lines 256-345):
```csharp
// ❌ WRONG: Mixed patterns
group.MapPost("/auth/2fa/setup", async (HttpContext context, ITotpService totpService, ...
group.MapPost("/auth/2fa/verify", async (..., ITotpService totpService, ITempSessionService tempSessionService, IRateLimitService rateLimitService, IMediator mediator, ...
group.MapPost("/auth/2fa/enable", async (..., IMediator mediator, ...  // Correct pattern
```

**Summary Table**:

| Endpoint File | Legacy Injections | CQRS Injections | Status |
|---|---|---|---|
| AuthEndpoints.cs | 6 (TotpService, PasswordResetService, OAuthService, etc.) | ✅ Multiple | MIXED |
| ChatEndpoints.cs | 5 (ChatService [Obsolete]) | ❌ None | LEGACY |
| AiEndpoints.cs | 4 (ChatService, AiRequestLogService) | ✅ IMediator | MIXED |
| GameEndpoints.cs | 1 (ChatService) | ✅ IMediator | MIXED |
| AdminEndpoints.cs | Direct DbContext (17 ops) | ❌ None | LEGACY |
| PdfEndpoints.cs | IBackgroundTaskService | ✅ IMediator | MIXED |

---

### Error Handling in Endpoints (Mixed Patterns)

**AuthEndpoints.cs** contains proper try-catch (architecture-compliant):
- Lines 64-80: Catch ArgumentException, InvalidOperationException, DomainException
- Lines 118-180: Comprehensive error handling with logging

**But**: Some endpoints lack centralized error handling:
- AdminEndpoints.cs: Minimal error handling for DbContext operations
- ChatEndpoints.cs: Relies on middleware instead of endpoint-level handling

---

## 3. DEAD CODE INDICATORS

### Direct DbContext Operations in Endpoints

**AdminEndpoints.cs** - CRITICAL VIOLATIONS:

Line 207-213:
```csharp
var user = await db.Set<UserEntity>().FindAsync([session.User.Id], ct);
if (user == null)
    return Results.NotFound(new { error = "User not found" });
user.DisplayName = displayName;
user.Email = email;
await db.SaveChangesAsync(ct);  // ← Direct SaveChanges in endpoint
```

Similar patterns at:
- Lines 173, 185, 195, 235, 248, 257
- Line 88: `.FirstOrDefaultAsync()`
- Line 106: `.FirstOrDefaultAsync()`

**These should be moved to repositories/handlers**

---

### Duplicate Service Registrations

**ApplicationServiceExtensions.cs** lines 193-194:
```csharp
services.AddScoped<IAdminStatsService, AdminStatsService>();
services.AddScoped<AdminStatsService>();  // ← Duplicate registration
```

**AuthenticationServiceExtensions.cs** lines 110-111:
```csharp
services.AddScoped<IAlertingService, AlertingService>();
services.AddScoped<AlertingService>();  // ← Duplicate
```

---

### Commented-Out Code Blocks

**SessionManagementService** has TODO comments (line references):
- Potentially incomplete functionality

**ChatService.cs** line 14:
```csharp
// Entire service marked obsolete but not removed
```

---

## 4. LEGACY PATTERNS & ARCHITECTURE VIOLATIONS

### Pattern 1: Direct DbContext in Endpoints (CRITICAL)

**Anti-Pattern Example** - AdminEndpoints.cs:
```csharp
// ❌ BAD: DbContext directly in endpoint
group.MapPut("/admin/users/{userId:guid}", async (Guid userId, ..., MeepleAiDbContext db, ...) =>
{
    var user = await db.Set<UserEntity>().FindAsync([session.User.Id], ct);
    if (user == null) return Results.NotFound(...);
    user.DisplayName = displayName;
    await db.SaveChangesAsync(ct);  // Persistence logic in HTTP handler
});

// ✅ CORRECT: Move to repository/handler
var command = new UpdateUserCommand(userId, displayName, email);
var result = await mediator.Send(command, ct);
```

**Count**: 17 direct DbContext operations in `/Routing/` files  
**Files Affected**: AdminEndpoints.cs, AuthEndpoints.cs

### Pattern 2: Service Injection Instead of CQRS (MEDIUM)

**Anti-Pattern**:
```csharp
// ❌ Injects service, calls method directly
group.MapGet("/chats", async (..., ChatService chatService, ...) =>
{
    var chats = await chatService.GetUserChatsAsync(session.User.Id, 50, ct);
});

// ✅ Should use CQRS
group.MapGet("/chat-threads", async (..., IMediator mediator, ...) =>
{
    var query = new GetChatThreadsQuery(session.User.Id);
    var chats = await mediator.Send(query, ct);
});
```

**Scope**: 20+ instances across routing files

### Pattern 3: Obsolete Service Still in Active Use (CRITICAL)

ChatService [Obsolete] is injected in 6 endpoints - needs immediate migration

### Pattern 4: Mixed Error Handling

- Some endpoints use try-catch in endpoint
- Some endpoints use centralized middleware
- Some endpoints use domain exception throwing
- **Result**: Inconsistent error handling strategy

---

## 5. FILE STRUCTURE ISSUES

### Orphaned/Underutilized Files

| File | Lines | Status | Action |
|---|---|---|---|
| LlmService.cs | 700 | UNUSED (Replaced by HybridLlmService) | REMOVE |
| OllamaLlmService.cs | 152 | NOT REGISTERED | VERIFY/REMOVE |
| ChatService.cs | 438 | [Obsolete] but STILL USED | MIGRATE |
| DynamicTtlStrategy.cs | 0 | Empty/Stub | REMOVE |
| CacheMetricsRecorder.cs | 0 | Empty/Stub | REMOVE |
| IExportFormatter.cs | 0 | Empty/Stub | REMOVE |

### Inconsistent Organization

- `/Services/Pdf/` - 11 files (proper separation)
- `/Services/Qdrant/` - 6 files (proper separation)
- `/Services/Rag/` - 6 files (proper separation)
- **BUT**: Many large services (RagService 995L, PromptEvaluationService 872L) not separated

---

## 6. DETAILED FINDINGS BY SEVERITY

### CRITICAL (Immediate Action Required)

1. **ChatService [Obsolete] Still in Use**
   - 6 injection points across 4 files
   - Migration path exists (KnowledgeBase bounded context)
   - Estimated effort: 4-6 hours to migrate 4 endpoints

2. **Direct DbContext in Endpoints** (AdminEndpoints.cs)
   - 17 direct database operations
   - Violates DDD principle
   - Should move to handlers/repositories
   - Estimated effort: 3-4 hours

### MEDIUM (Should Be Fixed in Next Sprint)

1. **AiRequestLogService Direct Injection**
   - Used in 4 endpoints
   - Should be moved to handler/middleware
   - Estimated effort: 2 hours

2. **Mixed CQRS/Service Injection in AiEndpoints**
   - 4 endpoints mixing patterns
   - Some handlers exist already
   - Estimated effort: 3-4 hours

3. **Unused LlmService (700 lines)**
   - Not registered anywhere
   - Can be safely deleted
   - Estimated effort: 30 minutes (verify, delete, test)

### LOW (Nice to Have)

1. **Duplicate Service Registrations**
   - 2 instances in Extensions files
   - Clean but not critical
   - Estimated effort: 15 minutes

2. **Empty/Stub Classes**
   - CacheMetricsRecorder.cs, DynamicTtlStrategy.cs, etc.
   - Remove if truly unused
   - Estimated effort: 30 minutes

---

## 7. SPECIFIC FILE LOCATIONS

### Critical Files Needing Attention

```
/home/user/meepleai-monorepo/apps/api/src/Api/
├── Routing/
│   ├── AdminEndpoints.cs          ← 17 DbContext operations, needs handler migration
│   ├── AiEndpoints.cs             ← ChatService, AiRequestLogService injection
│   ├── AuthEndpoints.cs           ← Services injection (TotpService, etc.)
│   ├── ChatEndpoints.cs           ← [Obsolete] ChatService in 6 methods
│   └── GameEndpoints.cs           ← ChatService injection
├── Services/
│   ├── ChatService.cs             ← [Obsolete] but still used (438 lines)
│   ├── LlmService.cs              ← Unused, can be deleted (700 lines)
│   ├── AiRequestLogService.cs     ← Legacy pattern
│   └── OllamaLlmService.cs        ← Potentially unused
└── Extensions/
    └── ApplicationServiceExtensions.cs ← Duplicate registrations (lines 193-194)
```

---

## 8. RECOMMENDATIONS

### Immediate (This Sprint)

1. **Remove LlmService.cs** (20 min)
   - Verify no references remain
   - Delete file and unused imports
   - Confirm HybridLlmService handles all use cases

2. **Migrate ChatService Usage** (4-6 hours)
   - ChatEndpoints.cs: 5 endpoints → CQRS handlers
   - AiEndpoints.cs: 4 endpoints → existing handlers
   - GameEndpoints.cs: 1 endpoint
   - Mark as [Obsolete] once migration complete

3. **Refactor AdminEndpoints DbContext Usage** (4-6 hours)
   - Create handlers for user update operations
   - Move 17 DbContext operations to handlers
   - Keep endpoints thin (only routing + error handling)

### Next Sprint

4. **Migrate AiRequestLogService** (2-3 hours)
   - Move logging to command/query handlers or middleware
   - Remove from endpoint parameters

5. **Consolidate Error Handling** (2-3 hours)
   - Standardize on middleware + domain exceptions
   - Remove endpoint-level try-catch blocks (except validation)

6. **Remove Duplicate Service Registrations** (30 min)
   - ApplicationServiceExtensions.cs
   - AuthenticationServiceExtensions.cs

### Future Improvements

7. **Service Organization**
   - Move large services (>500 lines) to separate folders if they don't fit domain pattern
   - Consider extracting interfaces for all public services

---

## 9. MIGRATION PATH FOR ChatService

**Current** (Deprecated):
```csharp
// ChatEndpoints.cs
group.MapGet("/chats", async (HttpContext context, ChatService chatService, ...) =>
{
    var chats = await chatService.GetUserChatsAsync(session.User.Id, 50, ct);
});
```

**Target** (CQRS):
```csharp
// In KnowledgeBase bounded context
var query = new GetUserChatsQuery(session.User.Id);
var chats = await mediator.Send(query, ct);
```

**Status**: Query handler may already exist, endpoints just need routing change

---

## 10. CODE QUALITY METRICS

| Metric | Value | Status |
|---|---|---|
| Services with direct DbContext in endpoints | 17 | ❌ HIGH |
| Obsolete services still in active use | 1 (ChatService) | ❌ HIGH |
| Endpoints mixing CQRS + service injection | 4 | ❌ MEDIUM |
| Unused/dead service files | 2-3 | ⚠️ MEDIUM |
| Duplicate service registrations | 2 | ⚠️ LOW |
| Direct service injection instead of IMediator | 20+ | ❌ MEDIUM |

---

## Summary

The codebase has made significant DDD migration progress (7/7 contexts at 100%) but still contains:

1. **1 Obsolete Service actively used in 6 endpoints** (ChatService)
2. **17 Direct DbContext operations in endpoints** (AdminEndpoints) 
3. **20+ Service injections instead of IMediator** (legacy pattern)
4. **1 Unused 700-line service** (LlmService)
5. **2-3 Empty/stub files** (CacheMetricsRecorder, DynamicTtlStrategy)

**Estimated total cleanup effort**: 12-18 hours  
**Recommended priority**: Fix critical DbContext + ChatService issues first

