# 🗓️ Implementation Sequence - Optimal Execution Order for main-dev

**Date**: 2026-01-22
**Context**: Epic #2718 in progress on frontend-dev (Milestone 6: 8 frontend issues)
**Total Open Issues**: 105 (86 existing + 19 newly created)
**Target Branch**: main-dev (backend, infrastructure, testing)
**Parallel Branch**: frontend-dev (Epic #2718 frontend work)

---

## 📊 Executive Summary

### Current State
- **frontend-dev**: Epic #2718 (Game Sharing) - Milestone 6 Frontend (issues #2743-2750) 🔄
- **main-dev**: Ready for new work, must coordinate with frontend-dev to avoid conflicts

### Recommended Sequence (3 Phases)

**Phase 0-1 (Week 0-2)**: CRITICAL Infrastructure & Admin Testing
- **Focus**: Unblock all future work with test infrastructure
- **Parallel with frontend-dev**: ✅ No conflicts (backend/infra vs UI)
- **Issues**: 13 CRITICAL (#2911-2923)

**Phase 2A (Week 3-4)**: Backend for Epic #2718 Support
- **Focus**: Enable Epic #2718 frontend work with backend APIs
- **Parallel with frontend-dev**: ✅ Supporting work (backend enables UI)
- **Issues**: Epic #2718 backend gaps (if any)

**Phase 2B (Week 3-6)**: User Dashboard & Personal Library Backend
- **Focus**: Start Epics 2-3 backend while frontend finishes #2718
- **Parallel with frontend-dev**: ✅ Different epics (no conflicts)
- **Issues**: 12 backend issues (#2854-2856, #2863-2865 + related)

---

## 🎯 Phase 0-1: CRITICAL Foundation (Week 0-2)

### Priority 1: Test Infrastructure (BLOCKING ALL)
**Timeline**: Week 0-1 (parallel with Admin Testing)
**Branch**: main-dev
**Conflicts with frontend-dev**: ❌ None (infrastructure work)

**Issue Sequence**:
1. **#2760** (MSW Infrastructure) ✅ Already exists, may be done
2. **#2919** (Playwright Configuration) - 6-8 hours
3. **#2920** (Testcontainers Optimization) - 6-8 hours
4. **#2921** (CI/CD Test Pipeline) - 10-12 hours
5. **#2922** (Test Infrastructure Docs) - 6-8 hours
6. **#2923** (Visual Regression Clarification) - 2-3 hours

**Total Effort**: 30-38 hours (1.5-2 weeks with 1 DevOps engineer)

**Dependencies**:
- #2919 blocks → All E2E tests
- #2920 blocks → All integration tests
- #2921 blocks → Automated testing in CI
- #2760 blocks → Frontend integration tests

**Deliverables**:
- ✅ Playwright ready for E2E tests
- ✅ Testcontainers optimized for parallel execution
- ✅ CI/CD pipeline running all test types
- ✅ Test infrastructure documentation complete

**Why First**: All testing issues across ALL epics depend on this infrastructure.

---

### Priority 2: Admin Dashboard Testing (CRITICAL)
**Timeline**: Week 1-2 (parallel with Infrastructure)
**Branch**: main-dev
**Conflicts with frontend-dev**: ❌ None (different pages)

**Issue Sequence**:
1. **#2911** (Backend Unit Tests) - 6-8 hours
2. **#2912** (Backend Integration Tests) - 6-8 hours
3. **#2913** (Frontend Component Tests) - 6-8 hours
4. **#2914** (Frontend Integration Tests) - 5-6 hours (depends on #2760)
5. **#2915** (E2E Tests) - 8-10 hours (depends on #2919)
6. **#2916** (Visual Regression) - 4-6 hours (depends on #2919, #2923)
7. **#2917** (Performance Testing) - 5-6 hours
8. **#2918** (Load Testing) - 8-10 hours

**Total Effort**: 48-60 hours (2-3 weeks with 1 QA engineer)

**Dependencies**:
- #2914 depends on → #2760 (MSW)
- #2915, #2916 depend on → #2919 (Playwright)
- #2916 depends on → #2923 (Visual Regression scope)

**Parallel Strategy**:
- **Week 1**: Backend tests (#2911-2912) + Infrastructure (#2919-2920) in parallel
- **Week 2**: Frontend tests (#2913-2916) + CI/CD (#2921) in parallel

**Deliverables**:
- ✅ Admin Dashboard: 90%+ backend coverage
- ✅ Admin Dashboard: 85%+ frontend coverage
- ✅ All E2E journeys passing
- ✅ Visual regression baseline
- ✅ Performance baseline (Lighthouse 90+)
- ✅ Load test baseline established

**Why Second**: Establishes testing patterns for all other pages (User Dashboard, Library, etc.).

---

## 🔄 Phase 2A: Epic #2718 Backend Support (Week 3-4)

### Context: Frontend Work in Progress
**frontend-dev status**: Epic #2718 Milestone 6 (Frontend) - issues #2743-2750 🔄

**Backend Gaps to Fill** (enable frontend work):
Check if these backend APIs are implemented or need implementation on main-dev:

**Potentially Missing Backend Support**:
- **Share Request APIs** (may exist from Milestone 4)
  - GET /api/v1/shared-catalog/share-requests (for Dashboard #2744)
  - GET /api/v1/users/{id}/contributions (for Dashboard #2744)
  - GET /api/v1/users/{id}/badges (for Badge Display #2747)

**Action**: Review Epic #2718 Milestone 4 (API Endpoints) to verify all APIs exist.

**Issue Sequence** (if backend work needed):
1. Review #2733-2738 (API Endpoints Milestone 4) - verify all implemented
2. Create missing API endpoints if needed
3. Test backend APIs to ensure frontend can integrate

**Timeline**: 0-5 hours (if APIs already exist) or 1 week (if gaps found)

**Parallel Work**:
- **main-dev**: Backend support for #2718
- **frontend-dev**: Frontend implementation (#2743-2750)
- **Coordination**: Daily sync to ensure API contracts match

**Why Now**: Enable Epic #2718 frontend work to progress smoothly without backend blockers.

---

## 🚀 Phase 2B: User Dashboard & Personal Library Backend (Week 3-6)

### Epic 2: User Dashboard Backend (3 issues)
**Timeline**: Week 3-4
**Branch**: main-dev
**Conflicts with frontend-dev**: ❌ None (different epic)

**Issue Sequence**:
1. **#2854** (GetUserDashboardQuery) - aggregate data - 8-10 hours
2. **#2855** (GetLibraryQuotaQuery) - user library usage - 5-6 hours
3. **#2856** (GetActiveSessionsQuery) - currently playing - 6-8 hours

**Total Effort**: 19-24 hours (~1 week with 1 backend dev)

**Dependencies**:
- May need UserLibrary BC APIs (check if exist)
- May need GameSession tracking APIs (check if exist)

**Deliverables**:
- ✅ 3 backend queries implemented
- ✅ Unit tests (90%+ coverage)
- ✅ Integration tests (Testcontainers)
- ✅ API endpoints exposed

**Parallel Strategy**:
- **main-dev**: Backend queries (#2854-2856)
- **frontend-dev**: Continue Epic #2718 frontend (#2743-2750)
- **No conflicts**: Different bounded contexts

---

### Epic 3: Personal Library Backend (3 issues)
**Timeline**: Week 4-5 (can start parallel with User Dashboard)
**Branch**: main-dev
**Conflicts with frontend-dev**: ❌ None (different epic)

**Issue Sequence**:
1. **#2863** (GetUserLibraryQuery) - filters, sort, pagination - 10-12 hours
2. **#2864** (UpdateGameNotesCommand) - user notes - 5-6 hours
3. **#2865** (RemoveFromLibraryCommand) - soft delete - 4-5 hours

**Total Effort**: 19-23 hours (~1 week with 1 backend dev)

**Dependencies**:
- #2863 depends on → UserLibrary BC (should exist)
- #2864, #2865 depend on → #2863 (library query logic)

**Deliverables**:
- ✅ 3 backend commands/queries implemented
- ✅ Unit tests (90%+ coverage)
- ✅ Integration tests
- ✅ Soft delete working correctly

**Parallel Strategy**:
- **Backend Dev 1**: User Dashboard backend (#2854-2856) - Week 3-4
- **Backend Dev 2**: Personal Library backend (#2863-2865) - Week 4-5
- **frontend-dev**: Epic #2718 frontend wrapping up

---

## 🌊 Phase 3: Shared Catalog Backend (Week 6-7)

### Epic 4: Shared Catalog - Browse & Add (2 backend issues)
**Timeline**: Week 6
**Branch**: main-dev
**Conflicts with frontend-dev**: ⚠️ Potential overlap with Epic #2718

**Issue Sequence**:
1. **#2871** (GetSharedCatalogQuery) - advanced filters - 10-12 hours
2. **#2872** (AddToLibraryCommand) - add game to library - 6-8 hours

**Total Effort**: 16-20 hours (~1 week with 1 backend dev)

**Dependencies**:
- May overlap with Epic #2718 SharedGameCatalog BC
- Review #2871 vs existing SharedGame queries from #2718
- Coordinate with frontend-dev to avoid conflicts

**Coordination Required**:
- **Check**: Does Epic #2718 already have GetSharedCatalogQuery?
- **Check**: Does #2872 conflict with Epic #2718 share workflow?
- **Action**: Merge or coordinate implementation

**Parallel Strategy**:
- **main-dev**: Shared Catalog backend (#2871-2872)
- **frontend-dev**: May be ready for Epic 2-3 frontend (if #2718 done)
- **Coordination**: Sync on SharedGameCatalog BC changes

---

## 📋 Detailed Sequence for main-dev

### Week 0: Issue Creation & Planning (THIS WEEK)
**Issues Created**: ✅ #2911-2931 (19 issues)
**Actions**:
- ✅ All Phase 0 issues created on GitHub
- [ ] Review and assign issues to Sprint 1
- [ ] Team alignment meeting
- [ ] Setup development environment for infrastructure work

**Parallel**:
- **main-dev**: Planning and issue creation
- **frontend-dev**: Continue Epic #2718 Milestone 6

---

### Week 1-2: CRITICAL Foundation
**Focus**: Infrastructure + Admin Testing (BLOCKING all future work)

**main-dev Sequence**:

**Week 1 - Backend & Infrastructure Setup**:
1. **#2760** - MSW Infrastructure (if not done) - 4-6 hours
2. **#2919** - Playwright Configuration - 6-8 hours
3. **#2920** - Testcontainers Optimization - 6-8 hours
4. **#2911** - Admin Backend Unit Tests - 6-8 hours (parallel with #2919)
5. **#2912** - Admin Backend Integration Tests - 6-8 hours (depends on #2920)

**Week 2 - Frontend Testing & CI/CD**:
6. **#2913** - Admin Frontend Component Tests - 6-8 hours
7. **#2914** - Admin Frontend Integration Tests - 5-6 hours (depends on #2760)
8. **#2915** - Admin E2E Tests - 8-10 hours (depends on #2919)
9. **#2921** - CI/CD Test Pipeline - 10-12 hours (parallel with tests)
10. **#2916** - Admin Visual Regression - 4-6 hours (depends on #2919, #2923)
11. **#2917** - Admin Performance Testing - 5-6 hours
12. **#2918** - Admin Load Testing - 8-10 hours
13. **#2922** - Test Infrastructure Docs - 6-8 hours (parallel)
14. **#2923** - Visual Regression Scope - 2-3 hours (early in week)

**Parallel Execution Strategy**:
```
Week 1:
├─ Stream A (DevOps): #2919 (Playwright) + #2920 (Testcontainers)
├─ Stream B (Backend): #2760 (MSW) + #2911 (Backend Unit) + #2912 (Backend Integration)
└─ Stream C (QA): Setup test environments

Week 2:
├─ Stream A (Frontend): #2913 (Component Tests) + #2914 (Integration Tests)
├─ Stream B (QA): #2915 (E2E) + #2916 (Visual) + #2917 (Perf) + #2918 (Load)
├─ Stream C (DevOps): #2921 (CI/CD) + #2922 (Docs) + #2923 (Visual Scope)
```

**Deliverables**:
- ✅ Test infrastructure complete and documented
- ✅ Admin Dashboard 90%+ test coverage
- ✅ CI/CD pipeline running all test types
- ✅ All testing patterns established for Epics 2-7

**Parallel with frontend-dev**:
- frontend-dev continues Epic #2718 Milestone 6 (#2743-2750)
- No conflicts: main-dev = backend/infra, frontend-dev = UI components

---

### Week 3-4: User Dashboard Backend + Epic #2718 Support

**main-dev Sequence**:

**Week 3 - User Dashboard Queries**:
1. **#2854** - GetUserDashboardQuery - 8-10 hours
2. **#2855** - GetLibraryQuotaQuery - 5-6 hours
3. **#2856** - GetActiveSessionsQuery - 6-8 hours

**Week 3 - Epic #2718 Backend Review** (parallel):
- Review Epic #2718 Milestone 4 APIs (#2733-2738)
- Verify all backend support exists for frontend work
- Create/fix any missing APIs for #2743-2750
- Test backend endpoints for frontend integration

**Week 4 - User Dashboard Testing**:
4. **#2861** - User Dashboard Component Tests - 6-8 hours
5. **#2862** - User Dashboard E2E Tests - 8-10 hours

**Parallel Execution**:
```
Week 3:
├─ Stream A (Backend Dev 1): #2854, #2855, #2856 (User Dashboard)
├─ Stream B (Backend Dev 2): Review/support Epic #2718 backend
└─ frontend-dev: Epic #2718 Milestone 6 finishing (#2743-2750)

Week 4:
├─ Stream A (QA): #2861, #2862 (User Dashboard Testing)
├─ Stream B (Backend): Start Personal Library backend
└─ frontend-dev: Epic #2718 Milestone 7 (Testing #2751-2752) OR start Epic 2 frontend
```

**Deliverables**:
- ✅ User Dashboard backend complete
- ✅ Epic #2718 backend support verified/fixed
- ✅ User Dashboard tests complete
- ✅ Ready for User Dashboard frontend (if #2718 done)

**Coordination Points**:
- Sync with frontend-dev on Epic #2718 backend needs
- Plan Epic 2 frontend handoff (Week 5) if #2718 completes

---

### Week 5-6: Personal Library Backend + Shared Catalog Coordination

**main-dev Sequence**:

**Week 5 - Personal Library Queries & Commands**:
1. **#2863** - GetUserLibraryQuery - 10-12 hours
2. **#2864** - UpdateGameNotesCommand - 5-6 hours
3. **#2865** - RemoveFromLibraryCommand - 4-5 hours

**Week 5 - Testing** (parallel):
4. **#2870** - Personal Library E2E Tests - 8-10 hours

**Week 6 - Shared Catalog Backend**:
5. **#2871** - GetSharedCatalogQuery - 10-12 hours
6. **#2872** - AddToLibraryCommand - 6-8 hours

**Parallel Execution**:
```
Week 5:
├─ Stream A (Backend): #2863, #2864, #2865 (Personal Library)
├─ Stream B (QA): #2870 (Library E2E)
└─ frontend-dev: Epic 2 frontend (#2857-2860) OR Epic 3 frontend (#2866-2869)

Week 6:
├─ Stream A (Backend): #2871, #2872 (Shared Catalog)
├─ Stream B (Backend): Epic #2718 coordination (SharedGameCatalog BC)
└─ frontend-dev: Catalog frontend (#2873-2876) OR continue Epic 2-3
```

**Coordination Required**:
- **SharedGameCatalog BC**: Epic #2718 (share TO catalog) vs Epic 4 (browse catalog)
- Ensure #2871 (GetSharedCatalogQuery) doesn't conflict with Epic #2718 queries
- Coordinate database schema changes if needed
- Merge strategy: Both epics touch same BC, need careful coordination

**Deliverables**:
- ✅ Personal Library backend complete
- ✅ Shared Catalog browse backend complete
- ✅ Epic #2718 + Epic 4 coordination resolved

---

## 🗺️ Dependency Graph

### Infrastructure Dependencies
```
#2760 (MSW) ──┬──> #2914 (Admin Frontend Integration Tests)
              ├──> #2861 (User Dashboard Component Tests)
              └──> All frontend integration tests

#2919 (Playwright) ──┬──> #2915 (Admin E2E)
                     ├──> #2916 (Visual Regression)
                     ├──> #2862 (User Dashboard E2E)
                     └──> All E2E tests

#2920 (Testcontainers) ──> #2912 (Admin Integration Tests)
                         └──> All backend integration tests

#2921 (CI/CD Pipeline) ──> Automated testing for all PRs

#2923 (Visual Regression Scope) ──> #2916 (Admin Visual Regression)
```

### Epic Dependencies
```
Phase 0-1 (Infrastructure + Admin Testing)
    ↓
Phase 2A (Epic #2718 Backend Support)
    ↓
Phase 2B (User Dashboard Backend) ──┬──> Week 5: User Dashboard Frontend
                                     │
Personal Library Backend ────────────┴──> Week 6: Personal Library Frontend
    ↓
Shared Catalog Backend (coordinate with Epic #2718)
    ↓
Profile & Settings Backend
    ↓
User Management Backend
    ↓
Editor Dashboard Backend
```

### Cross-Branch Dependencies
```
main-dev (backend)          frontend-dev (UI)
─────────────────────────────────────────────
Week 1-2: Infrastructure    Epic #2718 M6 (#2743-2750)
         + Admin Testing

Week 3:   User Dashboard    Epic #2718 M7 (#2751-2752)
         Backend (#2854-56)  OR Epic 2 Frontend start
         + Epic #2718
         Backend Support

Week 4:   Personal Library  Epic 2 Frontend (#2857-2860)
         Backend (#2863-65)

Week 5-6: Shared Catalog    Epic 3 Frontend (#2866-2869)
         Backend (#2871-72)  + Epic 4 Frontend (#2873-2876)
         + Coordination      COORDINATION REQUIRED
```

---

## ⚡ Parallelization Strategy

### Week 1-2: Maximum Parallelization (3 Streams)
**main-dev work** (no conflicts with frontend-dev):

**Stream A - DevOps Engineer**:
- #2919 (Playwright Config) - 6-8h
- #2920 (Testcontainers) - 6-8h
- #2921 (CI/CD Pipeline) - 10-12h
- #2923 (Visual Scope) - 2-3h
- **Total**: 24-31 hours

**Stream B - Backend Developer**:
- #2760 (MSW) - 4-6h (if not done)
- #2911 (Backend Unit) - 6-8h
- #2912 (Backend Integration) - 6-8h
- **Total**: 16-22 hours

**Stream C - QA Engineer**:
- #2913 (Frontend Component) - 6-8h
- #2914 (Frontend Integration) - 5-6h
- #2915 (E2E Tests) - 8-10h
- #2916 (Visual Regression) - 4-6h
- #2917 (Performance) - 5-6h
- #2918 (Load Testing) - 8-10h
- #2922 (Docs) - 6-8h
- **Total**: 48-60 hours

**Result**: 88-113 hours of work completed in 2 weeks with 3 people (vs 11-14 weeks sequential)

---

### Week 3-6: Backend Implementation + Frontend Coordination

**Stream A - Backend Dev 1** (main-dev):
- Week 3: User Dashboard backend (#2854-2856)
- Week 4: Personal Library backend (#2863-2865)
- Week 5-6: Shared Catalog backend (#2871-2872)

**Stream B - Backend Dev 2** (main-dev):
- Week 3: Epic #2718 backend support/review
- Week 4-6: Profile & Settings backend (#2878-2880)

**Stream C - Frontend Dev** (frontend-dev):
- Week 3: Epic #2718 Milestone 6 finish (#2743-2750)
- Week 4: Epic #2718 Milestone 7 testing (#2751-2752)
- Week 5: Epic 2 frontend (#2857-2860) OR Epic 3 frontend (#2866-2869)
- Week 6: Shared Catalog frontend (#2873-2876)

**Coordination Needed**: Week 5-6 for SharedGameCatalog BC (Epic #2718 + Epic 4)

---

## 🎯 Critical Path Analysis

### Blocking Issues (Must Complete First)
```
CRITICAL PATH:
#2919 (Playwright) ──> #2921 (CI/CD) ──> All automated testing
     │
     └──> #2915, #2916, #2862, #2877, #2870 (All E2E tests)

#2920 (Testcontainers) ──> All backend integration tests

#2760 (MSW) ──> All frontend integration tests
```

### Parallel Opportunities
```
NO CONFLICTS (Can run in parallel):
├─ main-dev: Backend/Infra work
├─ frontend-dev: Epic #2718 frontend (#2743-2750)
└─ Independent bounded contexts

COORDINATION REQUIRED (Week 6+):
├─ SharedGameCatalog BC: Epic #2718 + Epic 4
└─ Need merge strategy for overlapping work
```

---

## 📊 Issue Priority Matrix for main-dev

### 🔴 CRITICAL (Start Immediately - Week 0-2)
**Blocking all future work**:

| Issue | Title | Effort | Blocks |
|-------|-------|--------|--------|
| #2919 | Playwright Configuration | 6-8h | All E2E tests |
| #2920 | Testcontainers Optimization | 6-8h | All integration tests |
| #2760 | MSW Infrastructure | 4-6h | Frontend integration tests |
| #2921 | CI/CD Test Pipeline | 10-12h | Automated testing |
| #2911 | Admin Backend Unit Tests | 6-8h | Admin testing baseline |
| #2912 | Admin Backend Integration Tests | 6-8h | Admin integration baseline |
| #2913 | Admin Frontend Component Tests | 6-8h | Admin frontend baseline |
| #2914 | Admin Frontend Integration Tests | 5-6h | Admin API integration |
| #2915 | Admin E2E Tests | 8-10h | Admin E2E baseline |
| #2916 | Admin Visual Regression | 4-6h | Visual testing patterns |
| #2917 | Admin Performance Testing | 5-6h | Performance baseline |
| #2918 | Admin Load Testing | 8-10h | Load testing baseline |
| #2922 | Test Infrastructure Docs | 6-8h | Team enablement |
| #2923 | Visual Regression Scope | 2-3h | Visual testing strategy |

**Total**: 88-113 hours | **Timeline**: 2 weeks with 3 people

---

### 🟡 HIGH (Week 3-4)
**Enable Epic 2 & support Epic #2718**:

| Issue | Title | Effort | Epic | Priority Reason |
|-------|-------|--------|------|-----------------|
| #2854 | GetUserDashboardQuery | 8-10h | User Dashboard | Enable frontend work |
| #2855 | GetLibraryQuotaQuery | 5-6h | User Dashboard | Enable quota widget |
| #2856 | GetActiveSessionsQuery | 6-8h | User Dashboard | Enable sessions panel |
| #2861 | User Dashboard Component Tests | 6-8h | User Dashboard | Quality gate |
| #2862 | User Dashboard E2E Tests | 8-10h | User Dashboard | Critical journeys |
| TBD | Epic #2718 Backend Review | 0-5h | Epic #2718 | Support frontend-dev |

**Total**: 33-47 hours | **Timeline**: 1-2 weeks with 2 people

---

### 🟢 MEDIUM (Week 4-6)
**Continue backend implementation**:

| Issue | Title | Effort | Epic | Dependencies |
|-------|-------|--------|------|--------------|
| #2863 | GetUserLibraryQuery | 10-12h | Personal Library | UserLibrary BC |
| #2864 | UpdateGameNotesCommand | 5-6h | Personal Library | #2863 |
| #2865 | RemoveFromLibraryCommand | 4-5h | Personal Library | #2863 |
| #2870 | Personal Library E2E Tests | 8-10h | Personal Library | #2863-2865, #2919 |
| #2871 | GetSharedCatalogQuery | 10-12h | Shared Catalog | Coordinate with #2718 |
| #2872 | AddToLibraryCommand | 6-8h | Shared Catalog | #2871 |
| #2877 | Shared Catalog E2E Tests | 8-10h | Shared Catalog | #2871-2872, #2919 |

**Total**: 51-63 hours | **Timeline**: 2-3 weeks with 2 people

---

### 🔵 LOW (Week 7+)
**Profile, User Management, Editor, Quality**:

**Profile & Settings Backend** (3 issues):
- #2878 (UpdateUserProfileCommand) - 5-6h
- #2879 (ChangePasswordCommand) - 6-8h
- #2880 (Enable/Disable 2FA) - 8-10h
- #2883 (E2E Tests) - 8-10h

**User Management Backend** (3 issues):
- #2884 (GetUsersQuery) - 10-12h
- #2885 (UpdateUserRoleCommand) - 4-5h
- #2886 (Suspend/Unsuspend Commands) - 5-6h
- #2891 (E2E Tests) - 8-10h

**Editor Dashboard Backend** (2 issues):
- #2892 (GetPendingApprovalsQuery) - 8-10h
- #2893 (BulkApprove/Reject Commands) - 6-8h
- #2897 (E2E Tests) - 8-10h

**Component Library** (3 issues):
- #2924 (Storybook Setup) - 8-10h
- #2930 (Extract Components) - 12-15h
- #2931 (Design System Docs) - 6-8h

**Performance & Quality** (3 issues):
- #2927 (Lighthouse CI) - 6-8h
- #2928 (k6 Load Testing) - 10-12h
- #2929 (Accessibility Audit) - 12-15h

**Total**: ~130-160 hours | **Timeline**: 3-4 weeks with 2-3 people

---

## 🚦 Conflict Analysis: main-dev vs frontend-dev

### ✅ NO CONFLICTS - Safe to Parallelize

**Week 1-2** (Infrastructure + Admin Testing):
- main-dev: Backend, infra, testing work
- frontend-dev: Epic #2718 frontend (#2743-2750)
- **Conflict**: ❌ None (different areas)

**Week 3-4** (User Dashboard Backend):
- main-dev: User Dashboard backend (#2854-2856)
- frontend-dev: Epic #2718 frontend wrapping up
- **Conflict**: ❌ None (different epics)

**Week 4-5** (Personal Library Backend):
- main-dev: Personal Library backend (#2863-2865)
- frontend-dev: User Dashboard frontend (#2857-2860) or Library frontend
- **Conflict**: ❌ None (backend before frontend)

---

### ⚠️ COORDINATION REQUIRED

**Week 6** (Shared Catalog Backend):
- main-dev: GetSharedCatalogQuery (#2871), AddToLibraryCommand (#2872)
- frontend-dev: May still be on Epic #2718 or moving to Epic 4
- **Conflict**: ⚠️ **YES - SharedGameCatalog Bounded Context overlap**

**Issues**:
- Epic #2718 has queries/commands for SharedGameCatalog BC (share TO catalog)
- Epic 4 has queries/commands for SharedGameCatalog BC (browse catalog, add TO library)
- Both touch same entity (SharedGame) and DB tables

**Mitigation**:
1. **Review SharedGameCatalog BC**: Identify all queries/commands from Epic #2718
2. **Compare with Epic 4**: Check for overlaps (#2871, #2872)
3. **Merge Strategy**:
   - Option A: Merge Epic #2718 SharedGameCatalog work to main-dev first, then Epic 4 builds on it
   - Option B: Coordinate development, ensure no query/command conflicts
   - Option C: Epic 4 waits until Epic #2718 fully merged
4. **Database Schema**: Ensure migrations don't conflict

**Recommendation**:
- **Week 5**: Merge Epic #2718 backend work (if any pending) to main-dev
- **Week 6**: Start Epic 4 backend building on Epic #2718 foundation
- **Coordination**: Daily sync between main-dev and frontend-dev

---

## 📅 Recommended Timeline for main-dev

### Week 0: Prep & Planning (THIS WEEK)
**Status**: ✅ Issues created (#2911-2931)

**Actions**:
- [ ] Review GAP_ANALYSIS_REPORT.md
- [ ] Review MISSING_ISSUES_TO_CREATE.md
- [ ] Assign Sprint 1 issues (Week 1-2)
- [ ] Team alignment meeting
- [ ] Setup dev environments

**Time**: 8-10 hours (planning)

---

### Week 1: Infrastructure Foundation
**Focus**: Test infrastructure (BLOCKING)

**Sequence**:
1. **#2760** (MSW) - 4-6h [if not done]
2. **#2919** (Playwright) - 6-8h [parallel with #2920]
3. **#2920** (Testcontainers) - 6-8h [parallel with #2919]
4. **#2923** (Visual Scope) - 2-3h
5. **#2911** (Admin Backend Unit) - 6-8h [parallel with infra]
6. **#2912** (Admin Backend Integration) - 6-8h [after #2920]

**Team**:
- DevOps: #2919, #2920, #2923
- Backend Dev: #2760, #2911, #2912
- QA: Test environment setup

**Deliverable**: Infrastructure ready for all testing

---

### Week 2: Admin Testing & CI/CD
**Focus**: Complete Admin testing, setup CI/CD

**Sequence**:
1. **#2913** (Admin Component Tests) - 6-8h
2. **#2914** (Admin Integration Tests) - 5-6h [depends on #2760]
3. **#2915** (Admin E2E) - 8-10h [depends on #2919]
4. **#2916** (Admin Visual) - 4-6h [depends on #2919, #2923]
5. **#2917** (Admin Performance) - 5-6h
6. **#2918** (Admin Load Test) - 8-10h
7. **#2921** (CI/CD Pipeline) - 10-12h [parallel]
8. **#2922** (Infra Docs) - 6-8h [parallel]

**Team**:
- QA: #2913-2918
- DevOps: #2921, #2922
- Backend: Fix any bugs found

**Deliverable**: Admin Dashboard 90%+ coverage, CI/CD operational

---

### Week 3: User Dashboard Backend + Epic #2718 Support
**Focus**: Enable Epic 2 frontend, support Epic #2718

**Sequence**:
1. **Epic #2718 Backend Review** - 0-5h
   - Verify Milestone 4 APIs (#2733-2738) all implemented
   - Test backend endpoints for frontend integration
   - Fix any gaps for frontend work
2. **#2854** (GetUserDashboardQuery) - 8-10h [parallel]
3. **#2855** (GetLibraryQuotaQuery) - 5-6h [parallel]
4. **#2856** (GetActiveSessionsQuery) - 6-8h [parallel]

**Team**:
- Backend Dev 1: #2854-2856
- Backend Dev 2: Epic #2718 support
- QA: Start writing test specs for Epic 2

**Deliverable**: User Dashboard backend ready, Epic #2718 backend verified

**Coordination with frontend-dev**:
- Sync on Epic #2718 API contracts
- Ensure frontend (#2743-2750) has all needed backend support

---

### Week 4: User Dashboard Testing + Personal Library Backend
**Focus**: Complete Epic 2 backend, start Epic 3

**Sequence**:
1. **#2861** (User Dashboard Component Tests) - 6-8h
2. **#2862** (User Dashboard E2E) - 8-10h
3. **#2863** (GetUserLibraryQuery) - 10-12h [parallel]
4. **#2864** (UpdateGameNotesCommand) - 5-6h [after #2863]
5. **#2865** (RemoveFromLibraryCommand) - 4-5h [parallel with #2864]

**Team**:
- QA: #2861, #2862
- Backend Dev: #2863-2865

**Deliverable**: Epic 2 backend complete and tested, Epic 3 backend in progress

---

### Week 5-6: Personal Library + Shared Catalog Backend
**Focus**: Complete Epic 3, start Epic 4 with coordination

**Week 5 Sequence**:
1. Complete #2863-2865 (if not done)
2. **#2870** (Library E2E Tests) - 8-10h

**Week 6 Sequence**:
3. **Review Epic #2718 SharedGameCatalog work** - coordinate with frontend-dev
4. **#2871** (GetSharedCatalogQuery) - 10-12h [coordinate with #2718]
5. **#2872** (AddToLibraryCommand) - 6-8h

**Team**:
- Backend Dev 1: #2871-2872 + Epic #2718 coordination
- Backend Dev 2: Start Profile backend (#2878-2880)
- QA: #2870 + start Catalog test specs

**Deliverable**: Epic 3 complete, Epic 4 backend started with coordination

**CRITICAL**: Coordinate with frontend-dev on SharedGameCatalog BC to avoid conflicts

---

## 🎲 Alternative Sequences (Trade-offs)

### Option A: Infrastructure First (RECOMMENDED) ✅
**Sequence**: Infrastructure (#2919-2923) → Admin Testing (#2911-2918) → Epics 2-7
**Pros**:
- Unblocks all testing work
- Establishes patterns early
- CI/CD ready from start
**Cons**:
- 2 weeks before feature work starts
**Best for**: Quality-focused teams, long-term efficiency

---

### Option B: Feature-First (RISKY) ⚠️
**Sequence**: User Dashboard backend (#2854-2856) → Infrastructure → Admin Testing
**Pros**:
- Faster feature delivery
- Early user value
**Cons**:
- No testing infrastructure = manual testing only
- Rework needed when infrastructure ready
- Technical debt accumulation
**Best for**: Prototype/MVP scenarios, NOT recommended for production

---

### Option C: Hybrid (BALANCED) 🔄
**Sequence**:
- Week 1: Minimal infra (#2919, #2760) + Start Epic 2 backend
- Week 2: Complete infra + Admin testing + Epic 2 frontend
**Pros**:
- Faster feature delivery than Option A
- Better quality than Option B
**Cons**:
- More complex coordination
- Risk of infrastructure gaps
**Best for**: Agile teams with strong coordination

---

## ✅ FINAL RECOMMENDATION: Sequence for main-dev

### Recommended: **Option A (Infrastructure First)** ✅

**Week 0** (THIS WEEK):
- ✅ Create 19 issues (DONE: #2911-2931)
- [ ] Team planning and assignment
- [ ] Environment setup

**Week 1-2** (Phase 0-1 CRITICAL):
```bash
# Parallel execution with 3 people
git checkout main-dev
git pull origin main-dev

# Stream A (DevOps): Infrastructure
# #2919, #2920, #2921, #2923, #2922

# Stream B (Backend): Admin Backend + MSW
# #2760, #2911, #2912

# Stream C (QA): Admin Frontend Testing
# #2913, #2914, #2915, #2916, #2917, #2918
```

**Week 3-4** (Epic 2 + Epic #2718 Support):
```bash
# Backend Dev 1: User Dashboard
# #2854, #2855, #2856

# Backend Dev 2: Epic #2718 Backend Support
# Review #2733-2738, fix gaps, test APIs

# QA: Epic 2 Testing
# #2861, #2862
```

**Week 5-6** (Epic 3-4 Backend):
```bash
# Backend Dev 1: Personal Library
# #2863, #2864, #2865

# Backend Dev 2: Shared Catalog (COORDINATE with Epic #2718)
# #2871, #2872 + Epic #2718 merge coordination

# QA: Testing
# #2870, #2877
```

**Week 7+** (Profile, User Mgmt, Editor, Quality):
```bash
# Backend: Profile + User Management + Editor
# #2878-2880, #2884-2886, #2892-2893

# Frontend: Component Library (can start anytime after Week 2)
# #2924, #2930, #2931

# QA: Performance & Quality
# #2927, #2928, #2929
```

---

## 🔗 Coordination Points with frontend-dev

### Week 1-2: Independent Work ✅
- **main-dev**: Infrastructure + Admin Testing
- **frontend-dev**: Epic #2718 Milestone 6 (#2743-2750)
- **Sync**: None needed (different areas)

### Week 3: Backend Support for Epic #2718 ⚠️
- **main-dev**: Review Epic #2718 backend APIs
- **frontend-dev**: Finishing Epic #2718 Milestone 6
- **Sync**: Daily - ensure API contracts match frontend needs

### Week 4: Epic #2718 Testing + Epic 2 Start 🔄
- **main-dev**: Epic 2 backend ready
- **frontend-dev**: Epic #2718 Milestone 7 (Testing #2751-2752) OR start Epic 2 frontend
- **Sync**: Plan Epic 2 frontend start

### Week 6: SharedGameCatalog BC Coordination 🚨
- **main-dev**: Epic 4 Shared Catalog backend (#2871-2872)
- **frontend-dev**: May be on Epic 4 frontend OR Epic #2718 cleanup
- **Sync**: CRITICAL - coordinate SharedGameCatalog BC changes
- **Action**: Merge Epic #2718 to main-dev before starting Epic 4 backend

---

## 🎯 Next Actions for main-dev

### Immediate (This Week - Week 0)
1. ✅ 19 issues created (#2911-2931)
2. [ ] Create Sprint 1 milestone (Week 1-2)
3. [ ] Assign issues #2911-2923 to Sprint 1
4. [ ] Assign developers to streams (DevOps, Backend, QA)
5. [ ] Setup development environments
6. [ ] Review test infrastructure requirements

### Week 1 Start (Monday)
1. [ ] DevOps: Start #2919 (Playwright Config)
2. [ ] DevOps: Start #2920 (Testcontainers) [parallel]
3. [ ] Backend: Start #2760 (MSW) [if not done]
4. [ ] Backend: Start #2911 (Admin Unit Tests) [parallel]
5. [ ] Daily standup to sync progress

### Week 2 Start (Monday)
1. [ ] QA: Start #2913 (Admin Component Tests)
2. [ ] QA: Start #2915 (Admin E2E) [parallel]
3. [ ] DevOps: Start #2921 (CI/CD Pipeline)
4. [ ] DevOps: Start #2922 (Infra Docs) [parallel]

### Week 2 End (Friday)
1. [ ] Sprint 1 retrospective
2. [ ] Create Sprint 2 milestone (Week 3-4)
3. [ ] Assign Epic 2 backend issues (#2854-2856)
4. [ ] Coordinate with frontend-dev on Epic #2718 handoff
5. [ ] Plan Epic 2 frontend start (if #2718 done)

---

## 📊 Summary Statistics

### Issues by Priority for main-dev
- **🔴 CRITICAL**: 14 issues (Week 1-2)
- **🟡 HIGH**: 11 issues (Week 3-4)
- **🟢 MEDIUM**: 13 issues (Week 5-6)
- **🔵 LOW**: 29 issues (Week 7+)

### Effort Estimates
- **Phase 0-1** (Critical): 88-113 hours (2 weeks, 3 people)
- **Phase 2** (High): 84-110 hours (2-3 weeks, 2-3 people)
- **Phase 3+** (Medium+Low): 180-220 hours (4-5 weeks, 2-3 people)

### Timeline Summary
- **Sequential**: 11-15 weeks
- **Parallel (3 people)**: 7-9 weeks
- **Parallel with frontend-dev**: 6-8 weeks (optimal)

---

**Document Created**: 2026-01-22
**Total Issues Analyzed**: 105 (86 existing + 19 new)
**Recommended Start**: Week 1 - Infrastructure (#2919, #2920, #2760, #2921)
**Critical Coordination**: Week 6 - SharedGameCatalog BC (Epic #2718 + Epic 4)
