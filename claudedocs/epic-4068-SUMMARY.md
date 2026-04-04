# Epic #4068: MeepleCard Enhancements - Summary

**Created**: 2026-02-12
**Status**: Planning Complete ✅
**Estimate**: 2-3 settimane (parallelizzato) o 5-7 settimane (sequenziale)

---

## ✅ Brainstorming Complete

**Attività completate:**
- ✅ Requirements discovery con domande strutturate
- ✅ Acceptance criteria dettagliati per tutte le 10 issue
- ✅ API contracts definiti (permission system)
- ✅ Mockup ASCII per tag layout
- ✅ Test scenarios per accessibility (WCAG 2.1 AA)
- ✅ 10 issue create su GitHub e collegate all'epic
- ✅ Dependency graph e timeline pianificati

---

## 📊 Issue Create (10 Total)

### 🔐 Area 1: Permission System (3 issue)

| # | Title | Estimate | Priority | Depends On |
|---|-------|----------|----------|------------|
| [#4177](https://github.com/meepleAi-app/meepleai-monorepo/issues/4177) | Permission Data Model & Schema | 3-4 giorni | P1-High | None |
| [#4178](https://github.com/meepleAi-app/meepleai-monorepo/issues/4178) | Permission Hooks & Utilities | 3-4 giorni | P1-High | #4177 |
| [#4179](https://github.com/meepleAi-app/meepleai-monorepo/issues/4179) | MeepleCard Permission Integration | 2-3 giorni | P1-High | #4178 |

**Scope**:
- **Tiers**: Free, Normal, Pro, Enterprise (hierarchy-based)
- **Roles**: user, editor, creator, admin, superAdmin
- **States**: game publication, collection visibility, document processing, account status
- **Logic**: Tier OR Role (accesso se soddisfa almeno una condizione)
- **Features**: conditional rendering wishlist, bulk-select, drag-drop, admin actions

---

### 💬 Area 2: Smart Tooltip Positioning (2 issue)

| # | Title | Estimate | Priority | Depends On |
|---|-------|----------|----------|------------|
| [#4186](https://github.com/meepleAi-app/meepleai-monorepo/issues/4186) | Tooltip Positioning System | 3-4 giorni | P2-Medium | None |
| [#4180](https://github.com/meepleAi-app/meepleai-monorepo/issues/4180) | Tooltip Accessibility WCAG 2.1 AA | 2-3 giorni | P1-High | #4186 |

**Scope**:
- **Positioning**: Viewport boundary detection, auto-flip (top/bottom/left/right)
- **Accessibility**: Keyboard nav (Tab/Enter/Esc), screen reader (aria-describedby)
- **Mobile**: Tap-to-show fallback, touch-friendly dismiss
- **Performance**: < 16ms calculation, debounced scroll/resize

---

### 🏷️ Area 3: Tag System Redesign (2 issue)

| # | Title | Estimate | Priority | Depends On |
|---|-------|----------|----------|------------|
| [#4181](https://github.com/meepleAi-app/meepleai-monorepo/issues/4181) | Vertical Tag Component | 3-4 giorni | P2-Medium | None |
| [#4182](https://github.com/meepleAi-app/meepleai-monorepo/issues/4182) | Tag System Integration in MeepleCard | 2 giorni | P2-Medium | #4181 |

**Scope**:
- **Layout**: Left edge vertical strip (32px desktop, 28px tablet, 24px mobile)
- **Max Tags**: 3 visibili + badge "+N" per overflow
- **Entity-Specific**: Game (tipologia, preferiti), Agent (RAG, Vision, Code), Document (format, status)
- **Responsive**: Full text → abbreviated → icon-only

**Visual Mockup**:
```
┌─────────────────────┐
│█ New   ┌─────────┐ │  ← 32px left strip
│█       │  Image  │ │
│█ Sale  │         │ │
│█       └─────────┘ │
│█ +2                │  ← overflow counter
│█                   │
│█  Title: Wingspan  │
└─────────────────────┘
```

---

### 📊 Area 4: Collection Limit Management (1 issue)

| # | Title | Estimate | Priority | Depends On |
|---|-------|----------|----------|------------|
| [#4183](https://github.com/meepleAi-app/meepleai-monorepo/issues/4183) | Collection Limit UI & Progress Indicators | 2-3 giorni | P2-Medium | #4178 |

**Scope**:
- **Limits**: Single collection per user (semplificato!)
  - Max games: Free (50), Normal (100), Pro (500), Enterprise (∞)
  - Storage quota: Free (100MB), Normal (500MB), Pro (5GB), Enterprise (∞)
- **UI**: Progress bars con color coding (green <75%, yellow 75-90%, red >90%)
- **Warnings**: Icon quando >75%, upgrade CTA quando >90%

**Visual Mockup**:
```
Your Collection (Pro Tier)
┌────────────────────────────┐
│ Games                      │
│ ████████████░░ 475/500 95% │  ← Red + warning
│ ⚠️ Approaching limit        │
│                            │
│ Storage                    │
│ ███░░░░░░░░░░ 1.2/5 GB 24% │  ← Green
└────────────────────────────┘
```

---

### 🤖 Area 5: Agent Support Enhancement (1 issue)

| # | Title | Estimate | Priority | Depends On |
|---|-------|----------|----------|------------|
| [#4184](https://github.com/meepleAi-app/meepleai-monorepo/issues/4184) | Agent-Specific Metadata & Status Display | 3 giorni | P2-Medium | None |

**Scope**:
- **Status Badge**: Active (green ●), Idle (gray ○), Training (yellow ◐), Error (red ✕)
- **Model Info**: name + parameters (temperature, max_tokens) in tooltip
- **Stats**: Invocation count (formatted: 342, 1.2K, 3.4M), last execution (relative time)
- **Capabilities Tags**: RAG, Vision, Code, Functions, MultiTurn (via tag system #4181)

**Visual Mockup**:
```
┌────────────────────────────┐
│█ RAG   ┌────────────────┐ │
│█       │  🤖 Avatar    │ │
│█ Vision│               │ │
│█       └────────────────┘ │
│█ Code                     │
│█                          │
│█ ● Active  Rules Expert   │  ← Status
│█           GPT-4o-mini    │
│█ [💬 342] [🕐 2h ago]    │  ← Stats
└────────────────────────────┘
```

---

### ✅ Area 6: Testing & Documentation (1 issue)

| # | Title | Estimate | Priority | Depends On |
|---|-------|----------|----------|------------|
| [#4185](https://github.com/meepleAi-app/meepleai-monorepo/issues/4185) | Integration Testing & Documentation | 2-3 giorni | P1-High | All above |

**Scope**:
- **E2E Tests**: Permission flows (Free/Normal/Pro/Enterprise users)
- **Accessibility**: axe-core audit, keyboard nav, screen reader, Lighthouse ≥95
- **Visual Regression**: Snapshot tests (all variants × entity types × responsive)
- **Documentation**: Update meeple-card.md, permission guide, tag examples, Storybook
- **Performance**: Tooltip <16ms, render <100ms, permission checks cached

---

## 🗺️ Dependency Graph

```
CRITICAL PATH (Sequential):
┌─────────┐   ┌─────────┐   ┌─────────┐   ┌─────────┐
│ #4177   │ → │ #4178   │ → │ #4179   │ → │ #4185   │
│ Perm    │   │ Perm    │   │ Meeple  │   │ Testing │
│ Model   │   │ Hooks   │   │ Perm    │   │ & Docs  │
└─────────┘   └─────────┘   └─────────┘   └─────────┘
  3-4d          3-4d          2-3d          2-3d
                                              ↑
                                              │
PARALLEL TRACKS:                              │
┌─────────┐   ┌─────────┐                    │
│ #4186   │ → │ #4180   │ ───────────────────┤
│ Tooltip │   │ Tooltip │                    │
│ Position│   │ A11y    │                    │
└─────────┘   └─────────┘                    │
  3-4d          2-3d                          │
                                              │
┌─────────┐   ┌─────────┐                    │
│ #4181   │ → │ #4182   │ ───────────────────┤
│ Tag     │   │ Tag     │                    │
│ Component│  │ Integration                  │
└─────────┘   └─────────┘                    │
  3-4d          2d                            │
                                              │
            ┌─────────┐                       │
            │ #4183   │ ──────────────────────┤
            │ Limits  │                       │
            └─────────┘                       │
              2-3d                            │
                                              │
            ┌─────────┐                       │
            │ #4184   │ ──────────────────────┘
            │ Agent   │
            └─────────┘
              3d
```

**Timeline Ottimizzata**:
- **Week 1**: Start #4177, #4186, #4181, #4184 in parallel
- **Week 2**: #4178 → #4179, complete #4180, #4182, #4183
- **Week 3**: #4185 integration testing → Ship! 🚀

---

## 📋 Key Design Decisions (From Brainstorming)

### Permission System
- **Logic**: Tier OR Role (più flessibile che AND)
- **Tiers**: Free < Normal < Pro < Enterprise (4 livelli)
- **Roles**: user < editor < creator < admin < superAdmin
- **Collection Model**: **Single collection per user** (semplifica permessi!)

### Tooltip System
- **Critical Issues**: Viewport clipping + Mobile touch (non solo hover)
- **Positioning**: Auto-flip 4-direction con collision detection
- **Accessibility**: Full WCAG 2.1 AA (keyboard + screen reader + contrast)

### Tag System
- **Position**: Left edge vertical strip (distinctive!)
- **Max Visible**: 3 tags + "+N" overflow counter
- **No Critical/Secondary**: Tag sono informativi, dipendono da entity type
- **Responsive**: 32px → 28px → 24px (icon-only mobile)

### Collection Limits
- **Architecture**: Single collection semplifica tutto
- **Limits**: Max games + Storage quota (tier-based)
- **UI**: Progress bars con color thresholds (75% yellow, 90% red)

### Agent Support
- **All Features**: Status + Model + Stats + Capabilities (comprehensive!)
- **Status**: 4 states (Active/Idle/Training/Error) con dot indicator
- **Tags**: Use tag system (#4181) per capabilities

---

## 📚 Documentation Created

**Specification Files**:
1. `claudedocs/epic-4068-issue-4060-specs.md` - Permission Model (API contracts, data model, testing)
2. `claudedocs/epic-4068-issue-4061-specs.md` - Permission Hooks (React context, components, examples)
3. `claudedocs/epic-4068-issue-4062-specs.md` - MeepleCard Permissions (integration patterns)
4. `claudedocs/epic-4068-issue-4063-specs.md` - Tooltip Positioning (algorithm, implementation)
5. `claudedocs/epic-4068-issue-4064-specs.md` - Tooltip Accessibility (WCAG checklist, ARIA)
6. `claudedocs/epic-4068-issue-4065-specs.md` - Vertical Tag Component (mockups, presets)
7. `claudedocs/epic-4068-issues-4066-4069-specs.md` - Final 4 issues (integration, limits, agent, testing)

**Summary**: This file

---

## 🎯 Next Steps

### Immediate Actions
1. **Review issue breakdown** - Verify tutte le 10 issue sono corrette
2. **Assign milestone** - Se esiste milestone per questo epic
3. **Prioritize** - Conferma critical path e parallel tracks
4. **Start Sprint 1** - Kick off #4177 (Permission Model)

### Before Starting Implementation
- [ ] Review specs in `claudedocs/` directory
- [ ] Confirm tier/role definitions con business team
- [ ] Verify WCAG 2.1 AA requirements
- [ ] Setup test infrastructure (axe-core, Playwright, Chromatic)

### During Implementation
- [ ] Follow critical path: #4177 → #4178 → #4179
- [ ] Run parallel tracks early: #4186, #4181, #4184
- [ ] Update issue status progressively
- [ ] Document decisions in ADRs if needed

### Epic Completion
- [ ] All 10 issue closed
- [ ] #4185 (Testing) passes all gates
- [ ] Documentation merged
- [ ] Epic #4068 closed

---

## 📈 Success Metrics

**Quality Gates**:
- WCAG 2.1 AA compliance: 100% (axe-core 0 violations)
- Test coverage: >85% (frontend)
- Performance: Tooltip <16ms, Card render <100ms
- Bundle size impact: <15KB gzipped

**Business Impact**:
- Permission system enables tier-based monetization
- Accessibility compliance expands user base
- Tag system improves information density
- Collection limits encourage tier upgrades
- Agent metadata enhances AI feature visibility

---

## 💡 Key Insights From Brainstorming

1. **Single Collection Model**: Semplifica permissions e UX (no "quale collezione?" confusion)
2. **Left Edge Tag Strip**: Distinctive visual signature per MeepleAI
3. **Tier OR Role Logic**: Balance tra flessibilità e controllo
4. **Mobile-First Tooltip**: Touch targets critici quanto positioning
5. **Agent All Features**: Comprehensive metadata = better AI platform showcase

---

## 🔗 Quick Links

- **Epic**: [#4068](https://github.com/meepleAi-app/meepleai-monorepo/issues/4068)
- **All Issues**: [#4177-#4186](https://github.com/meepleAi-app/meepleai-monorepo/issues?q=is%3Aissue+4177+OR+4178+OR+4179+OR+4180+OR+4181+OR+4182+OR+4183+OR+4184+OR+4185+OR+4186)
- **Component**: `apps/web/src/components/ui/data-display/meeple-card.tsx`
- **Docs**: `docs/frontend/components/meeple-card.md`
- **Specs**: `claudedocs/epic-4068-*.md`

---

## 🚀 Ready to Implement!

**Epic pronto per execution** con:
- ✅ 10 issue dettagliate e strutturate
- ✅ Clear dependencies e critical path
- ✅ Acceptance criteria testabili
- ✅ API contracts definiti
- ✅ Visual mockups per reference
- ✅ Test scenarios per quality assurance

**Recommended Starting Point**: Issue #4177 (Permission Data Model)

**Parallelization Opportunity**: Start #4186 (Tooltip) e #4181 (Tags) concurrently con #4177

Buon lavoro! 🎉
