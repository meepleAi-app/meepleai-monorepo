# API TODO Tracking Document

**Last Updated:** 2025-11-17
**Purpose:** Central tracking of all TODO comments in the API codebase
**Status:** 10 active TODOs across 6 bounded contexts

---

## Summary by Priority

| Priority | Count | Category |
|----------|-------|----------|
| **HIGH** | 3 | Issue #866 - Agent Migration |
| **MEDIUM** | 4 | Architecture Improvements |
| **LOW** | 3 | Feature Enhancements |

---

## HIGH PRIORITY - Issue #866 (Agent Migration)

### 1. GameEndpoints.cs:141
**Location:** `apps/api/src/Api/Routing/GameEndpoints.cs:141`
**TODO:** `Issue #866: This legacy endpoint was removed. Agents are now in KnowledgeBase context.`
**Context:** Legacy endpoint `/games/{gameId}/agents` was removed during DDD migration
**Status:** ✅ RESOLVED - Documented in cleanup, endpoint removed
**Action:** None - already addressed, kept as historical reference
**Related Issue:** #866

### 2. AgentEntity.cs:15
**Location:** `apps/api/src/Api/Infrastructure/Entities/AgentEntity.cs:15`
**TODO:** `Issue #866: DEPRECATED - For backward compatibility with legacy ChatService only`
**Context:** Deprecated properties (GameId, Kind, Chats) still in AgentEntity for backward compatibility
**Status:** ⏳ BLOCKED - Waiting for Issue #866 completion
**Action:** Remove deprecated properties after Agent aggregate migration is finalized
**Properties to Remove:**
- `GameId` (Obsolete)
- `Kind` (Obsolete - use Type instead)
- `Chats` collection (Obsolete)

**Priority:** HIGH
**Related Issue:** #866

### 3. ChatEntityConfiguration.cs:27
**Location:** `apps/api/src/Api/Infrastructure/EntityConfigurations/ChatEntityConfiguration.cs:27`
**TODO:** `Issue #866: Agent relationship uses deprecated Chats collection for backward compat`
**Context:** EF Core configuration maps deprecated Chats navigation property
**Status:** ⏳ BLOCKED - Waiting for Issue #866 completion
**Action:** Remove deprecated mapping after Agent migration
**Priority:** HIGH
**Related Issue:** #866

---

## MEDIUM PRIORITY - Architecture Improvements

### 4. GetPdfTextQueryHandler.cs:12
**Location:** `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Handlers/GetPdfTextQueryHandler.cs:12`
**TODO:** `Consider adding ExtractedText to domain entity or create read model.`
**Context:** Query handler directly accesses EF entity instead of domain model
**Current Approach:** Direct database access for performance
**Recommendation:** Create read model `PdfExtractedTextReadModel` for CQRS separation
**Priority:** MEDIUM
**Effort:** 2-3 hours (create read model, update handler)
**Benefits:**
- Better CQRS separation
- Improved testability
- Domain purity

### 5. GetPdfProgressQueryHandler.cs:12
**Location:** `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Handlers/GetPdfProgressQueryHandler.cs:12`
**TODO:** `Consider adding ProcessingProgress to domain entity as value object.`
**Context:** Query handler accesses raw entity properties instead of value object
**Current Approach:** Direct property access to QualityScore, Status, etc.
**Recommendation:** Create `ProcessingProgress` value object encapsulating:
- `QualityScore` (double)
- `ProcessingStatus` (enum)
- `ProcessedAt` (DateTime?)
- `ErrorMessage` (string?)

**Priority:** MEDIUM
**Effort:** 3-4 hours (create value object, update entity, migrate data)
**Benefits:**
- Encapsulated domain logic
- Validation in one place
- Better domain modeling

### 6. GetLowQualityResponsesQueryHandler.cs:14
**Location:** `apps/api/src/Api/BoundedContexts/Administration/Application/Handlers/GetLowQualityResponsesQueryHandler.cs:14`
**TODO:** `Create proper repository when AiRequestLog is migrated to bounded context.`
**Context:** Direct DbContext access instead of repository pattern
**Current Approach:** `_dbContext.Set<AiRequestLogEntity>()` direct access
**Recommendation:**
1. Create `AiRequestLogRepository` in Administration/Infrastructure
2. Define `IAiRequestLogRepository` interface
3. Implement `GetLowQualityResponses` query method
4. Update handler to use repository

**Priority:** MEDIUM
**Effort:** 2 hours
**Benefits:**
- Consistent repository pattern
- Testable query logic
- Domain boundary enforcement

### 7. AdminEndpoints.cs:158
**Location:** `apps/api/src/Api/Routing/AdminEndpoints.cs:158`
**TODO:** `Add feedback stats when AgentFeedbackService is migrated to CQRS`
**Context:** Commented-out feedback stats properties awaiting CQRS migration
**Current State:** Feedback stats not exposed in API response
**Recommendation:**
1. Create `GetAgentFeedbackStatsQuery` in KnowledgeBase context
2. Create `GetAgentFeedbackStatsQueryHandler`
3. Update AdminEndpoints.cs to include feedback stats
4. Remove TODO comment

**Priority:** MEDIUM
**Effort:** 4 hours (query + handler + integration)
**Related:** AgentFeedbackService CQRS migration

---

## LOW PRIORITY - Feature Enhancements

### 8. AgentOrchestrationService.cs:118
**Location:** `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Services/AgentOrchestrationService.cs:118`
**TODO:** `Track actual token usage from LLM calls`
**Context:** `agent.RecordInvocation(context.Query, 0)` - hardcoded 0 for token usage
**Current Limitation:** Token usage not tracked for billing/analytics
**Recommendation:**
1. Extract token count from LLM response
2. Update `RecordInvocation` call with actual tokens
3. Expose token metrics in admin dashboard

**Priority:** LOW
**Effort:** 2 hours
**Benefits:**
- Accurate cost tracking
- Usage analytics
- Billing metrics

**Duplicate Location:** `InvokeAgentCommandHandler.cs:154` (same issue)

### 9. InvokeAgentCommandHandler.cs:154
**Location:** `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Handlers/InvokeAgentCommandHandler.cs:154`
**TODO:** `Track actual token usage from LLM calls`
**Context:** Same as #8 - hardcoded 0 token usage
**Status:** DUPLICATE of #8
**Action:** Fix both locations together with #8

### 10. AdminEndpoints.cs:149
**Location:** `apps/api/src/Api/Routing/AdminEndpoints.cs:149`
**TODO:** `Add feedback stats query when AgentFeedbackService is migrated to CQRS`
**Status:** DUPLICATE of #7
**Action:** Same as #7

---

## Implementation Roadmap

### Phase 1: Issue #866 Cleanup (HIGH Priority)
**Timeframe:** When Issue #866 is fully closed
**Tasks:**
1. Remove deprecated `AgentEntity` properties (#2)
2. Remove deprecated `ChatEntityConfiguration` mappings (#3)
3. Update documentation to reflect migration completion (#1)

**Effort:** 1-2 hours
**Dependencies:** Issue #866 completion

### Phase 2: Architecture Improvements (MEDIUM Priority)
**Timeframe:** Next sprint (2-3 days effort)
**Tasks:**
1. Create `PdfExtractedTextReadModel` (#4)
2. Create `ProcessingProgress` value object (#5)
3. Create `AiRequestLogRepository` (#6)
4. Migrate `AgentFeedbackService` to CQRS + expose stats (#7)

**Effort:** 11-13 hours total
**Benefits:** Better domain modeling, CQRS compliance

### Phase 3: Feature Enhancements (LOW Priority)
**Timeframe:** Future sprint (nice-to-have)
**Tasks:**
1. Implement token usage tracking (#8, #9)

**Effort:** 2 hours
**Benefits:** Cost tracking, analytics

---

## TODO Resolution Guidelines

### When to Create GitHub Issue
- Complex changes (>4 hours effort)
- Requires architectural decision
- Affects multiple bounded contexts
- Breaking changes required

### When to Resolve In-Place
- Simple improvements (<2 hours)
- Isolated to single file
- Non-breaking changes
- Documentation updates

### When to Keep TODO
- Blocked by external dependency
- Awaiting architectural decision
- Future enhancement (not critical)
- Marked with specific issue number

---

## Maintenance

**Review Schedule:** Monthly
**Owner:** Engineering Lead
**Next Review:** 2025-12-17

**Process:**
1. Review all TODOs in this document
2. Update status/priority based on project needs
3. Create GitHub issues for actionable items
4. Remove completed TODOs (keep in git history)
5. Add new TODOs discovered during code reviews

---

**Note:** This document is tracked in git. Update whenever TODO comments are added/resolved/modified in the codebase.
