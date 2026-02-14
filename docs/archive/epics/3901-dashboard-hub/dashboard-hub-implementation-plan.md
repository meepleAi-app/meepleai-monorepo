# Dashboard Hub - Implementation Plan

**Goal**: Trasformare la dashboard attuale in un hub riassuntivo multi-sezione con collegamenti a pagine specializzate.

**Document Status**: Planning Phase
**Created**: 2026-01-21
**Owner**: Frontend Team

---

## 📋 Overview

### Current State
- Dashboard esistente con sezioni base (Issue #1836)
- Componenti parziali: GreetingSection, ActiveSessionsSection, LibraryQuotaSection, ChatHistorySection
- Focus su recent games grid (6 giochi)

### Target State
- Dashboard hub con 10 sezioni integrate
- Snapshot multi-dominio (Library, Sessions, Chat, Wishlist, Catalog)
- AI-powered insights e suggerimenti personalizzati
- Collegamenti espliciti a pagine dedicate
- Collection Dashboard (flip cards 3D) spostata a `/library`

---

## 🎯 Epic Structure

### Epic 1: Dashboard Hub Core (MVP) - **Phase 1**
**Priority**: P0 - Critical
**Target**: Sprint Current + 1
**Estimated Effort**: 21 story points (3 sprints)

**Scope**:
- Refactoring layout dashboard principale
- Backend API aggregata `/api/v1/dashboard`
- Integrazione componenti esistenti
- Nuove sezioni: Enhanced Activity Feed, Library Snapshot completo
- Responsive design (mobile-first)

**Issues**:
1. #TBD-001: Backend - Dashboard Aggregated API Endpoint
2. #TBD-002: Frontend - Dashboard Hub Layout Refactoring
3. #TBD-003: Frontend - Enhanced Activity Feed Timeline
4. #TBD-004: Frontend - Library Snapshot Component
5. #TBD-005: Frontend - Quick Actions Grid Enhancement
6. #TBD-006: Frontend - Responsive Layout Mobile/Desktop
7. #TBD-007: Testing - Dashboard Hub Integration Tests

---

### Epic 2: AI Insights & Recommendations - **Phase 2**
**Priority**: P1 - High
**Target**: Sprint +2
**Estimated Effort**: 13 story points (2 sprints)

**Scope**:
- AI-powered insights widget
- RAG-based game recommendations
- Backlog alerts (giochi non giocati 30+ giorni)
- Wishlist management + highlights
- Catalog trending analytics

**Issues**:
1. #TBD-008: Backend - AI Insights Service (RAG Integration)
2. #TBD-009: Frontend - AI Insights Widget Component
3. #TBD-010: Backend - Wishlist Management API
4. #TBD-011: Frontend - Wishlist Highlights Component
5. #TBD-012: Backend - Catalog Trending Analytics
6. #TBD-013: Frontend - Catalog Trending Widget

---

### Epic 3: Gamification & Advanced Features - **Phase 3**
**Priority**: P2 - Medium
**Target**: Sprint +4
**Estimated Effort**: 8 story points (1-2 sprints)

**Scope**:
- Achievement system (badges, streaks, milestones)
- User level progression
- Advanced activity timeline (filters, search)
- Personalized dashboard customization

**Issues**:
1. #TBD-014: Backend - Achievement System & Badge Engine
2. #TBD-015: Frontend - Achievements Widget Component
3. #TBD-016: Frontend - Advanced Activity Timeline (Filters)
4. #TBD-017: Frontend - Dashboard Layout Customization

---

## 📊 Dependencies Map

```
Epic 1 (Dashboard Hub Core)
├─ No blocking dependencies (can start immediately)
├─ Uses existing: GreetingSection, ActiveSessionsSection, ChatHistorySection
└─ Requires: New backend aggregated API

Epic 2 (AI Insights & Recommendations)
├─ Depends on: Epic 1 completion (dashboard layout ready)
├─ Requires: RAG service available (existing in backend)
└─ Optional: OpenRouter API for enhanced recommendations

Epic 3 (Gamification & Advanced Features)
├─ Depends on: Epic 1 completion
├─ Soft dependency on Epic 2 (achievements could tie to AI insights)
└─ Independent feature set (can be parallelized)
```

---

## 🚀 Rollout Strategy

### Sprint Planning

**Sprint N (Current)**: Planning & Design
- ✅ Requirements gathering (this document)
- ✅ Epic creation and issue breakdown
- [ ] API contract definition (backend/frontend alignment)
- [ ] UI mockups approval

**Sprint N+1**: Epic 1 Foundation
- Backend: Dashboard aggregated API (#TBD-001)
- Frontend: Layout refactoring (#TBD-002)
- Component development: Activity Feed, Library Snapshot

**Sprint N+2**: Epic 1 Completion + Epic 2 Start
- Frontend: Responsive design + testing
- Backend: AI Insights service development
- Integration testing

**Sprint N+3**: Epic 2 Completion
- Wishlist management
- Catalog trending
- AI insights widget

**Sprint N+4**: Epic 3 (Optional)
- Gamification features
- Advanced timeline
- Dashboard customization

---

## 🎨 Design Assets Needed

### UI/UX
- [ ] Dashboard hub wireframes (Figma)
- [ ] Activity feed timeline design
- [ ] AI insights widget visual design
- [ ] Mobile responsive mockups
- [ ] Icon set for quick actions

### Data Visualization
- [ ] Stats card color palette finalization
- [ ] Progress bar styles (library quota)
- [ ] Badge/achievement visual system
- [ ] Timeline event type icons

---

## 📐 API Contracts

### New Endpoints

```typescript
// Epic 1
GET /api/v1/dashboard
Response: {
  user: UserProfile;
  stats: DashboardStats;
  activeSessions: GameSession[];
  librarySnapshot: LibrarySnapshot;
  recentActivity: Activity[];
  chatHistory: ChatThread[];
}

// Epic 2
GET /api/v1/dashboard/insights
Response: {
  insights: AIInsight[];
  recommendations: GameRecommendation[];
  backlogAlerts: BacklogAlert[];
}

GET /api/v1/wishlist
GET /api/v1/catalog/trending

// Epic 3
GET /api/v1/achievements
GET /api/v1/activity/timeline?filters=...
```

---

## 🧪 Testing Strategy

### Unit Tests
- All new components (Activity Feed, Library Snapshot, AI Insights, etc.)
- Zustand stores (if new state management needed)
- API client functions

### Integration Tests
- Dashboard data aggregation
- Component integration (parent/child data flow)
- API endpoint integration

### E2E Tests (Playwright)
- User navigates dashboard → clicks stats card → lands on correct page
- Activity feed displays recent events correctly
- AI insights click-through to filtered views
- Mobile responsive behavior

### Visual Regression (Chromatic)
- Dashboard layout snapshots (desktop/tablet/mobile)
- Component states (loading, error, empty, populated)
- Dark mode compatibility (if applicable)

---

## 📊 Success Metrics

### User Engagement
- **Time on Dashboard**: > 2 minutes average (up from 1min)
- **Click-through Rate**: > 40% users click at least one CTA
- **Return Rate**: > 50% daily active users visit dashboard first

### Performance
- **Dashboard Load Time**: < 1.5s (aggregated API)
- **LCP (Largest Contentful Paint)**: < 2.5s
- **FID (First Input Delay)**: < 100ms

### Feature Adoption
- **AI Insights Engagement**: > 30% users click insights within 7 days
- **Quick Actions Usage**: > 60% users use at least one quick action
- **Wishlist Interaction**: > 25% users manage wishlist from dashboard

---

## 🔄 Migration Strategy

### Code Changes
1. **Preserve Existing Dashboard**: Keep current `/dashboard/page.tsx` as backup
2. **Create New Hub**: Implement in `/dashboard/hub-page.tsx` (temporary)
3. **Feature Flag**: Use feature flag to toggle between old/new dashboard
4. **Gradual Rollout**: 10% → 50% → 100% users over 2 weeks
5. **Cleanup**: Remove old dashboard after 100% rollout + 1 week monitoring

### Data Migration
- No data migration needed (read-only dashboard)
- Backend API changes backward-compatible

### User Communication
- [ ] Changelog announcement
- [ ] In-app notification: "Nuova dashboard disponibile!"
- [ ] User guide/tutorial (optional)

---

## 🚨 Risks & Mitigations

### Technical Risks

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| **Backend API performance** (aggregated call slow) | High | Medium | Implement Redis caching (5min TTL), optimize queries, pagination |
| **RAG service availability** (Epic 2 blocker) | Medium | Low | Graceful degradation (hide AI insights if service down) |
| **Mobile performance** (too many sections) | Medium | Medium | Lazy loading, virtualization, collapsible sections |
| **Breaking changes** (existing components) | Low | Low | Backward compatibility, feature flags, gradual rollout |

### Product Risks

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| **User confusion** (too much info) | Medium | Medium | User testing, progressive disclosure, clear CTAs |
| **Low engagement** (AI insights ignored) | Medium | Medium | A/B testing, personalization tuning, user feedback loop |
| **Feature creep** (scope expansion) | High | High | Strict epic boundaries, MVP-first approach, defer Phase 3 if needed |

---

## 📝 Open Questions

### Product Decisions
- [ ] Should achievements be part of MVP or deferred to Phase 3?
- [ ] How many sections should be visible on first load (mobile)?
- [ ] Should users be able to customize dashboard layout order?
- [ ] Dark mode support required for launch?

### Technical Decisions
- [ ] Use single aggregated API call vs. multiple parallel calls?
- [ ] Client-side caching strategy (React Query staleTime)?
- [ ] WebSocket for real-time activity feed updates?
- [ ] Analytics tracking: Mixpanel vs. custom solution?

### Design Decisions
- [ ] Animation budget (how much Framer Motion)?
- [ ] Icon library: Lucide vs. custom icons?
- [ ] Mobile navigation: Bottom sheet vs. hamburger menu?

---

## 📚 Reference Documents

- [Dashboard Overview Hub Spec](./dashboard-overview-hub.md)
- [Collection Dashboard (Opzione A)](./dashboard-collection-centric-option-a.md)
- [Current Dashboard Implementation](../apps/web/src/app/(public)/dashboard/page.tsx)
- [Issue #1836 - Original Dashboard](https://github.com/org/repo/issues/1836)
- [Issue #2445 - Library Quota Section](https://github.com/org/repo/issues/2445)
- [Issue #2617 - Active Sessions Widget](https://github.com/org/repo/issues/2617)
- [Issue #2612 - Recently Added Games](https://github.com/org/repo/issues/2612)

---

## 🎯 Next Actions

1. **Immediate** (This Sprint):
   - [ ] Review this implementation plan with team
   - [ ] Get stakeholder approval on Epic priorities
   - [ ] Create GitHub Epic and Issue tickets
   - [ ] Define API contracts with backend team
   - [ ] Start UI mockups in Figma

2. **Sprint N+1** (Next Sprint):
   - [ ] Backend: Implement aggregated dashboard API
   - [ ] Frontend: Begin layout refactoring
   - [ ] Setup feature flag system
   - [ ] Create test environment

3. **Continuous**:
   - [ ] Weekly sync between frontend/backend teams
   - [ ] Bi-weekly user testing sessions
   - [ ] Monitor performance metrics
   - [ ] Collect user feedback via in-app surveys

---

**Status**: ✅ Planning Complete - Ready for Epic/Issue Creation
**Next Step**: Create individual Epic and Issue documents
