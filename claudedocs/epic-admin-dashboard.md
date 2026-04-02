# Epic: Admin Dashboard with Block System

**Epic ID**: TBD (to be created on GitHub)
**Status**: Planning
**Priority**: High
**Assignee**: TBD
**Created**: 2026-02-12

---

## 📖 Overview

Implement a modular admin dashboard using MeepleAI's native components (MeepleCard, StatCard) with a block-based architecture. Replace current mock dashboards with real API-driven interfaces for managing shared games and users.

**Goal**: Provide admins with a powerful, intuitive interface to manage the MeepleAI community catalog and user base.

---

## 🎯 Objectives

1. **API Integration**: Connect dashboard blocks to real backend endpoints
2. **Component Reuse**: Use existing MeepleCard system for consistency
3. **Modular Design**: Block-based architecture for easy expansion
4. **Navigation**: Link blocks to detailed management pages
5. **User Experience**: Multi-view (grid/list), search, filters, bulk operations

---

## 🏗️ Architecture

### Block System
```
Admin Dashboard (/admin/dashboard)
├── Block 1: Collection Overview (Stats + Link to detail)
├── Block 2: Approval Queue (Shared Games + Link to full list)
├── Block 3: User Management (Users + Link to full management)
└── Future Blocks: Analytics, System Health, Activity Feed
```

### Components Used
- **StatCard**: Metric displays with trend indicators
- **MeepleCard**: Entity cards (game, player) with multi-view
- **Sheet**: Slide-in detail panels
- **shadcn/ui**: Input, Select, Button, Badge, Checkbox

### API Endpoints Required

**Stats**:
- `GET /admin/stats` - Dashboard statistics

**Shared Games**:
- `GET /admin/shared-games/approval-queue` - Pending approvals
- `POST /admin/shared-games/batch-approve` - Batch approve
- `POST /admin/shared-games/batch-reject` - Batch reject

**User Management**:
- `GET /admin/users` - User list with pagination
- `GET /admin/users/{id}` - User detail
- `GET /admin/users/{userId}/library/stats` - Library stats
- `GET /admin/users/{userId}/badges` - Achievement badges
- `POST /admin/users/{userId}/suspend` - Suspend account
- `POST /admin/users/{userId}/unsuspend` - Unsuspend account

---

## 📦 Issue Breakdown

### Frontend Issues

1. **Issue #1**: Collection Overview Block - Stats Display
2. **Issue #2**: Approval Queue Block - Shared Games Management
3. **Issue #3**: User Management Block - User Cards & Detail Panel
4. **Issue #4**: Detail Pages - Collection, Approvals, Users
5. **Issue #5**: API Client Integration - Switch from Mock to Real API

### Backend Issues

6. **Issue #6**: Admin Stats Endpoint - Dashboard Metrics
7. **Issue #7**: Approval Queue Endpoint - Pending Games with Metadata
8. **Issue #8**: User Management Endpoints - CRUD + Actions
9. **Issue #9**: User Library Stats Endpoint - Gaming Activity Metrics
10. **Issue #10**: User Badges Endpoint - Achievement System

### Testing Issues

11. **Issue #11**: Frontend Tests - Dashboard Components
12. **Issue #12**: E2E Tests - Admin Workflows (Approve, Reject, Suspend)
13. **Issue #13**: API Tests - Endpoint Integration Tests

---

## 🎯 Success Criteria

### Functional
- [ ] Dashboard displays real data from API
- [ ] Stats update in real-time (or on refresh)
- [ ] Approval queue shows pending games
- [ ] Bulk approve/reject operations work
- [ ] User management shows all users with filters
- [ ] User detail panel displays complete profile
- [ ] Search and filters work correctly
- [ ] Navigation to detail pages works

### Performance
- [ ] Initial load < 2 seconds
- [ ] React Query caching works (no unnecessary API calls)
- [ ] Optimistic updates for instant feedback
- [ ] Pagination handles 1000+ items efficiently

### UX
- [ ] Responsive design (mobile, tablet, desktop)
- [ ] Grid/List view toggles work smoothly
- [ ] Loading states with skeletons
- [ ] Error handling with toast notifications
- [ ] Keyboard navigation support

### Quality
- [ ] 85%+ test coverage (frontend)
- [ ] 90%+ test coverage (backend)
- [ ] TypeScript strict mode passes
- [ ] No console errors
- [ ] Accessibility audit passes (WCAG AA)

---

## 📊 Dependencies

### Technical Dependencies
- Next.js 16 + React 19
- Tailwind 4 + shadcn/ui
- React Query (data fetching)
- MeepleCard system (existing)
- StatCard component (existing)

### API Dependencies
- Administration bounded context
- SharedGameCatalog bounded context
- User management services
- Analytics services

### Design System
- Glassmorphic design tokens
- Amber/orange accent colors
- Quicksand + Nunito fonts
- MeepleAI component library

---

## 🚀 Implementation Phases

### Phase 1: Foundation (Issues #1, #6)
- Collection Overview block (frontend)
- Admin Stats endpoint (backend)
- **Deliverable**: Working stats display with real data

### Phase 2: Shared Games (Issues #2, #7)
- Approval Queue block (frontend)
- Approval Queue endpoint (backend)
- Batch operations (approve/reject)
- **Deliverable**: Working approval queue with actions

### Phase 3: User Management (Issues #3, #8, #9, #10)
- User Management block (frontend)
- User endpoints (backend)
- User detail panel
- Library stats and badges
- **Deliverable**: Complete user management interface

### Phase 4: Detail Pages (Issue #4)
- Collection overview detail page
- Approvals full list page
- Users management page
- **Deliverable**: Full navigation flow

### Phase 5: API Integration (Issue #5)
- Replace mock client with real API
- Error handling
- Loading states
- Optimistic updates
- **Deliverable**: Production-ready dashboard

### Phase 6: Testing (Issues #11, #12, #13)
- Component tests
- Integration tests
- E2E workflows
- **Deliverable**: 85%+ coverage

---

## 📝 Technical Specifications

### Frontend Stack
```typescript
// Components
StatCard (existing)
MeepleCard (existing)
Sheet, Input, Select, Button, Badge (shadcn/ui)

// State Management
React Query (server state)
Zustand (client state)

// Styling
Tailwind 4 with custom admin-dashboard.css
Glassmorphic design system
```

### Backend Stack
```csharp
// CQRS Pattern
Queries: GetAdminStatsQuery, GetApprovalQueueQuery, etc.
Commands: BatchApproveGamesCommand, SuspendUserCommand, etc.
Handlers: Query/Command handlers with validation

// Data Layer
EF Core with PostgreSQL
Caching with HybridCache (5min for stats)
Pagination with PagedResult<T>
```

### API Response Formats
```typescript
PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

AdminStats {
  totalGames: number;
  publishedGames: number;
  pendingGames: number;
  totalUsers: number;
  activeUsers: number;
  newUsers: number;
  approvalRate: number;
  pendingApprovals: number;
  recentSubmissions: number;
}
```

---

## 🔍 Out of Scope

- Real-time updates via SSE (Phase 2 future)
- Advanced analytics charts (Phase 2 future)
- Virtualized tables for 10,000+ items (Phase 2 future)
- Export functionality (Phase 2 future)
- Custom dashboard layouts (Phase 3 future)

---

## 📚 References

- **Design Doc**: `claudedocs/admin-dashboard-implementation.md`
- **API Endpoints**: `apps/api/src/Api/Routing/SharedGameCatalogEndpoints.cs`, `AdminUserEndpoints.cs`
- **Components**: `apps/web/src/components/ui/data-display/meeple-card.tsx`, `stat-card.tsx`
- **Types**: `apps/web/src/types/admin-dashboard.ts`
- **Related Epic**: #3689 (Enterprise Admin Dashboard)

---

## 🎯 Acceptance Criteria

### Epic Complete When:
- [ ] All 13 issues closed
- [ ] Dashboard displays real data from API
- [ ] All workflows tested (approve, reject, suspend, etc.)
- [ ] Tests pass with 85%+ coverage
- [ ] Production deployment successful
- [ ] Admin users trained and onboarded

---

**Estimated Effort**: 3-5 days (1 developer)
**Risk Level**: Medium (API integration complexity)
**Business Value**: High (critical admin functionality)
