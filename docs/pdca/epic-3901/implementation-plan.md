# Epic #3901 - Dashboard Hub Implementation Plan

**PM Agent Strategy**: Sequential /implementa workflow
**Repository**: Frontend (meepleai-monorepo-frontend)
**Date**: 2026-02-09

---

## 📋 Issue Classification

### Backend Issues (BLOCKED - Different Repo)
- ❌ #3907: Backend API Endpoint (backend repo needed)
- ❌ #3908: Activity Service (backend repo needed)
- ❌ #3909: Cache Strategy (backend repo needed)

**Action**: These require backend repository access. Defer or implement in backend repo.

### Frontend Issues (CAN IMPLEMENT NOW)
- ✅ #3910: Dashboard Hub Layout Refactoring + Cleanup (3 SP)
- ✅ #3911: Library Snapshot Component (2 SP)
- ✅ #3912: Enhanced Activity Feed Timeline (3 SP)
- ✅ #3913: Quick Actions Grid Enhancement (2 SP)
- ✅ #3914: Responsive Layout Mobile/Desktop (3 SP)

### Testing Issues (AFTER FRONTEND)
- ⏳ #3915: Integration & E2E Tests (3 SP)

---

## 🚀 Frontend-First Implementation Strategy

### Option 1: Mock Data Development (RECOMMENDED)
**Approach**: Implement frontend with mock data, integrate real API when backend ready

**Execution Order**:
1. `/implementa 3910` - Layout + Cleanup (foundation)
2. `/implementa 3911` - Library Snapshot (with mock data)
3. `/implementa 3912` - Activity Feed (with mock data)
4. `/implementa 3913` - Quick Actions (navigation only)
5. `/implementa 3914` - Responsive Polish
6. `/implementa 3915` - E2E Tests (mock API)

**Advantages**:
- ✅ Can start immediately (no backend dependency)
- ✅ Frontend team unblocked
- ✅ UI/UX validated early
- ✅ Easy to swap mock → real API later

**Time**: ~16-20h total (5 frontend issues)

---

### Option 2: Backend-First (Blocked)
**Approach**: Wait for backend issues #3907-3909

**Blocker**: Requires backend repository access
**Time Lost**: Unknown (depends on backend team)

---

### Option 3: Parallel Development
**Approach**: Frontend with mocks + Backend in parallel

**Frontend**: Implement with mock data
**Backend**: Separate team/session implements #3907-3909
**Integration**: Connect when both ready

---

## 🎯 RECOMMENDED: Frontend-First with /implementa

### Implementation Sequence

#### Step 1: `/implementa 3910` - Layout Refactoring (3-4h)
**Scope**:
- Create new `/dashboard` page structure
- Remove legacy UserDashboard.tsx (1137 lines)
- Grid layout for widgets
- Section containers (Library, Sessions, Activity, Quick Actions)

**Mock Data**:
```typescript
const MOCK_DASHBOARD_DATA = {
  stats: { libraryCount: 12, playedLast30Days: 8, chatCount: 15 },
  librarySnapshot: { topGames: [...], quota: { used: 12, limit: 50 } },
  activeSessions: [...],
  recentActivity: [...],
};
```

**Deliverable**: Clean hub structure ready for components

---

#### Step 2: `/implementa 3911` - Library Snapshot (2h)
**Scope**:
- Top 3 games cards (MeepleCard)
- Quota display
- "View All" link to /library

**Mock**: Use existing game data from library

**Deliverable**: Library widget functional

---

#### Step 3: `/implementa 3912` - Activity Feed (3-4h)
**Scope**:
- Timeline component with date grouping
- Activity icons and descriptions
- Mock activity data

**Deliverable**: Activity feed with Today/Yesterday/Last 7 days

---

#### Step 4: `/implementa 3913` - Quick Actions (2h)
**Scope**:
- Action cards (Start Session, Browse Games, New Chat)
- Navigation links
- Icon grid

**Deliverable**: Quick action navigation

---

#### Step 5: `/implementa 3914` - Responsive (3-4h)
**Scope**:
- Mobile-first grid
- Breakpoint optimization
- Touch-friendly UI

**Deliverable**: Mobile responsive dashboard

---

#### Step 6: `/implementa 3915` - E2E Tests (3h)
**Scope**:
- Playwright tests for dashboard flow
- Mock API responses
- Performance validation

**Deliverable**: >85% coverage, tests passing

---

## 📊 Estimated Timeline (Frontend-First)

**Total Time**: 16-20 hours
**Issues**: 6 frontend/testing issues
**Average**: 2.7-3.3h per issue

**Schedule**:
- Day 1: #3910 Layout (3-4h)
- Day 2: #3911 Library + #3913 Actions (4h)
- Day 3: #3912 Activity Feed (3-4h)
- Day 4: #3914 Responsive (3-4h)
- Day 5: #3915 Tests (3h)

**Total**: 5 days frontend work

---

## 🔄 Backend Integration Plan (When Ready)

### API Connection Points
```typescript
// Replace mock with real API
const { data } = useQuery({
  queryKey: ['dashboard'],
  queryFn: () => api.dashboard.getData(), // Real API
  // vs
  queryFn: () => Promise.resolve(MOCK_DATA), // Mock
});
```

### Files to Update (Later)
- `apps/web/src/lib/api/dashboard.ts` - Add real API client
- `apps/web/src/app/(authenticated)/dashboard/client.tsx` - Swap mock → real
- Remove mock data constants

**Estimated Integration Time**: 1-2h

---

## ✅ Decision: Frontend-First with Mock Data

**Rationale**:
1. ✅ Unblocks frontend team immediately
2. ✅ Validates UX early
3. ✅ Easy to integrate real API later (1-2h swap)
4. ✅ Parallel with backend development possible
5. ✅ Demonstrates value quickly

**Starting Point**: `/implementa 3910` (Dashboard Hub Layout)

---

**Status**: PLAN COMPLETE
**Next**: Execute `/implementa 3910` to start implementation?
