# Refactoring Action Items - Backend Codebase

## Critical Issues (Address First)

### 1. AdminEndpoints.cs - SPLIT REQUIRED
**File**: `/home/user/meepleai-monorepo/apps/api/src/Api/Routing/AdminEndpoints.cs`
**Size**: 2031 LOC (unmanageable)
**Action**: Split into 6 separate files
- `ConfigurationEndpoints.cs` - Configuration management
- `AnalyticsEndpoints.cs` - Dashboard & statistics  
- `AlertEndpoints.cs` - Alert management
- `AuditEndpoints.cs` - Audit logs
- `FeatureFlagEndpoints.cs` - Feature flags
- `PromptManagementEndpoints.cs` - Prompt operations

**Estimated Time**: 40-50 hours
**Impact**: High - Improves code navigation and reduces merge conflicts

---

### 2. ConfigurationService.cs - MIGRATE TO CQRS
**File**: `/home/user/meepleai-monorepo/apps/api/src/Api/Services/ConfigurationService.cs`
**Size**: 805 LOC with 17 public methods (SRP violation)
**Current Location**: `/home/user/meepleai-monorepo/apps/api/src/Api/Services/`

**Action**: Create handlers in SystemConfiguration bounded context
- `GetConfigurationByIdQueryHandler`
- `GetConfigurationByKeyQueryHandler` 
- `GetConfigurationHistoryQueryHandler`
- `ListConfigurationsQueryHandler`
- `CreateConfigurationCommandHandler`
- `UpdateConfigurationCommandHandler`
- `DeleteConfigurationCommandHandler`
- `ToggleConfigurationCommandHandler`
- `RollbackConfigurationCommandHandler`
- `BulkUpdateConfigurationsCommandHandler`

**Extract New Services**:
- `ConfigurationValidator` (~100 LOC) - validation logic
- `ConfigurationVersionManager` (~150 LOC) - versioning logic

**Reduce ConfigurationService to ~300 LOC** (infrastructure only)

**Location**: `/home/user/meepleai-monorepo/apps/api/src/Api/BoundedContexts/SystemConfiguration/Application/`

**Estimated Time**: 50-60 hours
**Impact**: High - Aligns with DDD/CQRS architecture

---

### 3. RagService.cs - REFACTOR MONOLITHIC SERVICE
**File**: `/home/user/meepleai-monorepo/apps/api/src/Api/Services/RagService.cs`
**Size**: 995 LOC with multiple responsibilities
**Problem**: 24 try-catch blocks with duplicated exception handling

**Action**:
1. Extract `RagConfigurationProvider` service
   - Move: `GetRagConfigAsync()` (lines 930-960)
   - Move: `ValidateRagConfig()` (lines 965-994)
   - Size: ~40 LOC

2. Fully utilize existing `RagExceptionHandler`
   - Located: `/home/user/meepleai-monorepo/apps/api/src/Api/Services/Rag/`
   - Currently used in RagService
   - Extend to other services with similar exception patterns

3. Create orchestration service
   - New file: `RagOrchestrationService` (~300 LOC)
   - Coordinate the 3 main RAG operations:
     - AskAsync
     - ExplainAsync
     - AskWithHybridSearchAsync

**Reduce RagService to 4 focused methods** (~400 LOC)

**Estimated Time**: 40-50 hours
**Impact**: High - Reduces cyclomatic complexity, improves testability

---

## High Priority Issues (Weeks 2-3)

### 4. AuthEndpoints.cs - SPLIT LARGE ENDPOINT FILE
**File**: `/home/user/meepleai-monorepo/apps/api/src/Api/Routing/AuthEndpoints.cs`
**Size**: 1077 LOC

**Split into**:
- `AuthenticationEndpoints.cs` - Login/logout/registration
- `OAuthEndpoints.cs` - OAuth flows  
- `2FAEndpoints.cs` - 2FA management
- `PasswordEndpoints.cs` - Password reset/change

**Estimated Time**: 20-30 hours
**Impact**: Medium

---

### 5. Validation Pattern Extraction
**Issue**: 398 instances of `IsNullOrWhiteSpace()` duplicated across codebase
**Locations**:
- `/home/user/meepleai-monorepo/apps/api/src/Api/BoundedContexts/` (148 instances)
- `/home/user/meepleai-monorepo/apps/api/src/Api/Routing/` (64+ instances)
- `/home/user/meepleai-monorepo/apps/api/src/Api/Services/` (100+ instances)

**Action**: Create validation utilities
```csharp
// New file: /Api/SharedKernel/ValidationExtensions.cs
public static class ValidationExtensions
{
    public static (bool Valid, string Error) ValidateNotEmpty(string value, string fieldName)
    public static (bool Valid, string Error) ValidateGameId(string gameId)
    public static (bool Valid, string Error) ValidateQuery(string query)
    // etc.
}
```

**Estimated Time**: 20-30 hours
**Impact**: Medium - 20-30% reduction in duplicate code

---

## Medium Priority Issues (Weeks 4-5)

### 6. PromptEvaluationService.cs - EXTRACT CONCERNS
**File**: `/home/user/meepleai-monorepo/apps/api/src/Api/Services/PromptEvaluationService.cs`
**Size**: 965 LOC
**Problem**: Dataset loading, evaluation, and reporting mixed

**Action**:
1. Extract `DatasetLoader` (~150 LOC)
   - Move: `LoadDatasetAsync()` 
   - Handle: Security validation (PathSecurity)

2. Extract `PromptTestEvaluator` (~200 LOC)
   - Move: `EvaluateAsync()` implementation
   - Complex evaluation logic in isolation

3. Extract `EvaluationReportBuilder` (~100 LOC)
   - Move: Report generation logic
   - Support multiple formats

4. Create CQRS handlers (post-extraction)
   - `EvaluatePromptCommandHandler`
   - `GetEvaluationReportQueryHandler`

**Estimated Time**: 30-40 hours
**Impact**: Medium

---

### 7. Handler Refactoring - Extract Orchestrators
**Large Handlers to Refactor** (200-636 LOC):

| Handler | LOC | Extract Service | Est. Time |
|---------|-----|-----------------|-----------|
| StreamSetupGuideQueryHandler | 636 | SetupGuideGenerator | 20h |
| InvokeChessAgentCommandHandler | 450 | ChessAgentOrchestrator | 15h |
| GenerateFollowUpQuestionsQueryHandler | 305 | FollowUpQuestionGenerator | 10h |
| StreamQaQueryHandler | 287 | QaStreamingFormatter | 10h |
| IndexPdfCommandHandler | 243 | PdfIndexingOrchestrator | 12h |
| StreamExplainQueryHandler | 233 | ExplainStreamingFormatter | 10h |

**Current Location**: `/home/user/meepleai-monorepo/apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Handlers/`

**Create Services in**: `/home/user/meepleai-monorepo/apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/`

**Estimated Time**: 80-100 hours (all handlers)
**Impact**: Low-Medium - Improves testability

---

## Low Priority Issues (Ongoing)

### 8. Exception Handling Duplication
**Issue**: 24+ similar `catch (HttpRequestException)` blocks
**Pattern**: All in `/home/user/meepleai-monorepo/apps/api/src/Api/Services/`

**Current Status**: RagExceptionHandler exists at `/home/user/meepleai-monorepo/apps/api/src/Api/Services/Rag/RagExceptionHandler.cs`

**Action**: Extend usage pattern to:
- HybridSearchService
- QdrantService
- EmbeddingService
- BggApiService

**Estimated Time**: 10-15 hours
**Impact**: Medium - Standardizes error handling

---

## Quick Wins (Can Do Immediately)

### Quick Win 1: Extract RagExceptionHandler Pattern
**Time**: 4 hours
**Files Affected**:
- `/home/user/meepleai-monorepo/apps/api/src/Api/Services/RagService.cs` (use existing)
- `/home/user/meepleai-monorepo/apps/api/src/Api/Services/HybridSearchService.cs` (add similar)
- `/home/user/meepleai-monorepo/apps/api/src/Api/Services/QdrantService.cs` (add similar)

### Quick Win 2: Create Query Validation Helper
**Time**: 2-3 hours
**New File**: `/home/user/meepleai-monorepo/apps/api/src/Api/SharedKernel/ValidationExtensions.cs`

### Quick Win 3: Extract Session Validation Middleware
**Time**: 3 hours
**Enhancement**: All 64 instances of `TryGetActiveSession()` could move to middleware

---

## Repository Status

**Well-Designed Repositories** (No changes needed):
- `/home/user/meepleai-monorepo/apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Persistence/ChatThreadRepository.cs` (331 LOC - good)
- `/home/user/meepleai-monorepo/apps/api/src/Api/BoundedContexts/Authentication/Infrastructure/Persistence/UserRepository.cs` (258 LOC - good)
- All other repositories: appropriately sized

---

## Implementation Sequence (Recommended)

**Week 1-2 (CRITICAL)**
1. Split AdminEndpoints.cs (40-50h)
2. Migrate ConfigurationService to CQRS (50-60h)

**Week 3-4 (HIGH)**
3. Refactor RagService (40-50h)
4. Split AuthEndpoints.cs (20-30h)

**Week 5-6 (MEDIUM)**
5. Extract Validation Framework (20-30h)
6. Refactor PromptEvaluationService (30-40h)

**Ongoing**
7. Refactor large handlers (80-100h)
8. Improve exception handling patterns (10-15h)

**Total Estimated Effort**: 300-400 hours
**Total Estimated Timeline**: 8-10 weeks (assuming 40 hours/week)

---

## Success Metrics

After completing Phase 1-3:
- [ ] No service files > 600 LOC
- [ ] No endpoint files > 800 LOC
- [ ] 100% CQRS compliance for business logic
- [ ] 50%+ reduction in validation code duplication
- [ ] All handlers < 300 LOC (with extracted services)
- [ ] Zero high-complexity methods (CC > 10)

