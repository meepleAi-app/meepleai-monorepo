# Issue #010: Resolve Backend TODO Comments

**Priority:** 🟡 MEDIUM
**Category:** Backend / Technical Debt
**Estimated Effort:** 3-5 days
**Sprint:** SHORT-TERM (1-2 months)

## Summary

35+ TODO/FIXME comments exist across backend codebase. These represent incomplete implementations, planned features, or technical debt that should be tracked and resolved.

## Critical Backend TODOs

### 1. Alerting System (BGAI-042)
**File:** `apps/api/src/Api/Services/WeeklyEvaluationService.cs:303`
```csharp
// TODO BGAI-042: Uncomment when SendAlertCommand is available
// await _mediator.Send(new SendAlertCommand(...));
```
**Action:** Implement `SendAlertCommand` in Administration context
**Priority:** HIGH (affects quality monitoring)

### 2. Analytics/Feedback Integration
**Files:**
- `apps/api/src/Api/Routing/AnalyticsEndpoints.cs:46`
- `apps/api/src/Api/Routing/AnalyticsEndpoints.cs:55`

```csharp
// TODO: Add feedback stats when AgentFeedbackService migrated to CQRS
```
**Action:** Migrate AgentFeedbackService to CQRS pattern
**Priority:** MEDIUM (analytics feature)

### 3. PDF Domain Enhancement
**File:** `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Handlers/GetPdfTextQueryHandler.cs:12`
```csharp
// TODO: Consider adding ExtractedText to domain entity
```
**Action:** Evaluate if ExtractedText should be part of PdfDocument aggregate
**Priority:** LOW (architectural consideration)

### 4. Low Quality Responses Repository
**File:** `apps/api/src/Api/BoundedContexts/Administration/Application/Handlers/GetLowQualityResponsesQueryHandler.cs:14`
```csharp
// TODO: Create proper repository when AiRequestLog is migrated to domain
```
**Action:** Migrate AiRequestLog to domain model, create repository
**Priority:** MEDIUM (data access pattern)

### 5. Integration Test Coverage
**File:** `apps/api/tests/Api.Tests/BoundedContexts/DocumentProcessing/Application/Commands/UploadPdfCommandHandlerTests.cs:22`
```csharp
// TODO: Add comprehensive integration tests for:
// - Invalid PDF files
// - Large file handling
// - Concurrent uploads
// - Storage failures
```
**Action:** Add integration test suite
**Priority:** HIGH (quality assurance)

### 6. Token Usage Tracking
**Files:**
- `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Services/AgentOrchestration/AgentOrchestrationService.cs:118`
- `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Handlers/InvokeAgentCommandHandler.cs:154`

```csharp
agent.RecordInvocation(context.Query, 0); // TODO: Track actual token usage
```
**Action:** Implement token counting for AI invocations
**Priority:** MEDIUM (cost tracking)

## Tasks

### Phase 1: Categorization (1 day)
- [ ] Create comprehensive list of all TODO/FIXME comments
- [ ] Categorize by bounded context:
  - Authentication
  - GameManagement
  - KnowledgeBase
  - DocumentProcessing
  - WorkflowIntegration
  - SystemConfiguration
  - Administration
- [ ] Assign priority (Critical/High/Medium/Low)
- [ ] Estimate effort for each

### Phase 2: Create GitHub Issues (0.5 days)
- [ ] Create tracking issues for each category
- [ ] Link to specific code locations
- [ ] Add acceptance criteria
- [ ] Assign to appropriate milestone

### Phase 3: Implementation (varies by TODO)
- [ ] **Week 1-2:** Critical TODOs (Alerting, Tests)
- [ ] **Week 3-4:** High Priority (Analytics, Token Tracking)
- [ ] **Month 2:** Medium Priority (Repository migrations)
- [ ] **Month 3:** Low Priority (Architectural improvements)

### Phase 4: Cleanup
- [ ] Remove completed TODO comments
- [ ] Update related documentation
- [ ] Verify all implementations

## Implementation Plan by TODO

### SendAlertCommand (BGAI-042)
```
1. Create SendAlertCommand in Administration/Application/Commands/
2. Create SendAlertCommandHandler
3. Implement email/Slack/PagerDuty integration (already exists in AlertingService)
4. Add validation and tests
5. Uncomment usage in WeeklyEvaluationService
6. Test end-to-end alert flow
```

### AgentFeedbackService Migration
```
1. Create FeedbackAggregate in KnowledgeBase/Domain/
2. Create GetFeedbackStatsQuery
3. Create GetFeedbackStatsQueryHandler
4. Migrate repository to Infrastructure/
5. Update AnalyticsEndpoints to use MediatR
6. Remove legacy service
7. Test analytics endpoints
```

### Integration Test Suite
```
1. Create test fixtures for invalid PDFs
2. Implement large file test (>50MB)
3. Add concurrent upload test with Testcontainers
4. Mock storage failures
5. Verify error handling and rollback
6. Achieve >90% coverage
```

### Token Usage Tracking
```
1. Add TokenCount property to AgentInvocation value object
2. Integrate with OpenRouter token counting API
3. Update RecordInvocation to accept token count
4. Add metrics/logging for token usage
5. Create cost tracking query
6. Dashboard integration
```

## Success Criteria

- [ ] All critical TODOs resolved
- [ ] High priority TODOs implemented or tracked in GitHub
- [ ] Medium/Low TODOs documented with timeline
- [ ] No TODO comments without corresponding GitHub issue
- [ ] Test coverage maintained at 90%+
- [ ] All new features documented

## References

- Backend TODO locations: See legacy code analysis Section 1.B
- CQRS pattern: `docs/01-architecture/patterns/cqrs.md`
- Bounded contexts: `apps/api/src/Api/BoundedContexts/`
- Testing guide: `docs/02-development/testing/test-writing-guide.md`

## Related Issues

- BGAI-042: Weekly Evaluation Alerting
- Issue #011: Implement Missing Frontend APIs
- Issue #012: Remove Backward Compatibility Layers

## Notes

**Search command to find all TODOs:**
```bash
grep -r "TODO" apps/api/src --include="*.cs" -n
grep -r "FIXME" apps/api/src --include="*.cs" -n
```

**Estimated total effort:** 15-20 developer days spread over 2-3 months
