# Parallel Execution Plan - 2 Terminals Strategy

**Generated**: 2026-02-09
**PM Agent Analysis**: 38 open issues across 5 epics
**Strategy**: Maximum parallelization with dependency management
**Time Savings**: 54 hours (44% faster than sequential)

---

## Executive Summary

### Scope
- **38 open issues** across 5 epics
- **Total Effort**: 51 SP + 20h (≈122h sequential, 68h parallel)
- **Time Savings**: 44% reduction (54 hours saved)
- **Completion**: 8.5 days @ 8h/day with 2 terminals

### Priority Distribution
- 🔴 **HIGH Priority**: Epic #3927, #3901, #3905, Issue #3956 (31 issues, 33 SP)
- 🟡 **MEDIUM Priority**: Epic #3906 (4 issues, 8 SP)

### Critical Path
- **Terminal 2 (Frontend)**: 68h bottleneck
- **Terminal 1 (Backend)**: 54h (balanced with Terminal 2)

---

## Terminal 1: Backend Development Flow

**Total**: 27 SP (≈54 hours)

### Phase 1: Dashboard Hub Core Backend (7 SP, 14-18h)
**Epic**: #3901 - Dashboard Hub Core MVP
**Priority**: 🔴 HIGH
**Dependencies**: None (can start immediately)

| Issue | Description | SP | Time | Notes |
|-------|-------------|----|----- |-------|
| #3907 | Dashboard Aggregated API Endpoint | 3 | 6h | Core aggregation logic |
| #3908 | Activity Timeline Aggregation Service | 2 | 4h | Timeline data processing |
| #3909 | Cache Invalidation Strategy | 2 | 4h | Performance optimization |

**Deliverables**:
- `/api/v1/dashboard` aggregated endpoint
- Activity timeline service
- Cache invalidation patterns
- API documentation

**Quality Gates**:
- [ ] API response time < 500ms (cached)
- [ ] Unit test coverage > 90%
- [ ] Integration tests with Testcontainers
- [ ] API documentation in Scalar

---

### Phase 2: Technical Debt Cleanup (5 SP, 10h)
**Issue**: #3956 - Complete Deferred Phase 1+2 Work
**Priority**: 🔴 HIGH
**Dependencies**: Issues #3493, #3494, #3498 (already completed)

**Deferred Work Items**:

#### PostgreSQL Schema (#3493)
- [ ] GDPR cleanup job for conversation_memory (>90 days)
- [ ] Strategy pattern seeding (common game openings)
- [ ] Integration tests with Testcontainers
- [ ] Migration rollback testing
- [ ] Vector embedding query performance tests (<100ms P95)

#### Redis Cache (#3494)
- [ ] Prometheus metrics export implementation
- [ ] Grafana dashboard integration (JSON exists, needs connection)
- [ ] Production cache hit rate measurement (>80% target)

#### Conversation Memory (#3498)
- [ ] Retrieval nDCG measurement (>0.8 target)
- [ ] Latency verification (<200ms P95 target)

**Quality Gates**:
- [ ] All deferred tests passing
- [ ] GDPR cleanup job scheduled and tested
- [ ] Prometheus metrics visible in Grafana
- [ ] Performance targets verified with benchmarks
- [ ] Documentation updated with actual metrics

---

### Phase 3: AI Insights Backend (7 SP, 14-18h)
**Epic**: #3905 - AI Insights & Recommendations
**Priority**: 🔴 HIGH
**Dependencies**: ✅ Epic #3901 Backend complete (Phase 1)

| Issue | Description | SP | Time | Notes |
|-------|-------------|----|----- |-------|
| #3916 | AI Insights Service (RAG Integration) | 3 | 6h | RAG-based recommendations |
| #3917 | Wishlist Management API (CRUD) | 2 | 4h | Wishlist operations |
| #3918 | Catalog Trending Analytics Service | 2 | 4h | Community trending data |

**Deliverables**:
- AI insights service with RAG integration
- Wishlist CRUD API endpoints
- Catalog trending analytics
- Graceful degradation if RAG unavailable

**Quality Gates**:
- [ ] AI insights API < 1s response time (p95)
- [ ] RAG recommendations accuracy > 75%
- [ ] Wishlist CRUD API < 300ms
- [ ] Test coverage > 85%

---

### Phase 4: Gamification Backend (4 SP, 8h)
**Epic**: #3906 - Gamification & Advanced Features
**Priority**: 🟡 MEDIUM
**Dependencies**: ✅ Epic #3901 Backend complete (Phase 1)

| Issue | Description | SP | Time | Notes |
|-------|-------------|----|----- |-------|
| #3922 | Achievement System & Badge Engine | 3 | 6h | Streaks, milestones, badges |
| #3923 | Advanced Timeline Service (Filters + Search) | 1 | 2h | Timeline filtering logic |

**Deliverables**:
- Achievement calculation engine
- Badge system with persistence
- Timeline filters and search API

**Quality Gates**:
- [ ] Achievement calculation < 30s per 10K users
- [ ] Timeline search < 500ms
- [ ] Test coverage > 85%

---

## Terminal 2: Frontend Development Flow

**Total**: 24 SP + 20h (≈68 hours)

### Phase 1: Admin UI Quick Wins (16-24h)
**Epic**: #3927 - Admin UI Completion
**Priority**: 🔴 HIGH (QUICK WINS)
**Dependencies**: None (backend APIs already exist!)

| Issue | Description | Time | Priority | Value/Effort |
|-------|-------------|------|----------|--------------|
| #3928 | Pending Approvals Workflow UI | 3-4h | HIGH | ⚡ Immediate |
| #3931 | Global Sessions Monitoring Dashboard | 3-4h | MEDIUM | ⚡ Immediate |
| #3932 | API Keys Stats & Analytics Dashboard | 3-4h | MEDIUM | ⚡ Immediate |
| #3933 | Workflow Errors Monitoring View | 3-4h | MEDIUM | ⚡ Immediate |
| #3929 | User Activity Timeline View | 3-4h | HIGH | High |
| #3930 | Bulk User Actions Modal | 4-6h | HIGH | High |

**Components to Reuse**:
- `MeepleCard` (entity="game", variant="list")
- `EntityListView` (when Epic #3875 completes)
- `Button`, `Badge`, `Dialog` (shadcn/ui)

**Deliverables**:
- 6 admin UI pages/features
- Real-time badge count updates
- Bulk operation workflows
- Mobile responsive design

**Quality Gates**:
- [ ] All 6 features functional
- [ ] Mobile responsive (<640px tested)
- [ ] Unit tests for approval logic
- [ ] E2E tests for critical workflows

**⚡ Quick Win Strategy**:
Start with #3928, #3931, #3932, #3933 (4 issues, ~12-16h) for immediate value delivery in first 2 days.

---

### Phase 2: Dashboard Hub Frontend (14 SP, 28h)
**Epic**: #3901 - Dashboard Hub Core MVP
**Priority**: 🔴 HIGH
**Dependencies**: ✅ Epic #3901 Backend ready (Terminal 1 Phase 1)

| Issue | Description | SP | Time | Notes |
|-------|-------------|----|----- |-------|
| #3912 | Library Snapshot Component | 2 | 4h | Top 3 games + stats |
| #3913 | Quick Actions Grid Enhancement | 2 | 4h | Quick action buttons |
| #3911 | Enhanced Activity Feed Timeline Component | 3 | 6h | Timeline with real data |
| #3914 | Responsive Layout Mobile/Desktop Optimization | 3 | 6h | Mobile-first responsive |
| #3915 | Testing: Dashboard Hub Integration & E2E | 3 | 6h | E2E test suite |

**Legacy Cleanup** (included in effort):
- [ ] Remove `UserDashboard.tsx` (1137 lines)
- [ ] Remove `UserDashboardCompact.tsx`
- [ ] Remove `dashboard-client.tsx` legacy
- [ ] Remove mock constants and unused sub-components
- [ ] Verify zero `grep -r "UserDashboard"` results

**Quality Gates**:
- [ ] Dashboard load time < 1.5s
- [ ] Lighthouse Performance Score > 90
- [ ] Test coverage > 85%
- [ ] Zero breaking changes to existing components
- [ ] Zero legacy code remaining

---

### Phase 3: AI Insights Frontend (6 SP, 12h)
**Epic**: #3905 - AI Insights & Recommendations
**Priority**: 🔴 HIGH
**Dependencies**: ✅ Epic #3905 Backend ready (Terminal 1 Phase 3)

| Issue | Description | SP | Time | Notes |
|-------|-------------|----|----- |-------|
| #3920 | Wishlist Highlights Component | 2 | 4h | Wishlist widget |
| #3921 | Catalog Trending Widget | 2 | 4h | Trending games display |
| #3919 | AI Insights Widget Component | 2 | 4h | RAG recommendations |

**Deliverables**:
- AI insights widget with recommendations
- Wishlist highlights component
- Catalog trending widget
- Graceful degradation for AI service failures

**Quality Gates**:
- [ ] AI insights engagement > 30%
- [ ] Wishlist additions from recommendations > 20%
- [ ] Trending widget clicks > 15%
- [ ] Graceful fallback if RAG unavailable

---

### Phase 4: Gamification Frontend (4 SP, 8h)
**Epic**: #3906 - Gamification & Advanced Features
**Priority**: 🟡 MEDIUM
**Dependencies**: ✅ Epic #3906 Backend ready (Terminal 1 Phase 4)

| Issue | Description | SP | Time | Notes |
|-------|-------------|----|----- |-------|
| #3924 | Achievements Widget Component | 2 | 4h | Badge display |
| #3925 | Advanced Timeline Filters & Search | 2 | 4h | Filter UI |

**Deliverables**:
- Achievement display widget
- Timeline filter components
- Search interface

**Quality Gates**:
- [ ] Achievement engagement > 40%
- [ ] Timeline filter usage > 20%
- [ ] Test coverage > 85%

---

## Execution Timeline

### Day 1-3 (24h): Quick Wins + Foundation
- **Terminal 1**: Epic #3901 Backend (14-18h)
- **Terminal 2**: Epic #3927 Frontend (16-24h) ⚡
- **Output**: 6 admin UI features + Dashboard API ready

### Day 3-4.5 (10h): Tech Debt
- **Terminal 1**: Issue #3956 Tech Debt (10h)
- **Terminal 2**: Continue Epic #3927 if needed, start Epic #3901 Frontend
- **Output**: Production-ready monitoring + tests

### Day 4.5-8 (28h): Dashboard Hub + AI Backend
- **Terminal 1**: Epic #3905 Backend (14-18h)
- **Terminal 2**: Epic #3901 Frontend (28h)
- **Output**: Dashboard Hub MVP + AI API ready

### Day 8-8.5 (4h): AI Insights Frontend
- **Terminal 1**: Start Epic #3906 Backend (8h)
- **Terminal 2**: Epic #3905 Frontend (12h)
- **Output**: AI Insights features complete

### Day 8.5+ (Final): Gamification
- **Terminal 1**: Complete Epic #3906 Backend
- **Terminal 2**: Epic #3906 Frontend (8h)
- **Output**: Full feature set complete

---

## Success Metrics

### Performance
- **Time Saved**: 54 hours (44% reduction)
- **Sequential**: 122 hours (15.25 days)
- **Parallel**: 68 hours (8.5 days)

### Value Delivery
- **Days 1-3**: 6 admin UI features (immediate value)
- **Days 4-8**: Dashboard Hub MVP + AI Insights
- **Days 8+**: Gamification features

### Quality
- **Test Coverage**: >85% all epics
- **Performance**: All targets met
- **Technical Debt**: Zero deferred work remaining

---

## Risk Management

### Critical Path: Terminal 2 (Frontend)
**Risk**: Frontend work (68h) exceeds backend (54h)
**Mitigation**: Start Epic #3927 immediately (quick wins, no dependencies)

### Dependency Risk: Epic #3905
**Risk**: AI Insights depends on Epic #3901 completion
**Mitigation**: Backend Phase 1 completes before Frontend Phase 2 starts

### Quality Risk: Legacy Code Cleanup
**Risk**: Breaking changes during UserDashboard removal
**Mitigation**: Comprehensive E2E tests (#3915) before cleanup

### Resource Risk: Parallel Execution
**Risk**: Context switching between terminals
**Mitigation**: Clear phase boundaries, independent work streams

---

## Post-Completion Actions

### Documentation
- [ ] Update all epic status on GitHub
- [ ] Close completed issues with commit references
- [ ] Update `docs/07-frontend/dashboard-overview-hub.md`
- [ ] Create success metrics report

### Deployment
- [ ] Merge PRs sequentially (backend first, then frontend)
- [ ] Production deployment with feature flags
- [ ] Monitor performance metrics (Grafana)
- [ ] Gather user feedback on admin UI

### Continuous Improvement
- [ ] Document successful parallel execution patterns
- [ ] Update PM Agent execution templates
- [ ] Capture lessons learned in `docs/planning/lessons-learned.md`

---

## Appendix: Issue Reference

### Epic #3927 Issues
- #3928, #3929, #3930, #3931, #3932, #3933 (6 frontend)

### Epic #3901 Issues
- #3907, #3908, #3909 (3 backend)
- #3911, #3912, #3913, #3914, #3915 (5 frontend)

### Epic #3905 Issues
- #3916, #3917, #3918 (3 backend)
- #3919, #3920, #3921 (3 frontend)

### Epic #3906 Issues
- #3922, #3923 (2 backend)
- #3924, #3925 (2 frontend)

### Technical Debt
- #3956 (1 backend issue, 5 SP)

---

**Total**: 38 issues resolved across 2 terminals in 8.5 days with 44% time savings.
