# Plan: Epic #3327 - User Flow Gaps: Security & Quota Enforcement

## Hypothesis

**What**: Implement 6 remaining security and quota enforcement features (1 already completed) to close critical gaps in user flows.

**Why**:
- Session limits exist but aren't enforced → resource abuse risk
- Email verification missing → account security gaps
- Admin configs require DB access → operational friction
- AI usage invisible to users → cost blindness
- Device management missing → unauthorized access risk

**Approach**: Wave-based parallel orchestration with 3 sprints

### Expected Outcomes (Quantitative)

| Metric | Current | Target | Impact |
|--------|---------|--------|--------|
| **Test Coverage** | N/A (new features) | ≥90% | Quality assurance |
| **Implementation Time** | - | ~4 weeks (3 sprints) | Reasonable velocity |
| **Security** | Gaps present | OWASP compliant | Risk reduction |
| **Resource Efficiency** | Parallel execution | 3 waves | 40% time saving |
| **Epic Progress** | 1/7 done (14%) | 7/7 (100%) | Complete closure |

### Risks & Mitigation

| Risk | Severity | Probability | Mitigation |
|------|----------|-------------|------------|
| **Middleware conflicts** | High | Medium | Isolated dev → integration tests → staged rollout |
| **Migration failures** | Critical | Low | Test on prod-like data + rollback scripts + grace periods |
| **Performance degradation** | Medium | Medium | Benchmark before/after + caching + async operations |
| **Cross-issue pattern drift** | Medium | Medium | Daily sync + shared code reviews + pattern docs |
| **Scope creep** | Low | Low | Strict DoD adherence + no feature additions |

## Implementation Strategy

### Wave 1: Core Security (Sprint 1 - 2 weeks)
**Issues**: #3671, #3672, #3677 | **Total**: 13 SP

**Parallel Streams**:
- **Stream A**: #3671 Session Limits Enforcement (Backend Security Engineer)
  - SessionQuotaMiddleware + auto-termination
  - Pattern: Similar to RateLimitingMiddleware
  - Risk: Must not block legitimate users

- **Stream B**: #3672 Email Verification Flow (Backend Security Engineer)
  - User entity + commands + middleware
  - Pattern: 7-day grace period for existing users
  - Risk: Migration must preserve existing user access

- **Stream C**: #3677 Login Device Management (Backend Security Engineer)
  - UserDevice entity + fingerprinting + max 5 limit
  - Pattern: Auto-revoke oldest device on overflow
  - Risk: Fingerprinting reliability across browsers

**Coordination**: Daily sync on middleware integration patterns

---

### Wave 2: Admin UI & Tracking (Sprint 2 - 1 week)
**Issues**: #3673, #3675 | **Total**: 8 SP

**Parallel Streams**:
- **Stream A**: #3673 PDF Upload Limits Admin UI (Backend + Frontend)
  - Admin endpoints + configuration form
  - Pattern: CRUD on SystemConfiguration
  - Risk: Cache invalidation timing

- **Stream B**: #3675 AI Token Usage Tracking (Backend + Frontend)
  - Aggregation service + dashboard UI
  - Pattern: Time-series aggregation with caching
  - Risk: Query performance on large datasets

**Coordination**: Shared DTO patterns and API conventions

---

### Wave 3: Verification & Documentation (Sprint 3 - 3 days)
**Issues**: #3674 | **Total**: 5 SP

**Single Stream**: #3674 Feature Flag Tier-Based Access (Backend Architect)
- Verify #3073 implementation completeness
- Integration tests for all tiers
- Feature matrix documentation
- Seed data for default flags

**Low Risk**: Verification task, minimal new code

---

## Success Criteria

### Per-Issue DoD
- ✅ Unit tests ≥90% coverage
- ✅ Integration tests for critical paths
- ✅ Code review score ≥80%
- ✅ API docs updated (Scalar)
- ✅ Migration tested (where applicable)
- ✅ No new security vulnerabilities

### Epic-Level DoD
- ✅ All 7 sub-issues closed
- ✅ Middleware stack integrated correctly
- ✅ Cross-issue patterns consistent
- ✅ No regression in existing flows
- ✅ Performance benchmarks met
- ✅ Security audit passed

### Business Impact
- ✅ Session abuse prevention active
- ✅ Email verification rate >95%
- ✅ Admin operational efficiency improved
- ✅ AI usage transparency increased
- ✅ Device management adoption >50%

---

## Architectural Decisions

### Middleware Stack Order (Critical)
```
1. RateLimitingMiddleware (existing) - DOS prevention
2. SessionQuotaMiddleware (#3671) - Resource limits
3. EmailVerificationMiddleware (#3672) - Account security
4. Authentication (existing) - Identity
5. Authorization (existing) - Permissions
```

**Rationale**: Each layer builds on previous, fail-fast for invalid requests

### Grace Period Strategy
- **#3672**: 7 days for existing users to verify email
- **Why**: Avoid disrupting current user base
- **Implementation**: `VerificationGracePeriodEndsAt` field + migration

### Auto-Termination Patterns
- **#3671**: Oldest session when quota exceeded
- **#3677**: Oldest device when 5-device limit reached
- **Rationale**: Least disruptive, user maintains recent activity

### Caching Strategy
- **#3673**: 5-min cache on PDF limits (low change frequency)
- **#3675**: 5-min cache on AI usage aggregations (high query load)
- **Invalidation**: Event-driven via `ConfigurationUpdatedEvent`

---

## Resource Allocation

### Backend Engineers (3x)
- **Engineer 1**: Wave 1A (#3671) → Wave 2A (#3673)
- **Engineer 2**: Wave 1B (#3672) → Wave 3 (#3674)
- **Engineer 3**: Wave 1C (#3677) → Wave 2B (#3675)

### Frontend Engineers (2x)
- **Engineer 1**: Wave 2A UI (#3673) + quota bars (#3671)
- **Engineer 2**: Wave 2B UI (#3675) + device list (#3677)

### QA Engineers (1x)
- **Continuous**: Integration tests across all waves

### PM Agent (Orchestration)
- **Continuous**: Daily progress tracking, blocker resolution, pattern alignment

---

## Dependencies Map

```
Epic #3327 (Parent)
├─ ✅ #3676 (Completed via PR #3679)
├─ 🟡 #3671 (No dependencies)
├─ 🟡 #3672 (No dependencies)
├─ 🟡 #3677 (No dependencies)
├─ 🟡 #3673 (No dependencies)
├─ 🟡 #3675 (No dependencies)
└─ 🟡 #3674 (Verify #3073 implementation)
```

**Key Insight**: Zero blocking dependencies → ideal for parallel execution

---

## Communication Protocol

### Daily Standups (15 min)
- Progress on current wave
- Blockers (technical, resource, design)
- Pattern alignment discussions

### Weekly Reviews (30 min)
- Wave completion review
- Epic progress metrics
- Quality gate checks
- Risk reassessment

### Epic Closure (2 hours)
- Full integration test suite execution
- Security audit report presentation
- Documentation completeness review
- Production deployment plan finalization

---

*Created: 2026-02-05*
*PM Agent: PDCA Planning Phase*
