# Issue #2310 - Remaining 22 Tests Analysis

**Date**: 2026-01-10
**Current Test Count**: 4,277 backend tests (293 from Epic #2310)
**Target**: 315 additional tests for complete coverage
**Gap**: 22 tests identified

---

## Summary

Analysis reveals 3 primary categories of missing test coverage:

1. **AlertRules & AlertConfiguration** (8 handlers, 0 tests) - HIGH priority
2. **PromptEvaluation** (2 handlers, 0 tests) - MEDIUM priority
3. **Foreign Key Constraints** (SystemConfiguration, DocumentProcessing) - HIGH priority
4. **Concurrency/Deadlock Scenarios** (cross-context operations) - MEDIUM priority
5. **Edge Case Workflows** (complex multi-step scenarios) - LOW priority

---

## Category 1: AlertRules & AlertConfiguration (8 tests - HIGH)

### Test 1: CreateAlertRuleCommandHandlerTests
**File**: `apps/api/tests/Api.Tests/BoundedContexts/Administration/Application/Handlers/AlertRules/CreateAlertRuleCommandHandlerTests.cs`
**Scenario**: Create alert rule with valid conditions, validate creation, test duplicate prevention
**Bounded Context**: Administration
**Priority**: HIGH
**Rationale**: Core alert management feature with no test coverage

### Test 2: UpdateAlertRuleCommandHandlerTests
**File**: `apps/api/tests/Api.Tests/BoundedContexts/Administration/Application/Handlers/AlertRules/UpdateAlertRuleCommandHandlerTests.cs`
**Scenario**: Update alert rule conditions, validate version increment, test concurrent updates
**Bounded Context**: Administration
**Priority**: HIGH
**Rationale**: Critical for alert rule modification workflows

### Test 3: DeleteAlertRuleCommandHandlerTests
**File**: `apps/api/tests/Api.Tests/BoundedContexts/Administration/Application/Handlers/AlertRules/DeleteAlertRuleCommandHandlerTests.cs`
**Scenario**: Delete alert rule, verify cascade behavior, test orphaned alert handling
**Bounded Context**: Administration
**Priority**: HIGH
**Rationale**: Must validate FK constraint behavior

### Test 4: EnableAlertRuleCommandHandlerTests
**File**: `apps/api/tests/Api.Tests/BoundedContexts/Administration/Application/Handlers/AlertRules/EnableAlertRuleCommandHandlerTests.cs`
**Scenario**: Enable/disable alert rule, validate state transitions, test idempotency
**Bounded Context**: Administration
**Priority**: MEDIUM
**Rationale**: State management validation

### Test 5: GetAlertRuleByIdQueryHandlerTests
**File**: `apps/api/tests/Api.Tests/BoundedContexts/Administration/Application/Handlers/AlertRules/GetAlertRuleByIdQueryHandlerTests.cs`
**Scenario**: Retrieve alert rule by ID, validate not found scenarios, test soft-deleted rules
**Bounded Context**: Administration
**Priority**: MEDIUM
**Rationale**: Query validation for alert retrieval

### Test 6: GetAllAlertRulesQueryHandlerTests
**File**: `apps/api/tests/Api.Tests/BoundedContexts/Administration/Application/Handlers/AlertRules/GetAllAlertRulesQueryHandlerTests.cs`
**Scenario**: List all alert rules with filters, validate pagination, test large datasets
**Bounded Context**: Administration
**Priority**: MEDIUM
**Rationale**: Performance validation for list queries

### Test 7: UpdateAlertConfigurationCommandHandlerTests
**File**: `apps/api/tests/Api.Tests/BoundedContexts/Administration/Application/Handlers/AlertConfiguration/UpdateAlertConfigurationCommandHandlerTests.cs`
**Scenario**: Update alert configuration, validate validation rules, test concurrent modifications
**Bounded Context**: Administration
**Priority**: HIGH
**Rationale**: Configuration management with concurrency concerns

### Test 8: GetAlertConfigurationQueryHandlerTests
**File**: `apps/api/tests/Api.Tests/BoundedContexts/Administration/Application/Handlers/AlertConfiguration/GetAlertConfigurationQueryHandlerTests.cs`
**Scenario**: Retrieve alert configuration, validate default values, test cache invalidation
**Bounded Context**: Administration
**Priority**: MEDIUM
**Rationale**: Configuration retrieval validation

---

## Category 2: PromptEvaluation (2 tests - MEDIUM)

### Test 9: EvaluatePromptCommandHandlerTests
**File**: `apps/api/tests/Api.Tests/BoundedContexts/Administration/Application/Commands/PromptEvaluation/EvaluatePromptCommandHandlerTests.cs`
**Scenario**: Evaluate prompt against test cases, validate metrics calculation, test error handling
**Bounded Context**: Administration
**Priority**: MEDIUM
**Rationale**: AI evaluation workflows need coverage

### Test 10: ComparePromptVersionsCommandHandlerTests
**File**: `apps/api/tests/Api.Tests/BoundedContexts/Administration/Application/Commands/PromptEvaluation/ComparePromptVersionsCommandHandlerTests.cs`
**Scenario**: Compare 2+ prompt versions, validate diff calculation, test performance metrics
**Bounded Context**: Administration
**Priority**: MEDIUM
**Rationale**: Version comparison logic validation

---

## Category 3: Foreign Key Constraints (7 tests - HIGH)

### Test 11: SystemConfigurationForeignKeyConstraintsTests
**File**: `apps/api/tests/Api.Tests/Integration/SystemConfiguration/SystemConfigurationForeignKeyConstraintsTests.cs`
**Scenario**: Test SystemConfiguration.CreatedBy/UpdatedBy FK restrictions (DeleteBehavior.Restrict)
**Bounded Context**: SystemConfiguration
**Priority**: HIGH
**Rationale**: Validates FK constraint `OnDelete(DeleteBehavior.Restrict)` enforcement
**Test Cases**:
- Cannot delete User if referenced by SystemConfiguration.CreatedBy
- Cannot delete User if referenced by SystemConfiguration.UpdatedBy
- Orphan detection on user deletion attempt

### Test 12: DocumentCollectionCascadeDeleteTests
**File**: `apps/api/tests/Api.Tests/Integration/DocumentProcessing/DocumentCollectionCascadeDeleteTests.cs`
**Scenario**: Test DocumentCollection.Game cascade delete (DeleteBehavior.Cascade)
**Bounded Context**: DocumentProcessing
**Priority**: HIGH
**Rationale**: Validates cascade behavior when Game is deleted
**Test Cases**:
- Deleting Game cascades to DocumentCollection
- No orphaned DocumentCollection entities
- ChatThreadCollection junction table cleanup

### Test 13: DocumentCollectionUserRestrictionTests
**File**: `apps/api/tests/Api.Tests/Integration/DocumentProcessing/DocumentCollectionUserRestrictionTests.cs`
**Scenario**: Test DocumentCollection.CreatedBy FK restriction (DeleteBehavior.Restrict)
**Bounded Context**: DocumentProcessing
**Priority**: HIGH
**Rationale**: Validates user cannot be deleted if they created collections
**Test Cases**:
- Cannot delete User if referenced by DocumentCollection.CreatedBy
- Error message validation
- Alternative: soft-delete user instead

### Test 14: ChunkedUploadSessionForeignKeyTests
**File**: `apps/api/tests/Api.Tests/Integration/DocumentProcessing/ChunkedUploadSessionForeignKeyTests.cs`
**Scenario**: Test ChunkedUploadSession.User FK constraint behavior
**Bounded Context**: DocumentProcessing
**Priority**: MEDIUM
**Rationale**: Validates upload session cleanup on user deletion
**Test Cases**:
- FK restriction or cascade delete validation
- Orphaned session detection
- Session expiration cleanup

### Test 15: RuleSpecCommentForeignKeyTests
**File**: `apps/api/tests/Api.Tests/Integration/GameManagement/RuleSpecCommentForeignKeyTests.cs`
**Scenario**: Test RuleSpecComment.User and RuleSpecComment.RuleSpec FK constraints
**Bounded Context**: GameManagement
**Priority**: MEDIUM
**Rationale**: Validates comment cascade/restriction behavior
**Test Cases**:
- Cannot delete User if they have comments (Restrict)
- Deleting RuleSpec cascades to comments (Cascade)
- Orphan prevention validation

### Test 16: ChatThreadCollectionForeignKeyTests
**File**: `apps/api/tests/Api.Tests/Integration/KnowledgeBase/ChatThreadCollectionForeignKeyTests.cs`
**Scenario**: Test ChatThreadCollection junction table FK constraints
**Bounded Context**: KnowledgeBase
**Priority**: MEDIUM
**Rationale**: Validates many-to-many relationship integrity
**Test Cases**:
- Deleting DocumentCollection cascades to junction table
- Deleting ChatThread cascades to junction table
- No orphaned junction records

### Test 17: ShareLinkForeignKeyTests
**File**: `apps/api/tests/Api.Tests/Integration/Authentication/ShareLinkForeignKeyTests.cs`
**Scenario**: Test ShareLink.CreatedBy and ShareLink.ChatThread FK constraints
**Bounded Context**: Authentication
**Priority**: MEDIUM
**Rationale**: Validates share link cleanup on user/thread deletion
**Test Cases**:
- Deleting ChatThread cascades to ShareLink
- User deletion restricted if active share links exist
- Expired link cleanup

---

## Category 4: Concurrency/Deadlock Scenarios (2 tests - MEDIUM)

### Test 18: SystemConfigurationConcurrentUpdateTests
**File**: `apps/api/tests/Api.Tests/Integration/SystemConfiguration/SystemConfigurationConcurrentUpdateTests.cs`
**Scenario**: Test optimistic concurrency for config updates, validate version conflicts
**Bounded Context**: SystemConfiguration
**Priority**: MEDIUM
**Rationale**: Configuration updates are frequent and concurrent
**Test Cases**:
- Parallel updates to same config key trigger DbUpdateConcurrencyException
- Version increment validation
- Last-write-wins vs optimistic locking

### Test 19: ChatThreadConcurrentMessageTests
**File**: `apps/api/tests/Api.Tests/Integration/KnowledgeBase/ChatThreadConcurrentMessageTests.cs`
**Scenario**: Test concurrent message creation in same ChatThread, validate order preservation
**Bounded Context**: KnowledgeBase
**Priority**: MEDIUM
**Rationale**: Chat workflows have high concurrency potential
**Test Cases**:
- Parallel message creation preserves order
- No message loss on concurrent inserts
- Transaction isolation validation

---

## Category 5: Edge Case Workflows (3 tests - LOW)

### Test 20: DocumentCollectionToRagPipelineIntegrationTests
**File**: `apps/api/tests/Api.Tests/Integration/CrossContext/DocumentCollectionToRagPipelineIntegrationTests.cs`
**Scenario**: Test end-to-end: Create collection → Add PDFs → Index → Query RAG
**Bounded Context**: CrossContext (DocumentProcessing + KnowledgeBase)
**Priority**: LOW
**Rationale**: E2E workflow validation across bounded contexts
**Test Cases**:
- Multi-document collection indexing
- RAG queries return results from all docs in collection
- Collection deletion cleanup

### Test 21: AlertRuleEvaluationWorkflowTests
**File**: `apps/api/tests/Api.Tests/Integration/Administration/AlertRuleEvaluationWorkflowTests.cs`
**Scenario**: Test alert rule trigger → evaluation → notification → resolution workflow
**Bounded Context**: Administration
**Priority**: LOW
**Rationale**: End-to-end alert lifecycle validation
**Test Cases**:
- Rule condition evaluation accuracy
- Alert creation on threshold breach
- Notification dispatch validation
- Alert resolution and history tracking

### Test 22: ~~GameFAQUpvoteRaceConditionTests~~ (REMOVED - Legacy System)
**Status**: OBSOLETE - Legacy GameFAQ system removed in favor of GameFaq (ISSUE-2370)
- No lost updates
- Idempotency for same user upvoting twice

---

## Implementation Priorities

### Priority Breakdown
- **HIGH**: 11 tests (AlertRules handlers + FK constraints)
- **MEDIUM**: 8 tests (PromptEvaluation + FK constraints + concurrency)
- **LOW**: 3 tests (Edge case workflows)

### Recommended Implementation Order

**Phase 1: Critical FK Constraints** (7 tests, ~2 hours)
1. SystemConfigurationForeignKeyConstraintsTests
2. DocumentCollectionCascadeDeleteTests
3. DocumentCollectionUserRestrictionTests
4. ChunkedUploadSessionForeignKeyTests
5. RuleSpecCommentForeignKeyTests
6. ChatThreadCollectionForeignKeyTests
7. ShareLinkForeignKeyTests

**Phase 2: AlertRules Handlers** (8 tests, ~3 hours)
8. CreateAlertRuleCommandHandlerTests
9. UpdateAlertRuleCommandHandlerTests
10. DeleteAlertRuleCommandHandlerTests
11. EnableAlertRuleCommandHandlerTests
12. GetAlertRuleByIdQueryHandlerTests
13. GetAllAlertRulesQueryHandlerTests
14. UpdateAlertConfigurationCommandHandlerTests
15. GetAlertConfigurationQueryHandlerTests

**Phase 3: Concurrency & Edge Cases** (7 tests, ~2.5 hours)
16. EvaluatePromptCommandHandlerTests
17. ComparePromptVersionsCommandHandlerTests
18. SystemConfigurationConcurrentUpdateTests
19. ChatThreadConcurrentMessageTests
20. DocumentCollectionToRagPipelineIntegrationTests
21. AlertRuleEvaluationWorkflowTests
22. ~~GameFAQUpvoteRaceConditionTests~~ (REMOVED - Legacy System)

**Total Estimated Effort**: ~7 hours for 21 active tests (1 test removed due to legacy system cleanup)

---

## Test Implementation Patterns

### Foreign Key Constraint Tests (Pattern)
```csharp
[Fact]
public async Task DeleteUser_WithSystemConfigurationReferences_ThrowsException()
{
    // Arrange: Create User → Create SystemConfiguration referencing User
    var userId = Guid.NewGuid().ToString();
    var user = UserEntity.Create(...);
    await _context.Users.AddAsync(user);

    var config = SystemConfigurationEntity.Create(
        key: "test.key",
        value: "test",
        createdByUserId: userId,
        ...
    );
    await _context.SystemConfigurations.AddAsync(config);
    await _context.SaveChangesAsync();

    // Act & Assert: Cannot delete User (FK Restrict)
    _context.Users.Remove(user);
    await Assert.ThrowsAsync<DbUpdateException>(() => _context.SaveChangesAsync());
}
```

### Concurrency Tests (Pattern)
```csharp
[Fact]
public async Task ConcurrentUpdates_SameConfig_TriggersConcurrencyException()
{
    // Arrange: Create SystemConfiguration
    var config = SystemConfigurationEntity.Create(...);
    await _context.SystemConfigurations.AddAsync(config);
    await _context.SaveChangesAsync();

    // Act: Parallel updates to same config
    var tasks = Enumerable.Range(0, 10).Select(async i =>
    {
        var dbConfig = await _context.SystemConfigurations.FindAsync(config.Id);
        dbConfig.UpdateValue($"value_{i}");
        return _context.SaveChangesAsync();
    }).ToArray();

    // Assert: At least 1 DbUpdateConcurrencyException
    var exceptions = await Assert.ThrowsAnyAsync<Exception>(() => Task.WhenAll(tasks));
    Assert.Contains(exceptions, ex => ex is DbUpdateConcurrencyException);
}
```

---

## Coverage Impact Projection

### Before (Current State)
- **Backend Line Coverage**: ~85-88% (post-Issue #2310)
- **Backend Branch Coverage**: ~80-85%
- **Total Backend Tests**: 4,277

### After (22 Tests Added)
- **Backend Line Coverage**: ~88-90% (+2-3%)
- **Backend Branch Coverage**: ~85-88% (+3-5%)
- **Total Backend Tests**: 4,299

### Expected Gap Closure
- **FK Constraint Coverage**: 90% → 98% (+8%)
- **AlertRules Coverage**: 0% → 90% (+90%)
- **Concurrency Coverage**: 30% → 60% (+30%)

---

## Remaining Gaps After 22 Tests

### Low-Priority Areas (Acceptable <90% coverage)
1. **Observability**: Metrics collection, tracing (covered by E2E)
2. **Health Checks**: Liveness/readiness probes (covered by Integration)
3. **Legacy Services**: ConfigurationService, AdminStatsService (Phase out pending)
4. **Development Tools**: Swagger, debugging endpoints (non-production)

### Future Work (Post-Alpha)
1. **Load Testing**: Stress tests for concurrent workflows
2. **Chaos Engineering**: Infrastructure failure scenarios
3. **Performance Regression**: Benchmark test suite
4. **Security Penetration**: Automated security scanning

---

## Recommendation

**Proceed with Phase 1 (7 FK constraint tests)** as highest ROI:
- Validates critical data integrity
- Prevents production data corruption bugs
- Fastest to implement (~2 hours)
- Highest coverage impact per test

**Defer Phase 3 (Edge cases)** if time-constrained:
- Lower production risk
- Can be covered by E2E tests
- Acceptable gap for alpha stage

---

**Analysis Date**: 2026-01-10
**Analyst**: Backend Architect Agent
**Status**: Ready for implementation prioritization
**Next Action**: Create Phase 1 FK constraint tests (7 tests, ~2 hours)
