# Plan: Admin Dashboard Epic Implementation

**Feature**: Admin Dashboard with Block System
**Date**: 2026-02-12
**PM Agent**: Orchestrating implementation across specialists

---

## 🎯 Hypothesis

**What**: Implement modular admin dashboard using MeepleAI native components (StatCard, MeepleCard) with block-based architecture

**Why**:
- Current dashboards use mock data → Need real API integration
- Admins need efficient tools to manage shared games and users
- Modular blocks allow future expansion (analytics, system health)

**Approach**: Parallel development tracks
- **Track A (Backend)**: API endpoints first to validate data models
- **Track B (Frontend)**: Dashboard blocks using existing components
- **Track C (Integration)**: Connect frontend to real API
- **Track D (Testing)**: Quality validation across all layers

---

## 📋 Expected Outcomes (Quantitative)

### Functional Targets
- ✅ 3 working dashboard blocks (Collection, Approvals, Users)
- ✅ 10 API endpoints operational
- ✅ 3 detail pages with full functionality

### Quality Targets
- **Test Coverage**: Frontend 85%+, Backend 90%+
- **Performance**: Dashboard load < 2s, API responses < 500ms
- **Accessibility**: WCAG AA compliance
- **Code Quality**: TypeScript strict mode, zero console errors

### Timeline
- **Total Effort**: 56-72 hours
- **Parallel Development**: 3-4 days with coordination
- **Single Developer**: 7-9 days sequential

---

## 🏗️ Implementation Strategy

### Phase 1: Foundation (Parallel)
**Duration**: 6-8 hours

**Track A - Backend** (backend-architect):
- Issue #6: Admin Stats Endpoint
  - Check if `GetAdminStatsQuery` exists
  - Implement or verify endpoint
  - Add caching (HybridCache, 5min)
  - Unit tests

**Track B - Frontend** (frontend-architect):
- Issue #1: Collection Overview Block
  - Already created with mock data
  - Verify StatCard integration
  - Add React Query setup
  - Loading states

**Deliverable**: Stats block showing real data

---

### Phase 2: Shared Games Management (Parallel)
**Duration**: 6-8 hours

**Track A - Backend** (backend-architect):
- Issue #7: Approval Queue Endpoint
  - Check if `GetApprovalQueueQuery` exists
  - Implement pagination and filters
  - Batch approve/reject commands
  - Unit tests

**Track B - Frontend** (frontend-architect):
- Issue #2: Approval Queue Block
  - Already created with MeepleCard
  - Grid/List view toggle
  - Search and filters
  - Bulk operations UI

**Deliverable**: Working approval queue with actions

---

### Phase 3: User Management (Parallel)
**Duration**: 12-16 hours

**Track A - Backend** (backend-architect):
- Issue #8: User Management Endpoints
  - Verify existing endpoints in `AdminUserEndpoints.cs`
  - Check suspend/unsuspend implementation
  - Add any missing functionality

- Issue #9: User Library Stats
  - Check if `GetUserLibraryStatsQueryHandler` exists
  - Implement stats calculation
  - Add caching

- Issue #10: User Badges
  - Check if `GetUserBadgesQueryHandler` exists
  - Implement badge retrieval
  - Sort by earned date

**Track B - Frontend** (frontend-architect):
- Issue #3: User Management Block
  - Already created with MeepleCard
  - Detail panel (Sheet) integration
  - Library stats display
  - Badge rendering
  - Quick actions

**Deliverable**: Complete user management interface

---

### Phase 4: Detail Pages (Frontend Focus)
**Duration**: 8-10 hours

**Frontend** (frontend-architect):
- Issue #4: Create 3 detail pages
  - `/admin/collection/overview` - Extended stats
  - `/admin/shared-games/approvals` - Full approval list
  - `/admin/users/management` - Full user management
  - Advanced filters and pagination
  - Export functionality (optional)

**Deliverable**: Full navigation flow

---

### Phase 5: API Integration (Critical Path)
**Duration**: 3-4 hours

**Frontend** (frontend-architect):
- Issue #5: Replace mock with real API
  - Update imports in all dashboard components
  - Add error handling
  - Configure API base URL
  - Test with real backend
  - Fix any integration issues

**Deliverable**: Production-ready dashboard

---

### Phase 6: Testing (Parallel)
**Duration**: 16-21 hours

**Track A - Frontend Tests** (quality-engineer):
- Issue #11: Component unit tests
  - Vitest + React Testing Library
  - Mock React Query
  - 85%+ coverage

**Track B - E2E Tests** (quality-engineer):
- Issue #12: Playwright workflows
  - 8 critical workflows
  - Cross-browser testing

**Track C - API Tests** (quality-engineer):
- Issue #13: Integration tests
  - xUnit + Testcontainers
  - 90%+ coverage
  - Authorization and validation tests

**Deliverable**: Comprehensive test coverage

---

## 🎯 Risks & Mitigation

### Risk 1: API Endpoints May Already Exist
**Impact**: Medium (wasted effort reimplementing)
**Mitigation**:
- ✅ **Discovery Phase First**: Check existing endpoints before implementing
- Use Serena MCP to search for existing queries/handlers
- Read `AdminUserEndpoints.cs` and `SharedGameCatalogEndpoints.cs`

### Risk 2: Data Model Gaps
**Impact**: High (blocks frontend integration)
**Mitigation**:
- ✅ **Backend-First Approach**: Validate data models before frontend
- Create DTOs early and share with frontend
- Mock data matches real schema

### Risk 3: Performance Issues with Large Datasets
**Impact**: Medium (UX degradation)
**Mitigation**:
- ✅ **Pagination from Start**: Never load all records
- Implement caching (HybridCache)
- Monitor query performance in tests

### Risk 4: Integration Issues Between Mock and Real API
**Impact**: Medium (delays in Phase 5)
**Mitigation**:
- ✅ **Mock Matches Real Schema**: Ensure mock client has same signatures
- Integration tests catch mismatches early
- Type safety with TypeScript

---

## 🔍 Pre-Implementation Discovery

Before starting implementation, verify existing code:

### Backend Endpoints to Check
```bash
# Check if these already exist:
- GetAdminStatsQuery / GetAdminStatsQueryHandler
- GetApprovalQueueQuery / GetApprovalQueueQueryHandler
- GetUserLibraryStatsQueryHandler
- GetUserBadgesQueryHandler
- SuspendUserCommand / UnsuspendUserCommand

# Routing files to review:
- AdminUserEndpoints.cs (user management endpoints)
- SharedGameCatalogEndpoints.cs (approval queue endpoints)
- AdminResourcesEndpoints.cs or AnalyticsEndpoints.cs (stats)
```

### Frontend Components to Verify
```bash
# Already created (mock data):
- ✅ StatsOverview component
- ✅ SharedGamesBlock component
- ✅ UserManagementBlock component
- ✅ DashboardShell layout
- ✅ admin-client.ts + admin-client-mock.ts

# Need to create:
- Detail pages (3 pages)
- Full table components for detail pages
```

---

## 📊 Execution Plan

### Step 1: Discovery (1-2 hours)
**Owner**: PM Agent
- Search backend codebase for existing endpoints
- Map which APIs need implementation vs verification
- Update issue specifications with findings
- Create precise task breakdown

### Step 2: Backend Track (Parallel) (15-20 hours)
**Owner**: backend-architect
- Implement/verify Issues #6, #7, #8, #9, #10
- CQRS pattern (queries, commands, handlers)
- Unit tests for each endpoint
- Integration tests

### Step 3: Frontend Track (Parallel) (25-30 hours)
**Owner**: frontend-architect
- Finalize Issues #1, #2, #3 (remove mock, add polish)
- Create Issue #4 (detail pages)
- Issue #5 (API integration)
- Component tests

### Step 4: Testing Track (After integration) (16-21 hours)
**Owner**: quality-engineer
- Issue #11: Frontend tests
- Issue #12: E2E tests
- Issue #13: API tests
- Coverage reports

### Step 5: Documentation & Cleanup (2-3 hours)
**Owner**: PM Agent
- Update PDCA docs (check.md, act.md)
- Move patterns to docs/patterns/
- Update CLAUDE.md if needed
- Epic completion report

---

## 🎬 Next Actions

1. **Discovery**: Search codebase for existing endpoints (PM Agent)
2. **Task Creation**: Create TodoWrite with all 13 issues
3. **Delegation**: Assign specialists to parallel tracks
4. **Monitoring**: Track progress and validate quality gates
5. **Documentation**: Maintain PDCA cycle throughout

---

**Ready to begin implementation with parallel tracks!** 🚀
