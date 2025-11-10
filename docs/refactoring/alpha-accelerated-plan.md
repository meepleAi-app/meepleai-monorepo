# Alpha-Optimized DDD Refactoring Plan

**Status**: ALPHA PHASE - Pre-Production
**Timeline**: 10-12 weeks (vs 16 weeks for production)
**Approach**: Aggressive refactoring with direct replacement
**Risk Level**: LOW (no production users to impact)

---

## 🚀 Alpha Phase Advantage

### Why Alpha is PERFECT for Major Refactoring

✅ **No Production Constraints**
- No downtime concerns
- No user impact from breaking changes
- Can rebuild databases from scratch
- No backward compatibility requirements

✅ **Faster Iteration**
- Direct replacement of old code (no dual-run complexity)
- No feature flags for old vs new implementations
- No gradual rollout procedures
- Can break things temporarily and fix quickly

✅ **Simplified Testing**
- Focus on functional correctness
- Performance optimization can wait
- Less emphasis on regression testing
- Integration tests more critical than production monitoring

✅ **Architecture Freedom**
- Can change database schemas aggressively
- Can redesign APIs without versioning
- Can reorganize entire codebase
- Can delete/rewrite large portions of code

---

## ⚡ Accelerated Timeline: 11 Weeks

### Original Plan (Production): 16 weeks
- Dual-run mode implementation: 3 weeks
- Feature flag infrastructure: 1 week
- Gradual rollout procedures: 1 week
- Extensive performance benchmarking: 1 week

### Alpha-Optimized: 11 weeks
- **Eliminated**: Dual-run, feature flags, gradual rollout, extensive benchmarking
- **Savings**: 5 weeks (31% faster)
- **Approach**: Direct replacement, aggressive migration

---

## 📅 Phase Breakdown (11 Weeks)

### Phase 1: Foundation & Shared Kernel (1.5 weeks)

**Week 1**:
- [ ] Create `SharedKernel/` and `BoundedContexts/` directories
- [ ] Implement base domain classes: `Entity<TId>`, `AggregateRoot<TId>`, `ValueObject`
- [ ] Implement interfaces: `IRepository<T>`, `IUnitOfWork`, `ICommand<T>`, `IQuery<T>`
- [ ] Add MediatR for CQRS
- [ ] Create all 7 bounded context folder structures

**Week 2 (First Half)**:
- [ ] Setup DI registration helpers in `Program.cs`
- [ ] Verify build with empty bounded contexts
- [ ] Document SharedKernel usage patterns

**Alpha Optimization**:
- ❌ Skip feature flag infrastructure
- ❌ Skip dual-run mode setup
- ✅ Direct implementation only

---

### Phase 2: Authentication Context (2 weeks)

**Week 2 (Second Half) - Week 3**:
- [ ] Domain layer: `User`, `Session`, `ApiKey`, `OAuthAccount` entities
- [ ] Value objects: `Email`, `PasswordHash`, `TotpSecret`
- [ ] Domain services: `AuthDomainService`, `TotpDomainService`
- [ ] Domain events: `UserLoggedIn`, `TwoFactorEnabled`, etc.
- [ ] Unit tests for domain logic

**Week 4**:
- [ ] Application layer: Commands, queries, DTOs, handlers
- [ ] Infrastructure layer: Repositories, external adapters
- [ ] Integration tests with Testcontainers
- [ ] **Direct replacement**: Delete old `AuthService`, wire up new context
- [ ] Update API endpoints to use new context
- [ ] Functional testing (manual + automated)

**Alpha Optimization**:
- ❌ No dual-run mode (old + new side-by-side)
- ❌ No feature flags (`Features:UseNewAuthContext`)
- ❌ No gradual rollout (0% → 10% → 50% → 100%)
- ✅ Direct replacement: Old service deleted, new context active immediately
- ✅ Rollback via git revert if issues (acceptable in alpha)

---

### Phase 3: KnowledgeBase Context (3 weeks)

**Week 5**:
- [ ] Domain layer: Split `RagService` (995 lines) into 5 domain services:
  - [ ] `EmbeddingDomainService` (~150 lines)
  - [ ] `VectorSearchDomainService` (~200 lines)
  - [ ] `QueryExpansionDomainService` (~150 lines)
  - [ ] `RrfFusionDomainService` (~180 lines)
  - [ ] `QualityTrackingDomainService` (~200 lines)
- [ ] Domain entities: `VectorDocument`, `Embedding`, `SearchResult`
- [ ] Domain events: `EmbeddingGenerated`, `SearchPerformed`, etc.
- [ ] Unit tests for 5 domain services

**Week 6**:
- [ ] Application layer: Commands, queries, DTOs
- [ ] Application services: `RagApplicationService`, `LlmApplicationService`, `StreamingQaApplicationService`
- [ ] Command/query handlers with MediatR
- [ ] Unit tests for application layer

**Week 7**:
- [ ] Infrastructure layer: Repositories, Qdrant adapter, OpenRouter adapter
- [ ] Integration tests (RAG end-to-end flows)
- [ ] **Direct replacement**: Delete old `RagService`, wire up new context
- [ ] Update API endpoints
- [ ] Functional testing + performance spot-check (no extensive benchmarking)

**Alpha Optimization**:
- ❌ No dual-run mode
- ❌ No feature flags
- ❌ No gradual rollout
- ❌ No extensive performance benchmarking (just spot-check)
- ✅ Direct replacement of `RagService` (995 lines → 5 services)
- ✅ Performance baseline capture (for future optimization)

---

### Phase 4: DocumentProcessing & GameManagement (2 weeks)

**Week 8**:
- [ ] **DocumentProcessing Context**:
  - [ ] Domain: `PdfDocument`, `Page`, `TextChunk`, `Table`
  - [ ] Application: `PdfApplicationService`, `ValidationApplicationService`
  - [ ] Infrastructure: Repositories, Docnet/iText7 adapters
  - [ ] Tests: Unit + integration
  - [ ] **Direct replacement**: Delete old PDF services, wire up new context

**Week 9**:
- [ ] **GameManagement Context**:
  - [ ] Domain: `Game`, `RuleSpec`, `RuleSpecVersion`, `Comment`
  - [ ] Application: `GameApplicationService`, `RuleSpecApplicationService`
  - [ ] Infrastructure: Repositories, BGG adapter
  - [ ] Tests: Unit + integration
  - [ ] **Direct replacement**: Delete old game services, wire up new context

**Alpha Optimization**:
- ✅ Parallel migration (2 contexts simultaneously)
- ❌ No dual-run complexity
- ❌ No gradual rollout per context
- ✅ Aggressive timeline (1 week per context)

---

### Phase 5: SystemConfiguration & Administration (1.5 weeks)

**Week 10**:
- [ ] **SystemConfiguration Context**:
  - [ ] Split `ConfigurationService` (814 lines, 14 operations) into 4 services:
    - [ ] `ConfigurationApplicationService` (CRUD, validation, ~200 lines)
    - [ ] `ConfigurationVersioningApplicationService` (History, rollback, ~200 lines)
    - [ ] `ConfigurationBulkApplicationService` (Bulk, import/export, ~220 lines)
    - [ ] `ConfigurationCacheApplicationService` (Cache invalidation, ~180 lines)
  - [ ] Domain: `Configuration`, `FeatureFlag`, `PromptTemplate`
  - [ ] Infrastructure: Repositories, Redis cache adapter
  - [ ] Tests: Unit + integration
  - [ ] **Direct replacement**: Delete old `ConfigurationService`, wire up new context

**Week 11 (First Half)**:
- [ ] **Administration Context**:
  - [ ] Domain: `AdminUser`, `Alert`, `AuditLog`, `Statistic`
  - [ ] Application: `UserManagementApplicationService`, `AlertingApplicationService`, `StatsApplicationService`
  - [ ] Infrastructure: Repositories, alert adapters (Email, Slack, PagerDuty)
  - [ ] Tests: Unit + integration
  - [ ] **Direct replacement**: Delete old admin services, wire up new context

**Alpha Optimization**:
- ✅ Aggressive timeline (1.5 weeks for 2 contexts)
- ❌ No dual-run for configuration (can reset config if needed in alpha)
- ❌ No extensive alert testing (manual testing sufficient)

---

### Phase 6: WorkflowIntegration & Test Reorganization (1 week)

**Week 11 (Second Half)**:
- [ ] **WorkflowIntegration Context**:
  - [ ] Domain: `WorkflowTemplate`, `WorkflowExecution`, `WorkflowError`
  - [ ] Application: `WorkflowApplicationService`, `N8nTemplateApplicationService`
  - [ ] Infrastructure: n8n API adapter
  - [ ] Tests: Unit + integration
  - [ ] **Direct replacement**: Delete old workflow services

**Week 11 (Second Half) - Week 12 (Optional)**:
- [ ] **Test Reorganization** (Can be done incrementally throughout):
  - [ ] Split `PasswordResetServiceTests.cs` (1454 lines) → 4 files (~350 lines each)
  - [ ] Split `RagServiceTests.cs` (1364 lines) → 6 files (~220 lines each)
  - [ ] Split `LlmServiceTests.cs` (1180 lines) → 3 files (~390 lines each)
  - [ ] Reorganize tests by bounded context:
    ```
    tests/
    ├── Authentication.Tests/Domain/
    ├── Authentication.Tests/Application/
    ├── Authentication.Tests/Integration/
    ├── KnowledgeBase.Tests/...
    └── Shared/Fixtures/ (extract shared fixtures)
    ```
  - [ ] Extract shared test infrastructure → `tests/Shared/`
  - [ ] Update CI pipeline (optional: can parallelize tests by context)

**Alpha Optimization**:
- ✅ Test reorganization can be incremental (not blocking)
- ✅ Can defer test reorganization to Week 12 if time-constrained
- ❌ CI pipeline optimization optional (nice-to-have, not critical)

---

## 🎯 Success Criteria for Alpha

### Critical (Must Have)
- ✅ All 7 bounded contexts migrated
- ✅ Old services deleted (no code duplication)
- ✅ Test coverage maintained at 90%+
- ✅ All tests passing (unit + integration)
- ✅ Functional correctness verified (manual + automated testing)
- ✅ Documentation updated

### Important (Should Have)
- ✅ Average service file size: 700 lines → 300 lines
- ✅ Average test file size: 800 lines → 300 lines
- ✅ Performance baseline captured (for future optimization)
- ✅ No critical bugs in alpha testing

### Nice-to-Have (Can Defer)
- ⚠️ CI pipeline parallelization by bounded context
- ⚠️ Extensive performance benchmarking (can do in beta)
- ⚠️ Production monitoring setup (do before beta launch)
- ⚠️ Complete test reorganization (can finish incrementally)

---

## 🛡️ Alpha-Specific Risk Mitigation

### Risk 1: Breaking Alpha Testers' Workflows
**Likelihood**: Medium
**Impact**: Low (alpha testers expect instability)
**Mitigation**:
- Communicate breaking changes in advance (Slack, email)
- Provide migration guide for API changes
- Offer support channel for alpha testers

### Risk 2: Incomplete Migration at Alpha Launch
**Likelihood**: Low (11-week timeline is conservative)
**Impact**: High (delays alpha release)
**Mitigation**:
- Prioritize critical contexts first (Auth, KnowledgeBase, Games)
- Can defer non-critical contexts (Workflows) if needed
- Maintain working state at end of each phase

### Risk 3: Test Coverage Drops Below 90%
**Likelihood**: Low (enforced in CI)
**Impact**: High (loss of confidence in changes)
**Mitigation**:
- Enforce 90% coverage in CI (fail build if below)
- Write tests before migrating code (TDD approach)
- Code review focus on test quality

### Risk 4: Performance Regression
**Likelihood**: Medium
**Impact**: Medium (can optimize in beta)
**Mitigation**:
- Capture performance baseline before migration
- Spot-check performance after each phase
- Defer deep optimization to beta phase

---

## 📊 Comparison: Alpha vs Production Refactoring

| Aspect | Alpha (11 weeks) | Production (16 weeks) |
|--------|------------------|----------------------|
| **Dual-Run Mode** | ❌ Not needed | ✅ Required |
| **Feature Flags** | ❌ Not needed | ✅ Required |
| **Gradual Rollout** | ❌ Direct replacement | ✅ 0%→10%→50%→100% |
| **Performance Benchmarks** | ⚠️ Spot-check only | ✅ Extensive |
| **Production Monitoring** | ❌ Defer to beta | ✅ Required |
| **Breaking Changes** | ✅ Acceptable | ❌ Avoided |
| **Database Resets** | ✅ Acceptable | ❌ Migration required |
| **Rollback Procedure** | Git revert | Feature flag disable |
| **Timeline** | 11 weeks | 16 weeks |
| **Complexity** | Low | High |
| **Risk** | Low (no users) | Medium (production users) |

---

## 🚦 Go/No-Go Decision Points

### After Phase 2 (Authentication) - Week 4
**Review**:
- [ ] Authentication context functional?
- [ ] Tests passing (90%+ coverage)?
- [ ] No critical bugs?
- [ ] Team comfortable with DDD approach?

**Decision**: Continue to Phase 3 or pause for adjustments

### After Phase 3 (KnowledgeBase) - Week 7
**Review**:
- [ ] RAG functionality working correctly?
- [ ] Performance acceptable (spot-check)?
- [ ] Test coverage maintained?
- [ ] RagService successfully split into 5 services?

**Decision**: Continue to Phase 4 or address issues

### After Phase 5 (Config + Admin) - Week 11
**Review**:
- [ ] 5 of 7 bounded contexts complete?
- [ ] All critical functionality working?
- [ ] Timeline on track for alpha launch?
- [ ] Technical debt manageable?

**Decision**: Complete Phase 6 or defer test reorganization

---

## 📋 Alpha Checklist

### Pre-Refactoring (Week 0)
- [ ] Stakeholder approval for 11-week refactoring
- [ ] 1-2 developers allocated
- [ ] GitHub Project setup for tracking
- [ ] Communication plan (Slack, weekly syncs)
- [ ] Pause new feature development

### During Refactoring (Weeks 1-11)
- [ ] Weekly sync meetings (review progress, adjust timeline)
- [ ] Daily commits to feature branches
- [ ] Continuous integration (CI passes on every commit)
- [ ] Test coverage monitored (90%+ enforced)
- [ ] Documentation updated incrementally

### Post-Refactoring (Week 12)
- [ ] All 7 bounded contexts migrated
- [ ] Old services deleted
- [ ] Tests reorganized (or plan for incremental completion)
- [ ] Documentation complete
- [ ] Alpha testing resumed
- [ ] Retrospective meeting (lessons learned)

---

## 🎓 Lessons from Alpha Phase

### Advantages We're Leveraging
1. **No Production Users**: Can break things temporarily
2. **No Backward Compatibility**: Can change APIs aggressively
3. **No Downtime Requirements**: Can pause services for migration
4. **Fast Iteration**: Direct replacement vs dual-run complexity
5. **Database Flexibility**: Can reset/recreate schemas as needed

### Best Practices for Alpha Refactoring
1. **Commit Frequently**: Small commits for easy rollback
2. **Test First**: Write tests before migrating code (TDD)
3. **Document As You Go**: Don't defer documentation to the end
4. **Manual Testing**: Supplement automated tests with manual checks
5. **Communication**: Keep team informed of breaking changes

### Red Flags to Watch For
⚠️ **Test Coverage Dropping**: Stop and write more tests
⚠️ **Timeline Slipping**: Reassess priorities, defer nice-to-haves
⚠️ **Burnout**: 11 weeks is intensive, manage team energy
⚠️ **Scope Creep**: Stay focused on DDD migration, defer new features

---

## 🎉 Expected Outcome

After 11 weeks:
- ✅ Clean DDD architecture with 7 bounded contexts
- ✅ 700-1000 line services → 200-400 line focused modules
- ✅ 1000-1400 line tests → 200-400 line feature suites
- ✅ 90%+ test coverage maintained
- ✅ Solid foundation for beta launch
- ✅ No technical debt from old layered architecture
- ✅ Team confident in codebase structure
- ✅ Ready for beta testers and production launch

**Next Steps After Refactoring**:
1. Resume alpha testing with refactored codebase
2. Performance optimization pass (if needed)
3. Beta launch preparation (monitoring, alerting, ops docs)
4. Continue feature development on solid DDD foundation

---

**Document Owner**: Architecture Lead
**Last Updated**: 2025-11-10
**Status**: Ready for Stakeholder Approval
**Recommendation**: ✅ Proceed with 11-week aggressive refactoring in alpha phase
