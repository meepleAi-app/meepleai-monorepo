# Piano Esecuzione Parallelo - Dashboard & Admin UI Focus

**Generato**: 2026-02-09
**Strategia**: Opzione A - Dashboard & Admin UI (valore immediato)
**Scope**: 28/99 issue prioritarie
**Backlog**: 71 issue rinviate (epic #3490, #3688, AI Platform, Infrastructure)
**Timeline**: 3-4 settimane (110h totali con 2 terminali)
**Efficienza**: 44% risparmio vs sequenziale (197h → 110h)

---

## 📋 Executive Summary

### Scope Selezionato (28 issues)

✅ **Epic #3927** - Admin UI Completion (6 issues, 16-24h)
✅ **Epic #3901** - Dashboard Hub Core MVP (8 issues, 21 SP ≈42h)
✅ **Epic #3905** - AI Insights & Recommendations (6 issues, 13 SP ≈26h)
✅ **Epic #3906** - Gamification & Advanced Features (4 issues, 8 SP ≈16h)
✅ **Issue #3956** - Technical Debt Phase 1+2 (1 issue, 5 SP ≈10h)
✅ **Testing & Polish** (3 issues standalone)

### Valore Deliverable

🎯 **Immediate Value (Week 1-2)**:
- 6 admin UI features operative (backend già pronto)
- Dashboard Hub MVP live
- Zero technical debt deferred

🚀 **Advanced Value (Week 3-4)**:
- AI-powered recommendations integrate
- Gamification system active
- Complete test coverage

### Backlog (71 issues rinviate)

📦 **Sprint Futuri**:
- Epic #3490: Multi-Agent AI System (20+ issues) → Sprint 5-8
- Epic #3688: Business & Simulations (10 issues) → Sprint 6-7
- Epic AI Platform (11 issues) → Sprint 7-9
- Epic Infrastructure (6 issues) → Ongoing
- Altri epic medium/low priority (24 issues) → Backlog

---

## Terminal 1: Backend Development Flow

**Total**: 27 SP (54 hours, 6.75 giorni @ 8h/giorno)

### 🔴 PHASE 1: Dashboard Hub Backend (7 SP, 14-18h)

**Epic**: #3901 - Dashboard Hub Core MVP
**Priority**: HIGH
**Dependencies**: None (start immediately)
**Timeline**: Giorni 1-2.5

#### Issues Sequence

| # | Issue | Descrizione | SP | Ore | Output |
|---|-------|-------------|----|----- |--------|
| 1 | #3907 | Dashboard Aggregated API Endpoint | 3 | 6h | `/api/v1/dashboard` endpoint |
| 2 | #3908 | Activity Timeline Aggregation Service | 2 | 4h | Timeline service + repository |
| 3 | #3909 | Cache Invalidation Strategy | 2 | 4h | Cache patterns + Redis integration |

**Deliverables**:
```
✅ /api/v1/dashboard - Aggregated endpoint (library, sessions, activity)
✅ ActivityTimelineService - Data aggregation logic
✅ Cache invalidation patterns (Redis)
✅ Integration tests (Testcontainers)
✅ API documentation (Scalar)
```

**Quality Gates**:
- [ ] API response time < 500ms (cached)
- [ ] API response time < 2s (uncached)
- [ ] Unit test coverage > 90%
- [ ] Integration tests passing
- [ ] Scalar documentation complete

**Blockers Risolti**:
- Cache strategy design → Use Redis with 5min TTL
- Data aggregation → CQRS pattern with MediatR
- Timeline pagination → Offset-based, 50 items default

---

### 🟡 PHASE 2: Technical Debt Cleanup (5 SP, 10h)

**Issue**: #3956 - Complete Deferred Phase 1+2 Work
**Priority**: HIGH (strategic break)
**Dependencies**: Issues #3493, #3494, #3498 already completed ✅
**Timeline**: Giorni 2.5-4

#### Work Items Breakdown

**PostgreSQL Schema (#3493) - 3h**
- [ ] GDPR cleanup job (conversation_memory >90 days) - 1h
  - Implement background job (Hangfire)
  - Schedule daily execution
  - Test with mock data
- [ ] Strategy pattern seeding (common game openings) - 1h
  - Chess openings data
  - Go joseki patterns
  - Seed migration
- [ ] Integration tests (Testcontainers) - 0.5h
- [ ] Migration rollback testing - 0.3h
- [ ] Vector embedding performance tests (<100ms P95) - 0.2h

**Redis Cache (#3494) - 4h**
- [ ] Prometheus metrics export - 2h
  - Cache hit rate
  - Eviction count
  - Memory usage
  - Connection pool stats
- [ ] Grafana dashboard integration - 2h
  - Import existing JSON
  - Connect to Prometheus
  - Verify visualization

**Conversation Memory (#3498) - 3h**
- [ ] Retrieval nDCG measurement (>0.8 target) - 2h
  - Implement nDCG calculation
  - Benchmark with test queries
  - Document results
- [ ] Latency verification (<200ms P95) - 1h
  - Load testing script
  - P95 measurement
  - Optimization if needed

**Deliverables**:
```
✅ GDPR cleanup job scheduled (Hangfire daily)
✅ Prometheus metrics exported
✅ Grafana dashboard live
✅ Performance benchmarks documented
✅ All deferred tests passing
```

**Quality Gates**:
- [ ] GDPR job tested with 90-day-old data
- [ ] Prometheus metrics visible in Grafana
- [ ] Cache hit rate >80% measured
- [ ] nDCG score >0.8 achieved
- [ ] P95 latency <200ms verified
- [ ] Documentation updated in ADR

---

### 🔴 PHASE 3: AI Insights Backend (7 SP, 14-18h)

**Epic**: #3905 - AI Insights & Recommendations
**Priority**: HIGH
**Dependencies**: ✅ Epic #3901 Backend complete (Phase 1)
**Timeline**: Giorni 4-6.5

#### Issues Sequence

| # | Issue | Descrizione | SP | Ore | Output |
|---|-------|-------------|----|----- |--------|
| 4 | #3916 | AI Insights Service (RAG Integration) | 3 | 6h | RAG recommendations service |
| 5 | #3917 | Wishlist Management API (CRUD) | 2 | 4h | Wishlist endpoints |
| 6 | #3918 | Catalog Trending Analytics Service | 2 | 4h | Trending games service |

**#3916 - AI Insights Service Details**:
```
✅ RAG integration with existing vector DB (Qdrant)
✅ Recommendation algorithm (similar games, backlog alerts)
✅ Graceful degradation (fallback if RAG unavailable)
✅ Response caching (Redis, 1h TTL)
✅ Endpoints:
  - GET /api/v1/ai/insights/recommendations
  - GET /api/v1/ai/insights/backlog-alerts
  - GET /api/v1/ai/insights/similar-games/{gameId}
```

**#3917 - Wishlist API Details**:
```
✅ CRUD operations:
  - POST /api/v1/wishlist/games
  - GET /api/v1/wishlist/games
  - DELETE /api/v1/wishlist/games/{gameId}
  - GET /api/v1/wishlist/highlights (top 5)
✅ User-specific wishlist (multi-tenancy)
✅ Availability alerts (notify when in stock)
```

**#3918 - Catalog Trending Details**:
```
✅ Community trending algorithm (views, adds, sessions)
✅ Time-based windows (day, week, month)
✅ Endpoints:
  - GET /api/v1/catalog/trending?window=week
  - GET /api/v1/catalog/trending/categories
✅ Cache (Redis, 15min TTL)
```

**Quality Gates**:
- [ ] AI insights API <1s response time (p95)
- [ ] RAG accuracy >75% (manual validation with 20 test cases)
- [ ] Wishlist CRUD API <300ms
- [ ] Trending analytics <500ms
- [ ] Graceful degradation tested (RAG service down)
- [ ] Test coverage >85%

---

### 🟡 PHASE 4: Gamification Backend (4 SP, 8h)

**Epic**: #3906 - Gamification & Advanced Features
**Priority**: MEDIUM
**Dependencies**: ✅ Epic #3901 Backend complete (Phase 1)
**Timeline**: Giorni 6.5-7.75 (in parallelo con Frontend Phase 3)

#### Issues Sequence

| # | Issue | Descrizione | SP | Ore | Output |
|---|-------|-------------|----|----- |--------|
| 7 | #3922 | Achievement System & Badge Engine | 3 | 6h | Achievements + badges |
| 8 | #3923 | Advanced Timeline Service (Filters + Search) | 1 | 2h | Timeline filters API |

**#3922 - Achievement System Details**:
```
✅ Achievement types:
  - Streaks (7-day, 30-day play streaks)
  - Milestones (10 games, 100 games, 1000 games)
  - Competenze (expert in category, all mechanics tried)
✅ Badge engine:
  - Badge assignment on achievement unlock
  - Badge display metadata (icon, color, rarity)
✅ Background job (Hangfire) - achievement calculation
✅ Endpoints:
  - GET /api/v1/achievements/user
  - GET /api/v1/achievements/available
  - GET /api/v1/badges/user
```

**#3923 - Timeline Filters Details**:
```
✅ Filter capabilities:
  - Date range (from, to)
  - Activity type (played, added, rated, reviewed)
  - Game category
  - User (for admin)
✅ Search (text in game title, notes)
✅ Pagination (offset, limit)
✅ Endpoints:
  - GET /api/v1/timeline/activities?filters={...}
  - GET /api/v1/timeline/search?q={query}
```

**Quality Gates**:
- [ ] Achievement calculation <30s per 10K users
- [ ] Timeline search <500ms
- [ ] Layout persistence (localStorage + backend sync)
- [ ] Test coverage >85%

---

## Terminal 2: Frontend Development Flow

**Total**: 24 SP + 20h (68 hours, 8.5 giorni @ 8h/giorno)

### ⚡ PHASE 1: Admin UI Quick Wins (16-24h)

**Epic**: #3927 - Admin UI Completion
**Priority**: HIGH (IMMEDIATE VALUE)
**Dependencies**: None - Backend APIs già esistenti! ✅
**Timeline**: Giorni 1-3

#### Issues Sequence (Priorità Value/Effort)

| # | Issue | Descrizione | Ore | Priority | Value |
|---|-------|-------------|-----|----------|-------|
| 1 | #3928 | Pending Approvals Workflow UI | 3-4h | HIGH | ⚡⚡⚡ |
| 2 | #3931 | Global Sessions Monitoring Dashboard | 3-4h | MEDIUM | ⚡⚡⚡ |
| 3 | #3932 | API Keys Stats & Analytics Dashboard | 3-4h | MEDIUM | ⚡⚡ |
| 4 | #3933 | Workflow Errors Monitoring View | 3-4h | MEDIUM | ⚡⚡ |
| 5 | #3929 | User Activity Timeline View | 3-4h | HIGH | ⚡⚡ |
| 6 | #3930 | Bulk User Actions Modal | 4-6h | HIGH | ⚡ |

**Quick Win Strategy**: Start con #3928, #3931, #3932, #3933 (primi 4 = 12-16h) per delivery immediato in 2 giorni.

#### #3928 - Pending Approvals Workflow UI

**Backend APIs Ready**:
```
✅ GET /admin/shared-games/pending-approvals
✅ POST /admin/shared-games/{id}/approve-publication
✅ POST /admin/shared-games/{id}/reject-publication
```

**Implementation**:
```typescript
// Page: /admin/shared-games/pending-approvals
- MeepleCard (entity="game", variant="list")
- Filter tabs: All | Pending | Approved | Rejected
- Badge count in admin sidebar (real-time)
- Approval: Single click + confirmation toast
- Rejection: Modal with reason input (required)
- Bulk actions: Select multiple → Approve/Reject all
```

**Components**:
- `PendingApprovalsPage.tsx` (new)
- `GameApprovalCard.tsx` (wrapper for MeepleCard)
- `RejectReasonDialog.tsx` (shadcn Dialog)
- `BulkActionsToolbar.tsx` (reusable)

**Tests**:
- [ ] Unit: Approval logic
- [ ] E2E: Approve workflow
- [ ] E2E: Reject workflow with reason
- [ ] E2E: Bulk approve

---

#### #3931 - Global Sessions Monitoring Dashboard

**Backend API Ready**:
```
✅ GET /admin/sessions/active
✅ GET /admin/sessions/stats
```

**Implementation**:
```typescript
// Page: /admin/sessions
- Real-time active sessions count
- Session list (user, game, start time, duration)
- Filters: Active | All | By User
- Session details modal (click to expand)
- Auto-refresh every 30s
```

**Components**:
- `SessionsMonitoringPage.tsx` (new)
- `SessionStatsCard.tsx` (stats widget)
- `SessionListView.tsx` (EntityListView pattern)
- `SessionDetailDialog.tsx`

---

#### #3932 - API Keys Stats & Analytics Dashboard

**Backend API Ready**:
```
✅ GET /admin/api-keys/stats
✅ GET /admin/api-keys/usage
```

**Implementation**:
```typescript
// Page: /admin/api-keys (enhance existing)
- Usage charts (requests per day, by key)
- Rate limit status per key
- Top consumers list
- Error rate tracking
```

**Components**:
- Enhance existing `ApiKeysPage.tsx`
- `ApiKeyStatsChart.tsx` (Recharts)
- `UsageHeatmap.tsx`

---

#### #3933 - Workflow Errors Monitoring View

**Backend API Ready**:
```
✅ GET /admin/n8n-templates/errors
✅ GET /admin/n8n-templates/error-stats
```

**Implementation**:
```typescript
// Page: /admin/n8n-templates (add Errors tab)
- Error list (template, error message, timestamp, count)
- Filter: Last 24h | Week | Month
- Error severity badges
- Quick actions: View details, Mark resolved
```

**Components**:
- Enhance `N8nTemplatesPage.tsx` with tabs
- `WorkflowErrorsList.tsx`
- `ErrorSeverityBadge.tsx`

---

#### #3929 - User Activity Timeline View

**Backend API Ready**:
```
✅ GET /admin/users/{id}/activity
```

**Implementation**:
```typescript
// Page: /admin/users/[id] (add Activity tab)
- Timeline view (chronological)
- Activity types: Played, Added, Rated, Reviewed
- Filter by type, date range
- Pagination (infinite scroll)
```

---

#### #3930 - Bulk User Actions Modal

**Backend APIs Ready**:
```
✅ POST /admin/users/bulk/password-reset
✅ POST /admin/users/bulk/role-change
✅ POST /admin/users/bulk/export
✅ POST /admin/users/bulk/import
```

**Implementation**:
```typescript
// Component: BulkUserActionsModal (reusable)
- Multi-select users (checkbox)
- Actions dropdown: Reset Password | Change Role | Export | Import
- Confirmation step with preview
- Progress indicator for bulk operations
```

---

**Phase 1 Deliverables**:
```
✅ 6 admin UI features live
✅ Backend APIs integrated
✅ Real-time updates (SSE where applicable)
✅ Mobile responsive (<640px tested)
✅ Unit + E2E tests
✅ Badge count updates in sidebar
```

**Quality Gates**:
- [ ] All 6 features functional
- [ ] Mobile responsive verified
- [ ] Loading states during API calls
- [ ] Error handling with toast messages
- [ ] Confirmation dialogs for destructive actions
- [ ] Test coverage >80%

---

### 🔴 PHASE 2: Dashboard Hub Frontend (14 SP, 28h)

**Epic**: #3901 - Dashboard Hub Core MVP
**Priority**: HIGH
**Dependencies**: ✅ Epic #3901 Backend ready (Terminal 1 Phase 1)
**Timeline**: Giorni 3-6.5

#### Issues Sequence

| # | Issue | Descrizione | SP | Ore | Priority |
|---|-------|-------------|----|----- |----------|
| 1 | #3912 | Library Snapshot Component | 2 | 4h | HIGH |
| 2 | #3913 | Quick Actions Grid Enhancement | 2 | 4h | HIGH |
| 3 | #3911 | Enhanced Activity Feed Timeline | 3 | 6h | HIGH |
| 4 | #3914 | Responsive Layout Mobile/Desktop | 3 | 6h | HIGH |
| 5 | #3915 | Testing: Integration & E2E Suite | 3 | 6h | HIGH |

#### #3912 - Library Snapshot Component

**Data Source**: `/api/v1/dashboard` (aggregated)

**Implementation**:
```typescript
// Component: LibrarySnapshot.tsx
interface LibrarySnapshotProps {
  totalGames: number;
  topGames: Game[]; // top 3 by play count
  recentlyAdded: Game[];
  categories: CategoryStats[];
}

// UI Elements:
- Total games count (big number)
- Top 3 games (MeepleCard variant="compact")
- Category distribution (mini chart)
- "View Full Library" CTA button → /library
```

**Components**:
- `LibrarySnapshot.tsx` (new)
- Use `MeepleCard` for game display
- Mini chart (Recharts PieChart)

---

#### #3913 - Quick Actions Grid Enhancement

**Implementation**:
```typescript
// Component: QuickActionsGrid.tsx (enhance existing)
// Actions:
- Log Play Session → /sessions/new
- Add Game to Library → /games/search
- Start AI Chat → /chat
- View Stats → /stats
- Wishlist → /wishlist
- Achievements → /achievements

// Grid: 2x3 on desktop, 2x2 on mobile
// Icons: Lucide React
// Hover states: Glassmorphic effect
```

---

#### #3911 - Enhanced Activity Feed Timeline

**Data Source**: `/api/v1/dashboard` (activity timeline)

**Implementation**:
```typescript
// Component: ActivityFeedTimeline.tsx
interface Activity {
  id: string;
  type: 'played' | 'added' | 'rated' | 'reviewed';
  game: Game;
  timestamp: Date;
  details: string;
}

// UI Elements:
- Timeline layout (vertical)
- Activity cards (type icon + game + action + time)
- Load more (pagination)
- Filter by type (tabs)
- Empty state
```

**Components**:
- `ActivityFeedTimeline.tsx` (new)
- `ActivityCard.tsx`
- `ActivityTypeIcon.tsx`
- Infinite scroll (IntersectionObserver)

---

#### #3914 - Responsive Layout Mobile/Desktop

**Implementation**:
```typescript
// Layout refactoring for /dashboard
// Desktop (≥768px):
- 3-column grid
- Sidebar navigation
- Large cards

// Tablet (640px-768px):
- 2-column grid
- Collapsible sidebar

// Mobile (<640px):
- Single column stack
- Bottom navigation
- Compact cards

// Breakpoints (Tailwind):
- sm: 640px
- md: 768px
- lg: 1024px
- xl: 1280px
```

**Files Modified**:
- `app/(authenticated)/dashboard/page.tsx`
- `components/dashboard/DashboardLayout.tsx`
- Update all dashboard components for responsive

---

#### #3915 - Testing: Integration & E2E Suite

**Test Coverage**:

**Integration Tests (Vitest)**:
```typescript
// LibrarySnapshot.test.tsx
- Renders with data from API
- Handles loading state
- Handles error state
- "View Full Library" navigation

// ActivityFeedTimeline.test.tsx
- Renders timeline items
- Pagination works
- Filter by type works
- Empty state shown when no activities
```

**E2E Tests (Playwright)**:
```typescript
// dashboard.spec.ts
test('User loads dashboard and sees all widgets', async ({ page }) => {
  await page.goto('/dashboard');
  await expect(page.locator('[data-testid="library-snapshot"]')).toBeVisible();
  await expect(page.locator('[data-testid="activity-feed"]')).toBeVisible();
  await expect(page.locator('[data-testid="quick-actions"]')).toBeVisible();
});

test('User navigates from dashboard to library', async ({ page }) => {
  await page.goto('/dashboard');
  await page.click('[data-testid="view-full-library"]');
  await expect(page).toHaveURL('/library');
});

test('Dashboard loads in < 1.5s', async ({ page }) => {
  const start = Date.now();
  await page.goto('/dashboard');
  await page.waitForLoadState('networkidle');
  const duration = Date.now() - start;
  expect(duration).toBeLessThan(1500);
});
```

**Quality Gates**:
- [ ] Unit test coverage >85%
- [ ] E2E tests passing
- [ ] Lighthouse Performance Score >90
- [ ] Load time <1.5s verified
- [ ] Mobile responsive tested (<640px)

---

#### Legacy Code Cleanup (included in Phase 2)

**Files to Remove**:
```bash
# After Dashboard Hub MVP complete
rm apps/web/src/components/dashboard/UserDashboard.tsx  # 1137 lines
rm apps/web/src/components/dashboard/UserDashboardCompact.tsx
rm apps/web/src/app/(authenticated)/dashboard/dashboard-client.tsx
# Remove unused sub-components
rm apps/web/src/components/dashboard/StatCardCompact.tsx
rm apps/web/src/components/dashboard/QuickGameCard.tsx
rm apps/web/src/components/dashboard/ActivityRow.tsx
```

**Validation**:
```bash
# Verify zero legacy references
grep -r "UserDashboard" apps/web/src/
grep -r "dashboard-client-legacy" apps/web/src/
# Should return no results

# Tests still passing after cleanup
pnpm test
pnpm test:e2e
```

---

**Phase 2 Deliverables**:
```
✅ Dashboard Hub MVP live (/dashboard)
✅ Library Snapshot widget functional
✅ Activity Feed with real-time data
✅ Quick Actions grid enhanced
✅ Mobile responsive (<640px)
✅ Legacy code removed (UserDashboard.tsx)
✅ Test coverage >85%
✅ Lighthouse Performance >90
```

---

### 🔴 PHASE 3: AI Insights Frontend (6 SP, 12h)

**Epic**: #3905 - AI Insights & Recommendations
**Priority**: HIGH
**Dependencies**: ✅ Epic #3905 Backend ready (Terminal 1 Phase 3)
**Timeline**: Giorni 6.5-8

#### Issues Sequence

| # | Issue | Descrizione | SP | Ore | Priority |
|---|-------|-------------|----|----- |----------|
| 1 | #3920 | Wishlist Highlights Component | 2 | 4h | HIGH |
| 2 | #3921 | Catalog Trending Widget | 2 | 4h | MEDIUM |
| 3 | #3919 | AI Insights Widget Component | 2 | 4h | HIGH |

#### #3920 - Wishlist Highlights Component

**Data Source**: `/api/v1/wishlist/highlights`

**Implementation**:
```typescript
// Component: WishlistHighlights.tsx
interface WishlistHighlight {
  game: Game;
  addedDate: Date;
  availability: 'in_stock' | 'out_of_stock' | 'pre_order';
  priceAlert?: boolean;
}

// UI Elements:
- Top 5 wishlist games (MeepleCard variant="compact")
- Availability badge
- Price alert indicator
- "Manage Wishlist" CTA → /wishlist
- Add to wishlist button (quick action)
```

**Components**:
- `WishlistHighlights.tsx` (new)
- `AvailabilityBadge.tsx`
- Use `MeepleCard` for display

---

#### #3921 - Catalog Trending Widget

**Data Source**: `/api/v1/catalog/trending?window=week`

**Implementation**:
```typescript
// Component: CatalogTrendingWidget.tsx
interface TrendingGame {
  game: Game;
  trendScore: number;
  change: 'up' | 'down' | 'new' | 'stable';
  rank: number;
}

// UI Elements:
- Top 10 trending games
- Trend indicator (🔥 hot, ⬆️ rising, 🆕 new)
- Rank badge
- Time window selector (Day | Week | Month)
- Click → game detail page
```

**Components**:
- `CatalogTrendingWidget.tsx` (new)
- `TrendIndicator.tsx`
- `TimeWindowSelector.tsx`

---

#### #3919 - AI Insights Widget Component

**Data Source**: `/api/v1/ai/insights/recommendations`

**Implementation**:
```typescript
// Component: AIInsightsWidget.tsx
interface AIInsight {
  type: 'recommendation' | 'backlog_alert' | 'similar_game';
  game: Game;
  reason: string;
  confidence: number; // 0-1
  action?: 'play' | 'add' | 'explore';
}

// UI Elements:
- AI-powered recommendations (3-5 games)
- Reason/explanation text
- Confidence indicator (subtle)
- Backlog alerts (games not played in 30+ days)
- "Similar to X" suggestions
- Graceful fallback if RAG unavailable
```

**Graceful Degradation**:
```typescript
// If /api/v1/ai/insights fails:
// 1. Show cached recommendations (localStorage, 24h TTL)
// 2. Fallback to rule-based (recently added, popular in category)
// 3. Show "AI recommendations temporarily unavailable" notice
// 4. Hide widget if no fallback available
```

**Components**:
- `AIInsightsWidget.tsx` (new)
- `RecommendationCard.tsx`
- `ConfidenceIndicator.tsx` (subtle, low opacity)
- `BacklogAlert.tsx`

---

**Phase 3 Deliverables**:
```
✅ AI Insights widget integrated
✅ Wishlist highlights functional
✅ Catalog trending live
✅ Graceful degradation tested
✅ Engagement metrics tracked
```

**Quality Gates**:
- [ ] AI insights engagement >30% (click rate)
- [ ] Wishlist additions from recommendations >20%
- [ ] Trending widget clicks >15%
- [ ] Graceful fallback tested (RAG service down)
- [ ] Test coverage >85%

---

### 🟡 PHASE 4: Gamification Frontend (4 SP, 8h)

**Epic**: #3906 - Gamification & Advanced Features
**Priority**: MEDIUM
**Dependencies**: ✅ Epic #3906 Backend ready (Terminal 1 Phase 4)
**Timeline**: Giorni 8-8.5 (in parallelo con Terminal 1 Phase 4)

#### Issues Sequence

| # | Issue | Descrizione | SP | Ore | Priority |
|---|-------|-------------|----|----- |----------|
| 1 | #3924 | Achievements Widget Component | 2 | 4h | MEDIUM |
| 2 | #3925 | Advanced Timeline Filters & Search | 2 | 4h | MEDIUM |

#### #3924 - Achievements Widget Component

**Data Source**: `/api/v1/achievements/user`

**Implementation**:
```typescript
// Component: AchievementsWidget.tsx
interface Achievement {
  id: string;
  name: string;
  description: string;
  badge: {
    icon: string;
    color: string;
    rarity: 'common' | 'rare' | 'epic' | 'legendary';
  };
  unlocked: boolean;
  unlockedAt?: Date;
  progress?: {
    current: number;
    target: number;
  };
}

// UI Elements:
- Recent unlocks (last 3 achievements)
- Progress toward next achievement
- Badge display (icon + rarity color)
- "View All Achievements" CTA → /achievements
- Celebration animation on unlock (confetti)
```

**Components**:
- `AchievementsWidget.tsx` (new)
- `AchievementBadge.tsx`
- `ProgressBar.tsx`
- Confetti animation (react-confetti)

---

#### #3925 - Advanced Timeline Filters & Search

**Data Source**: `/api/v1/timeline/activities?filters={...}`

**Implementation**:
```typescript
// Component: TimelineFilters.tsx (enhance existing ActivityFeedTimeline)
interface TimelineFilters {
  dateRange: { from: Date; to: Date };
  activityTypes: ('played' | 'added' | 'rated' | 'reviewed')[];
  gameCategory?: string;
  searchQuery?: string;
}

// UI Elements:
- Filter panel (collapsible)
- Date range picker (Radix UI Calendar)
- Activity type checkboxes
- Game category dropdown
- Search input (debounced)
- Clear filters button
- Applied filters chips
```

**Components**:
- `TimelineFilters.tsx` (new)
- `DateRangePicker.tsx` (shadcn Calendar)
- `FilterChips.tsx`
- Update `ActivityFeedTimeline.tsx` to use filters

**Search Implementation**:
```typescript
// Debounced search (500ms)
const [searchQuery, setSearchQuery] = useState('');
const debouncedSearch = useDebounce(searchQuery, 500);

useEffect(() => {
  if (debouncedSearch) {
    fetchActivities({ search: debouncedSearch });
  }
}, [debouncedSearch]);
```

---

**Phase 4 Deliverables**:
```
✅ Achievements widget live
✅ Timeline filters functional
✅ Search working (<500ms response)
✅ Badge system integrated
```

**Quality Gates**:
- [ ] Achievement engagement >40%
- [ ] Timeline filter usage >20%
- [ ] Search response <500ms
- [ ] Test coverage >85%

---

## 📅 Execution Timeline

### Week 1: Foundation + Quick Wins (Giorni 1-5)

**Terminal 1**:
- Day 1-2.5: Epic #3901 Backend (Dashboard API) ✅
- Day 2.5-4: Issue #3956 Tech Debt ✅

**Terminal 2**:
- Day 1-3: Epic #3927 Admin UI (6 features) ⚡ QUICK WINS
- Day 3-5: Epic #3901 Frontend start (Library Snapshot, Quick Actions)

**Deliverables Week 1**:
- ✅ 6 admin UI features live
- ✅ Dashboard backend API ready
- ✅ Zero technical debt
- ✅ Library Snapshot + Quick Actions components

---

### Week 2: Dashboard Hub MVP (Giorni 5-10)

**Terminal 1**:
- Day 4-6.5: Epic #3905 Backend (AI Insights API) ✅

**Terminal 2**:
- Day 5-6.5: Epic #3901 Frontend complete (Activity Feed, Responsive, Testing)
- Day 6.5-8: Epic #3905 Frontend start (Wishlist Highlights)

**Deliverables Week 2**:
- ✅ Dashboard Hub MVP live
- ✅ Legacy code cleaned
- ✅ AI Insights backend ready
- ✅ Wishlist features started

---

### Week 3: AI Integration + Gamification (Giorni 10-15)

**Terminal 1**:
- Day 6.5-7.75: Epic #3906 Backend (Gamification) ✅

**Terminal 2**:
- Day 8: Epic #3905 Frontend complete (AI Insights, Trending)
- Day 8-8.5: Epic #3906 Frontend (Achievements, Filters)

**Deliverables Week 3**:
- ✅ AI Insights widget integrated
- ✅ Catalog trending live
- ✅ Gamification system active
- ✅ Complete feature set deployed

---

### Week 4: Polish + Testing (Giorni 15-20, if needed)

**Both Terminals**:
- Performance optimization
- Bug fixes from user feedback
- Documentation updates
- Monitoring setup

**Deliverables Week 4**:
- ✅ All quality gates met
- ✅ Performance targets achieved
- ✅ Documentation complete
- ✅ Production ready

---

## 📊 Success Metrics

### Performance Metrics

**Backend**:
- [ ] Dashboard API <500ms (cached)
- [ ] AI Insights API <1s (p95)
- [ ] Wishlist CRUD <300ms
- [ ] Timeline search <500ms
- [ ] Achievement calculation <30s per 10K users

**Frontend**:
- [ ] Dashboard load time <1.5s
- [ ] Lighthouse Performance Score >90
- [ ] Mobile responsive (<640px tested)
- [ ] Test coverage >85%

### User Engagement Metrics

**Admin UI** (Week 1-2):
- [ ] Admin workflow time reduction >40%
- [ ] Manual workarounds eliminated (curl/Postman)

**Dashboard Hub** (Week 2):
- [ ] Click-through rate dashboard → library >40%
- [ ] Time on dashboard >2 minutes
- [ ] Mobile bounce rate <15%

**AI Insights** (Week 3):
- [ ] AI insights engagement >30%
- [ ] Wishlist additions from recommendations >20%
- [ ] Trending widget clicks >15%

**Gamification** (Week 3):
- [ ] Achievement engagement >40%
- [ ] Streak retention rate +15%
- [ ] Timeline filter usage >20%

### Business Impact

**Value Delivered**:
- [ ] 6 admin UI features (immediate admin efficiency)
- [ ] Dashboard Hub MVP (user retention +10%)
- [ ] AI recommendations (discovery +30%)
- [ ] Gamification (session frequency +15%)

**Technical Debt**:
- [ ] Zero deferred work remaining
- [ ] Legacy code eliminated (1137 lines removed)
- [ ] Test coverage >85% all features
- [ ] Performance targets met

---

## 🎯 Risk Management

### Critical Path: Terminal 2 (68h)

**Risk**: Frontend work exceeds backend
**Mitigation**: Start Epic #3927 immediately (quick wins, no dependencies)
**Status**: ✅ Mitigated - Admin UI in Phase 1

### Dependency Risk: Epic #3905

**Risk**: AI Insights depends on Epic #3901
**Mitigation**: Backend Phase 1 completes before Frontend Phase 2
**Status**: ✅ Mitigated - Sequenced correctly

### Quality Risk: Legacy Code Cleanup

**Risk**: Breaking changes during UserDashboard removal
**Mitigation**: E2E tests (#3915) before cleanup, feature flags
**Status**: ⚠️ Monitor - Comprehensive testing required

### Resource Risk: Parallel Execution

**Risk**: Context switching between terminals
**Mitigation**: Clear phase boundaries, independent work streams
**Status**: ✅ Mitigated - Phases aligned

### Technical Risk: RAG Service Availability

**Risk**: AI Insights fails if RAG unavailable
**Mitigation**: Graceful degradation, fallback to rule-based
**Status**: ✅ Mitigated - Fallback implemented

---

## 📦 Backlog Management (71 issues rinviate)

### Sprint 5-8: Multi-Agent AI System (20+ issues)

**Epic #3490** - GRANDE progetto
**Timeline**: 6-8 settimane
**Issues**: #3761-#3809

```
Arbitro Agent (rule enforcement)
Decisore Agent (move suggestions)
Multi-Agent Orchestration
Integration & Testing
```

**Dependencies**: Requires current sprint complete + AI Platform

---

### Sprint 6-7: Business & Simulations (10 issues)

**Epic #3688** - Admin Enterprise
**Timeline**: 4-5 settimane
**Issues**: #3719-#3728

```
Financial Ledger System (8 issues)
App Usage Dashboard (2 issues)
```

**Dependencies**: Requires Admin UI framework from Sprint 1

---

### Sprint 7-9: AI Platform Features (11 issues)

**Epic "AI Platform"**
**Timeline**: 5-6 settimane
**Issues**: #3708-#3718

```
Agent Builder UI
Visual Pipeline Builder (LARGE complexity)
Analytics (Chat, PDF, Model Performance)
A/B Testing Framework
```

**Dependencies**: Requires Epic #3905 (AI Insights)

---

### Ongoing: Infrastructure (6 issues)

**Epic #3366** - Infrastructure Enhancements
**Epic #2967** - Zero-Cost CI/CD
**Issues**: #2968-#2973, #3367-#3368

```
Self-hosted GitHub Actions Runner
Monitoring (Prometheus + Grafana)
Load Testing (k6)
Log Aggregation
```

**Priority**: MEDIUM - Parallel with feature development

---

### Backlog: Medium/Low Priority (24 issues)

```
Epic #3348 - Advanced Features AI/Admin
Epic #3341 - Game Session Toolkit Phase 2
Epic #3320 - Gamification Dashboard
Epic #3356 - Advanced RAG Strategies (9 variants, LOW priority)
Issue #3082 - Missing E2E Test Flows (50 flows)
Issue #3120 - Private Games & Catalog Proposal
```

**Prioritization**: Revisit after Sprint 4 based on user feedback

---

## 🚀 Deployment Strategy

### Phase 1: Admin UI (Week 1)

**Deploy**: Rolling deployment (no downtime)
**Feature Flags**: None needed (isolated admin features)
**Rollback**: Revert PR if issues detected
**Monitoring**: Admin usage metrics (Grafana)

```bash
# Deploy sequence
1. Merge PRs: #3928, #3931, #3932, #3933, #3929, #3930
2. Deploy frontend (Next.js)
3. Verify admin features
4. Monitor for 24h
```

---

### Phase 2: Dashboard Hub MVP (Week 2)

**Deploy**: Feature flag enabled (gradual rollout)
**Feature Flags**: `ENABLE_DASHBOARD_HUB_MVP=true`
**Rollback**: Toggle feature flag off
**Monitoring**: Performance metrics, error rates

```bash
# Deploy sequence
1. Merge backend PRs: #3907, #3908, #3909
2. Deploy backend API
3. Merge frontend PRs: #3912, #3913, #3911, #3914, #3915
4. Deploy frontend with flag OFF
5. Enable flag for 10% users (canary)
6. Monitor for 48h
7. Enable for 50% users
8. Monitor for 24h
9. Enable for 100% users
```

---

### Phase 3: AI Insights (Week 3)

**Deploy**: Feature flag enabled + fallback tested
**Feature Flags**: `ENABLE_AI_INSIGHTS=true`
**Rollback**: Toggle flag off, fallback to rule-based
**Monitoring**: RAG service health, recommendation accuracy

```bash
# Deploy sequence
1. Verify RAG service health
2. Merge backend PRs: #3916, #3917, #3918
3. Deploy backend API
4. Merge frontend PRs: #3920, #3921, #3919
5. Deploy frontend with flag OFF
6. Enable flag for 10% users
7. Monitor RAG latency, accuracy
8. Gradual rollout to 100%
```

---

### Phase 4: Gamification (Week 3-4)

**Deploy**: Feature flag enabled
**Feature Flags**: `ENABLE_GAMIFICATION=true`
**Rollback**: Toggle flag off
**Monitoring**: Achievement calculation performance

```bash
# Deploy sequence
1. Merge backend PRs: #3922, #3923
2. Deploy backend + background jobs
3. Verify Hangfire job scheduled
4. Merge frontend PRs: #3924, #3925
5. Deploy frontend with flag OFF
6. Enable flag for 20% users
7. Monitor achievement calculation time
8. Gradual rollout to 100%
```

---

## 📝 Documentation Requirements

### Per Issue

**During Development**:
- [ ] API documentation (Scalar) for backend issues
- [ ] Component documentation (Storybook) for frontend issues
- [ ] README updates if new patterns introduced

**Before Merge**:
- [ ] PR description with screenshots/videos
- [ ] Migration guide if breaking changes
- [ ] ADR if architectural decisions made

---

### Epic-Level Documentation

**Epic #3927** (Admin UI):
```
📄 docs/admin/admin-ui-features.md
- List of all admin features
- Screenshot gallery
- User guide for admins
```

**Epic #3901** (Dashboard Hub):
```
📄 docs/frontend/dashboard-hub-guide.md
- Architecture overview
- Component catalog
- Performance optimization notes
- Migration from UserDashboard.tsx
```

**Epic #3905** (AI Insights):
```
📄 docs/ai/ai-insights-integration.md
- RAG integration architecture
- Recommendation algorithm explanation
- Graceful degradation strategy
- Accuracy measurement methodology
```

**Epic #3906** (Gamification):
```
📄 docs/gamification/achievement-system.md
- Achievement types catalog
- Badge design guidelines
- Background job architecture
- User progression formulas
```

---

### Post-Sprint Documentation

**Week 4**:
```
📄 docs/planning/sprint-1-retrospective.md
- What went well
- What could improve
- Lessons learned
- Metrics achieved

📄 docs/planning/backlog-prioritization.md
- Sprint 5-8 roadmap
- Epic #3490 breakdown
- Resource allocation plan
```

---

## ✅ Definition of Done

### Per Issue

**Code**:
- [ ] Feature implemented as specified
- [ ] Code reviewed and approved
- [ ] No linter errors
- [ ] No TypeScript errors

**Tests**:
- [ ] Unit tests written (>85% coverage)
- [ ] Integration tests passing (backend)
- [ ] E2E tests passing (frontend)
- [ ] Manual QA performed

**Documentation**:
- [ ] API documented (Scalar)
- [ ] Component documented (comments)
- [ ] PR description complete

**Deployment**:
- [ ] Merged to parent branch
- [ ] Deployed to staging
- [ ] Verified in staging
- [ ] Ready for production

---

### Per Epic

**Functionality**:
- [ ] All sub-issues completed
- [ ] Success criteria met
- [ ] Performance targets achieved
- [ ] No critical bugs

**Quality**:
- [ ] Test coverage >85%
- [ ] Lighthouse Performance >90 (frontend)
- [ ] Accessibility score >90 (frontend)
- [ ] Security scan passed

**Documentation**:
- [ ] Epic documentation complete
- [ ] User guide written
- [ ] Migration guide (if needed)
- [ ] ADRs documented

**Business**:
- [ ] User acceptance criteria met
- [ ] Engagement metrics tracked
- [ ] Stakeholder approval
- [ ] Ready for user rollout

---

## 🎉 Success Summary

### Immediate Value (Week 1-2)

✅ **6 admin UI features** operational
✅ **Dashboard Hub MVP** live
✅ **Zero technical debt** remaining
✅ **Performance targets** achieved

### Advanced Value (Week 3-4)

✅ **AI-powered recommendations** integrated
✅ **Gamification system** active
✅ **Complete test coverage** (>85%)
✅ **Production ready** all features

### Backlog Clarity (71 issues)

✅ **Sprint 5-8 roadmap** defined
✅ **Epic prioritization** clear
✅ **Resource plan** documented
✅ **Business alignment** achieved

---

**Total**: 28 issues resolved, 71 backlogged, 3-4 weeks timeline, 44% time savings

🚀 Ready to execute!
