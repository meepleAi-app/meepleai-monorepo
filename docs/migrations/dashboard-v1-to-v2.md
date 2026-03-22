# Dashboard V1 → V2 Migration Plan

**Issue**: #4577
**Epic**: #4575 Gaming Hub Dashboard
**Date**: 2026-02-17

---

## 🎯 Migration Overview

Sostituire la dashboard attuale (`/dashboard`) con Gaming Hub Dashboard (`/`) mantenendo le funzionalità core e deprecando quelle non necessarie.

### Current (V1)
- **Route**: `/dashboard`
- **Component**: `DashboardHub` (Epic #3901, Issue #3975)
- **Scope**: Multi-section overview hub (10+ widgets)
- **API**: `/api/v1/dashboard` (aggregated endpoint)

### Target (V2)
- **Route**: `/` (authenticated homepage)
- **Component**: `GamingHubClient` (Epic #4575)
- **Scope**: Focus su gaming activity & collection
- **API**: 3 dedicated endpoints (stats, sessions, games)

---

## 📦 Component Inventory

### Components to REMOVE (`components/dashboard/`)

| Component | Purpose | Replacement | Action |
|-----------|---------|-------------|--------|
| `DashboardHub.tsx` | Main container | `GamingHubClient` | ❌ Delete |
| `HeroStats.tsx` | Stats overview | `QuickStats` (v2) | ✅ Rewrite |
| `ActiveSessionsWidget.tsx` | Current sessions | `RecentSessions` (v2) | ✅ Rewrite |
| `LibrarySnapshot.tsx` | Library quick view | `GameCollectionGrid` (v2) | ✅ Rewrite |
| `ActivityFeedWithFilters.tsx` | Activity timeline | ❌ Out of scope | ❌ Remove |
| `WishlistHighlights.tsx` | Wishlist preview | ❌ Future feature | ❌ Remove |
| `CatalogTrending.tsx` | Trending games | ❌ Future feature | ❌ Remove |
| `AchievementsWidget.tsx` | User achievements | ❌ Future feature | ❌ Remove |
| `ChatHistorySection.tsx` | Recent chats | ❌ Out of scope | ❌ Remove |
| `QuickActionsGrid.tsx` | Quick actions | ⚠️ Maybe migrate | ⏳ TBD |

**Total**: 10 components → 3 core components rewritten

### Sub-Components (`components/dashboard/`)

These will be deleted as they're only used by removed widgets:
- `ActivityFeed.tsx`, `ActivityFeedFilters.tsx`, `ActivityItem.tsx`
- `achievements-widget.tsx`, `advanced-activity-timeline.tsx`
- `ai-insights-widget.tsx`, `catalog-trending-widget.tsx`
- `ConnectionStatus.tsx`, `Dashboard.tsx`
- Plus ~15 more sub-components

---

## 🔌 API Endpoints Audit

### Current Endpoint (V1)

**`GET /api/v1/dashboard`** - Aggregated dashboard data

**Handler**: Likely `GetDashboardQueryHandler` (needs verification)

**Response Structure**:
```typescript
interface DashboardDto {
  heroStats: {...};
  activeSessions: [...];
  librarySnapshot: {...};
  activityFeed: [...];
  wishlistHighlights: [...];
  catalogTrending: [...];
  achievements: [...];
  chatHistory: [...];
}
```

**Action**: ❌ **Deprecate** (not delete - might be used elsewhere)

### New Endpoints (V2)

Will be created in Phase 1:
- ✅ `GET /api/v1/users/me/stats` - User statistics
- ✅ `GET /api/v1/sessions/recent` - Recent sessions
- ✅ `GET /api/v1/users/me/games` - User games collection

---

## 🎣 Hooks & State Management

### Hooks to REMOVE

| Hook | Purpose | Replacement | Location |
|------|---------|-------------|----------|
| `useDashboardData` | Fetch dashboard data | `useDashboardStore` (Zustand) | `hooks/useDashboardData.ts` |
| `useDashboardAnalytics` | Track events | ⚠️ Keep? | `hooks/useDashboardAnalytics.ts` |
| `useWishlistHighlights` | Wishlist data | ❌ Remove | `hooks/useWishlistHighlights.ts` |
| `useCatalogTrending` | Trending data | ❌ Remove | `hooks/useCatalogTrending.ts` |
| `useRecentAchievements` | Achievement data | ❌ Remove | `hooks/useRecentAchievements.ts` |
| `useActivityTimeline` | Activity feed | ❌ Remove | `hooks/useActivityTimeline.ts` |
| `useDashboardStream` | SSE streaming | ❌ Remove | `lib/hooks/useDashboardStream.ts` |

### New State (V2)

**Zustand Store**: `lib/stores/dashboard-store.ts`
- Replaces all dashboard hooks with centralized state
- Actions: `fetchStats()`, `fetchSessions()`, `fetchGames()`, `updateFilters()`

---

## 🔗 Navigation Links Update

### Files with `/dashboard` References (74 total)

**High Priority** (Direct navigation):
- `config/navigation.ts` - Main navigation config
- `components/layout/Navbar/NavItems.tsx` - Navbar links
- `components/layout/Navbar/Navbar.tsx` - Navigation component
- `components/layout/app-shell.tsx` - App shell layout
- `components/auth/AuthModal.tsx` - Post-login redirect
- `app/(auth)/welcome/page.tsx` - Welcome redirect
- `app/(authenticated)/layout.tsx` - Authenticated layout

**Medium Priority** (Links/breadcrumbs):
- `components/admin/__tests__/AdminBreadcrumbs.test.tsx`
- `components/layouts/PublicFooter.tsx`
- Various test files referencing dashboard

**Low Priority** (Documentation/comments):
- Test files with dashboard URLs
- Storybook examples
- Type definitions

### Update Strategy

1. **Replace** `/dashboard` → `/` in navigation config
2. **Add redirect** in `app/(authenticated)/dashboard/page.tsx`
3. **Update tests** to use new homepage route
4. **Verify** no broken links remain

---

## 📚 Adapters & Utils

### Keep & Migrate

| File | Purpose | Action |
|------|---------|--------|
| `lib/adapters/dashboardAdapter.ts` | Data transformation | ⚠️ Review - might be needed |
| `lib/animations.ts` | Animation utils | ✅ Keep (used elsewhere) |

### Remove

| File | Purpose | Action |
|------|---------|--------|
| `lib/api/dashboard.ts` | V1 API client | ❌ Remove (replace with `dashboard-client.ts`) |
| `lib/api/clients/dashboardClient.ts` | V1 typed client | ❌ Remove (rewrite in Phase 2) |
| `lib/api/insights.ts` | AI insights | ❌ Remove (out of scope) |
| `lib/api/achievements.ts` | Achievements | ❌ Remove (future feature) |

---

## 🧪 Tests Migration

### Tests to MIGRATE/REWRITE

| Test File | Component | Action |
|-----------|-----------|--------|
| `components/dashboard/__tests__/DashboardHub.test.tsx` | DashboardHub | ✅ Rewrite for GamingHubClient |
| `components/dashboard/__tests__/ActivityFeedWithFilters.test.tsx` | ActivityFeed | ❌ Remove |
| `lib/hooks/__tests__/useDashboardStream.test.ts` | Dashboard hook | ❌ Remove |
| `app/(authenticated)/dashboard/__tests__/*` | Dashboard pages | ✅ Migrate to `/` page tests |

### New Tests (V2)

Will be created in Phase 3:
- `components/dashboard/__tests__/*.test.tsx` - All v2 components
- `lib/api/__tests__/dashboard-client.test.ts` - API client
- `lib/stores/__tests__/dashboard-store.test.ts` - Zustand store
- `apps/web/__tests__/e2e/gaming-hub.spec.ts` - E2E tests

---

## 📊 Data Flow Comparison

### V1 Flow
```
User → /dashboard
  → DashboardHub component
    → useDashboardData hook
      → GET /api/v1/dashboard (aggregated)
        → Returns 10+ widget data sections
          → Renders 10+ widgets (lazy loaded)
```

### V2 Flow
```
User → / (authenticated)
  → GamingHubClient component
    → useDashboardStore (Zustand)
      → Parallel fetches:
        - GET /api/v1/users/me/stats
        - GET /api/v1/sessions/recent
        - GET /api/v1/users/me/games
      → Renders 4 core sections:
        - QuickStats (4 cards)
        - RecentSessions (list)
        - GameCollectionGrid (MeepleCard grid)
        - Empty state (upcoming games)
```

**Key Differences**:
- V1: 1 aggregated endpoint → 10+ widgets
- V2: 3 focused endpoints → 4 core sections
- V1: Complex multi-section layout
- V2: Gaming-focused simplified layout

---

## 🚨 Breaking Changes

### For Users
- ✅ **No breaking changes**: `/dashboard` will redirect to `/`
- ✅ Core functionality preserved (stats, sessions, collection)
- ⚠️ **Lost features**: Achievements, wishlist highlights, catalog trending, activity feed
  - These are **future features** (out of current scope)

### For Developers
- ❌ **Breaking**: `useDashboardData()` hook removed
  - Replace with: `useDashboardStore()`
- ❌ **Breaking**: `/api/v1/dashboard` endpoint deprecated
  - Replace with: 3 new focused endpoints
- ❌ **Breaking**: `DashboardHub` component removed
  - Replace with: `GamingHubClient`

---

## ✅ Migration Checklist

### Phase 0: Audit (Issue #4577)
- [x] Document component inventory
- [x] Identify API endpoints
- [x] Map hooks and state
- [x] List navigation references (74 files)
- [x] Create migration plan

### Phase 4: Execute Migration (Issue #4588)
- [ ] Remove `components/dashboard/` directory
- [ ] Remove `app/(authenticated)/dashboard/` (except redirect page)
- [ ] Create redirect: `/dashboard` → `/`
- [ ] Update navigation config
- [ ] Remove deprecated hooks
- [ ] Remove deprecated API clients
- [ ] Update all 74 navigation references
- [ ] Verify no broken links
- [ ] Run full test suite

---

## 🔄 Rollback Plan

If V2 has critical issues:

1. **Revert PR** that removed old dashboard
2. **Restore route**: `/dashboard` points to `DashboardHub`
3. **Update navigation**: Links back to `/dashboard`
4. **Document issues**: Create tickets for V2 fixes

**Assets preserved for rollback**:
- Git history has all V1 components
- API endpoint `/api/v1/dashboard` remains functional
- Database schema unchanged

---

## 📝 Reusable Components

### Maybe Keep (Review Needed)

| Component | Purpose | Used Elsewhere? | Decision |
|-----------|---------|-----------------|----------|
| `QuickActionsGrid.tsx` | Action buttons | ❓ Unknown | ⏳ Audit needed |
| `ConnectionStatus.tsx` | SSE connection | ❓ Unknown | ⏳ Audit needed |
| Error boundaries | Error handling | ✅ Pattern reuse | ✅ Keep pattern |
| Skeleton components | Loading states | ✅ Pattern reuse | ✅ Keep pattern |

### Animation Patterns to Keep

- Framer Motion stagger animations
- Reduced motion support
- Lazy loading (Intersection Observer)
- Touch-friendly targets (44px min)

These patterns should be applied in V2 components.

---

## 🎯 Next Steps

1. ✅ **Complete this audit** (Issue #4577)
2. ⏳ **Implement Phase 1** (Backend APIs) - Issues #4578, #4579, #4580
3. ⏳ **Implement Phase 2** (Frontend) - Issues #4581-#4584
4. ⏳ **Testing** (Phase 3) - Issues #4585-#4587
5. ⏳ **Execute migration** (Phase 4) - Issues #4588-#4589

---

## 📊 Impact Assessment

### Low Risk
- ✅ Seed data created (testing ready)
- ✅ Redirect preserves old URLs
- ✅ Core features preserved

### Medium Risk
- ⚠️ 74 navigation references to update
- ⚠️ Lost features (achievements, trending, etc.)
- ⚠️ User experience change

### High Risk
- ❌ None identified
- Database schema unchanged
- Backend API additions only (no breaking changes)

---

## 🔗 References

- Epic #4575: `docs/epics/epic-gaming-hub-dashboard.md`
- Issue Breakdown: `docs/epics/epic-gaming-hub-dashboard-issues.md`
- V1 Docs: `docs/07-frontend/dashboard-overview-hub.md` (existing)
- V2 Docs: `docs/07-frontend/gaming-hub-dashboard.md` (Phase 4)
