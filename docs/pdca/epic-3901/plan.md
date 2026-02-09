# Plan: Epic #3901 - Dashboard Hub Core Implementation

**Created**: 2026-02-09, 01:30
**PM Agent**: Orchestration mode active
**Strategy**: Sequential /implementa workflow

---

## 📋 Epic Overview

**Goal**: Transform post-login dashboard into multi-section hub with collection, sessions, chat AI overview

**Sub-Issues**: 8 issues (3 backend + 5 frontend + 1 testing)
**Story Points**: 21 SP total
**Estimated Time**: ~21-28 hours (at 1h per SP)

---

## 🎯 Implementation Plan with /implementa

### Phase 1: Backend Foundation (7 SP, ~7-9h)

**Order**: Backend-first approach (API must exist before frontend)

#### Issue #3907: Dashboard Aggregated API Endpoint (3 SP, ~3-4h)
- **Command**: `/implementa 3907`
- **Scope**: Create `/api/v1/dashboard` endpoint
- **Delivers**: Aggregated data (library stats, recent sessions, chat threads)
- **Dependencies**: None
- **Backend**: .NET, CQRS, MediatR pattern
- **Priority**: 🔴 CRITICAL (blocks all frontend work)

#### Issue #3908: Activity Timeline Aggregation Service (2 SP, ~2-3h)
- **Command**: `/implementa 3908`
- **Scope**: Service to aggregate user activities across bounded contexts
- **Delivers**: Timeline data with type-safe aggregation
- **Dependencies**: None (can parallel with #3907)
- **Backend**: DDD service, repository pattern

#### Issue #3909: Cache Invalidation Strategy (2 SP, ~2h)
- **Command**: `/implementa 3909`
- **Scope**: Redis/HybridCache strategy for dashboard data
- **Delivers**: <500ms API response with proper invalidation
- **Dependencies**: #3907 complete
- **Backend**: Caching layer, invalidation hooks

---

### Phase 2: Frontend Core (5 SP, ~5-7h)

**Order**: Layout first, then components

#### Issue #3910: Dashboard Hub Layout Refactoring + Cleanup (3 SP, ~3-4h)
- **Command**: `/implementa 3910`
- **Scope**:
  - New hub layout structure
  - Remove UserDashboard.tsx legacy (1137 lines)
  - Remove dashboard-client.tsx legacy
  - Remove mock data
- **Delivers**: Clean modern hub structure
- **Dependencies**: #3907 complete (needs real API)
- **Frontend**: Next.js, React, Zustand
- **Priority**: 🔴 HIGH (foundation for all frontend)

#### Issue #3911: Library Snapshot Component (2 SP, ~2h)
- **Command**: `/implementa 3911`
- **Scope**: Top 3 games + collection stats widget
- **Delivers**: MeepleCard-based library preview
- **Dependencies**: #3910 complete (needs layout slots)
- **Frontend**: React component, MeepleCard reuse

---

### Phase 3: Frontend Features (6 SP, ~6-8h)

**Order**: Activity → Actions → Polish

#### Issue #3912: Enhanced Activity Feed Timeline (3 SP, ~3-4h)
- **Command**: `/implementa 3912`
- **Scope**: Activity timeline with grouping, icons, filters
- **Delivers**: Chronological feed (Today, Yesterday, Last 7 days)
- **Dependencies**: #3908 complete (needs aggregated data)
- **Frontend**: Timeline component, date grouping

#### Issue #3913: Quick Actions Grid Enhancement (2 SP, ~2h)
- **Command**: `/implementa 3913`
- **Scope**: Action cards for quick navigation
- **Delivers**: "Start Session", "Continue Chat", "Browse Games"
- **Dependencies**: #3910 complete
- **Frontend**: Action cards, navigation

#### Issue #3914: Responsive Layout Mobile/Desktop (3 SP, ~3-4h)
- **Command**: `/implementa 3914`
- **Scope**: Mobile-first responsive design
- **Delivers**: < 640px optimized layout
- **Dependencies**: All components complete (#3910-3913)
- **Frontend**: Tailwind, responsive grid

---

### Phase 4: Testing & Validation (3 SP, ~3h)

#### Issue #3925: Integration & E2E Test Suite (3 SP, ~3h)
- **Command**: `/implementa 3925`
- **Scope**:
  - Integration tests for API
  - E2E tests for critical flows
  - Performance validation
- **Delivers**: >85% coverage, Lighthouse >90
- **Dependencies**: All issues complete
- **Testing**: Vitest, Playwright, Lighthouse CI

---

## 📊 Dependency Graph

```
Backend Layer:
  #3907 (API) ─┬─→ #3909 (Cache)
               └─→ #3910 (Frontend Layout)

  #3908 (Activity) ──→ #3912 (Activity Feed)

Frontend Layer:
  #3910 (Layout) ─┬─→ #3911 (Library)
                  ├─→ #3912 (Activity)
                  ├─→ #3913 (Quick Actions)
                  └─→ #3914 (Responsive)

Testing Layer:
  [All Complete] ──→ #3925 (E2E Tests)
```

---

## 🚀 Execution Strategy

### Week 1: Backend Foundation
- Day 1-2: `/implementa 3907` (API Endpoint)
- Day 3: `/implementa 3908` (Activity Service)
- Day 4: `/implementa 3909` (Cache Strategy)
- **Milestone**: Backend complete, API ready

### Week 2: Frontend Core
- Day 1-2: `/implementa 3910` (Layout + Cleanup)
- Day 3: `/implementa 3911` (Library Snapshot)
- **Milestone**: Core structure complete

### Week 3: Frontend Polish
- Day 1-2: `/implementa 3912` (Activity Feed)
- Day 2-3: `/implementa 3913` (Quick Actions)
- Day 4-5: `/implementa 3914` (Responsive)
- **Milestone**: All features complete

### Week 4: Testing & Launch
- Day 1-2: `/implementa 3925` (E2E Tests)
- Day 3: Final validation
- Day 4: Production deploy
- **Milestone**: Epic complete, dashboard live

---

## ⚠️ Risk Mitigation

### Technical Risks
| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| Backend API slow | Medium | High | Cache strategy (#3909), query optimization |
| Legacy cleanup breaks UI | Low | High | Comprehensive tests before removal |
| Mobile performance | Medium | Medium | Code splitting, lazy loading |

### Schedule Risks
| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| Backend delays frontend | Medium | High | Mock API for parallel frontend dev |
| Responsive takes longer | Low | Medium | Use existing Tailwind patterns |
| E2E tests complex | Medium | Low | Start E2E early, iterate |

---

## 📈 Success Metrics Targets

### Performance
- API response: < 500ms (cached)
- Page load: < 1.5s (LCP)
- Lighthouse: > 90
- Mobile FCP: < 1.8s

### Quality
- Test coverage: > 85%
- TypeScript: 0 errors
- ESLint: 0 violations
- Build: Success

### UX
- Click-through: > 40%
- Time on page: > 2 min
- Mobile bounce: < 15%

---

## 🔄 PDCA Integration

**This Document (Plan)**:
- Hypothesis: Sequential /implementa with backend-first
- Expected: 21-28h total, 4 weeks delivery
- Risks: Documented with mitigation

**Next Documents**:
- `do.md`: Implementation log during execution
- `check.md`: Results vs expectations validation
- `act.md`: Lessons learned, pattern formalization

---

## 🎯 Expected Outcomes

**After Phase 1** (Backend):
- ✅ /api/v1/dashboard endpoint responding < 500ms
- ✅ Activity aggregation working
- ✅ Cache strategy implemented
- ✅ Backend tests > 90%

**After Phase 2** (Frontend Core):
- ✅ New hub layout deployed
- ✅ Legacy code completely removed
- ✅ Library snapshot functional
- ✅ Clean component structure

**After Phase 3** (Frontend Features):
- ✅ Activity feed with grouping
- ✅ Quick actions navigation
- ✅ Mobile responsive complete
- ✅ All sections integrated

**After Phase 4** (Testing):
- ✅ E2E tests passing
- ✅ Performance validated
- ✅ Ready for production

---

**Status**: PLAN COMPLETE
**Next**: Check which sub-issues to implement first
**Recommendation**: Start with backend (#3907) if backend repo available, or frontend (#3910) for parallel dev
