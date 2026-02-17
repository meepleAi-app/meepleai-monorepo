# Dashboard Hub - Quick Reference

**Quick reference guide for Dashboard Hub development**

---

## 🚀 Quick Start

```bash
# Start API + Web
cd apps/api/src/Api && dotnet run  # :8080
cd apps/web && pnpm dev            # :3000
```

---

## 📍 File Locations

### Backend
```
apps/api/src/Api/BoundedContexts/
├── UserLibrary/        # Collezione, quota, top games
├── GameManagement/     # Sessioni attive, recent activity
├── KnowledgeBase/      # Chat history
└── (Future) UserNotifications/  # Wishlist
```

### Frontend
```
apps/web/src/
├── app/(authenticated)/dashboard/
│   ├── page.tsx                 # Main dashboard route
│   ├── dashboard-hub.tsx        # Hub layout component
│   └── _components/
│       ├── hero-section.tsx
│       ├── library-snapshot.tsx
│       ├── activity-feed.tsx
│       └── quick-actions.tsx
├── components/dashboard/
│   └── (legacy components - to be removed)
└── lib/api/
    └── dashboard-client.ts      # API client
```

---

## 🔌 API Endpoints

### Aggregated Dashboard Data
```typescript
GET /api/v1/dashboard
Response: {
  user: User;
  stats: {
    libraryCount: number;
    playedLast30Days: number;
    chatCount: number;
    wishlistCount: number;
    currentStreak: number;
  };
  activeSessions: GameSession[];
  librarySnapshot: {
    quota: { used: number; total: number };
    topGames: Game[];
  };
  recentActivity: Activity[];
  chatHistory: ChatThread[];
}
```

**Cache**: Redis, TTL 5 minutes
**Performance**: < 500ms (p99)

---

## 🧩 Component Structure

### Main Layout
```tsx
<DashboardHub>
  {/* Hero + Stats */}
  <HeroSection stats={data.stats} />

  {/* Active Sessions */}
  <ActiveSessionsWidget sessions={data.activeSessions} />

  {/* Two Column Layout */}
  <TwoColumnLayout>
    <LibrarySnapshot snapshot={data.librarySnapshot} />
    <ActivityFeed activities={data.recentActivity} />
  </TwoColumnLayout>

  {/* Chat + Actions */}
  <ChatHistory threads={data.chatHistory} />
  <QuickActionsGrid />
</DashboardHub>
```

### Data Fetching
```tsx
// TanStack Query hook
const { data, isLoading, error } = useQuery({
  queryKey: ['dashboard'],
  queryFn: fetchDashboardData,
  staleTime: 5 * 60 * 1000, // 5 min
  retry: 2,
});
```

---

## 🎨 Responsive Breakpoints

| Breakpoint | Width | Layout | Cols |
|------------|-------|--------|------|
| **Mobile** | < 640px | Stack | 1-col |
| **Tablet** | 640-1024px | Split | 2-col |
| **Desktop** | > 1024px | Asymmetric | 3-col |

```tsx
// Tailwind example
<div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
  <section className="md:col-span-2 lg:col-span-1">...</section>
  <aside className="md:col-span-1 lg:col-span-2">...</aside>
</div>
```

---

## 🎨 Color Coding

| Section | Color | Tailwind |
|---------|-------|----------|
| 📚 Collezione | Amber/Orange | `amber-500` |
| 🎲 Sessioni | Emerald/Green | `emerald-500` |
| 💬 Chat AI | Blue | `blue-500` |
| ⭐ Wishlist | Purple | `purple-500` |
| 💡 Insights | Yellow | `yellow-500` |
| 🏆 Achievements | Gold | `yellow-600` |

---

## 📊 Success Criteria Checklist

### User Experience
- [ ] Snapshot collezione visible in < 2s
- [ ] 1-click session continuation
- [ ] Clear CTA for dedicated pages
- [ ] Mobile fully functional (< 640px)

### Technical
- [ ] API response < 500ms (cached)
- [ ] Lighthouse Performance > 90
- [ ] Test coverage > 85%
- [ ] Zero breaking changes

### Business
- [ ] CTR dashboard → library > 40%
- [ ] Time on dashboard > 2 minutes
- [ ] Mobile bounce rate < 15%

---

## 🧪 Testing Commands

```bash
# Unit tests
cd apps/web
pnpm test
pnpm test:coverage

# E2E tests
pnpm test:e2e

# Type checking
pnpm typecheck

# Lint
pnpm lint
```

---

## 🔗 Navigation Flow

```
Dashboard (/) → Hub centrale
├── /library            # Collezione completa (flip cards)
├── /sessions           # Gestione sessioni
│   ├── /{id}          # Sessione specifica
│   ├── /history       # Storico
│   └── /new           # Nuova sessione
├── /chat              # Chat AI
│   ├── /              # Nuova chat
│   └── /history       # Storico conversazioni
├── /games/catalog     # Catalogo condiviso
├── /wishlist          # Lista desideri
├── /activity          # Timeline completa
├── /achievements      # Badge e gamification
└── /settings          # Preferenze utente
```

---

## 🧹 Legacy Code Cleanup

**Files to Remove** (after migration):
```
✅ Check before deleting:
- apps/web/src/components/dashboard/UserDashboard.tsx (1137 lines)
- apps/web/src/components/dashboard/UserDashboardCompact.tsx
- apps/web/src/app/(authenticated)/dashboard/dashboard-client.tsx (legacy)

⚠️ Validation:
grep -r "UserDashboard" apps/web/src/  # Should return 0 results
grep -r "dashboard-client-legacy" apps/web/src/  # Should return 0 results
```

---

## 🚨 Common Issues

### API Timeout
```bash
# Check API health
curl http://localhost:8080/health

# Check Redis cache
docker exec -it meepleai-redis redis-cli
> KEYS dashboard:*
> TTL dashboard:user:{id}
```

### Slow Dashboard Load
```bash
# Profile API endpoint
curl -w "@curl-format.txt" http://localhost:8080/api/v1/dashboard

# Check DB query performance
# See apps/api logs for EF Core query execution times
```

### Broken Component
```bash
# Verify all dependencies
pnpm install

# Check for type errors
pnpm typecheck

# Run component tests
pnpm test -- --grep "DashboardHub"
```

---

## 📦 Dependencies

### External (Already Implemented)
- ✅ Active Sessions Widget (Issue #2617)
- ✅ Library Quota Section (Issue #2445)
- ✅ Chat History Section (existing)
- ✅ Greeting Section (existing)

### Internal (Epic #3901)
```
Backend API (#1)
  ↓
Layout Refactoring (#3)
  ↓
Components (#4, #5, #6) - Parallel
  ↓
Responsive Layout (#7)
  ↓
Testing (#8)
```

---

## 🎯 Performance Targets

| Metric | Target | Actual |
|--------|--------|--------|
| API response time (p99) | < 500ms | TBD |
| Dashboard load time | < 1.5s | TBD |
| Lighthouse Performance | > 90 | TBD |
| Lighthouse Accessibility | > 95 | TBD |
| Test coverage | > 85% | TBD |
| Bundle size increase | < 50KB | TBD |

---

## 📚 Full Documentation

- [Dashboard Overview Spec](./dashboard-overview-hub.md)
- [Epic Details](./epics/epic-dashboard-hub-core.md)
- [Implementation Plan](./dashboard-hub-implementation-plan.md)
- [Index](./DASHBOARD-HUB-INDEX.md)

---

**Last Updated**: 2026-02-09
**Status**: Planning Phase
**Epic**: #3901
