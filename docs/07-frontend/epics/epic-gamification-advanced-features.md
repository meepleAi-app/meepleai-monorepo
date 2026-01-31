# Epic: Gamification & Advanced Features

**Epic ID**: EPIC-DH-003
**Priority**: P2 - Medium
**Status**: Planning
**Target Sprint**: N+5 to N+6
**Estimated Effort**: 8 story points

---

## 📋 Epic Summary

Sistema di gamification con achievements, badges, progressione utente e funzionalità avanzate dashboard (customization, advanced timeline).

### Goals
- ✅ Sistema achievement automatico (streak, milestone, usage)
- ✅ Badge visibili con progressione e rarità
- ✅ User level basato su engagement metrics
- ✅ Advanced activity timeline con filtri e search
- ✅ Dashboard customization (riordino sezioni - optional)

### Non-Goals (Out of Scope)
- ❌ Leaderboard globale / ranking community (future)
- ❌ Rewards tangibili (punti, premi, sconti)
- ❌ Social achievements (condivisione badge)
- ❌ Competizioni / sfide tra utenti

---

## 🎯 Success Criteria

### User Experience
- [ ] User può vedere achievements sbloccati con progressione chiara
- [ ] Badge hanno animazioni di unlock (celebrazione)
- [ ] Activity timeline è filtrabile per tipo evento
- [ ] User comprende come sbloccare nuovi achievements

### Technical
- [ ] Achievement engine valuta condizioni in background (cron + event-driven)
- [ ] Badge unlock notifiche real-time (optional WebSocket)
- [ ] Timeline advanced query performance < 500ms
- [ ] Storage achievement state < 1KB per user

### Business
- [ ] Achievement engagement > 40% (user apre badge panel)
- [ ] Streak retention +15% (gamification effect)
- [ ] Advanced timeline usage > 20% users

---

## 📦 Issues Breakdown

### Backend (2 issues - 4 SP)

#### Issue #15: Achievement System & Badge Engine
**Story Points**: 3
**Assignee**: Backend Team
**Dependencies**: None

**Description**:
Sistema automatico che valuta condizioni achievements e assegna badge agli utenti.

**Acceptance Criteria**:
- [ ] Achievement engine con rule evaluation (event-driven + daily cron)
- [ ] Endpoint `GET /api/v1/achievements` - Lista tutti achievements (locked/unlocked)
- [ ] Endpoint `GET /api/v1/achievements/recent` - Ultimi 3 sbloccati
- [ ] Badge categories: Collezione, Gioco, Chat AI, Streak, Milestone
- [ ] Notification system: Push notification quando badge unlocked (optional)
- [ ] Admin panel: Creazione/modifica achievements via dashboard

**Achievement Examples**:
```typescript
const achievements = [
  {
    id: 'streak_7',
    title: 'Giocatore Costante',
    description: '7 giorni consecutivi di partite',
    icon: '🔥',
    category: 'Streak',
    rarity: 'common',
    condition: 'user.currentStreak >= 7',
  },
  {
    id: 'collector_100',
    title: 'Collezionista',
    description: '100+ giochi nella libreria',
    icon: '📚',
    category: 'Collezione',
    rarity: 'rare',
    condition: 'user.libraryCount >= 100',
  },
  {
    id: 'ai_expert_50',
    title: 'Esperto AI',
    description: '50+ chat completate',
    icon: '🤖',
    category: 'Chat',
    rarity: 'epic',
    condition: 'user.chatCount >= 50',
  },
];
```

**Database Schema** (Administration BC):
```sql
CREATE TABLE Achievements (
  Id UUID PRIMARY KEY,
  Title VARCHAR(100) NOT NULL,
  Description VARCHAR(500),
  Icon VARCHAR(10),
  Category VARCHAR(50),
  Rarity VARCHAR(20), -- common, rare, epic, legendary
  ConditionJson TEXT NOT NULL, -- JSON rule for evaluation
  CreatedAt TIMESTAMP
);

CREATE TABLE UserAchievements (
  UserId UUID NOT NULL,
  AchievementId UUID NOT NULL,
  UnlockedAt TIMESTAMP NOT NULL,
  PRIMARY KEY (UserId, AchievementId)
);
```

**Background Job** (Hangfire/Quartz):
```csharp
// Daily cron: Evaluate all achievement conditions for all users
public async Task EvaluateAchievements(CancellationToken ct) {
  var users = await GetActiveUsers();
  foreach (var user in users) {
    var userStats = await GetUserStats(user.Id);
    var newAchievements = await CheckAchievements(user, userStats);
    if (newAchievements.Any()) {
      await UnlockAchievements(user.Id, newAchievements);
      await SendNotification(user.Id, newAchievements);
    }
  }
}
```

---

#### Issue #16: Advanced Activity Timeline Query Service
**Story Points**: 1
**Assignee**: Backend Team
**Dependencies**: Epic 1 Issue #2 (Basic activity service)

**Description**:
Potenziamento servizio activity con filtri, search e paginazione per timeline avanzata.

**Acceptance Criteria**:
- [ ] Endpoint `GET /api/v1/activity/timeline` con query params:
  - `type`: Filter by event type (game_added, session, chat, wishlist)
  - `search`: Full-text search su nomi giochi/chat
  - `dateFrom`, `dateTo`: Date range filter
  - `page`, `pageSize`: Pagination (default 20 items)
- [ ] Performance: < 500ms con filtri attivi
- [ ] Index database su: UserId, EventType, Timestamp
- [ ] Response include total count per pagination UI

**Query Example**:
```
GET /api/v1/activity/timeline?type=game_added&dateFrom=2026-01-01&page=1&pageSize=20
```

---

### Frontend (2 issues - 4 SP)

#### Issue #17: Achievements Widget Component
**Story Points**: 2
**Assignee**: Frontend Team
**Dependencies**: #15 (Backend achievement system)

**Description**:
Widget dashboard che mostra ultimi 3 achievements sbloccati con animazione celebrativa.

**Acceptance Criteria**:
- [ ] Visualizza ultimi 3 achievements con: icon, title, description, unlock date
- [ ] Badge rarity: Visual distinction (common: gray, rare: blue, epic: purple, legendary: gold)
- [ ] Animation unlock: Confetti effect + scale pulse (Framer Motion)
- [ ] CTA: "Vedi Tutti i Badge →" → `/achievements` (pagina dedicata)
- [ ] Empty state: "Nessun achievement sbloccato" + progress verso prossimo
- [ ] Tooltip: Hover badge → mostra come sbloccare (se locked)

**Component API**:
```tsx
interface AchievementsWidgetProps {
  achievements: Achievement[];
  totalCount: number;
  onViewAll?: () => void;
}

<AchievementsWidget
  achievements={recentAchievements}
  totalCount={user.achievementCount}
/>
```

**Design**:
- Card layout: Horizontal 3-col grid (1-col mobile)
- Rarity colors: `common: stone`, `rare: blue`, `epic: purple`, `legendary: amber`
- Icon: Large emoji (32px) + badge outline
- Animation: Framer Motion `whileHover={{ scale: 1.05 }}`

---

#### Issue #18: Advanced Activity Timeline with Filters
**Story Points**: 2
**Assignee**: Frontend Team
**Dependencies**: #16 (Backend advanced query)

**Description**:
Componente timeline avanzato con filtri tipo evento, search full-text e paginazione.

**Acceptance Criteria**:
- [ ] Filter bar: Checkboxes per event type (Giochi, Sessioni, Chat, Wishlist)
- [ ] Search input: Full-text search su nomi (debounced 300ms)
- [ ] Date range picker (optional): "Ultima settimana", "Ultimo mese", "Custom range"
- [ ] Pagination: "Carica altri" button (infinite scroll optional)
- [ ] Active filter badges: Visual indicator con count
- [ ] Reset filters button

**Component API**:
```tsx
interface AdvancedActivityTimelineProps {
  initialActivities: Activity[];
  filters?: ActivityFilters;
  onFilterChange?: (filters: ActivityFilters) => void;
}

<AdvancedActivityTimeline
  initialActivities={data.activities}
  filters={filterState}
  onFilterChange={setFilterState}
/>
```

**Design**:
- Filter bar: Sticky top on scroll (z-10)
- Pills/badges for active filters
- Search: Icon left + clear button right
- Smooth filter transitions (Framer Motion AnimatePresence)

---

## 🔗 Dependencies

### External Dependencies
- ✅ Epic 1 completed (Dashboard Hub layout ready)
- ✅ Notification system available (UserNotifications BC)

### Internal Dependencies (Within Epic)
```
Issue #15 (Achievement Engine)
  ↓
Issue #17 (Achievements Widget)

Issue #16 (Advanced Timeline Service)
  ↓
Issue #18 (Advanced Timeline Component)
```

---

## 🚀 Rollout Plan

### Sprint N+5 (Week 11-12)
- Backend: #15 Achievement System + #16 Advanced Timeline
- Define achievement ruleset with product team

### Sprint N+6 (Week 13-14)
- Frontend: #17 Achievements Widget + #18 Advanced Timeline
- QA: Testing + validation
- Deploy to staging

### Sprint N+7 (Week 15) - Polish
- User feedback collection
- Animation tuning
- Performance optimization
- Production deploy with feature flag (50% users)

---

## 📊 Metrics & KPIs

### Development Metrics
- Achievement evaluation job: < 5 min (daily)
- Timeline query with filters: < 500ms
- Widget render time: < 100ms
- Code coverage: > 75%

### User Metrics (Post-Launch)
- Achievement panel open rate: > 40%
- Streak retention: +15% (compared to pre-gamification)
- Advanced timeline usage: > 20%
- Time on dashboard: +30% (gamification engagement)

---

## 🚨 Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| Achievement fatigue (too many notifications) | Medium | Limit notifications, user preferences for notification frequency |
| Low achievement engagement | Medium | A/B test different badge designs, user research on motivation |
| Timeline performance degradation (large datasets) | Medium | Pagination, indexes, query optimization |
| Scope creep (feature requests) | High | Strict epic boundaries, defer advanced customization to Epic 4 |

---

## ✅ Definition of Done

Epic is considered complete when:
- [ ] All 4 issues closed and merged to `main-dev`
- [ ] Achievement system running in production (background job)
- [ ] Test coverage > 75%
- [ ] User testing completed with > 70% satisfaction
- [ ] Performance metrics met (job < 5min, queries < 500ms)
- [ ] Feature flag enabled for 50% users
- [ ] Analytics dashboard shows engagement data
- [ ] Documentation updated (achievement rules, API spec)

---

## 🔮 Future Enhancements (Epic 4 - TBD)

- **Social Gamification**: Leaderboard, share achievements, compare with friends
- **Advanced Customization**: Drag-drop dashboard sections, hide/show widgets
- **Achievement Store**: Unlock special features with achievement points
- **Seasonal Events**: Time-limited achievements (holidays, special events)

---

**Epic Owner**: Product Team + Frontend Lead
**Last Updated**: 2026-01-21
**Next Review**: Sprint Planning N+5
