# Epic: Dashboard Hub Core (MVP)

**Epic ID**: EPIC-DH-001
**Priority**: P0 - Critical
**Status**: Planning
**Target Sprint**: N+1 to N+3
**Estimated Effort**: 21 story points

---

## 📋 Epic Summary

Trasformare la dashboard post-login attuale in un **hub riassuntivo multi-sezione** che fornisce overview rapida di collezione, sessioni, chat AI e collegamenti a pagine dedicate.

### Goals
- ✅ Fornire snapshot immediato dello stato utente (collezione, partite, chat)
- ✅ Collegamenti espliciti a pagine specializzate (`/library`, `/sessions`, `/chat`)
- ✅ Performance ottimizzata (< 1.5s load time con API aggregata)
- ✅ Responsive design mobile-first
- ✅ Backward compatibility con componenti esistenti

### Non-Goals (Out of Scope)
- ❌ AI insights e raccomandazioni (Epic 2)
- ❌ Gamification system (Epic 3)
- ❌ Dashboard customization (Epic 3)
- ❌ Wishlist management (Epic 2)

---

## 🎯 Success Criteria

### User Experience
- [ ] User può vedere snapshot collezione (top 3 giochi + quota) in < 2s
- [ ] User può continuare sessioni attive con 1 click da dashboard
- [ ] User può navigare a pagine dedicate da ogni sezione (CTA chiari)
- [ ] Dashboard è completamente funzionale su mobile (< 640px)

### Technical
- [ ] Aggregated API `/api/v1/dashboard` risponde in < 500ms (cached)
- [ ] Lighthouse Performance Score > 90
- [ ] Test coverage > 85% (unit + integration)
- [ ] Zero breaking changes su componenti esistenti

### Business
- [ ] Click-through rate dashboard → library > 40%
- [ ] Time on dashboard > 2 minutes (up from 1min)
- [ ] Mobile bounce rate < 15%

---

## 📦 Issues Breakdown

### Backend (2 issues - 5 SP)

#### Issue #1: Dashboard Aggregated API Endpoint
**Story Points**: 3
**Assignee**: Backend Team
**Dependencies**: None

**Description**:
Creare endpoint REST che aggrega dati da più Bounded Contexts per popolare la dashboard hub.

**Acceptance Criteria**:
- [ ] `GET /api/v1/dashboard` restituisce `DashboardData` completo
- [ ] Response include: user, stats, activeSessions, librarySnapshot, recentActivity, chatHistory
- [ ] Query ottimizzate (max 6 DB queries con JOIN)
- [ ] Redis caching con TTL 5 minuti
- [ ] Response time < 500ms (99th percentile)
- [ ] OpenAPI spec aggiornato

**Technical Notes**:
```csharp
// Query Request
GET /api/v1/dashboard

// Query Response
{
  "user": { "id": "uuid", "username": "string", "email": "string" },
  "stats": {
    "libraryCount": 127,
    "playedLast30Days": 23,
    "chatCount": 12,
    "wishlistCount": 15,
    "currentStreak": 7
  },
  "activeSessions": [...],
  "librarySnapshot": {
    "quota": { "used": 127, "total": 200 },
    "topGames": [...]
  },
  "recentActivity": [...],
  "chatHistory": [...]
}
```

**Bounded Contexts Involved**:
- UserLibrary: libraryCount, quota, topGames
- GameManagement: activeSessions, recentActivity
- KnowledgeBase: chatHistory, chatCount
- (Future) UserNotifications: wishlistCount

---

#### Issue #2: Activity Timeline Aggregation Service
**Story Points**: 2
**Assignee**: Backend Team
**Dependencies**: #1

**Description**:
Creare servizio che aggrega eventi da multiple sorgenti per costruire timeline attività utente.

**Acceptance Criteria**:
- [ ] Aggrega eventi da: Library (giochi aggiunti), Sessions (partite), Chat (conversazioni), Wishlist (updates)
- [ ] Eventi ordinati cronologicamente (DESC)
- [ ] Limita a ultimi 10 eventi
- [ ] Include metadata: tipo evento, timestamp, entità correlata
- [ ] Performance: < 200ms per aggregazione

**Event Types**:
```typescript
type ActivityEvent =
  | { type: 'game_added', gameId: string, gameName: string }
  | { type: 'session_completed', sessionId: string, gameName: string }
  | { type: 'chat_saved', chatId: string, topic: string }
  | { type: 'wishlist_added', gameId: string, gameName: string };
```

---

### Frontend (5 issues - 13 SP)

#### Issue #3: Dashboard Hub Layout Refactoring
**Story Points**: 3
**Assignee**: Frontend Team
**Dependencies**: #1 (API available)

**Description**:
Refactoring layout principale dashboard da "recent games focus" a "multi-section hub".

**Acceptance Criteria**:
- [ ] Layout usa `DashboardData` da aggregated API
- [ ] Sezioni visibili: Hero Stats, Active Sessions, Library Snapshot, Activity Feed, Chat History, Quick Actions
- [ ] Responsive: 1-col mobile, 2-col tablet, 3-col asymmetric desktop
- [ ] Skeleton loading states per tutte le sezioni
- [ ] Error boundaries per fallimenti parziali (se 1 sezione fallisce, altre funzionano)

**File Changes**:
- `apps/web/src/app/(public)/dashboard/page.tsx` (refactor)
- `apps/web/src/app/(public)/dashboard/dashboard-hub.tsx` (new component)
- `apps/web/src/app/(public)/dashboard/layout.tsx` (update if needed)

**Technical Notes**:
```tsx
// TanStack Query hook
const { data, isLoading } = useQuery({
  queryKey: ['dashboard'],
  queryFn: fetchDashboardData,
  staleTime: 5 * 60 * 1000, // 5 min
});

// Layout structure
<DashboardHub>
  <HeroSection stats={data.stats} />
  <ActiveSessionsWidget sessions={data.activeSessions} />
  <TwoColumnLayout>
    <LibrarySnapshot snapshot={data.librarySnapshot} />
    <ActivityFeed activities={data.recentActivity} />
  </TwoColumnLayout>
  <ChatHistory threads={data.chatHistory} />
  <QuickActionsGrid />
</DashboardHub>
```

---

#### Issue #4: Enhanced Activity Feed Timeline Component
**Story Points**: 3
**Assignee**: Frontend Team
**Dependencies**: #2 (Backend activity service)

**Description**:
Creare componente timeline che visualizza gli ultimi eventi dell'utente con icone, timestamp e collegamenti.

**Acceptance Criteria**:
- [ ] Visualizza ultimi 10 eventi cronologici
- [ ] Icone distintive per tipo evento (📚 game_added, 🎲 session, 💬 chat, ⭐ wishlist)
- [ ] Timestamp relativo (es. "Oggi 15:00", "Ieri", "3 giorni fa")
- [ ] Link cliccabili a entità correlate (es. click gioco → `/library`, click chat → `/chat/{id}`)
- [ ] Empty state: "Nessuna attività recente" con CTA "Inizia a giocare"
- [ ] Skeleton loading (3 rows pulsing)

**Component API**:
```tsx
interface ActivityFeedProps {
  activities: Activity[];
  isLoading?: boolean;
  maxItems?: number; // default 10
}

<ActivityFeed
  activities={data.recentActivity}
  isLoading={isLoading}
/>
```

**Design Reference**:
- [GitHub Activity Feed](https://github.com) - Inspiration for timeline UI
- Tailwind + Lucide icons
- Hover: Background highlight con smooth transition

---

#### Issue #5: Library Snapshot Component
**Story Points**: 2
**Assignee**: Frontend Team
**Dependencies**: #1

**Description**:
Componente compatto che mostra quota collezione + top 3 giochi più giocati con link a `/library`.

**Acceptance Criteria**:
- [ ] Progress bar quota: "127/200 (64%)" con colori: < 50% green, 50-80% amber, > 80% red
- [ ] Top 3 giochi con: cover thumbnail, titolo, rating stars, play count
- [ ] CTA principale: "Vedi Collezione Completa" → `/library`
- [ ] Empty state: "Nessun gioco nella collezione" + CTA "Aggiungi primo gioco"
- [ ] Responsive: 1-col stack mobile, 3-col grid desktop

**Component API**:
```tsx
interface LibrarySnapshotProps {
  quota: { used: number; total: number };
  topGames: Array<{
    id: string;
    title: string;
    coverUrl: string;
    rating: number;
    playCount: number;
  }>;
}
```

**Design**:
- Card with white background, rounded-2xl, shadow-lg
- Progress bar: Tailwind gradient (green → amber → red)
- Game cards: Hover effect (lift y: -2px)

---

#### Issue #6: Quick Actions Grid Enhancement
**Story Points**: 2
**Assignee**: Frontend Team
**Dependencies**: None (uses existing QuickActions)

**Description**:
Potenziare il QuickActions esistente con 5 azioni principali e tracking analytics.

**Acceptance Criteria**:
- [ ] 5 azioni visibili: Vai a Collezione, Nuova Sessione, Chat AI, Esplora Catalogo, Impostazioni
- [ ] Icon + label per ogni azione (Lucide icons)
- [ ] Grid responsive: 2-col mobile, 5-col desktop
- [ ] Hover: Scale 1.05 + shadow intensify
- [ ] Analytics tracking: Click event con `action_name` property

**Actions Map**:
```tsx
const quickActions = [
  { icon: Library, label: 'Collezione', href: '/library', color: 'amber' },
  { icon: Gamepad2, label: 'Nuova Sessione', href: '/sessions/new', color: 'emerald' },
  { icon: MessageSquare, label: 'Chat AI', href: '/chat', color: 'blue' },
  { icon: Search, label: 'Catalogo', href: '/games/catalog', color: 'purple' },
  { icon: Settings, label: 'Impostazioni', href: '/settings', color: 'stone' },
];
```

---

#### Issue #7: Responsive Layout Mobile/Desktop
**Story Points**: 3
**Assignee**: Frontend Team
**Dependencies**: #3, #4, #5, #6

**Description**:
Implementare layout responsive con breakpoint optimization e mobile-first approach.

**Acceptance Criteria**:
- [ ] Mobile (< 640px): Single column layout, collapsible sections
- [ ] Tablet (640-1024px): 2-column layout, sidebar drawer
- [ ] Desktop (> 1024px): 3-column asymmetric layout
- [ ] Smooth transitions tra breakpoints (Framer Motion)
- [ ] Touch-friendly targets (min 44x44px) su mobile
- [ ] Keyboard navigation completa (tab order corretto)

**Breakpoint Strategy**:
```tsx
// Tailwind classes
<div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
  <section className="md:col-span-2 lg:col-span-1">...</section>
  <aside className="md:col-span-1 lg:col-span-2">...</aside>
</div>
```

**Mobile Optimizations**:
- Lazy load below-fold sections (Intersection Observer)
- Reduce animation complexity (prefers-reduced-motion)
- Collapsible sections (Accordion pattern)

---

### Testing (1 issue - 3 SP)

#### Issue #8: Dashboard Hub Integration & E2E Tests
**Story Points**: 3
**Assignee**: QA + Frontend Team
**Dependencies**: #3, #4, #5, #6, #7

**Description**:
Test suite completa per dashboard hub (unit, integration, E2E, visual regression).

**Acceptance Criteria**:
- [ ] **Unit tests** (Vitest): Tutti i nuovi componenti > 85% coverage
- [ ] **Integration tests**: Dashboard data flow (API → components)
- [ ] **E2E tests** (Playwright): 5 critical user flows
- [ ] **Visual regression** (Chromatic): Snapshots desktop/mobile
- [ ] **Accessibility tests**: WCAG AA compliance (axe-core)

**E2E User Flows**:
1. User login → Dashboard loads → Stats visible → Click "Collezione" → Library page loads
2. User sees active session → Click "Continua" → Session page loads with correct state
3. User clicks activity event → Navigates to linked entity (game/chat/session)
4. User uses quick action → Navigates to correct page
5. Mobile: User scrolls dashboard → All sections load correctly

**Visual Regression**:
- Dashboard loading state (skeleton)
- Dashboard populated state (all sections with data)
- Dashboard empty states (no games, no sessions, no activity)
- Dashboard error state (API failure)
- Mobile viewport (375px)
- Tablet viewport (768px)
- Desktop viewport (1440px)

---

## 🔗 Dependencies

### External Dependencies
- ✅ Active Sessions Widget (Issue #2617) - Already implemented
- ✅ Library Quota Section (Issue #2445) - Already implemented
- ✅ Chat History Section (Existing component)
- ✅ Greeting Section (Existing component)

### Internal Dependencies (Within Epic)
```
Issue #1 (Backend API)
  ↓
Issue #3 (Layout Refactoring)
  ↓
Issue #4, #5, #6 (Components) - Can run in parallel
  ↓
Issue #7 (Responsive Layout)
  ↓
Issue #8 (Testing)

Issue #2 (Activity Service)
  ↓
Issue #4 (Activity Feed Component)
```

---

## 🚀 Rollout Plan

### Sprint N+1 (Week 1-2)
- Backend: #1 Dashboard API + #2 Activity Service
- Frontend: Start #3 Layout Refactoring

### Sprint N+2 (Week 3-4)
- Frontend: Complete #3, #4, #5, #6 (component development)
- QA: Start testing

### Sprint N+3 (Week 5-6)
- Frontend: #7 Responsive optimization
- QA: #8 Full test suite
- Deploy to staging with feature flag

---

## 📊 Metrics & KPIs

### Development Metrics
- Code coverage: Target > 85%
- API response time: Target < 500ms (p99)
- Bundle size increase: Target < 50KB
- Lighthouse score: Target > 90

### User Metrics (Post-Launch)
- Dashboard load time: Target < 1.5s
- Click-through rate (stats → pages): Target > 40%
- Time on dashboard: Target > 2 minutes
- Mobile bounce rate: Target < 15%

---

## 🚨 Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| API performance degrades with aggregation | High | Redis caching, query optimization, pagination |
| Breaking changes to existing components | Medium | Feature flag, gradual rollout, backward compatibility |
| Mobile performance issues (too many sections) | Medium | Lazy loading, virtualization, collapsible sections |
| Delay in backend API delivery | High | Frontend mock data, parallel development |

---

## ✅ Definition of Done

Epic is considered complete when:
- [ ] All 8 issues closed and merged to `main-dev`
- [ ] Test coverage > 85% (unit + integration)
- [ ] E2E tests passing on CI/CD
- [ ] Lighthouse Performance > 90, Accessibility > 95
- [ ] Feature flag enabled for 10% users (staging)
- [ ] No critical bugs reported in 48h monitoring
- [ ] Documentation updated (API spec, component docs)
- [ ] Stakeholder approval after demo

---

## 📚 Reference Materials

- [Implementation Plan](../dashboard-hub-implementation-plan.md)
- [Dashboard Hub Spec](../dashboard-overview-hub.md)
- [Current Dashboard](../../apps/web/src/app/(public)/dashboard/page.tsx)
- [Figma Mockups](#) - TBD
- [API Contract](#) - TBD

---

**Epic Owner**: Frontend Team Lead
**Last Updated**: 2026-01-21
**Next Review**: Sprint Planning N+1
