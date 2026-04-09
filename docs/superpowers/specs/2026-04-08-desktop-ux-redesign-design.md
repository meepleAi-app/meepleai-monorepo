# Desktop User UX Redesign — Design Spec

**Date:** 2026-04-08
**Status:** Proposed
**Scope:** User-facing desktop experience (Dashboard, Library, Chat with Agent) + global navigation
**Out of scope:** Mobile (already has `carte-in-mano-nav` shipped), Admin pages, Sessions deep-dive pages, Game detail pages
**Related specs:** `2026-03-28-user-pages-redesign`, `2026-03-30-layout-nav-redesign`, `2026-03-25-library-visual-redesign`

---

## 1. Context & Goals

### Context

The desktop user experience currently has three friction points that drive this redesign:

1. **Dashboard drift.** `/dashboard` (Gaming Hub) exists with a widget system (`KpiWidget`, `LeaderboardWidget`, `LibraryWidget`, `LiveSessionWidget`, `TrendingWidget`, `ChatPreviewWidget`) but visually inconsistent with the MeepleCard design language shipped in recent sprints.
2. **Library information architecture.** `/library` uses searchParam-based tabs (`?tab=catalogo`, `?tab=wishlist`, default → personal). Shared catalog and personal collection feel disconnected. Users don't have a quick overview of "what's going on" in their library.
3. **Chat entry points fragmented.** Chat with agent lives contextually under `/library/games/[gameId]/agent` and `/sessions/live/[sessionId]/agent`. There is no quick access from elsewhere. Users who want "ask the agent about X" must navigate to a game first.

The redesign must **reuse existing infrastructure** (MeepleCard component library, `useCardHand` Zustand store, NavFooter, GridCard/HeroCard/CompactCard/ListCard variants, design tokens in `apps/web/src/styles/design-tokens.css`) — not introduce a parallel design system.

### Goals

- **Unify visual language** across Dashboard, Library, Chat with the MeepleCard design system (`--nh-*` palette, warm shadows, Quicksand/Nunito, entity HSL colors, 24px radius cards).
- **Clarify IA** with a 3-link top navigation (Home / Libreria / Sessioni) + contextual mini-nav + contextual hand stack.
- **Give Chat a first-class entry point** via a global slide-over panel accessible from the top bar, while preserving contextual entry from games and sessions.
- **Reuse the "carte in mano" (cards-in-hand) concept** on desktop — cross-page context memory of recently/pinned entities, already implemented for mobile in `stores/use-card-hand.ts`.
- **Build a Library Hub** (Netflix-style carousel landing) that shows "Continua a giocare", "La tua libreria", "Dal catalogo community", "Wishlist" as horizontal caroselli, with detail tabs for deep-dive.

### Non-goals

- Changing the backend or the Alpha Mode scope of Bounded Contexts.
- Redesigning mobile layouts.
- Changing the onboarding or landing pages (`(public)/*`, `(auth)/*`).
- Redesigning deep-dive pages (`/library/[gameId]`, `/sessions/live/[sessionId]/*`, `/play-records/*`).
- Adding new features (all work is layout/UI/UX restructuring over existing data).

---

## 2. Decisions Confirmed During Brainstorming

| Question | Decision | Reasoning |
|---|---|---|
| Dashboard concept | **Gaming Hub widgets** — keep widget logic, restyle with MeepleCard tokens | Existing widgets (`HeroBanner`, `KpiWidget`, `LibraryWidget`, `LiveSessionWidget`, `ChatPreviewWidget`) represent real user value; user wants to keep them, only restyle |
| Library organization | **Hub + detail model** — one `/library` page is a landing with carousel sections (Continua / Personal / Catalogo / Wishlist), plus dedicated tab detail views | Netflix/Spotify pattern; lets users scan everything at a glance while preserving deep-dive views for power users |
| Chat entry point | **Contextual + global slide-over** — no dedicated nav link. Entry from MeepleCard quick actions, game page, session live; global access via `💬` icon in top bar (slide-over panel) | Keeps the nav minimal while making chat always 1-click away. Slide-over remembers the game context from the hand stack |
| Navigation paradigm | **Top bar + contextual mini-nav + hand stack** (option 4). Reuse the "carte in mano" concept | Combines discoverability (top bar 3 links) with contextual depth (mini-nav tabs) and persistent memory of active entities (hand stack). Already has infrastructure in `useCardHand` store |
| Visual style | **MeepleCard light warm palette** (`--nh-bg-base #faf8f5`, warm shadows, Quicksand/Nunito, entity HSL) — NOT the dark v0app navy theme | User explicitly requested "usa proprio i token e i componenti UI nel frontend". The frontend design system already has MeepleCard tokens shipped and tested |

---

## 3. Visual Identity (Design Tokens — reuse only)

All tokens below **already exist** in `apps/web/src/styles/design-tokens.css`. No new tokens introduced.

**Palette (Hybrid Warm-Modern, spec 2026-03-25):**
```
--nh-bg-base:        #faf8f5   /* page bg */
--nh-bg-surface:     #fffcf8   /* cards bg */
--nh-bg-elevated:    #fffcf8
--nh-border-default: rgba(160,120,60,0.08)
--nh-text-primary:   #1a1a1a
--nh-text-secondary: #5a4a35
--nh-text-muted:     #8a7a65
```

**Shadows (warm-brown, not neutral):**
```
--shadow-warm-sm / md / lg / xl / 2xl
```

**Typography:**
```
--font-heading: Quicksand 500-800
--font-body:    Nunito 400-800
```

**Entity HSL colors** (from `meeple-card/tokens.ts`, used for accent edges, badges, glow):
```
game     → hsl(25 95% 45%)   Orange
player   → hsl(262 83% 58%)  Purple
session  → hsl(240 60% 55%)  Indigo
agent    → hsl(38 92% 50%)   Amber
kb       → hsl(210 40% 55%)  Slate
chat     → hsl(220 80% 55%)  Blue
event    → hsl(350 89% 60%)  Rose
toolkit  → hsl(142 70% 45%)  Green
```

**Radius:** `--radius-xl` (1rem) for small/medium surfaces, `rounded-2xl` (1.5rem / 24px) for MeepleCard, `--radius-lg` (0.75rem) for buttons/pills.

**Layout constants:**
```
--top-bar-height:          64px (increased from current 52px for breathing room)
--card-rack-width:         76px (expanded from 64px mobile to give desktop more presence)
--card-rack-hover-width:   240px (unchanged)
--mini-nav-height:         48px (new)
```

---

## 4. Shell Architecture (global layout)

Desktop layout is composed of four persistent regions:

```
┌────────────────────────────────────────────────────────────────┐
│  TOP BAR 64px                                                  │
│  [logo] [Home Libreria Sessioni] [search ⌘K] [💬 🔔 avatar]    │
├────────────────────────────────────────────────────────────────┤
│  MINI-NAV 48px                                                 │
│  ›section · tab1 tab2 tab3               [primary action]     │
├────┬───────────────────────────────────────────────────────────┤
│ H  │                                                           │
│ A  │                                                           │
│ N  │                   MAIN CONTENT                            │
│ D  │                                                           │
│    │                                                           │
│ 76 │                   (scrollable)                            │
│    │                                                           │
└────┴───────────────────────────────────────────────────────────┘
```

### 4.1 Top Bar (64px, sticky)

**Left:** Logo (hex gradient orange→amber, `Meeple**Ai**` wordmark with `.ai` in entity-game orange).
**Center-left:** 3 nav links (Home / Libreria / Sessioni) as pill buttons with entity-game tint for active state.
**Center:** Search pill (`⌘K`) — expandable global search across games/sessions/rules/players. Padding `11px 20px`, min-width 340px, radius 12px, warm hover state.
**Right:** Icon button `💬` (chat panel trigger) with notification dot, icon button `🔔` (notifications), user avatar 40px circle with purple border (entity-player color).

**Behavior:**
- Sticky position, backdrop-blur on scroll.
- Active top-level link determined by pathname segment (`/`, `/library`, `/sessions`).
- `💬` icon toggles the global Chat slide-over panel (§6.3).
- `⌘K` opens command palette / global search (future enhancement — MVP can be simple search input).

### 4.2 Mini-nav contestuale (48px)

**Left:** Breadcrumb `section · **current**` + contextual tabs (e.g., `Panoramica | Attività | Statistiche` on dashboard; `Hub | Personal | Catalogo | Wishlist | Collezioni` on library).
**Right:** Primary page action button (`＋ Aggiungi gioco` on library, `＋ Nuova partita` on dashboard, etc.), warm orange gradient.

**Behavior:**
- Rendered via `useNavConfig` hook (already exists) — each page registers its mini-nav config.
- Active tab has a 2px underline in `entity-game` orange.
- Primary action button is optional (shown only when the page declares one).

### 4.3 Hand Stack (76px, persistent left rail)

Reuses the `useCardHand` Zustand store (`apps/web/src/stores/use-card-hand.ts`) already shipped for mobile.

**Content:** Mini cards (52×72px) of the user's current "in hand" context — recently opened games, sessions, chats, agents. Pinnable cards persist across pages (localStorage). Non-pinned cards are LRU-evicted (max 10).

**Per-card layout:**
- Cover (42px height) with entity-gradient background
- 3px left accent edge in entity color
- Title (7px Quicksand, bottom)
- Active state: `translateX(6px) scale(1.1)`, warm-lg shadow, 2px outline in entity color
- Hover: `translateX(3px) scale(1.05)`, warm-md shadow

**Bottom toolbar (40px section):**
- 📌 Pin / unpin current focus
- ⇔ Expand rail to 240px (shows card title + metadata)

**Behavior:**
- Cards are drawn automatically when entering a page (`drawCard({ id, entity, title, href })`).
- Clicking a card navigates to its `href`.
- Pin state stored in `localStorage` under `meeple-card-pins`.
- Collapsed/expanded state under `meeple-hand-collapsed`.

---

## 5. Page-by-Page Specs

### 5.1 Dashboard — Gaming Hub (`/` or `/dashboard`)

Purpose: personalized landing after login showing "what's going on" and driving action.

**Mini-nav:** `Home · Gaming Hub | Panoramica · Attività · Statistiche · Trending` + action `＋ Nuova partita`.

**Sections (vertical scroll):**

1. **Greeting row** — `h1` "Ciao Marco 👋" with gradient orange-amber on name. Subtitle "Ecco cosa succede nella tua tavola oggi". Right side: 3 quick stats inline (Partite mese / Win rate / Tempo gioco).

2. **Hero Live Session widget** — full-width `HeroCard` variant of MeepleCard. Entity=session. Shows `hc-cover` (300px) + accent bar indigo + entity badge + title/subtitle/chips + action row (`Continua partita` primary, `Scoreboard` + `Chiedi regola` ghost) + `In corso` pulse badge top-right. Hidden if no active session — replaced by "last session" card.

3. **KPI strip** — 4 `StatCard` components (existing showcase component). Entities: game, session, player, chat. Each card: icon box tinted in entity color, label, value, trend chip (up/down with %).

4. **Continua a giocare** carousel — 4-5 `MeepleCard` (GridCard variant). Each card with progress bar overlay if there's an active session for that game. Quick actions hover: `▶ Continua` + `💬 Chat`.

5. **Chat recenti con l'agente** — 3 compact chat cards (custom layout, chat entity accent). Each shows icon box + title (`Azul · Turno finale`) + snippet (2-line clamp) + ConfidenceBadge + timestamp. Click opens chat panel with that conversation.

6. **Amici attivi** — 4 `CompactCard` Player variant. Thumb avatar with entity-player gradient + name + status line + online dot.

**Component reuse:**
- `HeroCard` variant of MeepleCard → hero live session
- `GridCard` variant of MeepleCard → game cards in carousel
- `CompactCard` variant → friends
- `StatCard` (showcase) → KPI strip
- `ConfidenceBadge` (showcase) → chat cards
- Existing widgets `HeroBanner`, `KpiWidget`, `LibraryWidget`, `LiveSessionWidget`, `ChatPreviewWidget` will be **restyled** (not rewritten) to use the above components internally.

### 5.2 Library Hub (`/library`)

Purpose: single entry point for personal library + shared catalog + wishlist, with deep-dive tabs.

**Mini-nav:** `Libreria · Hub | **Hub** · Personal 47 · Catalogo 230 · Wishlist 8 · Collezioni` + action `＋ Aggiungi gioco`.

**Tab routing:** keep existing searchParam pattern. Default (no tab) → Hub mode with carousels. `?tab=personal` → grid view. `?tab=catalogo` → catalog grid. `?tab=wishlist` → wishlist grid. `?tab=collezioni` → future.

**Hub mode layout (default tab):**

1. **Library header** — h1 "La tua libreria" with orange accent bar + subtitle. Right side: 3 stat cards (Tuoi giochi / Catalogo / Wishlist) — colored by entity (game / session / wishlist amber).

2. **Filter bar** — horizontal pill filters (Tutti, Strategici, Familiari, Cooperativi, Party, 👥 2–4, ⏱ <60m) + sort dropdown + view toggle (caroselli / griglia / lista). Filters apply to ALL sections below.

3. **Carousel "Continua a giocare"** — 4+ `MeepleCard` (GridCard), session accent. Each card has progress bar if in-progress session + "last played X days ago" footer.

4. **Carousel "La tua libreria personale"** — 5+ `MeepleCard` + "Add new game" ghost card (dashed border, ＋ icon) at the end. See-all → `?tab=personal`.

5. **Carousel "Dal catalogo community"** — 5+ `MeepleCard` with status badge `Catalog` (frosted pill, not-in-lib state). Quick actions: `➕` add to library, `⭐` add to wishlist. See-all → `?tab=catalogo`.

6. **Carousel "La tua wishlist"** — 4+ `MeepleCard` with status `★ Wishlist` amber. Quick actions: `✕` remove, `➕` add to library. See-all → `?tab=wishlist`.

**Detail tab views (`?tab=personal | catalogo | wishlist`):**
- Full grid (5-col desktop, existing `PersonalLibraryPage`, `PublicLibraryPageClient`, `WishlistPageClient`).
- Filter bar remains at top.
- Hand stack unchanged.

**Component reuse:**
- `GridCard` variant → all game cards
- `EntityListView` (showcase) → detail tab grids
- `GameCarousel` or custom horizontal carousel → hub caroselli (preference: extend existing carousel pattern used in other pages)
- Existing `PersonalLibraryPage`, `PublicLibraryPageClient`, `WishlistPageClient` → detail tabs (already shipped, visually refined only)

**Carousel scroll behavior:** horizontal `overflow-x-auto` with `scroll-snap-type: x mandatory`, snap points on each card. Nav buttons ‹ › in the section header scroll by ±1 page. See-all link navigates to the respective tab.

### 5.3 Chat Slide-Over Panel (global)

Purpose: first-class chat entry accessible from anywhere, context-aware.

**Trigger:** `💬` icon in top bar (any page). Also opens from MeepleCard `💬` quick action, from game page "Chiedi all'agente" CTA, from live session `Chiedi regola` button.

**Layout (overlay from right):**
- Panel width: `760px`, `max-width: 60%` of viewport
- Animates in with `translateX(30px) → 0` + fade over 350ms
- Backdrop behind: `rgba(40,28,14,0.28)` + subtle `blur(2px)` on underlying page
- Top bar remains visible; `💬` icon shows highlighted (blue tint + ring)

**Internal structure:**

```
┌ panel-header ─────────────────────────────┐
│ [💬 icon box] Chat con l'agente      [✕] │
│              KB aggiornata 2 giorni fa   │
├ ctx-bar ───────────────────────────────── │
│ Contesto: [Azul pill ▾]    [📜 Storia] [⚙]│
├ panel-body ─────────────────────────────── │
│ ┌── chat-side (220px) ──┬── chat-main ──┐ │
│ │ [+ Nuova chat]        │   messages    │ │
│ │                        │               │ │
│ │ ▾ Chat recenti        │   ...         │ │
│ │ • Azul Turno finale ● │               │ │
│ │ • Wingspan bonus      │   [input bar] │ │
│ │ • Everdell eventi     │               │ │
│ │                        │               │ │
│ │ ▾ Giochi con KB       │               │ │
│ │ • Azul    ● ready     │               │ │
│ │ • Wings   ● ready     │               │ │
│ │ • Everd   ● indexing  │               │ │
│ └───────────────────────┴───────────────┘ │
└───────────────────────────────────────────┘
```

**Context switcher (ctx-bar):**
- Current game pill: mini-cover + name + meta (year · N PDF · KB status) + chevron dropdown
- Dropdown opens game picker (searchable list of games with KB ready)
- On open, auto-selects the currently focused card from the hand stack if it's a game

**Chat sidebar (220px):**
- `＋ Nuova chat` button (blue gradient, entity-chat color)
- "Chat recenti" section: list of recent chats, each with game emoji, 2-line title, timestamp. Active chat has blue accent + tinted background.
- "Giochi con KB" section: list of games with KB, mini-thumb + name + status dot (ready green / indexing amber pulsing).
- Sidebar is collapsible (optional, future) — MVP always visible.

**Chat main area:**
- Messages list (scrollable). Each message = `ChatMessage` showcase component:
  - User: right-aligned bubble, amber tinted (`hsl(25 95% 94%)` bg + `hsla(25,95%,45%,0.2)` border), avatar with user initials on entity-player purple gradient.
  - Assistant: left-aligned bubble, elevated surface, avatar `🎲` on orange-amber gradient. Supports markdown (h4, strong, ul, p).
- **Citation RuleSourceCard** embedded below assistant messages: doc icon (entity-kb slate) + doc name + page badge(s) + italic excerpt + "Apri regolamento →" link.
- **Confidence badge** (high 90%+ green, med 70-90% amber, low red) + feedback buttons (👍 👎 📋 ↗) below each assistant message.
- **Suggested questions**: row of pill chips appearing after assistant messages or as empty state.

**Input bar:**
- Textarea input (resizable, rounded 16px, focus ring blue)
- Icon buttons: `📎` allega PDF, `🎤` dettatura, `➤ send` (blue gradient)
- Keyboard hints below: `Enter` send, `⇧ Enter` newline, `Esc` close
- Disclaimer: "✦ Risposte basate solo sulla KB caricata"

**Close behavior:**
- `Esc` key closes the panel (restores scroll lock on body)
- Click on backdrop closes
- Close button `✕` in header
- State persisted in URL via `?chat=open&gameId=X` (so refresh reopens)

**Component reuse:**
- `ChatMessage` (showcase) → message bubbles
- `RuleSourceCard` (showcase) → citations
- `ConfidenceBadge` (showcase) → confidence score
- `Sheet` primitive (existing UI overlays) → slide-over panel
- Existing chat services / API calls → no backend changes

---

## 6. Information Architecture

```
(authenticated)
├── / or /dashboard         → Dashboard (Gaming Hub) — restyled
├── /library                → Library Hub (default Hub mode)
│   ├── ?tab=personal      → personal grid (existing, restyled)
│   ├── ?tab=catalogo      → catalog grid (existing, restyled)
│   ├── ?tab=wishlist      → wishlist grid (existing, restyled)
│   └── ?tab=collezioni    → future, shell in place
├── /library/[gameId]       → game detail (existing, not in scope)
├── /library/private/[id]   → private game detail (existing, not in scope)
├── /sessions               → sessions list (existing, not in scope)
├── /sessions/live/[id]/*   → live session (existing, not in scope)
└── /play-records/*         → play records (existing, not in scope)
```

Chat has no dedicated route — it's a global overlay driven by `?chat=open` URL param.

**Routes REMOVED or MOVED:**
- `/library/games/[gameId]/agent` → replaced by chat slide-over opened with `?chat=open&gameId=X`
- `/knowledge-base` — stays for Editor/Admin, not in user IA
- `/editor/*`, `/n8n`, `/pipeline-builder` — not in user IA (admin/editor only)

---

## 7. Component Map (what to build / reuse)

### Reused as-is (0 changes)
- `MeepleCard` (all variants: `GridCard`, `HeroCard`, `CompactCard`, `ListCard`, `FeaturedCard`)
- `AccentBorder`, `Cover`, `EntityBadge`, `MetaChips`, `NavFooter`, `QuickActions`, `Rating`, `StatusBadge`, `TagStrip`
- `ChatMessage`, `RuleSourceCard`, `ConfidenceBadge`, `StatCard`, `CollectionProgress` (all from showcase)
- `Sheet`, `Dialog`, `Tooltip`, `Popover` (UI overlays)
- `useCardHand` store
- `useNavConfig` hook (mini-nav config)
- `PersonalLibraryPage`, `PublicLibraryPageClient`, `WishlistPageClient` (tab detail views)

### New components to build
- `LibraryHubCarousel` — horizontal carousel with scroll-snap, section header with count badge + nav buttons + see-all link. Wraps `MeepleCard` children. Accepts `entity` prop for accent color.
- `LibraryHubHeader` — `h1` with accent bar + subtitle + 3 stat cards row.
- `FilterBar` — horizontal pill filters + sort + view toggle. Extend or replace existing filter bar in library.
- `ChatSlideOverPanel` — the global slide-over panel (header + ctx-bar + 2-col body + input bar). Uses existing `Sheet` primitive for overlay mechanics.
- `ChatContextSwitcher` — the `ctx-pill` with game picker dropdown.
- `ChatSidebar` — 220px sidebar with new-chat button, chat history, KB games list.
- `DesktopHandStack` — desktop-specific variant of the mobile `HandSidebar`. Reuses `useCardHand` store, renders 52×72 mini cards vertically with `hand-toolbar` at bottom. The mobile component lives at `components/ui/data-display/meeple-card/mobile/HandSidebar.tsx` — extract shared logic into `shared/` and add `desktop/HandRail.tsx`.
- `TopBar` — desktop version replacing existing top-level nav. 64px, logo + 3 nav links + search pill + chat icon + notification icon + avatar.
- `MiniNav` — desktop contextual mini-nav (48px) driven by `useNavConfig`. May already exist as `NavContextTabs` — verify and extend.

### Restyled (keep structure, update visuals)
- `DashboardClient` and all widgets in `apps/web/src/app/(authenticated)/dashboard/widgets/` — replace internal card markup with MeepleCard variants.
- `UnifiedShell` / `HybridSidebar` — evolve into the new top bar + mini-nav + hand stack architecture. May be renamed/refactored.
- `FloatingActionPill` — may be removed or repurposed; its role is absorbed by the mini-nav primary action button on desktop (still needed for mobile).

---

## 8. Interaction Patterns

**Hand stack draw:** Every page mounts an effect that calls `drawCard({ id, entity, title, href, imageUrl })` with the current page's subject entity. The card stays in hand for the session; pinning makes it persist across sessions.

**Chat open from MeepleCard:** Quick action `💬` on a game MeepleCard dispatches `openChatPanel({ gameId })`. The panel opens, context switcher pre-selects the game, sidebar highlights the last chat for that game (if any), messages load.

**Chat open from top bar:** `💬` icon dispatches `openChatPanel()` with no gameId. The context switcher defaults to the currently focused card in the hand stack if it's a game; otherwise shows "seleziona gioco" prompt.

**Chat URL state:** Opening the panel sets `?chat=open&gameId=X&chatId=Y` (replace, not push). Closing removes these params. Refresh restores the state.

**Carousel keyboard nav:** `← →` arrows scroll the carousel by one card when focused. Tab moves focus to next card.

**Mini-nav sync:** Active tab is derived from URL. Clicking a tab updates URL (push). Breadcrumb is static per page.

**Search (⌘K):** MVP is a simple expandable input that navigates to `/library?q=...`. Future: command palette with entity jumps.

---

## 9. Responsive Considerations

**Desktop ≥1280px:** full layout as specified.
**Desktop 1024-1279px:** hand stack collapses to 60px width, search pill shrinks to 280px.
**Tablet 768-1023px:** hand stack hidden by default, accessible via toggle button in mini-nav. Top bar compressed (logo + hamburger + search + chat + avatar).
**Mobile <768px:** Not in scope. `carte-in-mano-nav` mobile experience already shipped and unchanged.

The existing `LibraryMobile`, `DashboardMobile` components remain untouched; the redesign only affects the `hidden md:block` desktop paths.

---

## 10. Migration Strategy

Incremental: ship shell first, then page by page. Feature-flag via `NEXT_PUBLIC_UX_REDESIGN=true` to allow A/B or gradual rollout.

1. **Phase 1 — Shell:** New `TopBar`, `MiniNav`, `DesktopHandStack`. Replace `UnifiedShell` contents behind flag. Dashboard and Library still render old widgets inside new shell.
2. **Phase 2 — Dashboard:** Restyle widgets to use MeepleCard variants. No structural change.
3. **Phase 3 — Library Hub:** Build `LibraryHubCarousel`, `LibraryHubHeader`, new filter bar. Wire default `?tab=` (no param) to hub mode. Existing tab detail views unchanged.
4. **Phase 4 — Chat slide-over:** Build `ChatSlideOverPanel`, wire top bar trigger + MeepleCard quick action. Deprecate `/library/games/[gameId]/agent` route (keep as redirect).
5. **Phase 5 — Cleanup:** Remove feature flag, delete old shell components, update tests.

Each phase is independently mergeable and shippable.

---

## 11. Open Questions & Risks

| # | Question / Risk | Impact | Mitigation |
|---|---|---|---|
| Q1 | Should `/dashboard` route remain or be promoted to `/`? | Low | Keep `/dashboard` for now; map `/` via redirect middleware. Decide in Phase 2. |
| Q2 | `NavContextTabs` component — does it match the new `MiniNav` spec, or needs refactor? | Med | Verify in Phase 1 during shell build. If mismatch, extend existing component. |
| Q3 | Carousel component — build new or extend `GameCarousel` from showcase? | Med | Review `GameCarousel` story first. If it supports custom cards + horizontal snap, extend it; otherwise build `LibraryHubCarousel` as a wrapper. |
| Q4 | Search behavior (⌘K) — simple input or command palette? | Low | MVP is simple input. Command palette is a follow-up spec. |
| R1 | Chat panel URL state may conflict with existing `?action=add` drawer state on library | Med | Namespace params: library uses `?action=add`, chat uses `?chat=open`. Verify no collision. |
| R2 | `useCardHand` store currently caps at 10 cards — enough for desktop? | Low | Yes, matches UX expectation. Revisit if users report scrolling fatigue. |
| R3 | Feature flag rollout may produce visual inconsistency during transition | Med | Ship Phase 1 (shell) fully before starting Phase 2. Flag toggles the whole desktop path at once. |
| R4 | `carte-in-mano-nav` mobile and new desktop hand stack share store — need to verify mobile behavior doesn't regress | High | Add integration tests for `useCardHand` covering both desktop and mobile mount scenarios in Phase 1. |

---

## 12. References

**Mockups (interactive, in `.superpowers/brainstorm/`):**
- `shell-overview-v2.html` — global shell architecture
- `dashboard-gaming-hub.html` — full Dashboard Gaming Hub
- `library-hub.html` — Library Hub with caroselli
- `chat-panel.html` — Chat slide-over panel

**Frontend source:**
- `apps/web/src/styles/design-tokens.css` — all design tokens (reuse)
- `apps/web/src/components/ui/data-display/meeple-card/` — MeepleCard component system (reuse)
- `apps/web/src/stores/use-card-hand.ts` — hand stack store (reuse)
- `apps/web/src/components/showcase/` — showcase components inventory
- `apps/web/src/app/(authenticated)/dashboard/` — current dashboard to restyle
- `apps/web/src/app/(authenticated)/library/_content.tsx` — current library tab routing

**Wireframe reference:**
- `v0app/` — wireframe layout reference (top bar + library + chat IA)
- `admin-mockups/meeple-card-real-app-render.html` — MeepleCard visual reference
- `admin-mockups/mobile-card-layout-mockup.html` — "carte in mano" concept reference

**Related specs (context):**
- `2026-03-25-library-visual-redesign` — Hybrid Warm-Modern palette spec
- `2026-03-28-user-pages-redesign` — prior user-pages pass
- `2026-03-30-layout-nav-redesign` — shell evolution prior context
- `2026-04-01-meeple-logo-redesign` — logo source of truth
