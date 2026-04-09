# Library-to-Game User Journey — Epic Design Spec

**Date:** 2026-04-09
**Status:** Proposed
**Type:** Epic (contains 6 sub-feature specs)
**Scope:** End-to-end user journey from Admin Dashboard to Game Detail Page, covering navbar view toggle, KB-filtered catalog, direct add-to-library flow, new desktop split-view game page, new mobile "hand of cards" game page, and E2E screenshot validation.

**Related specs (context):**
- `2026-04-07-meeplecard-rewrite-from-mockups` — MeepleCard component library (reused)
- `2026-04-08-desktop-ux-redesign` — Desktop navigation + Library Hub (sibling, not prerequisite)
- `admin-mockups/mobile-card-layout-mockup.html` — mobile "cards in hand" reference
- `.superpowers/brainstorm/39070-1775727380/content/07-real-render.html` — high-fidelity composite render

---

## 1. Context & Goals

### Context

Today an admin who wants to validate the user experience of browsing the shared catalog and adding a game to their library has a fragmented flow:

1. Admin dashboard has a link "Back to app" but no one-click view toggle in the top bar.
2. The shared catalog (`/library?tab=catalogo`) does not expose whether a game has an indexed Knowledge Base (KB) — users cannot filter "AI-ready" games, so they may add a game without realising the AI agent is not yet available for it.
3. The MeepleCard `+` quick action (add to library) opens a wizard by default; the user asked for **direct add + auto-navigate** as the primary path, with the wizard available as an opt-in alternative.
4. The current game detail page at `/library/games/[gameId]` uses a zone-based `GameTableLayout` (knowledge zone, tools zone, sessions zone) which is inconsistent with the new MeepleCard hero language and doesn't match the mental model of "la mia carta del gioco + gli strumenti".
5. Mobile is especially weak: the current `game-detail-mobile.tsx` uses ad-hoc bottom sheets, not the "carte in mano" pattern shipped elsewhere in the app.
6. No E2E test validates the full journey on mobile viewports, making regressions invisible.

### Goals

- **Unified user journey:** admin → one-click user view toggle → KB-filtered catalog → direct add via MeepleCard `+` → seamless navigate to a redesigned game detail page (desktop split + mobile card-hand).
- **Reuse existing infrastructure:** `MeepleCard`, `SplitViewLayout`, `useAddGameToLibrary`, `RequireRole`, existing Playwright config, existing design tokens. **No parallel design system.**
- **Evidence-based UX validation** via Playwright smoke + a11y tests on Pixel 5, iPhone 13, and desktop-chrome 1920×1080, running in CI and producing screenshot artifacts attached to the epic PR.
- **Shippable in small increments:** 6 sub-feature PRs, each under ~500 lines of diff, merged in sequence into an epic branch, then one epic PR into `main-dev`.

### Non-goals

- Redesigning `/library` landing page or the shared catalog search UX (out of scope; tracked in `2026-04-08-desktop-ux-redesign`).
- Changing the backend `UserLibrary` commands/queries — they already work.
- Adding new tabs or content beyond the 5 agreed: Info, AI Chat, Toolbox, House Rules, Partite.
- Implementing the `House Rules` or `Toolbox` content itself if it doesn't exist yet — the tab component wraps existing UI or shows a "coming soon" empty state.
- Visual regression with pixel diff (we use smoke + a11y; screenshots are artifacts, not assertions).
- Changing the wizard flow for add-to-library — it stays accessible via overflow menu.

---

## 2. Decisions Confirmed During Brainstorming

| # | Question | Decision | Source |
|---|---|---|---|
| Q1 | Scope | **Epic + 6 sub-feature specs**, each with its own design doc and plan | user selected option A |
| Q2 | Tab set on game page | **5 tabs:** `📖 Info` • `🤖 AI Chat` • `🧰 Toolbox` • `🏠 House Rules` • `🎲 Partite` | user selected option B (Completa) |
| Q3 | Mobile drawer pattern | **"Mano di carte":** hand stack left edge + focused `MeepleCard` center + **drawer from top** with 5 tabs + action bar globale. Reference: `admin-mockups/mobile-card-layout-mockup.html` | user pointed to existing mockup |
| Q4.1 | Hand stack on game page | **Yes** — keep the 44px vertical strip of mini-cards for cross-game navigation | user confirmed |
| Q4.2 | Drawer open trigger | **Tap on `📋 Dettagli` button** on the focused MeepleCard — no swipe, no edge gesture (avoid iOS back-swipe conflict) | user confirmed |
| Q4.3 | Global action bar on game page | **Keep** (Libreria / Home / Chat / Sessioni / Profilo), **auto-hide** in focus mode via scroll reveal | user confirmed |
| Q4.4 | Tab naming conflict `Sessioni` (tab) vs `Sessioni` (global nav) | **Rename tab to `Partite`** — domain note: 1 `GameSession` (game night) can contain N `MatchInstance` (partite). The tab shows all matches for THIS game across all sessions | user confirmed + domain clarification |
| Q4 (desktop) | Desktop tabs layout in right panel | **Vertical rail on left edge of right panel** (VSCode sidebar pattern), icon + label, scalable to more than 5 tabs in the future | user selected option B |
| Q5 | Admin↔User toggle form | **Icon switch** (light/dark toggle style, `👤 / ⚙️`) next to avatar in `UserTopNav`, gated on `isAdminRole()`, 1 click flips `sessionStorage.meepleai_view_mode` and calls `router.push` | user selected option A |
| Q6 | KB filter in library | **Toggle chip** in `CatalogFilters` bar, label `[✓ Solo giochi AI-ready]`, default OFF | user selected option A |
| Q7 | MeepleCard `+` add behavior | **Direct add (no wizard)** + 2s toast then navigate. Replacement: if `inLibrary === true` the `+` is hidden and replaced with `→ Vai al gioco`. Error: stay on catalog + toast. Wizard: accessible via overflow `⋯` menu "Aggiungi con opzioni" | user answered |
| Q8 | E2E validation scope | **Scope 3** (happy path + per-sub-feature tests), **smoke + a11y**, **viewport P** (Pixel 5 + iPhone 13 + desktop 1920), **CI on GitHub Actions** | user answered |
| Q9 | Implementation order | **1→S1, 2→S2, 3→S4, 4→S5, 5→S3, 6→S6** — each sub-feature is its own PR into the epic branch | user confirmed |

---

## 3. Architecture Overview

### 3.1 User Journey (end-to-end)

```
[Admin on Admin Dashboard]
    │  tap view-toggle in topbar (S1)
    ▼
sessionStorage.meepleai_view_mode = 'user'
router.push('/')
    │
[User home]
    │  navigate /library?tab=catalogo
    ▼
[Catalogo condiviso]
    │  tap "Solo giochi AI-ready" chip (S2)
    ▼
GET /api/v1/shared-catalog/games?hasKnowledgeBase=true
    │  list filtered to games with indexed KB
    │  tap "+" on MeepleCard of "Azul" (S3)
    ▼
POST /api/v1/library/games/{azulId}
optimistic update: inLibrary=true, quota+=1
    │  success
    ▼
Toast "Aggiunto alla libreria" (2s)
router.push('/library/games/{azulId}')
    │
[Game detail page — /library/games/{azulId}]
    │  desktop (md+)        │  mobile (<md)
    ▼                       ▼
<GameDetailDesktop>          <GameDetailMobile>
  SplitViewLayout              HandStack (left, 44px)
    MeepleCardHero             FocusedCardArea (focused Azul MeepleCard + card-links)
    GameTabsPanel              [tap 📋 Dettagli]
      ├─ rail vertical          ▼
      ├─ 📖 Info (default)      GameDetailsDrawer (slides from top)
      ├─ 🤖 AI Chat              ├─ 📖 Info (default)
      ├─ 🧰 Toolbox              ├─ 🤖 AI Chat
      ├─ 🏠 House Rules          ├─ 🧰 Toolbox
      └─ 🎲 Partite              ├─ 🏠 House Rules
                                 └─ 🎲 Partite
```

### 3.2 Sub-feature breakdown

| ID | Name | Bounded Context(s) | Size | Dependencies |
|---|---|---|---|---|
| **S1** | Admin↔User view toggle in navbar | Authentication (frontend only) | S | — |
| **S2** | `hasKnowledgeBase` filter on shared catalog | SharedGameCatalog + KnowledgeBase | M | — |
| **S3** | MeepleCard `+` direct add + auto-navigate | UserLibrary (frontend only) | S | S4 (destination page must exist) |
| **S4** | Game page desktop — SplitViewLayout + vertical rail + 5 tabs | GameManagement (frontend) | L | — |
| **S5** | Game page mobile — Hand stack + drawer from top + 5 tabs | GameManagement (frontend) | M | S4 (shares tab components) |
| **S6** | Playwright E2E: happy path + 5 sub-tests + a11y | Cross-cutting | S | S1–S5 |

Each sub-feature produces:
- One spec doc in `docs/superpowers/specs/2026-04-09-s{N}-{slug}-design.md`
- One implementation plan in `docs/superpowers/plans/2026-04-09-s{N}-{slug}.md` (generated by `writing-plans` skill)
- One feature branch `feature/s{N}-{slug}` → PR → `epic/library-to-game`
- One GitHub issue (sub-task of the epic issue)

### 3.3 Branch strategy

```
main-dev
 │
 └── epic/library-to-game   (created from main-dev at epic start)
      │
      ├── feature/s1-view-toggle           → PR to epic/library-to-game
      ├── feature/s2-kb-filter             → PR to epic/library-to-game
      ├── feature/s4-game-desktop          → PR to epic/library-to-game
      ├── feature/s5-game-mobile           → PR to epic/library-to-game
      ├── feature/s3-meeplecard-add        → PR to epic/library-to-game
      └── feature/s6-e2e-validation        → PR to epic/library-to-game

Final: PR epic/library-to-game → main-dev
```

Each sub-feature branch tracks its parent via `git config branch.<branch>.parent epic/library-to-game`.
`epic/library-to-game` is rebased weekly on `main-dev` to avoid drift.

---

## 4. Design Decisions Per Sub-Feature

### 4.1 — S1 · Admin↔User view toggle

**Problem:** Admins working on the admin dashboard cannot preview the user experience with one click; they have a "Admin Dashboard" link inside the avatar dropdown menu (from `UserMenuDropdown`) but no quick bidirectional toggle.

**Decision:** Add a `<ViewModeToggle>` icon switch to `UserTopNav` / `AdminTopNav`, next to the avatar. Gated on `isAdminRole(user.role)`. Single tap flips an internal state and navigates to the opposite shell.

**Visual:** 44×26 pill switch, gradient background (purple→orange), white knob with emoji indicator. `👤` on the left side, `⚙️` on the right. Knob slides between the two.

**State:**
- Source of truth: `sessionStorage.meepleai_view_mode` (`'admin'` | `'user'`)
- Fallback when sessionStorage unavailable (private browsing): in-memory state + toast "Modalità vista non persistente"
- Default: `'admin'` if landing on `/admin/*`, `'user'` otherwise

**Routing:**
- `'admin'` → guard redirects any user route to `/admin/overview`
- `'user'` → guard redirects any admin route to `/`
- Guards live in `app/(authenticated)/layout.tsx` and `app/admin/(dashboard)/layout.tsx`

**Accessibility:** `role="switch"`, `aria-checked={viewMode === 'admin'}`, `aria-label="Cambia vista tra admin e utente"`, keyboard-operable.

**Files touched:** ~4 new, ~3 modified. Details in `2026-04-09-s1-view-toggle-design.md`.

### 4.2 — S2 · `hasKnowledgeBase` filter on shared catalog

**Problem:** Users browsing the shared catalog have no way to filter games that have an indexed KB (AI agent ready). Adding a game without KB leads to a disappointing first impression when the user opens the game page and the AI Chat is empty.

**Decision:**
1. **Backend:** add `HasKnowledgeBase: bool` to `SharedGameDto` (computed), and `hasKnowledgeBase?: bool` optional filter parameter to `GetSharedGamesQuery`.
2. **Computation strategy (v1):** lazy join on `VectorDocument` count in the query handler. If p95 > 150ms → follow up with S2b: denormalized `HasKnowledgeBase` column on `SharedGameCatalogEntry`, updated via event handler on `VectorDocumentIndexedEvent`.
3. **Frontend:** add a toggle chip `[✓ Solo giochi AI-ready]` to `CatalogFilters`, default OFF, syncs with URL param `?hasKb=true` so the filter is bookmarkable.
4. **MeepleCard visual badge:** always show the green `✓ AI-READY` chip on cards that have `hasKnowledgeBase === true`, independently of the filter. This gives passive awareness even without filtering.

**Domain note:** "has KB" = at least 1 `VectorDocument` with `ProcessingState === 'Indexed'` exists for that `SharedGameId`. Private games are out of scope (they're in `PrivateGame` with `GameId = null`).

**Files touched:** ~3 backend, ~3 frontend. Details in `2026-04-09-s2-kb-filter-design.md`.

### 4.3 — S3 · MeepleCard `+` direct add + auto-navigate

**Problem:** The MeepleCard `+` quick action currently opens an optional wizard (`useMeepleCardActions.onAddToLibrary()` can dispatch a modal). The user wants a direct one-tap add + navigate to the game page.

**Decision:**
1. **Primary path:** tap `+` → call `addToLibrary.mutate({ gameId })` → on success, show toast `"Aggiunto alla libreria"` for 2 seconds → `router.push('/library/games/${gameId}')`.
2. **Conditional render:** if `useGameInLibraryStatus(gameId).inLibrary === true` → hide `+` and render `→` icon instead with label "Vai al gioco"; tap navigates without add.
3. **Error handling:**
   - `422 LibraryQuotaExceeded` → toast error "Libreria piena (X/Y)" with link to upgrade → stay on catalog, rollback optimistic update
   - `404 Game not found` (race: game deleted from catalog) → toast "Gioco non più disponibile" → refetch catalog → stay
   - Network down → rollback + toast retry → stay
4. **Wizard preservation:** the wizard path is kept accessible via a new overflow `⋯` menu action "Aggiungi con opzioni (wizard)" in `QuickActions`. Not the default, but not deleted.

**No backend changes.** `UserLibrary` commands and `useAddGameToLibrary` mutation are already correct.

**Files touched:** ~4 modified frontend (useMeepleCardActions, buildGameNavItems, QuickActions, useLibrary types). Details in `2026-04-09-s3-meeplecard-add-design.md`.

### 4.4 — S4 · Game page desktop (split + vertical rail + 5 tabs)

**Problem:** The current `/library/games/[gameId]/page.tsx` uses a zone-based `GameTableLayout` with `GameTableZoneKnowledge`, `GameTableZoneTools`, `GameTableZoneSessions`. This is inconsistent with the MeepleCard language and doesn't match the mental model "carta del gioco + strumenti".

**Decision:**
1. **Layout:** reuse the existing `SplitViewLayout.tsx` (resizable, persist `game-split-ratio` in localStorage, range 30–70%, default 50/50). No changes to `SplitViewLayout` itself.
2. **Left panel:** `<MeepleCardHero entity="game" {...gameData}>` — the real MeepleCard hero variant, full height of the pane. Shows cover, entity badge, rating, KB chip, title, subtitle, meta chips, nav footer with quick actions.
3. **Right panel:** new component `<GameTabsPanel>` with two sub-areas:
   - **Vertical rail** (74px wide, left edge): 5 tab items, each with `icon` + uppercase label, rail pattern VSCode-style. Active tab gets a left colored bar + subtle background tint.
   - **Content area** (scrollable): lazy-loaded tab content.
4. **Tabs (5, icon + label, in order):**
   - `📖 INFO` (default active) — game metadata, description, BGG data, KB status, agent status
   - `🤖 AI CHAT` — wraps existing `GameAgentChatView` component from `/library/games/[gameId]/agent`
   - `🧰 TOOLBOX` — wraps existing toolbox components, fallback placeholder if game has no toolbox
   - `🏠 HOUSE RULES` — new simple component showing house rules authored by user for this game; placeholder "coming soon" if feature gated
   - `🎲 PARTITE` — list of `MatchInstance` played for this `GameId` across all sessions, sortable by date
5. **Deprecation:** `GameTableLayout`, `GameTableZone*` components are removed. Existing routes `/library/games/[gameId]/agent`, `.../toolkit`, `.../toolbox` remain as direct-navigation aliases but the new tabs cover the same functionality in-page.

**Files touched:** ~2 modified, ~8 new (GameDetailDesktop, GameTabsPanel, 5 tab components, MeepleCardHero wrapper if needed). Details in `2026-04-09-s4-game-desktop-design.md`.

### 4.5 — S5 · Game page mobile (hand stack + drawer from top + 5 tabs)

**Problem:** The current `game-detail-mobile.tsx` is ad-hoc and doesn't match the "carte in mano" pattern that the user has already prototyped in `admin-mockups/mobile-card-layout-mockup.html`.

**Decision:**
1. **Layout** (reference: `admin-mockups/mobile-card-layout-mockup.html`):
   - `StatusBar` (24px) + `MobileTopNavbar` (52px) — reuse existing
   - `SearchBar` (42px) — reuse existing
   - `<ContentArea>` (flex-1):
     - `<HandStack>` (44px wide, left edge) — vertical stack of mini-cards (34×48) of other user library games + a "+" placeholder
     - `<FocusedCardArea>` (flex-1):
       - `<FocusedMeepleCard>` — the same Azul MeepleCard hero variant, flex-1 vertical, with `📋 Dettagli` button in the action row
       - `<CardLinksRow>` (40px) — horizontal icons of entities related to this game (Agent, Manual, Partite, overflow)
   - `<MobileActionBar>` (66px) — global: Libreria / Home / Chat / Sessioni / Profilo. Auto-hides on scroll down, reveals on scroll up.
2. **Drawer mechanism (S5 core):**
   - Trigger: tap `📋 Dettagli` button on the focused card (no swipe, no edge gesture)
   - Animation: slides down from `top: 84px` (below navbar+status), reveals tabs panel
   - Content: same 5 tab components as S4 (Info, AI Chat, Toolbox, House Rules, Partite) rendered in a mobile layout — horizontal scrollable tab bar at top, content scrollable below
   - Backdrop: dim the hand stack + focused card + action bar behind (opacity 0.35, blur 1px)
   - Close: tap backdrop, tap `✕` in navbar (replaces 🔔), swipe drawer up, or ESC key
3. **State management:** new Zustand store `game-detail-mobile-store.ts`:
   ```ts
   interface GameDetailMobileState {
     drawerOpen: boolean
     activeTab: 'info' | 'aiChat' | 'toolbox' | 'houseRules' | 'partite'
     focusedGameId: string
     handGameIds: string[]  // max 12
     openDrawer: (tab?: DrawerTab) => void
     closeDrawer: () => void
     switchTab: (tab: DrawerTab) => void
     focusGame: (gameId: string) => void
   }
   ```
4. **Hand stack population:** fetch user library (`useLibrary`), render as a **virtualized vertical list**. Show the first ~12 mini-cards without scrolling (sized to device height); remaining cards accessible via vertical scroll inside the 44px strip. Always exclude the currently focused game. Always append a `+` mini-card at the end linking to `/library?tab=catalogo`.
5. **Empty states:**
   - Hand stack empty (user has only 1 game in library) → show `+ Aggiungi giochi` placeholder
   - Card links empty (no related entities) → hide the row

**Files touched:** ~7 new (game-detail-mobile.tsx rewrite, HandStack, FocusedCardArea, GameDetailsDrawer, store, 5 mobile tab wrappers reusing S4 tab components), ~1 modified (MobileActionBar auto-hide). Details in `2026-04-09-s5-game-mobile-design.md`.

### 4.6 — S6 · Playwright E2E happy path + sub-tests + a11y

**Problem:** No end-to-end validation of the library-to-game user journey exists today, especially on mobile viewports.

**Decision:**
1. **Happy path test** `e2e/flows/library-to-game-happy-path.spec.ts`:
   - Runs on 3 projects: `mobile-chrome` (Pixel 5), `mobile-safari` (iPhone 13), `desktop-chrome` (1920×1080)
   - Steps:
     1. Login as admin fixture
     2. Navigate to `/admin/overview`, screenshot → `01-admin-dashboard.png`
     3. Tap `role="switch"` view toggle (S1) → assert URL changes away from `/admin/*`, screenshot → `02-user-home.png`
     4. Navigate to `/library?tab=catalogo`, screenshot → `03-catalog.png`
     5. Tap the "Solo giochi AI-ready" chip (S2) → assert URL contains `hasKb=true`, screenshot → `04-catalog-filtered.png`
     6. Run Axe a11y scan → assert 0 serious/critical violations
     7. Tap `+` on first MeepleCard (S3) → assert toast visible, screenshot → `05-add-toast.png`
     8. Wait for navigation to `/library/games/{id}`, screenshot → `06-game-page.png`
     9. If mobile: assert `[data-hand-stack]` visible, tap `📋 Dettagli`, assert drawer `role="dialog"` visible, screenshot → `07-mobile-drawer.png`
     10. If desktop: assert `role="tablist"` visible, screenshot → `07-desktop-split.png`
     11. Run final Axe scan

2. **Per-sub-feature tests** `e2e/sub-features/s{N}-*.spec.ts`:
   - `s1-admin-toggle.spec.ts` — focus on toggle behavior, sessionStorage persistence, private browsing fallback
   - `s2-kb-filter.spec.ts` — filter toggle, URL sync, empty state when no AI-ready games
   - `s3-meeplecard-add.spec.ts` — direct add, toast, navigate, error (quota), already-in-library state, wizard via overflow
   - `s4-game-desktop.spec.ts` — split layout, rail tab switching, lazy load tab content
   - `s5-game-mobile.spec.ts` — hand stack interactions, drawer open/close/swipe, tab switching inside drawer

3. **A11y test** `e2e/sub-features/library-to-game-a11y.spec.ts` — dedicated axe scan on each significant state (catalog, catalog filtered, game page desktop, game page mobile closed, game page mobile drawer open).

4. **CI integration:** new workflow `.github/workflows/e2e-library-to-game.yml` runs on PR to `epic/library-to-game`, uploads screenshots + Playwright HTML report as artifacts.

**Files touched:** ~8 new (1 happy path + 5 sub-tests + 1 a11y + 1 workflow), 0 modified. Details in `2026-04-09-s6-e2e-validation-design.md`.

---

## 5. Error Handling & Edge Cases (Epic-Wide)

| # | Case | Sub | Handling |
|---|---|---|---|
| 1 | sessionStorage unavailable (private browsing) | S1 | Fallback to in-memory state; toast info "Modalità vista non persistente" |
| 2 | KB filter query fallback if handler fails | S2 | Catch → return unfiltered + banner "Filtro KB temporaneamente non disponibile" |
| 3 | KB join performance > 150ms p95 | S2 | Promoted to S2b: denormalized column via event handler |
| 4 | Quota exceeded on add | S3 | 422 → toast "Libreria piena (X/Y)" → upgrade link → stay on catalog |
| 5 | Game deleted between fetch and tap | S3 | 404 → toast "Gioco non più disponibile" → refetch catalog |
| 6 | Network down during add | S3 | Optimistic rollback + toast retry |
| 7 | Tab content component missing (e.g., House Rules not implemented) | S4, S5 | Placeholder "In arrivo" with feature flag |
| 8 | Hand stack empty (user has only the current game in library) | S5 | Render only the `+ Aggiungi giochi` placeholder, linking to catalog |
| 9 | Drawer swipe vs iOS back-swipe edge conflict | S5 | Tap-only on `📋 Dettagli` button, no edge swipe |
| 10 | Deep link `/library/games/{id}` without login | Cross | Existing `RequireRole` redirect to login with `?returnTo=...` |
| 11 | Private vs shared game on game page | S4, S5 | Both supported; S3 add-to-library only applies to `SharedGameId` |
| 12 | Cache invalidation post-add | S3 | Invalidate `['library-list']`, `['library-status', id]`, `['library-quota']`, `['shared-catalog', ...]` (already in `useAddGameToLibrary`) |
| 13 | i18n | All | All new strings in `messages/it.json` + `messages/en.json` |
| 14 | Screenshot baseline missing on first CI run | S6 | Generate baseline + `test.skip` with warning the first time |

---

## 6. Performance Budgets

| Metric | Target | Responsible sub | Strategy |
|---|---|---|---|
| Catalog query with KB filter (p95) | < 150ms | S2 | Lazy join v1, denormalized column v2 if needed |
| Add-to-library + toast + navigate (time to first pixel on game page) | < 800ms from tap | S3 | Optimistic update + prefetch game detail on hover |
| Desktop game page LCP | < 1.5s | S4 | Lazy-load non-default tab content |
| Mobile hand stack initial render | < 300ms | S5 | Virtualized list (react-virtual or equivalent); ~12 items visible above the fold |
| E2E full suite runtime in CI | < 8min | S6 | 3 Playwright workers in parallel |
| Axe a11y violations (serious + critical) | 0 | S1-S5 | Per-component ARIA on PR review |

---

## 7. Testing Strategy

### Per sub-feature

| Sub | Unit (Vitest/xUnit) | Integration | E2E (Playwright) | Coverage target |
|---|---|---|---|---|
| S1 | `useViewMode` state machine, `ViewModeToggle` render | n/a | Click → redirect → sessionStorage | FE ≥ 85% |
| S2 | xUnit: `GetSharedGamesQueryHandler` with/without filter. Vitest: chip state, URL sync | xUnit Testcontainers: seed 2 AI-ready + 2 not → verify filter | Toggle chip → list reloads | BE ≥ 90% / FE ≥ 85% |
| S3 | Vitest: `buildGameNavItems` conditional, `useMeepleCardActions` branching | React Query + MSW | Tap `+` → toast → navigate | FE ≥ 85% |
| S4 | Vitest: `GameDetailDesktop` snapshot, `GameTabsPanel` tab switching, each tab component | n/a | Visit page → split + rail + 5 tabs | FE ≥ 85% |
| S5 | Vitest: `HandStack`, `FocusedCardArea`, `GameDetailsDrawer`, mobile store | n/a | Visit page → drawer open/close, tab switch | FE ≥ 85% |
| S6 | n/a (is the testing scaffolding) | n/a | All above + a11y | n/a |

### Happy path E2E pseudocode

See Section 4.6 for full test skeleton. Screenshots produced: 7 per viewport × 3 viewports = 21 PNG artifacts per CI run.

---

## 8. Definition of Done

### Epic DoD (required to merge `epic/library-to-game` → `main-dev`)

- [ ] All 6 sub-feature PRs merged into `epic/library-to-game`
- [ ] Happy path E2E test green on Pixel 5 + iPhone 13 + desktop 1920 in CI
- [ ] Backend coverage ≥ 90%, frontend coverage ≥ 85% on new files
- [ ] Axe a11y: 0 serious or critical violations at each happy path step
- [ ] Performance budgets respected (Section 6)
- [ ] Epic branch rebased on `main-dev` (no merge conflicts)
- [ ] Epic PR reviewed and approved
- [ ] Screenshot artifacts attached to the epic PR as evidence
- [ ] Documentation: `docs/frontend/game-page-layout.md` created with reference to high-fidelity mockup `.superpowers/brainstorm/39070-1775727380/content/07-real-render.html` + implementation notes
- [ ] Memory entry added to `.claude/projects/.../memory/` for this journey

### Sub-feature DoD template

- [ ] Design doc in `docs/superpowers/specs/2026-04-09-s{N}-{slug}-design.md` committed
- [ ] Implementation plan in `docs/superpowers/plans/2026-04-09-s{N}-{slug}.md` via `writing-plans` skill committed
- [ ] Code implemented following the plan
- [ ] Unit + integration tests pass locally
- [ ] Coverage target met on new files
- [ ] PR to `epic/library-to-game` with description, screenshots, link to plan
- [ ] Code review approved
- [ ] CI green (build, test, typecheck, lint)
- [ ] Squash merge into epic branch
- [ ] Linked GitHub issue closed

---

## 9. Risks & Mitigations

| Risk | Severity | Mitigation |
|---|---|---|
| S2 denormalization adds migration + event handler complexity | Med | Start with lazy join v1; promote to S2b only if p95 > 150ms |
| S4/S5 tab content missing for Toolbox / House Rules / Partite | High | Each tab is a thin wrapper; placeholder "In arrivo" under feature flag if underlying component missing |
| Hand stack fetches all user games → slow if user has 100+ | Med | Virtualized list + `useLibrary` React Query cache |
| Playwright pixel-diff flaky on font/shadow rendering | Low | No pixel-diff; only smoke + a11y. Screenshots are artifacts not assertions |
| Epic branch drifts from `main-dev` during 6 sequential PRs | Med | Weekly rebase of `epic/library-to-game` on `main-dev` |
| Mobile iOS back-swipe gesture conflicts with drawer open | Med | Tap-only drawer trigger (no swipe from edge) |
| ViewModeToggle accidentally leaks admin context | High | Guards in both shell layouts, sessionStorage cleared on logout, role check is authoritative not toggle state |

---

## 10. Appendix — Visual Reference

**High-fidelity composite mockup** (approved by user):
- File: `.superpowers/brainstorm/39070-1775727380/content/07-real-render.html`
- Shows: Desktop split view + 2 mobile states (card focus + drawer open)
- Uses real design tokens from `apps/web/src/styles/design-tokens.css`
- Fonts: Quicksand 400-700, Nunito 400-800
- Colors: warm shadows, entity HSL `game: 25 95% 45%`

**Reference mockups** (already in repo):
- `admin-mockups/mobile-card-layout-mockup.html` — "carte in mano" mobile reference
- `admin-mockups/meeple-card-real-app-render.html` — MeepleCard variants reference
- `admin-mockups/meeple-card-visual-test.html` — MeepleCard feature showcase

**Brainstorming session artifacts** (persisted in `.superpowers/brainstorm/39070-1775727380/content/`):
- `01-user-journey.html` — user flow + scope table
- `02-tabs-config.html` — tab config options (Q2)
- `03-mobile-drawer.html` — drawer pattern options (Q3)
- `04-drawer-pattern-confirmed.html` — "carte in mano" adaptation
- `05-desktop-split.html` — desktop split options (Q4)
- `07-real-render.html` — final composite render

---

## 11. Next Steps (post approval)

1. User approves this spec.
2. `writing-plans` skill generates `docs/superpowers/plans/2026-04-09-s1-view-toggle.md` (first sub-feature plan).
3. Create `epic/library-to-game` branch from `main-dev`.
4. Implement S1, PR, merge into epic.
5. Generate next sub-feature plan (S2). Repeat for S2→S4→S5→S3→S6.
6. Epic PR `epic/library-to-game` → `main-dev`.

---

**Author:** Claude (brainstorming session 2026-04-09)
**Reviewer:** user (pending)
**License:** Proprietary
