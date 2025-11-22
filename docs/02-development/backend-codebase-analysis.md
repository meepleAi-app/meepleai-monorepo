# Backend Codebase Analysis Report
**Date**: 2025-11-19  
**Focus Area**: apps/api/src/Api/  
**Analysis Type**: Thorough monolithic class detection, CQRS refactoring opportunities, code duplication analysis

---

## Executive Summary

The backend codebase shows **moderate technical debt** with several areas for refactoring:
- **4 monolithic classes** exceeding 500+ LOC with multiple responsibilities
- **9 large handlers** (200+ LOC) with potential for extraction
- **2 extremely large endpoint files** (2000+ LOC) requiring splitting
- **Significant code duplication** in validation patterns (398+ instances) and exception handling (24+ patterns)
- **Strong CQRS foundation** with 224 handlers already operational, enabling further refactoring

**Priority Refactoring**: ConfigurationService → CQRS, AdminEndpoints split, validation framework extraction

---

## 1. MONOLITHIC CLASSES (>500 LOC)

### 1.1 RagService - **HIGH PRIORITY**
**File**: `/home/user/meepleai-monorepo/apps/api/src/Api/Services/RagService.cs`  
**Metrics**:
- **Lines of Code**: 995 LOC
- **Public Methods**: 4 (but one handler with 24 try-catch blocks)
- **Dependencies**: 11 injected services
- **Cyclomatic Complexity**: Very High (multiple nested try-catch blocks, 24 exception handlers)

**Current Responsibilities**:
1. Q&A answering with vector search (AskAsync) - lines 75-286
2. Explanation generation (ExplainAsync) - lines 294-454
3. Hybrid search Q&A (AskWithHybridSearchAsync) - lines 522-725
4. Custom prompt evaluation (AskWithCustomPromptAsync) - lines 732-922
5. RAG configuration management (GetRagConfigAsync) - lines 930-994
6. Configuration validation (ValidateRagConfig) - lines 965-994

**Issues**:
- **SRP Violation**: Mixing RAG orchestration, configuration management, and exception handling
- **Repeated Exception Handling**: 24 catch blocks with similar logic (try-catch patterns repeated 3+ times in AskAsync, AskWithHybridSearchAsync, AskWithCustomPromptAsync)
- **Configuration Logic Duplication**: GetRagConfigAsync and ValidateRagConfig could be extracted

**Refactoring Recommendations**:
1. **Extract RagConfigurationProvider** (new service, ~40 LOC)
   - Move GetRagConfigAsync and ValidateRagConfig
   - Handle 3-tier fallback logic (DB → appsettings → defaults)
   
2. **Extract RagExceptionHandler** (already attempted, but incomplete)
   - Create dedicated exception handling orchestrator
   - Reuse RagExceptionHandler.HandleException pattern across all methods
   
3. **Create specialized RAG operation handlers** (CQRS):
   - `AskQuestionCommandHandler` (already exists, wrap RagService)
   - `ExplainTopicCommandHandler` (new CQRS handler)
   - `EvaluateCustomPromptCommandHandler` (new CQRS handler)

4. **Split into 3 focused services**:
   - `RagOrchestrationService` - coordinates RAG pipeline
   - `RagConfigurationService` - manages configuration with validation
   - `RagExceptionHandler` - centralized exception handling

**Priority**: **HIGH** - This is a service boundary that should be split

---

### 1.2 ConfigurationService - **CRITICAL PRIORITY**
**File**: `/home/user/meepleai-monorepo/apps/api/src/Api/Services/ConfigurationService.cs`  
**Metrics**:
- **Lines of Code**: 805 LOC
- **Public Methods**: 17 (SRP violation - too many responsibilities)
- **Dependencies**: 5 injected services
- **Cyclomatic Complexity**: Medium-High

**Current Responsibilities**:
1. Configuration CRUD (GetConfigurations, CreateConfiguration, UpdateConfiguration, DeleteConfiguration)
2. Configuration retrieval with caching (GetConfigurationByKey, GetValue)
3. Validation logic (ValidateConfiguration)
4. Configuration versioning/history (GetConfigurationHistory, RollbackConfiguration)
5. Bulk operations (BulkUpdateConfigurations)
6. Import/Export functionality (ExportConfigurations, ImportConfigurations)
7. Feature flag management via cache invalidation
8. Category management (GetCategories)

**Issues**:
- **SRP Violation**: 17 public methods serving 5+ different responsibilities
- **Feature Envy**: Closely coupled with IHybridCacheService and MeepleAiDbContext
- **Testability**: Difficult to test individual concerns (CRUD vs. validation vs. versioning)

**Refactoring Recommendations** (Create CQRS Handlers):
1. **Configuration Queries** (new handlers):
   - `GetConfigurationByIdQueryHandler` (wraps GetConfigurationByIdAsync)
   - `GetConfigurationByKeyQueryHandler` (wraps GetConfigurationByKeyAsync)
   - `GetConfigurationHistoryQueryHandler` (wraps GetConfigurationHistoryAsync)
   - `ListConfigurationsQueryHandler` (wraps GetConfigurationsAsync)

2. **Configuration Commands** (new handlers):
   - `CreateConfigurationCommandHandler` (wraps CreateConfigurationAsync)
   - `UpdateConfigurationCommandHandler` (wraps UpdateConfigurationAsync)
   - `DeleteConfigurationCommandHandler` (wraps DeleteConfigurationAsync)
   - `ToggleConfigurationCommandHandler` (wraps ToggleConfigurationAsync)
   - `RollbackConfigurationCommandHandler` (wraps RollbackConfigurationAsync)
   - `BulkUpdateConfigurationsCommandHandler` (wraps BulkUpdateConfigurationsAsync)

3. **Extract ConfigurationValidator** (new domain service, ~100 LOC):
   - Move ValidateConfiguration logic
   - Pure validation, no side effects
   
4. **Extract ConfigurationVersionManager** (new service, ~150 LOC):
   - Move GetConfigurationHistory and RollbackConfiguration
   - Handle versioning separately

5. **Reduce ConfigurationService to 300 LOC** (infrastructure only):
   - Keep only: Repository access, caching, persistence operations
   - Move all business logic to handlers/domain services

**Priority**: **CRITICAL** - This should be fully CQRS-compliant to match the DDD architecture

---

### 1.3 PromptEvaluationService - **MEDIUM PRIORITY**
**File**: `/home/user/meepleai-monorepo/apps/api/src/Api/Services/PromptEvaluationService.cs`  
**Metrics**:
- **Lines of Code**: 965 LOC
- **Public Methods**: 5
- **Dependencies**: 5 injected services
- **Cyclomatic Complexity**: High (complex evaluation logic)

**Current Responsibilities**:
1. Dataset loading with security validation (LoadDatasetAsync)
2. Prompt evaluation execution (EvaluateAsync) - **500+ LOC, very complex**
3. Evaluation report generation (GeneratePromptTestReport)
4. Dataset validation (ValidateDataset)
5. Internal evaluation methods

**Issues**:
- **Evaluation Logic Complexity**: EvaluateAsync method is monolithic with nested loops and complex state tracking
- **Security Validation**: Good (PathSecurity), but mixed with business logic
- **No separation of concerns**: Dataset loading, validation, and evaluation in one class

**Refactoring Recommendations**:
1. **Extract DatasetLoader** (new service, ~150 LOC):
   - Handle file I/O with security validation
   - Move LoadDatasetAsync logic
   
2. **Extract PromptTestEvaluator** (new service, ~200 LOC):
   - Handle prompt evaluation execution
   - Separate from report generation
   
3. **Create EvaluationReportBuilder** (new service, ~100 LOC):
   - Generate formatted reports
   - Support multiple output formats (JSON, CSV, etc.)

4. **Convert to CQRS** (post-refactoring):
   - `EvaluatePromptCommandHandler` wraps PromptTestEvaluator
   - `GetEvaluationReportQueryHandler` wraps report generation

**Priority**: **MEDIUM** - Less critical than ConfigurationService, but evaluation logic should be better separated

---

### 1.4 RagEvaluationService - **MEDIUM PRIORITY**
**File**: `/home/user/meepleai-monorepo/apps/api/src/Api/Services/RagEvaluationService.cs`  
**Metrics**:
- **Lines of Code**: 609 LOC
- **Public Methods**: 4 (in interface + 1 implementation)
- **Dependencies**: 3-4 injected services
- **Cyclomatic Complexity**: Medium

**Current Responsibilities**:
1. Dataset loading and validation
2. RAG evaluation metrics calculation (Precision@K, Recall@K, MRR)
3. Report generation (JSON and Markdown)

**Issues**:
- **Metrics Calculation**: Complex nested loops for computing evaluation metrics
- **Report Generation**: Mixed format handling (JSON + Markdown)

**Refactoring Recommendations**:
1. **Extract RagMetricsCalculator** (new service, ~200 LOC):
   - Handle precision, recall, MRR calculations
   - Testable in isolation
   
2. **Extract RagReportFormatter** (new service, ~150 LOC):
   - Handle JSON and Markdown report generation
   - Similar to existing PDF/TXT export formatters

**Priority**: **MEDIUM** - Good separation from main RAG pipeline, but internal structure needs improvement

---

## 2. ANALYSIS OF RETAINED SERVICES

### 2.1 AdminStatsService - **Appropriate Size**
**File**: `/home/user/meepleai-monorepo/apps/api/src/Api/Services/AdminStatsService.cs`  
**Metrics**:
- **Lines of Code**: 411 LOC
- **Public Methods**: 2 (appropriate)
- **Dependencies**: 3 injected services

**Responsibilities**:
1. Dashboard statistics aggregation (GetDashboardStatsAsync)
2. Data export functionality (ExportDashboardDataAsync)

**Assessment**: ✓ **Good** - Single responsibility focus, appropriate service boundary
**Recommendation**: Could be migrated to CQRS with two handlers, but not urgent

---

### 2.2 AlertingService - **Appropriate Size**
**File**: `/home/user/meepleai-monorepo/apps/api/src/Api/Services/AlertingService.cs`  
**Metrics**:
- **Lines of Code**: 287 LOC
- **Public Methods**: 4
- **Dependencies**: 4 injected services

**Responsibilities**:
1. Alert creation and delivery
2. Alert resolution
3. Active alerts retrieval
4. Throttling logic

**Assessment**: ✓ **Good** - Focused on alerting domain

---

### 2.3 Other Large Services (300-500 LOC)

| Service | LOC | Methods | Assessment |
|---------|-----|---------|------------|
| EmbeddingService | 552 | ~6-7 | **MEDIUM** - Multiple embedding models, consider extraction |
| QdrantService | 536 | ~8-10 | **MEDIUM** - Vector DB operations, mostly appropriate |
| PromptTemplateService | 517 | ~8-10 | **MEDIUM** - Prompt engineering logic, could be split |
| OAuthService | 497 | ~6-8 | **MEDIUM** - OAuth coordination, appropriate size |
| HybridCacheService | 494 | ~6-8 | **MEDIUM** - Cache orchestration, good separation |
| TotpService | 464 | ~6-8 | **MEDIUM** - 2FA logic, focused |
| HybridSearchService | 409 | ~6-8 | **MEDIUM** - Search orchestration, focused |
| BggApiService | 383 | ~4-5 | **GOOD** - External API integration |
| KeywordSearchService | 337 | ~4-6 | **GOOD** - Keyword search, focused |

---

## 3. LARGE ENDPOINT FILES - **HIGH PRIORITY**

### 3.1 AdminEndpoints - **CRITICAL REFACTORING NEEDED**
**File**: `/home/user/meepleai-monorepo/apps/api/src/Api/Routing/AdminEndpoints.cs`  
**Metrics**:
- **Lines of Code**: 2031 LOC (largest endpoint file)
- **Estimated Route Groups**: 8-10 distinct resources
- **Cyclomatic Complexity**: Very High

**Current Endpoints**:
- System configuration management
- Analytics/dashboard
- Alert management
- Audit log retrieval
- Feature flag management
- Prompt management/evaluation
- User management

**Issues**:
- Single 2000+ LOC file managing 8+ resource types
- Difficult to locate specific endpoints
- High merge conflict risk
- Violates single responsibility principle at file level

**Refactoring Plan** - Split into 6 files:
1. `ConfigurationEndpoints.cs` (~350 LOC)
   - System configuration CRUD
   - Configuration validation
   - Configuration history/rollback
   
2. `AnalyticsEndpoints.cs` (~250 LOC)
   - Dashboard statistics
   - Data export
   - Usage tracking
   
3. `AlertEndpoints.cs` (~200 LOC)
   - Alert management
   - Alert resolution
   - Alert history
   
4. `AuditEndpoints.cs` (~150 LOC)
   - Audit log retrieval
   - Filtered searches
   
5. `FeatureFlagEndpoints.cs` (~150 LOC)
   - Feature flag toggles
   - Rollback
   
6. `PromptManagementEndpoints.cs` (~300 LOC)
   - Prompt templates
   - Evaluation workflows
   - Version management

**Priority**: **CRITICAL** - This file is unmanageable at current size

---

### 3.2 AuthEndpoints - **Medium Priority**
**File**: `/home/user/meepleai-monorepo/apps/api/src/Api/Routing/AuthEndpoints.cs`  
**Metrics**:
- **Lines of Code**: 1077 LOC
- **Estimated Route Groups**: 5-6
- **Suggested Split**: Yes, into 3-4 files

**Potential Split**:
1. `AuthenticationEndpoints.cs` - Login/logout
2. `OAuthEndpoints.cs` - OAuth flows
3. `2FAEndpoints.cs` - 2FA management
4. `PasswordEndpoints.cs` - Password reset/change

**Priority**: **MEDIUM**

---

### 3.3 AiEndpoints - **Low Priority**
**File**: `/home/user/meepleai-monorepo/apps/api/src/Api/Routing/AiEndpoints.cs`  
**Metrics**:
- **Lines of Code**: 919 LOC
- **Estimated Route Groups**: 3-4 (acceptable)

**Assessment**: Acceptable size for now, but consider splitting if adding more features

---

## 4. CODE DUPLICATION ANALYSIS

### 4.1 Repeated Validation Patterns
**Finding**: 398 instances of `IsNullOrWhiteSpace` checks across codebase
**Location**: Mostly in BoundedContexts (~148 instances), handlers, services

**Example Pattern** (repeated in 20+ places):
```csharp
if (string.IsNullOrWhiteSpace(query))
{
    return new QaResponse("Please provide a question.", Array.Empty<Snippet>());
}
```

**Refactoring Opportunity** - Create validation utility class:
```csharp
public static class ValidationExtensions
{
    public static (bool Valid, string Error) ValidateQuery(string query)
        => string.IsNullOrWhiteSpace(query) 
            ? (false, "Please provide a question.")
            : (true, "");
}
```

**Impact**: Could reduce duplication by 20-30%, improve consistency

---

### 4.2 Exception Handling Duplication
**Finding**: 24+ `catch (HttpRequestException)` blocks with similar patterns

**Example** (RagService, lines 242-248, 681-687, 878-884):
```csharp
catch (HttpRequestException ex)
{
    return RagExceptionHandler.HandleException(
        ex, _logger,
        RagExceptionHandler.GetLogAction(...),
        gameId, "operation", activity, stopwatch,
        () => new QaResponse("Network error..."));
}
```

**Status**: RagExceptionHandler exists but not fully utilized across codebase

**Refactoring Opportunity**:
- Extract common exception mapping logic
- Create operation-specific error response factories
- Reduce try-catch boilerplate by 30%

---

### 4.3 Session Validation Duplication
**Finding**: 64 instances of `TryGetActiveSession()` in endpoints
**Location**: All endpoint files

**Pattern** (repeated in every endpoint):
```csharp
var (authenticated, session, error) = context.TryGetActiveSession();
if (!authenticated) return error!;
```

**Status**: Already extracted to extension method (good!)
**Assessment**: ✓ Well-handled with extension method

**Opportunity**: Consider middleware-based validation for more complex scenarios

---

### 4.4 Handler Dependency Injection Patterns
**Finding**: ~53 service dependencies across all handlers
**Average**: 2-5 dependencies per handler (appropriate)
**Range**: 1-6 dependencies

**High-Dependency Handlers**:
- IndexChessKnowledgeCommandHandler: 5 dependencies
- InvokeChessAgentCommandHandler: 4+ dependencies
- StreamSetupGuideQueryHandler: 7 dependencies

**Assessment**: Within acceptable range (not creating service locator anti-pattern)

---

## 5. LARGE HANDLER ANALYSIS

### 5.1 StreamSetupGuideQueryHandler - **Medium Refactoring**
**File**: `/home/user/meepleai-monorepo/apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Handlers/StreamSetupGuideQueryHandler.cs`  
**Metrics**:
- **Lines of Code**: 636 LOC
- **Public Methods**: 5 (handle + 4 private helpers)
- **Dependencies**: 7 injected services

**Responsibilities**:
1. Streaming event creation (CreateEvent, StreamStepsAsync)
2. Setup guide generation (GenerateSetupGuideInternalAsync)
3. Guide parsing and formatting

**Issues**:
- **Large handler**: 636 LOC for a single query handler
- **Complex streaming logic**: Multiple generator methods (StreamStepsAsync, GenerateSetupGuideInternalAsync)

**Refactoring**:
1. Extract SetupGuideGenerator service (200 LOC)
2. Extract SetupGuideStreamingFormatter (150 LOC)
3. Reduce handler to 200 LOC (orchestration only)

**Priority**: **MEDIUM**

---

### 5.2 InvokeChessAgentCommandHandler - **Medium Refactoring**
**File**: `/home/user/meepleai-monorepo/apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Handlers/InvokeChessAgentCommandHandler.cs`  
**Metrics**:
- **Lines of Code**: 450 LOC
- **Dependencies**: 4

**Issues**:
- Complex chess-specific logic mixed with handler concerns

**Refactoring**: Extract ChessAgentOrchestrator service

---

### 5.3 Other Large Handlers (200-300 LOC)
| Handler | LOC | Recommendation |
|---------|-----|-----------------|
| GenerateFollowUpQuestionsQueryHandler | 305 | Extract FollowUpQuestionGenerator |
| StreamQaQueryHandler | 287 | Extract QaStreamingFormatter |
| IndexPdfCommandHandler | 243 | Extract PdfIndexingOrchestrator |
| StreamExplainQueryHandler | 233 | Extract ExplainStreamingFormatter |
| AskQuestionQueryHandler | 213 | Refactor to use RagOrchestrationService |
| InvokeAgentCommandHandler | 211 | Extract AgentCommandOrchestrator |
| IndexChessKnowledgeCommandHandler | 203 | Extract ChessIndexingService |

**Priority**: **LOW-MEDIUM** - Handlers are appropriately sized for CQRS pattern, but could benefit from extracted orchestrators

---

## 6. INFRASTRUCTURE PATTERNS

### 6.1 Repository Implementations - Assessment
**Largest Repositories**:
- ChatThreadRepository: 331 LOC
- DocnetPdfValidator: 416 LOC (not a repository, but validator)
- UserRepository: 258 LOC
- GameSessionRepository: 247 LOC

**Assessment**: ✓ **Good** - Repositories appropriately sized, focused on data access

**Pattern Quality**: All repositories follow DDD patterns with:
- Clean interface definitions
- Specification pattern usage
- Appropriate query composition

---

### 6.2 Service Extension Configuration - **Duplication Opportunity**
**File**: `/home/user/meepleai-monorepo/apps/api/src/Api/Extensions/InfrastructureServiceExtensions.cs`  
**Metrics**:
- **Lines of Code**: 281 LOC
- **Service Registrations**: ~30+

**Opportunity**: 
- Consider grouping related service registrations into bounded context extension methods
- Already partially done in Program.cs (AddApplicationServices, AddAuthenticationServices)
- Good pattern established

**Assessment**: ✓ Good pattern, well-organized

---

## 7. INFRASTRUCTURE COMPLEXITY - External Adapters

### 7.1 PDF Processing Infrastructure - **Complex but Justified**
**Files**:
- EnhancedPdfProcessingOrchestrator: 786 LOC (justified - 3-stage pipeline)
- DocnetPdfValidator: 416 LOC
- DocnetPdfTextExtractor: 370 LOC
- SmolDoclingPdfTextExtractor: 352 LOC
- UnstructuredPdfTextExtractor: 264 LOC

**Assessment**: ✓ **Justified** - Multi-model PDF processing with fallback architecture is inherently complex

---

## 8. SUMMARY TABLE - Refactoring Priorities

| Class/File | LOC | Issue | Priority | Effort | Impact |
|------------|-----|-------|----------|--------|--------|
| **AdminEndpoints** | 2031 | Monolithic endpoint file | **CRITICAL** | High | High |
| **ConfigurationService** | 805 | Too many responsibilities (17 methods) | **CRITICAL** | High | High |
| **RagService** | 995 | Repeated exception handling, SRP violation | **HIGH** | High | High |
| **AuthEndpoints** | 1077 | Large endpoint file | **HIGH** | Medium | Medium |
| **PromptEvaluationService** | 965 | Complex evaluation logic | **MEDIUM** | Medium | Medium |
| **StreamSetupGuideQueryHandler** | 636 | Large handler with complex logic | **MEDIUM** | Medium | Medium |
| **RagEvaluationService** | 609 | Metrics calculation complexity | **MEDIUM** | Low | Low |
| **Validation Patterns** | 398 instances | Code duplication | **MEDIUM** | Medium | Medium |
| **Exception Handling** | 24+ patterns | Duplication of error responses | **MEDIUM** | Low | Medium |

---

## 9. RECOMMENDED REFACTORING ROADMAP

### Phase 1 (Weeks 1-2) - **CRITICAL Issues**
1. **Split AdminEndpoints** (2031 LOC → 6 files)
   - Estimated: 40-50 hours
   - High visibility improvement
   
2. **Migrate ConfigurationService to CQRS**
   - Create 10+ handlers wrapping existing methods
   - Extract ConfigurationValidator domain service
   - Estimated: 50-60 hours

### Phase 2 (Weeks 3-4) - **HIGH Priority**
1. **Refactor RagService**
   - Extract RagConfigurationProvider
   - Extract RagExceptionHandler logic
   - Create RagOrchestrationService
   - Estimated: 40-50 hours
   
2. **Split AuthEndpoints**
   - Create 3-4 focused endpoint files
   - Estimated: 20-30 hours

### Phase 3 (Weeks 5-6) - **MEDIUM Priority**
1. **Extract Validation Utility Framework**
   - Reduce 398 IsNullOrWhiteSpace duplications
   - Estimated: 20-30 hours
   
2. **Refactor PromptEvaluationService**
   - Extract dataset loader, evaluator, report builder
   - Estimated: 30-40 hours

### Phase 4 (Ongoing) - **LOW-MEDIUM Priority**
1. Refactor individual handlers (200+ LOC)
2. Extract specialized formatters/builders
3. Improve handler testing coverage

---

## 10. QUICK WINS (Low Effort, Medium Impact)

1. **Create ValidationExtensions utility** (~2 hours)
   - Reduce IsNullOrWhiteSpace calls
   - Improve consistency

2. **Extract RagExceptionHandler pattern** (~4 hours)
   - Standardize error responses
   - Reduce try-catch duplication

3. **Create QueryParameter validation helper** (~3 hours)
   - Extract common endpoint parameter validation
   - Reuse across endpoints

---

## 11. CYCLOMATIC COMPLEXITY HOTSPOTS

**High Complexity Methods**:
1. `RagService.AskAsync` - Lines 75-286 (multiple nested branches + 5 catch blocks)
2. `PromptEvaluationService.EvaluateAsync` - Large evaluation logic
3. `AdminEndpoints` handlers - Multiple nested if-else chains
4. `ConfigurationService.ValidateConfiguration` - Complex validation rules

**Recommendation**: Run CodeMetrics analyzer to quantify exact cyclomatic complexity

---

## 12. CQRS READINESS FOR REMAINING SERVICES

**Excellent Candidates for CQRS Migration**:
- ✓ ConfigurationService (17 methods → 12+ handlers)
- ✓ AdminStatsService (2 methods → 2 queries)
- ✓ AlertingService (4 methods → 4 handlers)
- ✓ RagEvaluationService (4 methods → 4 handlers)

**Already CQRS-compliant**:
- ✓ RagService (wrapped by handlers, called from endpoints)
- ✓ All BoundedContext handlers (224 operational handlers)

**Benefit**: 100% CQRS compliance would enable:
- Centralized cross-cutting concerns (audit, validation)
- Consistent behavior pipes
- Better testability
- Easier feature flag integration

---

## Conclusion

The codebase is in **good health** overall with a strong DDD/CQRS foundation (224 handlers, 100% DDD completion). However:

1. **2-3 remaining monolithic services** need CQRS migration
2. **1 critical endpoint file** (AdminEndpoints) needs splitting
3. **Validation duplication** (398 instances) should be addressed
4. **Handlers are appropriately sized** (not over-complex)

**Total estimated refactoring effort**: 300-400 hours across 4 phases over 8 weeks

**Expected outcome**: 
- 100% CQRS compliance across application services
- Improved code maintainability and testability
- 30%+ reduction in duplicate validation code
- Better endpoint file organization

