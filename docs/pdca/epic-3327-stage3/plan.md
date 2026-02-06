# Plan: Epic #3327 Stage 3 - Admin UI & Tracking

> Strategic plan for implementing Issues #3673 and #3675

## Hypothesis

**Goal**: Enable admin configuration of PDF limits and expose AI usage tracking to users

**Approach**: Sequential execution with pattern reuse from Stage 1-2
- Issue #3673 first (simpler, pure CQRS)
- Issue #3675 second (complex aggregation)

**Rationale**:
- #3673 builds admin endpoint patterns for #3675
- Sequential reduces context switching
- Both follow established CQRS patterns from Stage 1-2

## Expected Outcomes (Quantitative)

| Metric | Target | Measurement |
|--------|--------|-------------|
| **Time** | 5-7 hours | Track actual vs estimate |
| **Story Points** | 8 SP | #3673 (3) + #3675 (5) |
| **Test Coverage** | ≥90% | Unit tests for all handlers |
| **Code Quality** | 0 critical bugs | Code review with confidence scoring |
| **Pattern Consistency** | 100% | Follow CQRS, DDD, TimeProvider patterns |
| **Epic Progress** | 21/29 SP (72%) | After Stage 3 completion |

## Architecture Design

### Issue #3673: PDF Limits Admin UI

**Components**:
```
SystemConfiguration/
├── Application/
│   ├── Commands/UpdatePdfLimitsCommand.cs
│   ├── Handlers/UpdatePdfLimitsCommandHandler.cs
│   ├── Queries/GetPdfLimitsQuery.cs
│   ├── Handlers/GetPdfLimitsQueryHandler.cs
│   └── Validators/UpdatePdfLimitsCommandValidator.cs
└── Routing/AdminConfigEndpoints.cs
```

**Pattern Reuse**:
- `UpdateRateLimitConfigCommand` as template
- `ConfigurationUpdatedEvent` for cache invalidation
- `AuditService` for change tracking

**API Design**:
```
GET  /api/v1/admin/config/pdf-limits           → All tier limits
PUT  /api/v1/admin/config/pdf-limits/{tier}    → Update specific tier
```

**Validation Rules**:
- Tier ∈ {free, normal, premium}
- MaxPerDay > 0
- MaxPerWeek ≥ MaxPerDay
- MaxFileSizeMb ∈ (0, 100]

---

### Issue #3675: AI Usage Tracking

**Components**:
```
KnowledgeBase/
├── Domain/Services/IAiUsageAggregationService.cs
├── Infrastructure/Services/AiUsageAggregationService.cs
├── Application/
│   ├── Queries/GetAiUsageStatsQuery.cs
│   ├── Queries/GetAiUsageTrendQuery.cs
│   ├── Queries/GetAiUsageByModelQuery.cs
│   └── Handlers/GetAiUsageStats*.cs (3 handlers)
└── Routing/AiUsageEndpoints.cs
```

**Pattern Reuse**:
- Query + Handler pattern from existing codebase
- HybridCache for 5-min TTL
- Admin authorization from #3673

**API Design**:
```
GET /api/v1/users/{userId}/ai-usage              → Current stats
GET /api/v1/users/{userId}/ai-usage/trend        → 30-day trend
GET /api/v1/users/{userId}/ai-usage/by-model     → Model breakdown
GET /api/v1/admin/ai-usage/summary               → Admin: all users
```

**Database Strategy**:
```sql
-- Potential index for performance
CREATE INDEX IF NOT EXISTS idx_ai_request_logs_user_date
ON ai_request_logs(user_id, created_at, model);
```

**Caching Strategy**:
- Cache key: `ai_usage:{userId}:{period}:{groupBy}`
- TTL: 5 minutes
- Invalidation: Optional (can tolerate staleness)

**Cost Estimation**:
```csharp
// Configurable cost per 1K tokens
var modelCosts = new Dictionary<string, decimal>
{
    ["gpt-4"] = 0.03m,
    ["gpt-3.5-turbo"] = 0.002m,
    ["claude-3-opus"] = 0.015m,
    ["claude-3-sonnet"] = 0.003m
};
```

## Risks & Mitigation

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| **Aggregation query performance** | Medium | High | Add index, implement caching, test with realistic data volume |
| **Cache invalidation complexity** | Low | Medium | Use simple TTL, accept 5-min staleness |
| **Admin authorization bypass** | Low | Critical | Reuse AdminRequirement from existing endpoints |
| **Cost estimation inaccuracy** | High | Low | Add disclaimer, make rates configurable |
| **Time estimation error** | Medium | Low | Break into smaller tasks, track actual time |

## Quality Gates

### Per-Issue Gates
- [ ] Unit tests ≥90% coverage
- [ ] Code review score ≥80%
- [ ] No compilation errors/warnings
- [ ] All DoD items complete
- [ ] Pattern consistency verified

### Stage-Level Gates
- [ ] Both issues integrated without conflicts
- [ ] Admin endpoints secured with proper authorization
- [ ] Cache invalidation working correctly
- [ ] API documentation complete (Scalar)
- [ ] No regression in existing functionality

## Implementation Sequence

### Phase 1: Issue #3673 (2-3 hours)
1. **Planning** (15 min)
   - Review UpdateRateLimitConfigCommand pattern
   - Identify SystemConfiguration service integration points

2. **Implementation** (90 min)
   - Create Commands/Queries/Handlers/Validators
   - Implement admin endpoints
   - Add audit trail

3. **Testing** (45 min)
   - Unit tests for handlers and validators
   - Manual API testing via Scalar

4. **Code Review** (30 min)
   - Self-review with code-review agent
   - Fix any issues found

### Phase 2: Issue #3675 (3-4 hours)
1. **Planning** (20 min)
   - Design aggregation queries
   - Plan caching strategy

2. **Implementation** (120 min)
   - Create aggregation service
   - Implement query handlers
   - Add user + admin endpoints
   - Implement cost estimation

3. **Testing** (60 min)
   - Unit tests for aggregation logic
   - Test with realistic data volumes
   - Verify cache behavior

4. **Code Review** (30 min)
   - Performance review
   - Security review (data access control)

### Phase 3: Integration & Finalization (30 min)
1. PR creation for both issues
2. Update Epic #3327 progress
3. Document learnings in PDCA

## Success Criteria

**Issue #3673**:
- ✅ Admin can GET all PDF tier limits
- ✅ Admin can UPDATE specific tier limits
- ✅ Validation prevents invalid configurations
- ✅ Audit trail captures all changes
- ✅ Cache invalidated on updates

**Issue #3675**:
- ✅ Users see their own AI usage stats
- ✅ 30-day trend visualization data available
- ✅ Breakdown by AI model provided
- ✅ Cost estimation with disclaimer
- ✅ Admin can view all-users summary
- ✅ Performance acceptable with caching

**Stage 3**:
- ✅ 8 SP delivered
- ✅ Epic progress: 21/29 SP (72%)
- ✅ Zero regressions
- ✅ All tests green
- ✅ Documentation complete

---

**Plan Created**: 2026-02-06
**Estimated Completion**: Same day (5-7 hours)
**Next Step**: Execute Phase 1 (Issue #3673)
