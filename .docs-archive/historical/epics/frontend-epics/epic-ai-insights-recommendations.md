# Epic: AI Insights & Recommendations

**Epic ID**: EPIC-DH-002
**GitHub Epic**: #3902
**Priority**: P1 - High
**Status**: Planning
**Target Sprint**: N+3 to N+4
**Estimated Effort**: 13 story points

---

## 📋 Epic Summary

Aggiungere intelligenza AI alla dashboard hub tramite insights personalizzati, raccomandazioni di giochi basate su RAG, wishlist management e analytics del catalogo trending.

### Goals
- ✅ Fornire suggerimenti proattivi basati su comportamento utente (backlog alerts, streak maintenance)
- ✅ Raccomandare giochi simili usando embeddings RAG
- ✅ Gestione wishlist integrata con highlights dashboard
- ✅ Mostrare giochi trending nella community (catalog analytics)
- ✅ Graceful degradation se AI service non disponibile

### Non-Goals (Out of Scope)
- ❌ Machine Learning model training (usa modelli pre-esistenti)
- ❌ Real-time recommendations (batch processing ok)
- ❌ Price tracking e alerts (future enhancement)
- ❌ Social features (condivisione wishlist)

---

## 🎯 Success Criteria

### User Experience
- [ ] User vede almeno 3 insights personalizzati rilevanti
- [ ] User può aggiungere giochi a wishlist da catalog con 1 click
- [ ] Catalog trending mostra top 5 giochi con variazione % settimanale
- [ ] AI insights sono cliccabili e portano a azioni concrete

### Technical
- [ ] RAG service integration response time < 1s
- [ ] Insights accuracy > 70% (validated via user engagement)
- [ ] Wishlist API CRUD operations < 300ms
- [ ] Catalog trending aggiornato ogni 24h (cron job)

### Business
- [ ] AI insights engagement > 30% (click-through)
- [ ] Wishlist additions da dashboard > 20% total wishlist actions
- [ ] Trending games click-through > 15%

---

## 📦 Issues Breakdown

### Backend (3 issues - 7 SP)

#### Issue #4308: AI Insights Service (RAG Integration)
**Story Points**: 3
**GitHub Issue**: https://github.com/meepleAi-app/meepleai-monorepo/issues/4308
**Assignee**: Backend Team + AI Team
**Dependencies**: RAG service operational

**Description**:
Servizio che analizza comportamento utente e genera insights personalizzati usando RAG embeddings.

**Acceptance Criteria**:
- [ ] Endpoint `GET /api/v1/dashboard/insights` restituisce array di insights
- [ ] Genera insights per:
  - **Backlog Alert**: Giochi non giocati da 30+ giorni
  - **Rules Reminder**: Regole salvate non riviste da 7+ giorni
  - **Similar Games**: 3 giochi simili ai preferiti (RAG embeddings)
  - **Streak Maintenance**: Nudge per mantenere streak attivo
- [ ] Performance: < 1s response time (RAG query cached)
- [ ] Fallback graceful se RAG service down (empty array, no errors)

**Insight Schema**:
```typescript
interface AIInsight {
  id: string;
  type: 'backlog_alert' | 'rules_reminder' | 'recommendation' | 'streak_nudge';
  title: string;
  description: string;
  actionLabel: string; // es. "Scopri →"
  actionUrl: string;   // es. "/library?filter=unplayed"
  priority: number;    // 1-10 (higher = more important)
  createdAt: Date;
}
```

**RAG Integration**:
```csharp
// Use Qdrant to find similar games
var userTopGames = GetUserTopGames(userId, limit: 5);
var embeddings = await EmbeddingService.GetEmbeddings(userTopGames);
var similarGames = await QdrantClient.Search(embeddings, limit: 3);

return new AIInsight {
  Type = "recommendation",
  Title = "Giochi simili a Catan",
  Description = "Basato sui tuoi giochi preferiti",
  ActionUrl = $"/games/catalog?similar_to={userTopGames[0].Id}"
};
```

---

#### Issue #4309: Wishlist Management API
**Story Points**: 2
**GitHub Issue**: https://github.com/meepleAi-app/meepleai-monorepo/issues/4309
**Assignee**: Backend Team
**Dependencies**: None

**Description**:
CRUD API completo per gestione wishlist utente con priorità e note.

**Acceptance Criteria**:
- [ ] `GET /api/v1/wishlist` - Restituisce wishlist completa con paginazione
- [ ] `POST /api/v1/wishlist` - Aggiunge gioco (body: `{ gameId, priority?, notes? }`)
- [ ] `DELETE /api/v1/wishlist/{gameId}` - Rimuove gioco
- [ ] `PUT /api/v1/wishlist/{gameId}` - Aggiorna priorità/note
- [ ] `GET /api/v1/wishlist/highlights` - Restituisce top 5 wishlist per dashboard
- [ ] Validazione: User non può aggiungere gioco già posseduto
- [ ] Audit log: Track aggiunte/rimozioni wishlist

**Database Schema** (UserLibrary BC):
```sql
CREATE TABLE WishlistItems (
  Id UUID PRIMARY KEY,
  UserId UUID NOT NULL,
  GameId UUID NOT NULL,
  Priority INT DEFAULT 5, -- 1-10
  Notes VARCHAR(500),
  AddedAt TIMESTAMP NOT NULL,
  UNIQUE(UserId, GameId)
);
```

---

#### Issue #4310: Catalog Trending Analytics
**Story Points**: 2
**GitHub Issue**: https://github.com/meepleAi-app/meepleai-monorepo/issues/4310
**Assignee**: Backend Team
**Dependencies**: None

**Description**:
Sistema analytics che traccia popolarità giochi nel catalogo condiviso e genera ranking trending.

**Acceptance Criteria**:
- [ ] Background job (daily cron): Calcola trending score per ogni gioco
- [ ] Metriche tracciate: Aggiunte a libreria, wishlist, ricerche, visualizzazioni
- [ ] Endpoint `GET /api/v1/catalog/trending?period=week` restituisce top 10
- [ ] Response include: game info, trend score, variazione % (es. +15%)
- [ ] Redis cache con TTL 24h
- [ ] Admin panel per vedere analytics dettagliate (optional)

**Trending Score Formula**:
```csharp
TrendingScore = (
  LibraryAdds * 5 +
  WishlistAdds * 3 +
  Searches * 2 +
  Views * 1
) / DaysSinceRelease; // Newer games get boost

PercentageChange = ((ThisWeekScore - LastWeekScore) / LastWeekScore) * 100;
```

---

### Frontend (3 issues - 6 SP)

#### Issue #4311: AI Insights Widget Component
**Story Points**: 2
**GitHub Issue**: https://github.com/meepleAi-app/meepleai-monorepo/issues/4311
**Assignee**: Frontend Team
**Dependencies**: #4308 (Backend AI service)

**Description**:
Widget dashboard che visualizza insights AI con icone distintive e azioni cliccabili.

**Acceptance Criteria**:
- [ ] Visualizza fino a 5 insights ordinati per priority
- [ ] Design: Card con sfondo giallo/amber highlight (attira attenzione)
- [ ] Icone per tipo insight: 🎯 backlog, 📖 rules, 🆕 recommendation, 🔥 streak
- [ ] Click su insight → naviga a `actionUrl`
- [ ] Empty state: "Nessun suggerimento disponibile al momento"
- [ ] Loading state: Skeleton (3 rows pulsing)
- [ ] Error state: Graceful (hide widget se service down)

**Component API**:
```tsx
interface AIInsightsWidgetProps {
  insights: AIInsight[];
  isLoading?: boolean;
  onInsightClick?: (insight: AIInsight) => void; // Analytics tracking
}

<AIInsightsWidget
  insights={data.insights}
  isLoading={isLoading}
  onInsightClick={(insight) => trackEvent('insight_clicked', { type: insight.type })}
/>
```

**Design Specs**:
- Background: `bg-gradient-to-br from-amber-50 to-yellow-50`
- Border: `border-2 border-amber-200`
- Icon size: 24x24px (Lucide icons)
- Hover: Scale 1.02 + shadow intensify

---

#### Issue #4312: Wishlist Highlights Component
**Story Points**: 2
**GitHub Issue**: https://github.com/meepleAi-app/meepleai-monorepo/issues/4312
**Assignee**: Frontend Team
**Dependencies**: #4309 (Backend wishlist API)

**Description**:
Componente compatto che mostra top 5 wishlist con link a pagina gestione completa.

**Acceptance Criteria**:
- [ ] Lista top 5 giochi wishlist (ordinati per priority)
- [ ] Display: Titolo + priority indicator (⭐⭐⭐⭐⭐)
- [ ] Click gioco → naviga a `/games/{id}` (dettaglio gioco)
- [ ] CTA: "Gestisci Wishlist →" → `/wishlist`
- [ ] Empty state: "Nessun gioco in wishlist" + CTA "Esplora Catalogo"
- [ ] Quick add button (optional): "+" icon per aggiungere da catalog

**Component API**:
```tsx
interface WishlistHighlightsProps {
  games: Array<{
    id: string;
    title: string;
    priority: number; // 1-10
  }>;
  maxItems?: number; // default 5
}
```

**Design**:
- Compact list (no covers, title-only)
- Priority: Filled stars (amber-400) vs empty stars (stone-300)
- Hover: Background highlight

---

#### Issue #4313: Catalog Trending Widget
**Story Points**: 2
**GitHub Issue**: https://github.com/meepleAi-app/meepleai-monorepo/issues/4313
**Assignee**: Frontend Team
**Dependencies**: #4310 (Backend trending analytics)

**Description**:
Widget che mostra giochi trending nel catalogo community con variazione percentuale.

**Acceptance Criteria**:
- [ ] Lista top 3-5 giochi trending (periodo: settimana)
- [ ] Display: Nome gioco + variazione % (es. "+15% 🔥")
- [ ] Colori variazione: Verde (positivo), Rosso (negativo), Grigio (stabile)
- [ ] Click gioco → naviga a `/games/{id}`
- [ ] CTA: "Vedi Catalogo Completo →" → `/games/catalog`
- [ ] Update frequency: Cache 24h (mostra data ultimo update)

**Component API**:
```tsx
interface CatalogTrendingWidgetProps {
  games: Array<{
    id: string;
    title: string;
    trendScore: number;
    percentageChange: number; // +15, -5, 0
  }>;
  period: 'week' | 'month';
}
```

**Design**:
- Numbered list (1, 2, 3...)
- Trend indicator: Arrow up/down + percentage
- Last updated: "Aggiornato 2h fa" (relative time)

---

## 🔗 Dependencies

### External Dependencies
- ✅ RAG Service operational (Qdrant + Embeddings)
- ✅ OpenRouter API (optional for enhanced recommendations)
- ✅ Epic 1 completed (Dashboard Hub layout ready)

### Internal Dependencies (Within Epic)
```
Issue #4308 (AI Insights Service)
  ↓
Issue #4311 (AI Insights Widget)

Issue #4309 (Wishlist API)
  ↓
Issue #4312 (Wishlist Highlights)

Issue #4310 (Trending Analytics)
  ↓
Issue #4313 (Trending Widget)
```

---

## 🚀 Rollout Plan

### Sprint N+3 (Week 7-8)
- Backend: #4308 AI Insights + #4309 Wishlist API
- Frontend: Start component development

### Sprint N+4 (Week 9-10)
- Backend: #4310 Trending Analytics + cron job setup
- Frontend: Complete #4311, #4312, #4313
- QA: Integration testing

### Sprint N+5 (Week 11) - Optional Polish
- A/B testing insights vs. no-insights
- Tuning RAG recommendations accuracy
- Analytics review and optimization

---

## 📊 Metrics & KPIs

### Development Metrics
- RAG query response time: < 1s (p95)
- Wishlist API response time: < 300ms (p99)
- Trending calculation job: < 5 min (daily)
- Code coverage: > 80%

### User Metrics (Post-Launch)
- AI insights engagement: > 30%
- Wishlist additions from dashboard: > 20% of total
- Trending games click-through: > 15%
- RAG recommendations accuracy: > 70% (user adds recommended game)

---

## 🚨 Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| RAG service unavailable | High | Graceful degradation (hide insights widget), fallback to rule-based recommendations |
| Insights not relevant to users | Medium | A/B testing, user feedback loop, tuning priority algorithm |
| Trending analytics inaccurate | Low | Manual validation, admin override, multiple metrics weighting |
| Performance degradation (embedding queries) | Medium | Redis caching, batch processing, query optimization |

---

## ✅ Definition of Done

Epic is considered complete when:
- [ ] All 6 issues closed and merged to `main-dev`
- [ ] RAG recommendations accuracy validated > 70%
- [ ] Wishlist API fully functional with CRUD
- [ ] Trending analytics job running daily successfully
- [ ] Test coverage > 80% (unit + integration)
- [ ] Feature flag enabled for 25% users
- [ ] User feedback collected (survey or analytics)
- [ ] No critical bugs in 72h monitoring

---

## 📚 Reference Materials

- [Implementation Plan](../dashboard-hub-implementation-plan.md)
- [RAG Service Documentation](#) - TBD
- [Qdrant Integration Guide](#) - TBD
- [Wishlist PRD](#) - TBD

---

**Epic Owner**: Backend Team Lead + AI Team
**Created**: 2026-01-21
**Last Updated**: 2026-02-14 (Issues created: #4308-4313)
**Next Review**: Sprint Planning N+3
