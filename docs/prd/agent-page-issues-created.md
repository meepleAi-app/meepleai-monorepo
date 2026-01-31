# Agent Page Frontend - GitHub Issues Created

**Epic Document**: `docs/prd/agent-page-frontend-epic.md`
**Design Mockup**: `docs/mockups/agent-page-neon-brutalist.html`
**Created**: 2026-01-31
**Total Issues**: 12

---

## Issue Overview

| # | Title | Priority | Estimate | Sprint |
|---|-------|----------|----------|--------|
| [#3237](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3237) | Agent Page - Base Setup & Routing | P1 High | 0.5d | Sprint 1 |
| [#3238](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3238) | Agent Config Sheet Container | P1 High | 1d | Sprint 1 |
| [#3239](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3239) | Game/Template/Model Selectors | P1 High | 1.5d | Sprint 1 |
| [#3240](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3240) | Token Quota & Slot Cards Display | P1 High | 1d | Sprint 1 |
| [#3241](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3241) | Contextual Action Bar | P1 High | 1d | Sprint 1 |
| [#3242](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3242) | Chat Sheet Container | P1 High | 1d | Sprint 2 |
| [#3243](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3243) | Message Components & SSE Streaming | P0 Critical | 1.5d | Sprint 2 |
| [#3244](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3244) | Citations & Confidence Display | P2 Medium | 0.5d | Sprint 2 |
| [#3245](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3245) | Chat Input & SSE Integration | P1 High | 1d | Sprint 2 |
| [#3246](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3246) | Slot Management Page | P2 Medium | 1.5d | Sprint 3 |
| [#3247](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3247) | Upgrade Flow & Premium CTA | P2 Medium | 0.5d | Sprint 3 |
| [#3248](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3248) | E2E Tests & Responsive Validation | P0 Critical | 2d | Sprint 4 |

**Total Effort**: 12.5 days (≈3 work weeks for 1 frontend dev)

---

## Sprint Breakdown

### Sprint 1: Configuration Flow (Week 1) - 5 days
Focus: Agent setup UI with game/template/model selection + quota/slots display

**Issues**: #3237, #3238, #3239, #3240, #3241

**Deliverables**:
- ✅ Routing setup + styling system (neon brutalist theme)
- ✅ Config sheet container (bottom sheet pattern)
- ✅ All selectors functional (game/template/model)
- ✅ Token quota display with warnings
- ✅ Slot cards gamified visualization
- ✅ Contextual action bar (Cancel + Launch)

---

### Sprint 2: Chat Interface (Week 2) - 4.5 days
Focus: Real-time chat with SSE streaming, message display, citations

**Issues**: #3242, #3243, #3244, #3245

**Deliverables**:
- ✅ Chat sheet container (bottom sheet + header)
- ✅ Message components (user/agent bubbles)
- ✅ SSE streaming with progressive text reveal
- ✅ Citations + confidence bars
- ✅ Chat input with auto-resize
- ✅ Action bar (Settings + Export + Minimize)

---

### Sprint 3: Slot Management (Week 3) - 2 days
Focus: Manage active slots, end sessions, upgrade prompts

**Issues**: #3246, #3247

**Deliverables**:
- ✅ Slot management page (full CRUD)
- ✅ Active slot cards with actions
- ✅ End session flow with confirmation
- ✅ Premium upgrade CTA (purple glow)
- ✅ Action bar (View Usage + Back)

---

### Sprint 4: Testing & Polish (Week 4) - 2 days
Focus: E2E validation, responsive testing, quality assurance

**Issue**: #3248

**Deliverables**:
- ✅ 5 E2E test scenarios (Playwright)
- ✅ Unit tests >85% coverage (Vitest)
- ✅ Integration tests >80% coverage
- ✅ Responsive validation (mobile/tablet/desktop)
- ✅ Accessibility audit (axe-core)
- ✅ Performance validation (Lighthouse)

---

## Dependency Graph

```
#3237 (Base Setup)
  ├─→ #3238 (Config Sheet)
  │     ├─→ #3239 (Selectors)
  │     ├─→ #3240 (Quota & Slots)
  │     └─→ #3241 (Action Bar) ←─┐
  │                               │
  └─→ #3242 (Chat Sheet) ─────────┤
        ├─→ #3243 (Messages)      │
        │     ├─→ #3244 (Citations)
        │     └─→ #3245 (Input)   │
        │                          │
  #3240 ─→ #3246 (Slot Page) ──────┤
             └─→ #3247 (Upgrade)   │
                                   │
  All ────────────→ #3248 (E2E Tests)
```

---

## Implementation Strategy

### Week 1: Foundation (Sequential)
**Day 1-2**: #3237 → #3238 (routing + container)
**Day 3-4**: #3239 + #3240 (selectors + quota/slots in parallel)
**Day 5**: #3241 (action bar)

### Week 2: Chat (Sequential + Parallel)
**Day 6**: #3242 (chat container)
**Day 7-8**: #3243 (messages + streaming - critical path)
**Day 8-9**: #3244 + #3245 (citations + input in parallel)

### Week 3: Slots (Sequential)
**Day 10-11**: #3246 (slot management page)
**Day 11-12**: #3247 (upgrade flow)

### Week 4: Quality (Comprehensive)
**Day 13-14**: #3248 (all tests + validation)

---

## Labels Applied

All issues tagged with:
- `frontend` - Frontend implementation
- `area/agent` - Agent chat system
- `area/ui` - UI components
- `kind/feature` - Feature development
- Priority levels:
  - `priority: critical` - #3243, #3248 (SSE streaming, E2E tests)
  - `priority: high` - #3237, #3238, #3239, #3240, #3241, #3242, #3245
  - `priority: medium` - #3244, #3246, #3247

---

## Design System Assets

### Mockup HTML (Interactive)
**File**: `docs/mockups/agent-page-neon-brutalist.html`

**How to view**:
```bash
# Open in browser
start docs/mockups/agent-page-neon-brutalist.html

# Or with Live Server
cd docs/mockups
python -m http.server 8000
# Navigate to http://localhost:8000/agent-page-neon-brutalist.html
```

**Features**:
- Interactive navigation (Config / Chat / Slots)
- Working template carousel (click to select)
- Animated progress bars
- Contextual action bar (changes per screen)
- Neon glow effects on hover
- Responsive layout preview

### Color Scheme
```css
--agent-accent-cyan: #00ffff     (Primary CTAs, progress, active states)
--agent-accent-purple: #b026ff   (Premium features, upgrade)
--agent-accent-yellow: #ffd700   (Warnings, alerts)
--agent-accent-red: #ff1744      (Errors, critical states)
```

### Typography
- **Headings**: JetBrains Mono (brutal tech aesthetic)
- **Body**: Inter (readable, modern)
- **Numbers/Stats**: Chakra Petch (gaming feel)

---

## API Endpoints Required

### Already Implemented (Backend 90% complete)
✅ GET `/admin/agent-typologies?status=Approved`
✅ GET `/agents/models?tier={tier}`
✅ GET `/users/me/token-quota`
✅ GET `/agents/slots`
✅ POST `/library/games/{gameId}/agent-config`
✅ GET `/library/games/{gameId}/agent-config`
✅ POST `/game-sessions/{sessionId}/agent/launch`
✅ POST `/game-sessions/{sessionId}/agent/chat` (SSE)
✅ DELETE `/game-sessions/{sessionId}/agent`

### May Need Validation
⚠️ GET `/library/games?hasPdf=true` - Verify filtering works
⚠️ SSE endpoint stability - Load testing required

---

## Success Metrics

### User Experience
- Config flow completion rate >80%
- Chat engagement >3 messages/session
- Session duration >5 minutes average
- Upgrade CTA click rate >5%

### Technical Performance
- Page load (TTI) <3 seconds
- SSE connection success rate >95%
- Mobile responsiveness score >90 (Lighthouse)
- Zero critical accessibility violations

### Quality Metrics
- Test coverage >85% (unit + integration)
- E2E test pass rate 100%
- Accessibility score >95 (axe-core)
- Zero critical bugs in first week

---

## Next Steps

### Immediate Actions
1. ✅ Review mockup HTML in browser (`docs/mockups/agent-page-neon-brutalist.html`)
2. ✅ Validate design concept with stakeholders
3. ⏳ Start implementation with #3237 (Base Setup)
4. ⏳ Setup CI pipeline for agent tests
5. ⏳ Configure feature flag: `FEATURE_AGENT_PAGE=true`

### Before Starting Development
- [ ] Review PRD: `docs/prd/ai-agent-system-mvp.md`
- [ ] Review Architecture: `docs/01-architecture/adr/adr-004-ai-agents.md`
- [ ] Verify backend endpoints (90% complete, validate remaining 10%)
- [ ] Setup test environment with mock data
- [ ] Configure analytics tracking events

### Post-Implementation
- [ ] Create PR template for agent features
- [ ] Setup Vercel preview deployments
- [ ] Configure Sentry error tracking for agent components
- [ ] Schedule user testing session (first 20 users)
- [ ] Plan v2.0 features (voice input, multi-game agents)

---

**Status**: All 12 issues created and ready for implementation
**View all**: https://github.com/DegrassiAaron/meepleai-monorepo/issues?q=is:issue+label:area/agent+is:open
