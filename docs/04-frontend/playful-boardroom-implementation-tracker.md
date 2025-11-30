# Playful Boardroom - Implementation Tracker

**Design System**: Opzione A - Playful Boardroom
**Date Created**: 2025-11-30
**Status**: 🟢 Issues Created - Ready for Implementation

---

## 📊 Issue Summary

### Total Issue Count
- **New Issues Created**: 12 (7 UI components + 5 pages)
- **Existing Issues Updated**: 10 (BGAI frontend components)
- **FE-IMP Issues (Unchanged)**: 8 (architectural)
- **Total Layout Implementation**: 30 issues

---

## 🆕 New Issues Created (12)

### UI Components (7)
| Number | Title | Priority | Status |
|--------|-------|----------|--------|
| [#1828](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1828) | 🎨 [UI-001] MeepleAvatar Component with States | P1 | Open |
| [#1829](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1829) | 📱 [UI-002] BottomNav Component (Mobile-First) | P1 | Open |
| [#1830](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1830) | 🎲 [UI-003] GameCard Component (Grid/List variants) | P1 | Open |
| [#1831](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1831) | 💬 [UI-004] ChatMessage Component (User/AI) | P1 | Open |
| [#1832](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1832) | 🏅 [UI-005] ConfidenceBadge Component | P2 | Open |
| [#1833](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1833) | 📄 [UI-006] CitationLink Component | P2 | Open |
| [#1834](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1834) | 🎯 [UI-007] QuickActions Component | P2 | Open |

### Pages (5)
| Number | Title | Priority | Status |
|--------|-------|----------|--------|
| [#1835](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1835) | 🏠 [PAGE-001] Landing Page (Marketing) | P1 | Open |
| [#1836](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1836) | 📊 [PAGE-002] Dashboard Page (Post-Login) | P1 | Open |
| [#1838](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1838) | 🎲 [PAGE-003] Game Catalog Page (Hybrid View) | P1 | Open |
| [#1840](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1840) | 💬 [PAGE-004] Chat Page (Sidebar + Context) | P1 | Open |
| [#1841](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1841) | 📖 [PAGE-005] Game Detail Page (Tabs + Chat) | P2 | Open |

---

## ⚠️ Existing Issues Updated (10)

| Number | Title | Update |
|--------|-------|--------|
| [#989](https://github.com/DegrassiAaron/meepleai-monorepo/issues/989) | Base components (Button, Card, Input, Form) | ✅ Design reference added |
| [#1001](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1001) | QuestionInputForm component | ✅ Design reference added |
| [#1003](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1003) | GameSelector dropdown component | ✅ Design reference added |
| [#1004](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1004) | Loading and error states (UI/UX) | ✅ Design reference added |
| [#1008](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1008) | Error handling and retry logic | ✅ Design reference added |
| [#1013](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1013) | PDF viewer integration (react-pdf) | ✅ Design reference added |
| [#1014](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1014) | Citation click → jump to page | ✅ Design reference added |
| [#1016](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1016) | Complete Italian UI strings (200+ translations) | ✅ Design reference added |
| [#1017](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1017) | Game catalog page (/board-game-ai/games) | ✅ Design reference added |
| [#994](https://github.com/DegrassiAaron/meepleai-monorepo/issues/994) | Frontend build optimization | ✅ Design reference added |

---

## 📋 Dependency Graph

```
Week 1: Foundation
┌─────────────────────────────────┐
│ FE-IMP-001: App Router          │ (Existing - Modified)
│ + Google Fonts (Quicksand+Inter)│
└────────────┬────────────────────┘
             │
             ├─────────────────────┬────────────────────┐
             ▼                     ▼                    ▼
      ┌──────────┐          ┌──────────┐        ┌──────────┐
      │ #1828    │          │ #1829    │        │ FE-IMP-006│
      │ UI-001   │          │ UI-002   │        │ Forms     │
      │ Meeple   │          │ BottomNav│        │           │
      └────┬─────┘          └────┬─────┘        └────┬─────┘
           │                     │                   │
Week 2:    └──────┬──────────────┴───────────────────┘
                  │
                  ├─────────────────────┬────────────────────┐
                  ▼                     ▼                    ▼
           ┌──────────┐          ┌──────────┐        ┌──────────┐
           │ #1830    │          │ #1831    │        │ FE-IMP-003│
           │ UI-003   │          │ UI-004   │        │ TanStack  │
           │ GameCard │          │ ChatMsg  │        │ Query     │
           └────┬─────┘          └────┬─────┘        └────┬─────┘
                │                     │                   │
Week 3:          └──────┬──────────────┴───────────────────┘
                        │
                        ├─────────────────────┬────────────────────┐
                        ▼                     ▼                    ▼
                 ┌──────────┐          ┌──────────┐        ┌──────────┐
                 │ #1835    │          │ #1836    │        │ #989     │
                 │ PAGE-001 │          │ PAGE-002 │        │ Base     │
                 │ Landing  │          │ Dashboard│        │ Comps    │
                 └────┬─────┘          └────┬─────┘        └────┬─────┘
                      │                     │                   │
Week 4:               └──────┬──────────────┴───────────────────┘
                             │
                             ├─────────────────────┬────────────────────┐
                             ▼                     ▼                    ▼
                      ┌──────────┐          ┌──────────┐        ┌──────────┐
                      │ #1838    │          │ #1840    │        │ FE-IMP-007│
                      │ PAGE-003 │          │ PAGE-004 │        │ Zustand   │
                      │ Catalog  │          │ Chat     │        │ Store     │
                      └────┬─────┘          └────┬─────┘        └────┬─────┘
                           │                     │                   │
Week 5-6:                  └──────┬──────────────┴───────────────────┘
                                  │
                                  ├─────────┬─────────┬─────────┬────────┐
                                  ▼         ▼         ▼         ▼        ▼
                             ┌────────┬────────┬────────┬────────┬────────┐
                             │ #1832  │ #1833  │ #1834  │ #1841  │Storybook│
                             │ UI-005 │ UI-006 │ UI-007 │ PAGE-005│  #1823 │
                             │Confid. │Citation│ Quick  │ Detail  │        │
                             └────────┴────────┴────────┴────────┴────────┘
```

---

## 🗓️ Implementation Roadmap

### Week 1: Foundation (P1) - 3 issues
**Goal**: App Router setup + Core navigation

| Issue | Title | Effort | Owner |
|-------|-------|--------|-------|
| FE-IMP-001 | App Router + Google Fonts | 2d | TBD |
| #1828 (UI-001) | MeepleAvatar Component | 1.5d | TBD |
| #1829 (UI-002) | BottomNav Component | 1.5d | TBD |

**Deliverable**: App Router functional, fonts loaded, bottom nav on all pages

---

### Week 2: Core Components (P1) - 4 issues
**Goal**: Card and message components ready

| Issue | Title | Effort | Owner |
|-------|-------|--------|-------|
| #1830 (UI-003) | GameCard Component | 2d | TBD |
| #1831 (UI-004) | ChatMessage Component | 2d | TBD |
| FE-IMP-006 | Form System (RHF + Zod) | 1.5d | TBD |
| #989 | Base components (Shadcn) | 1.5d | TBD |

**Deliverable**: Reusable cards, chat messages, form components styled

---

### Week 3: Public Pages (P1) - 4 issues
**Goal**: Landing and Dashboard functional

| Issue | Title | Effort | Owner |
|-------|-------|--------|-------|
| #1835 (PAGE-001) | Landing Page | 2d | TBD |
| #1836 (PAGE-002) | Dashboard Page | 2d | TBD |
| FE-IMP-003 | TanStack Query | 1.5d | TBD |
| #1001 | QuestionInputForm | 1.5d | TBD |

**Deliverable**: Public landing, authenticated dashboard, data layer

---

### Week 4: Interactive Pages (P1) - 5 issues
**Goal**: Catalog and Chat fully functional

| Issue | Title | Effort | Owner |
|-------|-------|--------|-------|
| #1838 (PAGE-003) | Game Catalog Page | 2.5d | TBD |
| #1840 (PAGE-004) | Chat Page | 2.5d | TBD |
| FE-IMP-007 | Zustand Chat Store | 2d | TBD |
| #1003 | GameSelector dropdown | 1d | TBD |
| #1017 | Game catalog page (backend integration) | 1d | TBD |

**Deliverable**: Catalog browsable, chat streaming, game selection

---

### Week 5-6: Enhanced UX (P2) - 5 issues
**Goal**: Polish and advanced features

| Issue | Title | Effort | Owner |
|-------|-------|--------|-------|
| #1832 (UI-005) | ConfidenceBadge | 0.5d | TBD |
| #1833 (UI-006) | CitationLink | 0.5d | TBD |
| #1834 (UI-007) | QuickActions | 1d | TBD |
| #1841 (PAGE-005) | Game Detail Page | 2d | TBD |
| #1823 | Storybook Coverage | 3d | TBD |

**Deliverable**: All components documented, game detail page, visual testing

---

## 🎯 Priority Matrix

| Priority | Count | Issues | Focus |
|----------|-------|--------|-------|
| **P1 (High)** | 9 | #1828-1831, #1835-1836, #1838, #1840 + FE-IMP | MVP Functional |
| **P2 (Medium)** | 3 | #1832-1834, #1841 | Enhanced UX |
| **Support** | 10 | #989, #1001, #1003, #1004, #1008, #1013, #1014, #1016, #1017, #994 | BGAI Integration |

---

## 📂 Related Documentation

### Design References
- **Wireframes**: `docs/04-frontend/wireframes-playful-boardroom.md` (600+ lines)
- **Issue Analysis**: `docs/04-frontend/issue-analysis-playful-boardroom.md` (900+ lines)
- **Brand Decision**: `.serena/memories/meepleai-brand-decision.md`

### Implementation Guides
- **Design System**: `docs/04-frontend/improvements/05-design-system-overview.md`
- **Design Tokens**: `apps/web/src/styles/design-tokens.css`
- **Color Palette**: `apps/web/src/styles/globals.css` (updated with Opzione A)

### Existing Plans
- **FE-IMP Issues**: `docs/04-frontend/improvements/issues.md`
- **Refactor Roadmap**: `docs/04-frontend/improvements/frontend-refactor-15-issues.md`

---

## 🔗 Issue Links

### GitHub Links (All Issues)
```
UI Components:
https://github.com/DegrassiAaron/meepleai-monorepo/issues/1828
https://github.com/DegrassiAaron/meepleai-monorepo/issues/1829
https://github.com/DegrassiAaron/meepleai-monorepo/issues/1830
https://github.com/DegrassiAaron/meepleai-monorepo/issues/1831
https://github.com/DegrassiAaron/meepleai-monorepo/issues/1832
https://github.com/DegrassiAaron/meepleai-monorepo/issues/1833
https://github.com/DegrassiAaron/meepleai-monorepo/issues/1834

Pages:
https://github.com/DegrassiAaron/meepleai-monorepo/issues/1835
https://github.com/DegrassiAaron/meepleai-monorepo/issues/1836
https://github.com/DegrassiAaron/meepleai-monorepo/issues/1838
https://github.com/DegrassiAaron/meepleai-monorepo/issues/1840
https://github.com/DegrassiAaron/meepleai-monorepo/issues/1841

Updated BGAI Issues:
https://github.com/DegrassiAaron/meepleai-monorepo/issues/989
https://github.com/DegrassiAaron/meepleai-monorepo/issues/1001
https://github.com/DegrassiAaron/meepleai-monorepo/issues/1003
https://github.com/DegrassiAaron/meepleai-monorepo/issues/1004
https://github.com/DegrassiAaron/meepleai-monorepo/issues/1008
https://github.com/DegrassiAaron/meepleai-monorepo/issues/1013
https://github.com/DegrassiAaron/meepleai-monorepo/issues/1014
https://github.com/DegrassiAaron/meepleai-monorepo/issues/1016
https://github.com/DegrassiAaron/meepleai-monorepo/issues/1017
https://github.com/DegrassiAaron/meepleai-monorepo/issues/994
```

---

## 🎨 Design System Summary

### Color Palette (Opzione A)
```css
Primary:   #F97316  /* Orange Catan - CTAs, focus, active states */
Secondary: #16A34A  /* Green Carcassonne - success, secondary actions */
Accent:    #A855F7  /* Purple - highlights, badges */
Background: #F9F7F4 /* Beige chiaro - warm, inviting */
Foreground: #2B241F /* Marrone scuro - readable text */
```

### Typography
- **Headings**: Quicksand (Google Font) - Rounded, friendly
- **Body**: Inter - Modern, readable
- **Code**: JetBrains Mono

### Key Visual Elements
- Meeple mascot avatar (5 animated states)
- Subtle wood textures on cards (optional)
- Bounce animations on interactions
- Hexagonal badges (tile-style)
- Bottom navigation (mobile-first)

---

## 📊 Component Inventory

### Shadcn/UI Components Used
- Button (primary, secondary, ghost, outline)
- Card (default, hover effects)
- Badge (default, secondary, outline)
- Avatar (circular, fallback)
- Input (search, chat)
- Select (filters, sorting)
- Tabs (game detail sections)
- Sheet (mobile sidebar)
- Dialog (modals)
- ToggleGroup (grid/list view)
- Separator (dividers)
- Skeleton (loading)
- ScrollArea (chat messages)

### Custom Components Built
1. **MeepleAvatar** (#1828) - SVG with 5 states
2. **BottomNav** (#1829) - Mobile navigation
3. **GameCard** (#1830) - Grid/List dual mode
4. **ChatMessage** (#1831) - User/AI bubbles
5. **ConfidenceBadge** (#1832) - Visual confidence
6. **CitationLink** (#1833) - PDF reference
7. **QuickActions** (#1834) - Dashboard actions

### Pages Built
1. **Landing** (#1835) - Marketing homepage
2. **Dashboard** (#1836) - User home (post-login)
3. **Game Catalog** (#1838) - Hybrid view catalog
4. **Chat** (#1840) - AI chat interface
5. **Game Detail** (#1841) - Game-specific page with tabs

---

## ✅ Acceptance Criteria (MVP)

### Visual Quality
- ✅ All pages match wireframes exactly (pixel-perfect mobile)
- ✅ Color palette Opzione A applied consistently
- ✅ Typography: Quicksand headings, Inter body
- ✅ Animations smooth (60fps, respects prefers-reduced-motion)

### Technical Quality
- ✅ Test coverage ≥90% (components + pages)
- ✅ Lighthouse Performance ≥90
- ✅ Lighthouse Accessibility ≥95
- ✅ Bundle size <500KB (first load)

### Functional Quality
- ✅ Responsive: 375px → 768px → 1024px+
- ✅ Bottom Nav functional on all pages
- ✅ Chat streaming works (SSE integration)
- ✅ Game catalog filters/search functional
- ✅ Authentication flow complete (login → dashboard)

### User Experience
- ✅ Mobile-first: Touch targets ≥44px
- ✅ Keyboard navigation works (Tab, Enter, Escape)
- ✅ Screen reader friendly (ARIA labels)
- ✅ Dark mode functional (automatic theme switch)

---

## 🚀 Getting Started

### Setup (5 minutes)
```bash
cd apps/web

# Verify design tokens updated
cat src/styles/globals.css | grep "Opzione A"

# Check issue list
gh issue list --label "frontend" --limit 20

# Start with Week 1 foundation
git checkout -b feature/ui-001-meeple-avatar
```

### Week 1 Checklist
- [ ] FE-IMP-001: Setup App Router + import Google Fonts
- [ ] #1828: Create MeepleAvatar component (5 states)
- [ ] #1829: Create BottomNav component
- [ ] Verify fonts loading (Quicksand, Inter)
- [ ] Test BottomNav on mobile (375px)
- [ ] Commit: "feat: Week 1 foundation - App Router + BottomNav"

### Development Flow
```bash
# For each issue
git checkout -b feature/ui-XXX-component-name
# Implement component
# Write tests (≥90% coverage)
# Write Storybook story
# Verify responsive (375px → 1024px)
pnpm test
pnpm build
git commit -m "feat(ui-XXX): component description"
# Create PR → Review → Merge
```

---

## 📈 Progress Tracking

### Week 1 (Current)
- [ ] FE-IMP-001: App Router + Fonts
- [ ] #1828: MeepleAvatar
- [ ] #1829: BottomNav

### Week 2
- [ ] #1830: GameCard
- [ ] #1831: ChatMessage
- [ ] FE-IMP-006: Forms
- [ ] #989: Base components

### Week 3
- [ ] #1835: Landing Page
- [ ] #1836: Dashboard
- [ ] FE-IMP-003: TanStack Query
- [ ] #1001: QuestionInputForm

### Week 4
- [ ] #1838: Game Catalog
- [ ] #1840: Chat Page
- [ ] FE-IMP-007: Zustand Store
- [ ] #1003: GameSelector

### Week 5-6
- [ ] #1832-1834: UI-005, 006, 007
- [ ] #1841: Game Detail Page
- [ ] #1823: Storybook Coverage
- [ ] Responsive testing + A11y audit

---

## 🎯 Success Metrics

### Completion Tracking
- **Components**: 7/7 (0% complete)
- **Pages**: 5/5 (0% complete)
- **BGAI Integration**: 10/10 (0% complete)
- **Overall**: 22/30 issues (0% complete)

### Quality Metrics
- Test Coverage: Target ≥90%
- Lighthouse Performance: Target ≥90
- Lighthouse Accessibility: Target ≥95
- Bundle Size: Target <500KB

### Timeline
- **Start Date**: 2025-11-30
- **Target Week 4 MVP**: 2025-12-27 (4 weeks)
- **Target Week 6 Complete**: 2026-01-10 (6 weeks)

---

## 📝 Notes

### Closed Issues (Duplicates)
- #1837: Duplicate of #1838 (PAGE-003) - Closed
- #1839: Duplicate of #1840 (PAGE-004) - Closed

### Labels Created
- `design-system` (Color: D4A5FF)
- `playful-boardroom` (Color: F97316)
- `mobile-first` (Color: 06B6D4)

### Next Actions
1. **Assign owners** to Week 1 issues
2. **Create milestone** "Layout Phase 1" on GitHub
3. **Start implementation** FE-IMP-001 + #1828 + #1829
4. **Update this tracker** weekly with progress

---

**Version**: 1.0
**Last Updated**: 2025-11-30
**Status**: 🟢 All Issues Created - Ready to Start
**Next**: Begin Week 1 Implementation
