# Dashboard Hub - Quick Reference Guide

**Status**: Planning Phase
**Start Date**: 2026-01-21
**Target Completion**: Sprint N+6 (12 weeks)

---

## 🎯 Executive Summary

**What**: Trasformare dashboard post-login in hub riassuntivo multi-sezione
**Why**: Fornire overview completo (collezione, sessioni, chat, wishlist) con collegamenti a pagine dedicate
**Impact**: +40% click-through, +30% engagement, -50% time-to-action

---

## 📦 Deliverables Overview

### 3 Epic, 18 Issues, 42 Story Points

| Epic | Issues | SP | Sprint | Priority |
|------|--------|-----|--------|----------|
| **Epic 1: Dashboard Hub Core (MVP)** | 8 | 21 | N+1 to N+3 | P0 - Critical |
| **Epic 2: AI Insights & Recommendations** | 6 | 13 | N+3 to N+4 | P1 - High |
| **Epic 3: Gamification & Advanced Features** | 4 | 8 | N+5 to N+6 | P2 - Medium |

---

## 🚀 Sprint Timeline

```
Sprint N+1 (Week 1-2): Foundation
├─ Backend: #1 Dashboard API, #2 Activity Service
└─ Frontend: #3 Layout Refactoring start

Sprint N+2 (Week 3-4): Core Components
├─ Backend: #8 Cache Invalidation
└─ Frontend: #4 Activity Feed, #5 Library, #6 Quick Actions, #7 Responsive

Sprint N+3 (Week 5-6): Testing + Epic 2 Start
├─ Backend: #10 AI Insights, #11 Wishlist API
├─ Frontend: #9 Integration Tests, #13 AI Insights Widget
└─ Deploy Epic 1 to staging

Sprint N+4 (Week 7-8): Epic 2 Completion
├─ Backend: #12 Trending Analytics
└─ Frontend: #14 Wishlist, #15 Trending Widget

Sprint N+5 (Week 9-10): Epic 3 Start
├─ Backend: #16 Achievement System, #17 Advanced Timeline
└─ Testing & optimization

Sprint N+6 (Week 11-12): Epic 3 Completion
├─ Frontend: #18 Achievements, #19 Advanced Timeline
└─ Polish & production deploy
```

---

## 📋 Issue Quick List

### Epic 1: Dashboard Hub Core (MVP) ⭐ CRITICAL

**Backend**:
- [ ] #TBD-001: Dashboard Aggregated API (3 SP) - `GET /api/v1/dashboard`
- [ ] #TBD-002: Activity Timeline Service (2 SP) - Event aggregation
- [ ] #TBD-008: Cache Invalidation Strategy (2 SP) - Redis pub/sub

**Frontend**:
- [ ] #TBD-003: Layout Refactoring (3 SP) - Multi-section hub
- [ ] #TBD-004: Activity Feed Component (3 SP) - Timeline cronologica
- [ ] #TBD-005: Library Snapshot Component (2 SP) - Quota + top 3
- [ ] #TBD-006: Quick Actions Enhancement (2 SP) - 5 azioni con tracking
- [ ] #TBD-007: Responsive Layout (3 SP) - Mobile-first optimization

**Testing**:
- [ ] #TBD-009: Integration & E2E Tests (3 SP) - Full test suite

---

### Epic 2: AI Insights & Recommendations

**Backend**:
- [ ] #TBD-010: AI Insights Service (3 SP) - RAG recommendations
- [ ] #TBD-011: Wishlist API (2 SP) - CRUD completo
- [ ] #TBD-012: Trending Analytics (2 SP) - Daily cron job

**Frontend**:
- [ ] #TBD-013: AI Insights Widget (2 SP) - Yellow highlight card
- [ ] #TBD-014: Wishlist Highlights (2 SP) - Top 5 wishlist
- [ ] #TBD-015: Trending Widget (2 SP) - Catalog trending

---

### Epic 3: Gamification & Advanced Features

**Backend**:
- [ ] #TBD-016: Achievement System (3 SP) - Badge engine + rules
- [ ] #TBD-017: Advanced Timeline Service (1 SP) - Filters + search

**Frontend**:
- [ ] #TBD-018: Achievements Widget (2 SP) - Badge display con animations
- [ ] #TBD-019: Advanced Timeline Filters (2 SP) - Filter bar + search

---

## 🎨 Design Assets Checklist

- [ ] Figma wireframes dashboard hub (all breakpoints)
- [ ] Icon library (Lucide icons selected)
- [ ] Color palette finalized (amber/emerald/blue gradients)
- [ ] Typography scale (Playfair Display + Geist Sans)
- [ ] Component library: Stats cards, Timeline, Badges
- [ ] Animation specifications (Framer Motion variants)
- [ ] Responsive mockups (mobile/tablet/desktop)
- [ ] Dark mode palette (optional for Phase 2)

---

## 🧪 Testing Checklist

### Epic 1 Testing
- [ ] **Unit**: All components > 85% coverage
- [ ] **Integration**: Dashboard API → Frontend data flow
- [ ] **E2E**: 5 critical user journeys (Playwright)
- [ ] **Visual**: Chromatic snapshots (3 breakpoints × 5 states)
- [ ] **Performance**: Lighthouse > 90 (Performance + Accessibility)
- [ ] **Load**: 100 concurrent users (K6 or Artillery)

### Epic 2 Testing
- [ ] **Unit**: AI insights + wishlist components
- [ ] **Integration**: RAG service → insights accuracy
- [ ] **E2E**: Wishlist management flow
- [ ] **A/B**: Insights engagement tracking

### Epic 3 Testing
- [ ] **Unit**: Achievement unlock logic
- [ ] **Integration**: Background job execution
- [ ] **E2E**: Achievement unlock notification
- [ ] **User**: Gamification satisfaction survey

---

## 📊 Success Metrics Dashboard

### Development Metrics
```yaml
api_performance:
  dashboard_endpoint: "< 500ms (p99)"
  ai_insights: "< 1s (p95)"
  wishlist_crud: "< 300ms (p99)"

frontend_performance:
  lcp: "< 2.5s"
  fid: "< 100ms"
  cls: "< 0.1"

code_quality:
  coverage_backend: "> 90%"
  coverage_frontend: "> 85%"
  lighthouse_score: "> 90"
```

### User Engagement Metrics
```yaml
epic_1_kpis:
  click_through_rate: "> 40%"
  time_on_dashboard: "> 2 min"
  mobile_bounce: "< 15%"

epic_2_kpis:
  ai_insights_engagement: "> 30%"
  wishlist_additions: "> 20%"
  trending_clicks: "> 15%"

epic_3_kpis:
  achievement_engagement: "> 40%"
  streak_retention: "+15%"
  timeline_usage: "> 20%"
```

---

## 🔗 Navigation Map (Post-Implementation)

```
Dashboard Hub (/)
├─ Stats Cards (4) → Deep links
│  ├─ 📚 Collezione → /library (Collection Dashboard con flip cards)
│  ├─ 🎲 Giocati → /sessions/history
│  ├─ 💬 Chat AI → /chat
│  └─ ⭐ Wishlist → /wishlist
│
├─ Active Sessions → /sessions
│  └─ Continua Sessione → /sessions/{id}
│
├─ Library Snapshot → /library
│
├─ Activity Feed
│  ├─ Game event → /library or /games/{id}
│  ├─ Session event → /sessions/{id}
│  └─ Chat event → /chat/{id}
│
├─ Chat History → /chat
│  └─ Thread → /chat/{id}
│
├─ Quick Actions (5)
│  ├─ Collezione → /library
│  ├─ Nuova Sessione → /sessions/new
│  ├─ Chat AI → /chat
│  ├─ Catalogo → /games/catalog
│  └─ Impostazioni → /settings
│
├─ AI Insights → Dynamic links (Epic 2)
│  ├─ Backlog alert → /library?filter=unplayed
│  ├─ Rules reminder → /chat/history?filter=rules
│  └─ Recommendations → /games/catalog?similar_to={id}
│
├─ Wishlist Highlights → /wishlist (Epic 2)
│
├─ Trending → /games/catalog (Epic 2)
│
└─ Achievements → /achievements (Epic 3)
```

---

## 🚨 Risk Register

### High-Risk Items (Mitigation Required)
1. **API Performance Degradation** (Epic 1)
   - Risk: Aggregated query too slow
   - Mitigation: Redis caching, query optimization, parallel queries
   - Owner: Backend Lead

2. **Breaking Changes to Existing Components** (Epic 1)
   - Risk: Refactoring breaks current dashboard
   - Mitigation: Feature flag, backward compatibility, gradual rollout
   - Owner: Frontend Lead

3. **RAG Service Availability** (Epic 2)
   - Risk: AI insights service down
   - Mitigation: Graceful degradation, fallback to rule-based
   - Owner: AI Team

### Medium-Risk Items (Monitor)
4. **User Confusion** (Epic 1-3)
   - Risk: Too many sections overwhelming
   - Mitigation: User testing, progressive disclosure
   - Owner: Product Team

5. **Low AI Insights Engagement** (Epic 2)
   - Risk: Users ignore recommendations
   - Mitigation: A/B testing, personalization tuning
   - Owner: Product + AI Team

---

## 📞 Team Contacts & Ownership

| Area | Owner | Slack | GitHub |
|------|-------|-------|--------|
| **Epic 1 Overall** | Frontend Lead | @frontend-lead | @frontend-lead |
| **Backend APIs** | Backend Lead | @backend-lead | @backend-lead |
| **AI Insights** | AI Team | @ai-team | @ai-team |
| **Testing & QA** | QA Lead | @qa-lead | @qa-lead |
| **Design Assets** | UX Designer | @ux-designer | @ux-designer |

---

## 🔄 Sync Meetings Schedule

- **Daily Standup**: 9:30 AM (15min) - Blockers & progress
- **Sprint Planning**: Ogni 2 settimane (2h) - Issue refinement
- **Epic Review**: End of each epic (1h) - Demo & retrospective
- **Backend/Frontend Sync**: Bi-weekly (30min) - API contract alignment

---

## 📚 Documentation Index

### Planning Documents
1. [Implementation Plan Master](./dashboard-hub-implementation-plan.md) - Overview completo
2. [Dashboard Hub Spec](./dashboard-overview-hub.md) - Requirements dettagliati
3. [Collection Dashboard (Opzione A)](./dashboard-collection-centric-option-a.md) - Future `/library` page

### Epic Documents
4. [Epic 1: Dashboard Hub Core](./epics/epic-dashboard-hub-core.md)
5. [Epic 2: AI Insights](./epics/epic-ai-insights-recommendations.md)
6. [Epic 3: Gamification](./epics/epic-gamification-advanced-features.md)

### Issue Documents
7. [All Issues Summary](./epics/issues/ALL-ISSUES-SUMMARY.md) - Riepilogo 18 issues
8. [Issue #TBD-001: Dashboard API](./epics/issues/EPIC1-ISSUE-001-dashboard-api.md)
9. [Issue #TBD-003: Layout Refactoring](./epics/issues/EPIC1-ISSUE-003-layout-refactoring.md)

---

## 🎯 Next Actions

### Immediate (This Week)
- [ ] Review documentation con team (30min meeting)
- [ ] Creare GitHub Epic e Issue tickets (2h)
- [ ] Design kickoff con UX Designer (1h)
- [ ] Backend/Frontend API contract sync (30min)

### Sprint N+1 Prep
- [ ] Grooming session per Issue #1, #2, #3 (1h)
- [ ] Figma wireframes review (30min)
- [ ] Environment setup (staging database, Redis)
- [ ] Feature flag configuration

### Ongoing
- [ ] Update questo documento con progress
- [ ] Track metrics settimanalmente
- [ ] Risk review bi-weekly
- [ ] Stakeholder updates monthly

---

## ✅ How to Use This Guide

**For Product Managers**:
- Review [Implementation Plan](./dashboard-hub-implementation-plan.md) for high-level overview
- Track progress con [All Issues Summary](./epics/issues/ALL-ISSUES-SUMMARY.md)
- Monitor success metrics section

**For Backend Developers**:
- Start with [Epic 1 Document](./epics/epic-dashboard-hub-core.md)
- Read detailed issue specs in `epics/issues/` folder
- Follow Quick Start Guide in ALL-ISSUES-SUMMARY.md

**For Frontend Developers**:
- Read [Dashboard Hub Spec](./dashboard-overview-hub.md) for UX requirements
- Review [Collection Dashboard](./dashboard-collection-centric-option-a.md) for design inspiration
- Implement following issue specs in `epics/issues/`

**For QA Engineers**:
- Review testing checklists in each epic document
- Focus on E2E flows in Issue #9 spec
- Setup Chromatic for visual regression

**For Designers**:
- Reference design tokens and color palette in dashboard specs
- Create Figma mockups following Editorial Gaming aesthetic
- Provide animation specs for Framer Motion implementation

---

## 🎨 Design System Quick Ref

### Colors (Earthy Palette)
```css
--amber-primary: #D97706    /* Stats, CTAs, accents */
--emerald-secondary: #65A30D /* Success, active states */
--blue-accent: #1E40AF       /* Info, chat, links */
--purple-gamification: #7C3AED /* Achievements, badges */
--stone-neutral: #78716C     /* Text, borders, subtle elements */
```

### Typography
```css
--font-display: 'Playfair Display', serif   /* Headings, stats numbers */
--font-body: 'Geist Sans', 'Inter', sans    /* Body text, UI labels */
```

### Spacing Scale
```css
--spacing-section: 2rem    /* 32px - Between major sections */
--spacing-card: 1.5rem     /* 24px - Card gaps */
--spacing-compact: 0.75rem /* 12px - Tight spacing */
```

### Component Patterns
- **Cards**: `rounded-2xl`, `border-2`, `shadow-lg`, hover lift `y: -4px`
- **Buttons**: `rounded-full` (primary), `rounded-lg` (secondary)
- **Gradients**: `bg-gradient-to-br from-{color}-500/20 to-{color}-600/20`
- **Glassmorphism**: `backdrop-blur-xl bg-white/80`

---

## 🧰 Developer Commands

### Backend Development
```bash
# Start API with hot reload
cd apps/api/src/Api && dotnet watch run

# Run tests for dashboard
dotnet test --filter "Dashboard"

# Create migration
dotnet ef migrations add AddDashboardCache

# Check OpenAPI spec
curl http://localhost:8080/scalar/v1
```

### Frontend Development
```bash
# Start dev server
cd apps/web && pnpm dev

# Run tests
pnpm test dashboard
pnpm test:coverage

# Type checking
pnpm typecheck

# Visual regression
pnpm chromatic

# E2E tests
pnpm test:e2e dashboard
```

### Database Queries (Debug)
```sql
-- Check user dashboard data
SELECT u.Username,
       COUNT(DISTINCT ul.GameId) as LibraryCount,
       COUNT(DISTINCT s.Id) as ActiveSessions
FROM Users u
LEFT JOIN UserLibrary ul ON u.Id = ul.UserId
LEFT JOIN GameSessions s ON u.Id = s.UserId AND s.Status = 'Active'
WHERE u.Id = '{user-id}'
GROUP BY u.Username;

-- Check activity events
SELECT * FROM ActivityEvents
WHERE UserId = '{user-id}'
ORDER BY Timestamp DESC
LIMIT 10;
```

---

## 📈 Progress Tracking Template

### Weekly Update Format
```markdown
## Week {N} - Dashboard Hub Progress

**Epic 1 Status**: {X}/8 issues completed ({Y}%)
**Epic 2 Status**: {X}/6 issues completed ({Y}%)
**Epic 3 Status**: {X}/4 issues completed ({Y}%)

### Completed This Week
- ✅ Issue #{ID}: {Title}
- ✅ Issue #{ID}: {Title}

### In Progress
- 🔄 Issue #{ID}: {Title} ({Progress}%)

### Blockers
- 🚨 {Blocker description} - Owner: {Name}

### Next Week Plan
- [ ] Issue #{ID}: {Title}
- [ ] Issue #{ID}: {Title}

### Metrics
- API Performance: {XXX}ms
- Test Coverage: {XX}%
- User Engagement: {XX}% (if deployed)
```

---

## 🎯 Definition of Success

### Technical Success
- ✅ All 18 issues closed and merged
- ✅ Test coverage: Backend > 90%, Frontend > 85%
- ✅ Performance: API < 500ms, LCP < 2.5s
- ✅ Zero critical bugs in production
- ✅ Lighthouse scores: Performance > 90, Accessibility > 95

### User Success
- ✅ Dashboard engagement: +30% time on page
- ✅ Navigation efficiency: +40% click-through to deep pages
- ✅ User satisfaction: > 80% in post-launch survey
- ✅ Feature adoption: > 60% users interact with new sections

### Business Success
- ✅ Reduced support tickets (-20%) via AI insights
- ✅ Increased user retention (+10%) via gamification
- ✅ Improved onboarding completion (+25%) via quick actions
- ✅ Higher session frequency (+15%) via active sessions widget

---

## 📞 Emergency Contacts

**Production Issues**: @on-call-engineer (Slack)
**API Failures**: @backend-lead
**Frontend Bugs**: @frontend-lead
**Design Questions**: @ux-designer
**Product Decisions**: @product-manager

---

**Maintained By**: Engineering Team
**Last Updated**: 2026-01-21
**Next Review**: Sprint Planning N+1
