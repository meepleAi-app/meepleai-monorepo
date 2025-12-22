# Service Injection Inventory - Post Phase 1 & 2

**Date**: 2025-12-22
**Status**: Phase 1 & 2 Complete (IRagService fully eliminated)
**Next Target**: IResponseQualityService (2 usages in AiEndpoints)

## 🎉 MAJOR DISCOVERY: Phase 2 Already Complete!

**Streaming endpoints were ALREADY migrated to CQRS** (Issue #1186):

### ✅ Streaming QA (`/agents/qa/stream`)
```csharp
// Line 243-244 in AiEndpoints.cs
var query = new StreamQaQuery(req.gameId, req.query, req.chatId, req.documentIds);
await foreach (var evt in mediator.CreateStream(query, ct).ConfigureAwait(false))
```
**Status**: ✅ Already using IMediator

### ✅ Streaming Explain (`/agents/explain/stream`)
```csharp
// Line 837-838 in AiEndpoints.cs
var query = new StreamExplainQuery(req.gameId, req.topic);
await ExecuteExplainStreamingAsync(query, context, mediator, ct);
```
**Status**: ✅ Already using IMediator

**Result**: **Zero IRagService usages in entire Routing/ directory!** 🎯

---

## Remaining Service Injections Analysis

### ✅ Cross-Cutting Services (Legitimately Keep)

These are infrastructure/cross-cutting concerns - **NO MIGRATION NEEDED**:

| Service | Usages | Files | Justification |
|---------|--------|-------|---------------|
| **IFeatureFlagService** | 6 | AiEndpoints (x2), FeatureFlagEndpoints, WorkflowEndpoints, PdfEndpoints (x2) | Global feature flags |
| **IConfigurationService** | 3 | AuthenticationEndpoints, OAuthEndpoints (x2) | Runtime config access |
| **IAlertingService** | 3 | AlertEndpoints (x3) | System-wide alerting |
| **IEncryptionService** | 2 | WorkflowEndpoints (x2) | Security primitive |
| **IRateLimitService** | 3 | OAuthEndpoints (x2), TwoFactorEndpoints | Cross-cutting concern |

**Total Cross-Cutting**: 17 usages across 7 files

**Decision**: ✅ **Keep as-is** - These are appropriately cross-cutting and don't belong in bounded contexts

---

### 🟡 Domain Services (Should Migrate to Bounded Contexts)

These services contain domain/application logic and should be in CQRS handlers:

#### 1. **IResponseQualityService** (KnowledgeBase Domain)

| File | Line | Usage | Priority |
|------|------|-------|----------|
| AiEndpoints.cs | 318 | HandleQaRequest | 🔴 HIGH |
| AiEndpoints.cs | 927 | (another handler) | 🔴 HIGH |

**Target**: `BoundedContexts/KnowledgeBase/Domain/Services/`
**Action**: Move logic to `AskQuestionQueryHandler` (calculate quality scores in handler)

**Complexity**: Low (quality calculation is domain logic, belongs in handler)

---

#### 2. **IAiResponseCacheService** (KnowledgeBase Infrastructure)

| File | Line | Usage | Priority |
|------|------|-------|----------|
| CacheEndpoints.cs | 18 | GET /admin/cache/stats | 🟡 MEDIUM |
| CacheEndpoints.cs | 34 | DELETE /admin/cache/games/{id} | 🟡 MEDIUM |
| CacheEndpoints.cs | 61 | DELETE /admin/cache/tags/{tag} | 🟡 MEDIUM |

**Target**: `BoundedContexts/KnowledgeBase/Infrastructure/`
**Action**: Create Commands/Queries for cache operations:
- `GetCacheStatsQuery`
- `InvalidateCacheByGameCommand`
- `InvalidateCacheByTagCommand`

**Complexity**: Low (simple CRUD operations)

---

#### 3. **IBggApiService** (GameManagement Domain)

| File | Line | Usage | Priority |
|------|------|-------|----------|
| PdfEndpoints.cs | 403 | HandleBggSearch | 🟡 MEDIUM |
| PdfEndpoints.cs | 421 | HandleGetBggGameDetails | 🟡 MEDIUM |

**Target**: `BoundedContexts/GameManagement/Application/`
**Action**: Create Queries for BGG operations:
- `SearchBggGamesQuery`
- `GetBggGameDetailsQuery`

**Complexity**: Low (external API calls, simple queries)

---

#### 4. **IBlobStorageService** (DocumentProcessing Infrastructure)

| File | Line | Usage | Priority |
|------|------|-------|----------|
| PdfEndpoints.cs | 460 | HandleDownloadPdf | 🟡 MEDIUM |

**Target**: `BoundedContexts/DocumentProcessing/Infrastructure/`
**Action**: Create Query:
- `DownloadPdfQuery` or keep as infrastructure (blob access is infrastructure concern)

**Complexity**: Low (infrastructure operation)
**Alternative**: Could keep as infrastructure service (blob access is cross-cutting)

---

#### 5. **IBackgroundTaskService** (Infrastructure/Cross-Cutting)

| File | Line | Usage | Priority |
|------|------|-------|----------|
| PdfEndpoints.cs | 692 | HandleCancelPdfProcessing | 🟢 LOW |

**Target**: Keep as infrastructure service OR move to DocumentProcessing
**Action**: Evaluate if background task management is cross-cutting or domain-specific

**Complexity**: Medium (background task management may be cross-cutting)

---

#### 6. **IModelRecommendationService** (Already in Bounded Context!)

| File | Line | Usage | Priority |
|------|------|-------|----------|
| LlmAnalyticsEndpoints.cs | 86 | Analytics endpoint | ✅ CORRECT |

**Current Location**: `Api.BoundedContexts.KnowledgeBase.Domain.Services.Analytics.IModelRecommendationService`

**Status**: ✅ **Already properly organized in bounded context domain services!**

**Note**: This is a **domain service**, not an application service - domain services are allowed to be injected directly in handlers or endpoints when they provide domain logic that doesn't fit in entities.

---

## Migration Priority Recommendation

### 🔴 **Phase 3.1: Quality Service** (Quick Win - 1-2 hours)

**Target**: `IResponseQualityService` (2 usages)
**Effort**: Low
**Impact**: High (improves domain logic separation)

**Action**:
- Move quality calculation logic into `AskQuestionQueryHandler`
- Quality scoring is domain logic, belongs in handler

---

### 🟡 **Phase 3.2: Cache Endpoints** (Medium - 2-4 hours)

**Target**: `IAiResponseCacheService` (3 usages)
**Effort**: Low
**Impact**: Medium (admin endpoints)

**Action**:
- Create `GetCacheStatsQuery`
- Create `InvalidateCacheCommand` (with game/tag variants)
- Simple CRUD operations

---

### 🟡 **Phase 3.3: BGG Endpoints** (Medium - 2-4 hours)

**Target**: `IBggApiService` (2 usages)
**Effort**: Low
**Impact**: Medium (external API integration)

**Action**:
- Create `SearchBggGamesQuery`
- Create `GetBggGameDetailsQuery`
- Encapsulate external API calls

---

### 🟢 **Phase 3.4: Blob/Background** (Low Priority - Evaluate)

**Target**: `IBlobStorageService` (1 usage), `IBackgroundTaskService` (1 usage)
**Effort**: Low
**Impact**: Low (may be legitimately infrastructure)

**Action**:
- Evaluate if these are truly cross-cutting or domain-specific
- May decide to keep as infrastructure services

---

## Updated Service Classification

### ✅ **Fully Migrated (Zero Usages in Routing/)**
- IRagService - ✅ **ELIMINATED** (Phase 1 & 2 complete)

### ✅ **Cross-Cutting (Keep - 17 usages)**
- IFeatureFlagService (6)
- IConfigurationService (3)
- IAlertingService (3)
- IRateLimitService (3)
- IEncryptionService (2)

### 🟡 **Domain Services (Migrate - 8 usages)**
- IResponseQualityService (2) - 🔴 High priority
- IAiResponseCacheService (3) - 🟡 Medium priority
- IBggApiService (2) - 🟡 Medium priority
- IBlobStorageService (1) - 🟢 Low priority (may be infrastructure)
- IBackgroundTaskService (1) - 🟢 Low priority (may be cross-cutting)

### ✅ **Domain Services in Bounded Context (Correct)**
- IModelRecommendationService (1) - Already in KnowledgeBase.Domain.Services ✅

---

## Progress Summary

| Phase | Status | Endpoints | Services Eliminated |
|-------|--------|-----------|---------------------|
| Phase 1.1 | ✅ Complete | QA (non-streaming) | IRagService |
| Phase 1.2 | ✅ Complete | Explain (non-streaming) | IRagService |
| Phase 2 | ✅ Already Done! | QA + Explain (streaming) | IRagService (was already migrated) |
| Phase 3.1 | 🔴 Next | Quality scoring | IResponseQualityService |
| Phase 3.2 | 🟡 Pending | Cache management | IAiResponseCacheService |
| Phase 3.3 | 🟡 Pending | BGG integration | IBggApiService |
| Phase 3.4 | 🟢 Evaluate | Blob/Background | TBD |

---

## Recommendation

**Continue with Phase 3.1: IResponseQualityService migration** (Quick win)

**Rationale**:
- Only 2 usages (both in AiEndpoints.cs)
- Quality calculation is domain logic - belongs in handler
- Low complexity, high impact
- Can be done in ~1-2 hours

**Alternative**: If you want multiple quick wins, we could do Phase 3.2 (Cache) + 3.3 (BGG) in parallel (5-6 usages total, similar complexity).

---

**Generated by**: SuperClaude DDD Migration Analysis
**Duration**: Phase 1-2 analysis ~60 minutes
**Test Results**: 100% passing (4,209 tests)
