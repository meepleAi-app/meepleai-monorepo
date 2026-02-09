# Dashboard Hub Migration Guide

**From**: UserDashboard.tsx (legacy, 1137 lines)
**To**: Dashboard.tsx (modern hub layout)
**Issue**: #3910 - Dashboard Hub Layout Refactoring + Cleanup Legacy
**Date**: 2026-02-09

---

## Migration Overview

### What Changed

**Removed** (1137 lines total):
- `UserDashboard.tsx` - Monolithic dashboard component
- `UserDashboardCompact.tsx` - Compact variant
- `dashboard-client.tsx` - Legacy client component
- Mock constants: `MOCK_STATS`, `MOCK_QUICK_GAMES`, `MOCK_ACTIVITIES`
- Sub-components: `StatCardCompact`, `QuickGameCard`, `ActivityRow`

**Added** (Modular approach):
- `Dashboard.tsx` - Main hub layout (Section-based)
- `LibrarySnapshot.tsx` - Library widget (Issue #3912)
- `ActivityFeed.tsx` - Activity timeline (Issue #3911)
- `QuickActionsGrid.tsx` - Enhanced quick actions (Issue #3913)
- `HeroStats.tsx` - Stats overview
- `DashboardSection.tsx` - Reusable section wrapper
- Responsive layout (mobile/tablet/desktop) (Issue #3914)

---

## Breaking Changes

### Import Paths Changed

**Before**:
```typescript
import { UserDashboard } from '@/components/dashboard/UserDashboard';
import { UserDashboardCompact } from '@/components/dashboard/UserDashboardCompact';
```

**After**:
```typescript
import { Dashboard } from '@/components/dashboard/Dashboard';
import { LibrarySnapshot } from '@/components/dashboard/LibrarySnapshot';
import { ActivityFeed } from '@/components/dashboard/ActivityFeed';
import { QuickActionsGrid } from '@/components/dashboard/QuickActionsGrid';
```

---

### Component API Changed

**Before** (UserDashboard.tsx):
```typescript
interface UserDashboardProps {
  variant?: 'full' | 'compact';
  showStats?: boolean;
  showQuickActions?: boolean;
  showActivities?: boolean;
}

<UserDashboard variant="full" showStats showQuickActions showActivities />
```

**After** (Dashboard.tsx - Section-based):
```typescript
interface DashboardProps {
  sections?: SectionConfig[];
  layout?: 'grid' | 'stack';
  className?: string;
}

<Dashboard
  sections={[
    { id: 'library', component: LibrarySnapshot, props: { quota, topGames } },
    { id: 'activity', component: ActivityFeed, props: { events } },
    { id: 'quick-actions', component: QuickActionsGrid, props: { actions } },
  ]}
  layout="grid"
/>
```

---

### Data Fetching Changed

**Before** (Client-side fetch in UserDashboard):
```typescript
// UserDashboard.tsx
useEffect(() => {
  fetch('/api/v1/library')
    .then(res => res.json())
    .then(setLibrary);
  fetch('/api/v1/sessions')
    .then(res => res.json())
    .then(setSessions);
  // ... multiple fetch calls
}, []);
```

**After** (Aggregated API in page.tsx):
```typescript
// app/(authenticated)/dashboard/page.tsx
const dashboard = await fetch('/api/v1/dashboard').then(r => r.json());

<Dashboard
  sections={[
    { id: 'library', component: LibrarySnapshot, props: dashboard.library },
    { id: 'activity', component: ActivityFeed, props: dashboard.activity },
  ]}
/>
```

**Benefits**:
- Single API call vs multiple
- Reduced client-side fetching
- Better performance (< 500ms cached vs 3-5s multiple calls)
- Server-side data aggregation

---

### Styling Changed

**Before** (Inline styles + CSS modules):
```typescript
// UserDashboard.tsx
<div className="dashboard-container" style={{ padding: '24px' }}>
  <div className={styles.statsGrid}>
    ...
  </div>
</div>
```

**After** (Tailwind + Glassmorphism):
```typescript
// Dashboard.tsx
<div className="container mx-auto px-4 py-6">
  <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
    <div className="bg-white/70 backdrop-blur-md rounded-lg shadow-lg p-6">
      ...
    </div>
  </div>
</div>
```

**Design System**:
- Glassmorphic cards: `bg-white/70 backdrop-blur-md`
- Amber accent: `bg-amber-100 text-amber-900`
- Fonts: `font-quicksand` (headings), `font-nunito` (body)

---

## Migration Steps

### Step 1: Update Page Component

**File**: `apps/web/src/app/(authenticated)/dashboard/page.tsx`

**Before**:
```typescript
import { UserDashboard } from '@/components/dashboard/UserDashboard';

export default function DashboardPage() {
  return <UserDashboard variant="full" />;
}
```

**After**:
```typescript
import { Dashboard } from '@/components/dashboard';

export default async function DashboardPage() {
  // Fetch aggregated data (server component)
  const dashboard = await fetch(`${process.env.API_BASE}/api/v1/dashboard`, {
    headers: { Cookie: cookies().toString() },
  }).then(r => r.json());

  return (
    <Dashboard
      sections={[
        { id: 'hero', component: HeroStats, props: dashboard.stats },
        { id: 'library', component: LibrarySnapshot, props: dashboard.library },
        { id: 'activity', component: ActivityFeed, props: dashboard.activity },
        { id: 'quick-actions', component: QuickActionsGrid, props: dashboard.quickActions },
      ]}
    />
  );
}
```

---

### Step 2: Remove Legacy Imports

**Search and replace** across codebase:

```bash
# Find all UserDashboard imports
grep -r "UserDashboard" apps/web/src/ --exclude-dir=node_modules

# Expected after migration: ZERO results (except comments)
```

**If found**, update import:
```typescript
// Before
import { UserDashboard } from '@/components/dashboard/UserDashboard';

// After
import { Dashboard } from '@/components/dashboard';
```

---

### Step 3: Update Component Usage

**Pattern**: Replace monolithic UserDashboard with modular sections

**Before**:
```typescript
<UserDashboard
  variant="full"
  showStats={true}
  showQuickActions={true}
  showActivities={true}
/>
```

**After**:
```typescript
<Dashboard
  sections={[
    { id: 'hero', component: HeroStats, props: statsData },
    { id: 'library', component: LibrarySnapshot, props: libraryData },
    { id: 'activity', component: ActivityFeed, props: activityData },
    { id: 'quick-actions', component: QuickActionsGrid, props: actionsData },
  ]}
  layout="grid" // or "stack" for mobile
/>
```

---

### Step 4: Update API Calls

**Consolidate multiple API calls** into single aggregated endpoint

**Before** (Multiple calls):
```typescript
// UserDashboard.tsx
const [library, setLibrary] = useState();
const [sessions, setSessions] = useState();
const [activity, setActivity] = useState();

useEffect(() => {
  Promise.all([
    fetch('/api/v1/library').then(r => r.json()),
    fetch('/api/v1/sessions').then(r => r.json()),
    fetch('/api/v1/activity').then(r => r.json()),
  ]).then(([lib, sess, act]) => {
    setLibrary(lib);
    setSessions(sess);
    setActivity(act);
  });
}, []);
```

**After** (Single aggregated call):
```typescript
// page.tsx (Server Component)
const dashboard = await fetch('/api/v1/dashboard').then(r => r.json());

// dashboard.library, dashboard.activity, dashboard.sessions all available
```

**Performance Improvement**: 3-5s (multiple calls) → < 500ms (single aggregated, cached)

---

### Step 5: Test Migration

**Unit Tests**:
```bash
cd apps/web
pnpm test dashboard

# Expected: All tests passing
# No references to UserDashboard in test files
```

**E2E Tests**:
```bash
pnpm test:e2e dashboard-user-journey

# Expected: 6 DoD scenarios passing
# - DoD #1: Dashboard loads
# - DoD #2: Game navigation
# - DoD #3: Chat navigation
# - DoD #4: Quick actions
# - DoD #5: Library quota
# - DoD #6: Visual regression
```

**TypeScript**:
```bash
pnpm typecheck

# Expected: No errors
# No broken imports from UserDashboard
```

---

## Feature Mapping

### Legacy → Modern Component Mapping

| Legacy Feature | Modern Component | Status |
|----------------|------------------|--------|
| Stats overview | HeroStats.tsx | ✅ Enhanced |
| Library quota | LibrarySnapshot.tsx | ✅ Enhanced |
| Recent games | LibrarySnapshot.topGames | ✅ Enhanced |
| Quick actions | QuickActionsGrid.tsx | ✅ Enhanced |
| Activity feed | ActivityFeed.tsx | ✅ New implementation |
| Chat history | ChatHistorySection.tsx | ✅ Enhanced |
| Active sessions | ActiveSessionsWidget.tsx | ✅ Enhanced |

---

### Functionality Additions

**New Features** (not in legacy):
- ✅ Responsive layout (mobile/tablet/desktop)
- ✅ Glassmorphic design (modern aesthetic)
- ✅ Section-based architecture (modular, reusable)
- ✅ SSE real-time updates (/api/v1/dashboard/stream)
- ✅ AI insights widget (Epic #3905)
- ✅ Wishlist highlights (Epic #3905)
- ✅ Catalog trending (Epic #3905)
- ✅ Achievements widget (Epic #3906)
- ✅ Timeline filters & search (Epic #3906)

**Performance Improvements**:
- ✅ Aggregated API (< 500ms vs 3-5s)
- ✅ Redis caching (5-min TTL)
- ✅ Cache invalidation on user actions
- ✅ Lazy loading images
- ✅ Skeleton loading states

---

## Rollback Procedure (Emergency)

**If migration causes issues**, rollback is NOT possible because legacy code was removed.

**Alternative**:
1. Revert PR that removed UserDashboard.tsx
2. Restore from git history:
   ```bash
   git log --all --full-history -- apps/web/src/components/dashboard/UserDashboard.tsx
   git checkout <commit-hash> -- apps/web/src/components/dashboard/UserDashboard.tsx
   ```
3. Fix imports
4. Rebuild

**Recommendation**: DO NOT ROLLBACK. Fix forward instead. Dashboard.tsx is tested and functional.

---

## FAQ

### Q: Can I use both UserDashboard and Dashboard?

**A**: No. UserDashboard.tsx was removed in Issue #3910. Use Dashboard.tsx only.

---

### Q: How do I customize the dashboard layout?

**A**: Use `sections` prop to define which widgets to display:

```typescript
<Dashboard
  sections={[
    { id: 'library', component: LibrarySnapshot },
    { id: 'activity', component: ActivityFeed },
    // Add/remove sections as needed
  ]}
/>
```

---

### Q: Can I add custom sections?

**A**: Yes. Create your component and add to sections:

```typescript
// MyCustomWidget.tsx
export function MyCustomWidget(props: MyProps) {
  return <div>Custom content</div>;
}

// Usage
<Dashboard
  sections={[
    { id: 'custom', component: MyCustomWidget, props: customData },
  ]}
/>
```

---

### Q: How do I handle loading states?

**A**: Use `isLoading` prop on individual components:

```typescript
<LibrarySnapshot isLoading={!dashboard} quota={dashboard?.library.quota} />
```

Components have built-in Skeleton loading states.

---

### Q: Mobile layout broken after migration?

**A**: Ensure responsive classes applied:

```typescript
// Correct
<div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3">

// Wrong (fixed width)
<div className="grid grid-cols-3">
```

Test: Resize browser < 640px, should see single column.

---

## Support

**Issues**: Create issue on GitHub with label `area/dashboard`
**Documentation**: `docs/frontend/dashboard-hub-guide.md`
**Tests**: `apps/web/e2e/dashboard-user-journey.spec.ts`

---

**Migration Status**: ✅ COMPLETE (Issue #3910 closed)
