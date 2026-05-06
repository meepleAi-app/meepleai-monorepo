# MeepleAI Alpha — Feature Specification & UI Redesign

**Date**: 2026-03-18
**Status**: Draft
**Scope**: Alpha release (invite-only), mobile-first UI redesign, feature completeness end-to-end

---

## 1. Vision

Alpha MeepleAI is an invite-only board game companion app centered on the **MeepleCard** as the universal UI element. The alpha validates the full end-to-end user journey: receive invitation → build library → upload rules → play a game session with AI-assisted scoring, toolkit, and real-time RAG chat.

**Target users**: Mix of board game enthusiasts (expert players, session organizers, collectors) testing the complete flow.

**Design philosophy**: Mobile-first, gesture-based navigation, minimal chrome, MeepleCard-centric. Every screen is a collection of MeepleCards. The UI framework disappears — the cards ARE the interface.

---

## 2. Layout Architecture

### 2.1 Mobile Layout (Primary)

```
┌─────────────────────────────┐
│ AlphaTopNav                 │
│ [Logo]  [Titolo]  [🔔][👤] │
├─────────────────────────────┤
│                             │
│  SwipeableContainer         │
│  ┌───┬───┬───┬───┐         │
│  │ H │ L │ P │ C │ panels  │
│  └───┴───┴───┴───┘         │
│                             │
│  (active panel content)     │
│  MeepleCard grid/list       │
│  pull-to-refresh            │
│  infinite scroll            │
│                             │
├─────────────────────────────┤
│ AlphaTabBar                 │
│ [🏠Home][📚Lib][🎲Play][💬Chat]│
└─────────────────────────────┘
```

**AlphaTopNav** (sticky, h-14, z-40):
- Left: MeepleAI logo (small)
- Center: Current section title + entity color icon
- Right: Notification badge + user avatar (tap → profile/settings/logout slide-up)
- Style: `bg-background/90 backdrop-blur-xl`
- Behavior: Hide on scroll down, show on scroll up (mobile). Always visible desktop.

**AlphaTabBar** (fixed bottom, z-40):
- 4 tabs: Home, Libreria, Play, Chat
- Touch targets: 44x44px minimum (WCAG 2.1 AA)
- Active tab: entity-colored icon + label
- Style: `bg-card/90 backdrop-blur-md backdrop-saturate-150`

**SwipeableContainer**:
- Horizontal swipe between adjacent tabs
- Each tab is an independent panel with own scroll position
- Cross-fade transition on swipe (250ms ease-out)

**CardDetailModal**:
- Tap any MeepleCard → full-screen slide-up modal
- Uses existing ExtraMeepleCard/ExtraMeepleCardDrawer pattern
- Swipe down or back button to dismiss
- Transition: `framer-motion` layout animation (scale + opacity) — already used in project. No `viewTransitionName` (limited browser support, no Next.js native integration).

### 2.2 Desktop Adaptation

- Left sidebar: minimal icon rail (4 icons matching tabs) + expanded on hover
- Content area: MeepleCard responsive grid (1→2→3→4 columns)
- Detail view: `CardDetailModal` renders as side panel (right, 40% width) on desktop via internal responsive logic (`useResponsive()` hook). Same component as mobile, different presentation.
- TopNav: always visible, no hide-on-scroll

### 2.3 Components Removed from Alpha

| Component | Reason |
|-----------|--------|
| `CardStack` (desktop sidebar) | Replaced by AlphaTabBar + simple icon sidebar |
| `HandDrawer` (horizontal card scroll) | Replaced by SwipeableContainer |
| `ContextualBottomSheet` | Actions integrated into cards directly |
| `ContextualBottomNav` (desktop bottom) | Actions in card detail view |
| `useCardHand()` Zustand store | Not initialized by AlphaShell. Store still exists for legacy pages — no migration of 37 consumers needed. Pages within alpha tabs use `useAlphaNav()` instead. |
| `useContextualEntity()` | Not needed with modal-based detail |
| `useContextualSheetActions()` | Actions in cards |
| `usePlaceholderActions()` | Replaced by EmptyStateCard |
| `NavbarMiniCards` | Not needed without card-hand |

### 2.4 Components Retained Without Modification

- MeepleCard (all 16 entity types, 6 variants, 19+ features)
- ExtraMeepleCard + drawer entity components
- Chat components (SSE streaming, ChatThreadView)
- Session scoring components (Scoreboard, LiveIndicator)
- Toolkit / Tool rail
- Auth pages (register, login, verify-email, reset-password)
- Onboarding page
- Admin wizard (BGG import + PDF upload)
- PDF upload components

---

## 3. Design System Consolidation

### 3.1 Principle

Every screen is a **collection of MeepleCards**. Chrome (navigation, headers, toolbars) is minimal. Cards ARE the interface.

### 3.2 Card Density Hierarchy

| Context | Variant | Usage |
|---------|---------|-------|
| Feed / List | `grid` (1-col mobile, full-width) | Home feed, game list, sessions |
| Quick selection | `compact` (40×40 avatar) | Add players, choose game for session |
| Detail | `hero` + `expanded` | Tap card → hero header + expanded content |

### 3.3 Color System

All 16 entity colors retained. Coerency rules:

- **Card border**: `1px solid hsl(entity-color / 0.2)` — subtle colored border
- **Card hover/active**: `hsl(entity-color / 0.08)` background tint
- **Active tab**: icon colored with section's entity color
- **Status badges**: consistent style and colors everywhere
- **Page background**: neutral `bg-background`, cards bring the color

### 3.4 Spacing Scale

```css
--space-1: 4px;   /* micro gap (inside card) */
--space-2: 8px;   /* intra-element (badge to text) */
--space-3: 12px;  /* card padding (compact) */
--space-4: 16px;  /* card padding (grid/list) */
--space-6: 24px;  /* gap between cards in grid */
--space-8: 32px;  /* section separator */
--space-12: 48px; /* page margin top */
```

### 3.5 Typography Scale

| Token | Font | Size | Usage |
|-------|------|------|-------|
| `--text-xs` | Nunito Regular | 12px | Badge text, timestamps |
| `--text-sm` | Nunito Regular | 14px | Metadata, descriptions |
| `--text-base` | Nunito Regular | 16px | Body text |
| `--text-lg` | Quicksand Bold | 20px | Card titles, section headers |
| `--text-xl` | Quicksand Bold | 24px | Page titles |

Maximum 5 sizes, no exceptions. Quicksand Bold for titles, Nunito for everything else.

### 3.6 Glassmorphism (3 elements only)

| Element | Style |
|---------|-------|
| TopNav | `bg-background/90 backdrop-blur-xl` |
| TabBar | `bg-card/90 backdrop-blur-md` |
| Detail modal overlay | `bg-background/95 backdrop-blur-sm` |

No other glassmorphism. Too much = visual confusion.

### 3.7 Transitions

| Transition | Duration | Easing |
|------------|----------|--------|
| Card → Detail | 250ms | `framer-motion` layoutId animation (scale + opacity). Fallback: simple opacity fade. |
| Tab switch (swipe) | 250ms | ease-out cross-fade |
| List loading | — | Skeleton card (MeepleCard shape, pulsing) |
| Card hover (desktop) | 350ms | cubic-bezier(0.4, 0, 0.2, 1) |

### 3.8 Empty States

Every empty section shows an `EmptyStateCard` — a **standalone component** that visually mimics MeepleCard styling (same border radius, shadow, entity colors) but is NOT a MeepleCard variant. It does not modify or extend MeepleCard. It is rendered in the same grid as MeepleCards and replaces itself with real cards as data appears.

Properties:
- Light illustration (not stock art)
- Contextual message + primary CTA button
- Examples:
  - Library empty: "Nessun gioco ancora. Esplora il catalogo" → [Sfoglia Giochi]
  - Sessions empty: "Nessuna partita. Inizia una serata!" → [Nuova Sessione]
  - Chat empty: "Nessuna conversazione. Chiedi al tuo assistente AI!" → [Nuova Chat]
  - KB empty: "Nessun documento. Carica le regole del gioco" → [Carica PDF]

Placeholder cards replace themselves with real cards as the user completes actions.

---

## 4. User Flows

### 4.1 Onboarding (Invite → Ready to Play)

```
Admin sends email invitation (with link)
  → User clicks link → /register (name, email pre-filled, password)
  → /verify-email (email confirmation)
  → /onboarding (display name + avatar upload)
  → /home (dashboard with guided empty state)
```

**Home on first access** shows 3 EmptyStateCard placeholders in order:
1. "Aggiungi il tuo primo gioco" → navigates to Library/Catalog
2. "Carica le regole del tuo gioco" → navigates to PDF Upload
3. "Inizia la tua prima partita" → navigates to Create Session

Cards replace with real content as user progresses.

### 4.2 Library (Add Game + PDF → RAG Ready)

```
Library tab → sub-tabs [I Miei Giochi | Catalogo | Wishlist]

Catalogo:
  → MeepleCard grid (shared games, search + filter by mechanic/category)
  → Tap card → CardDetailModal (hero + expanded)
  → [Aggiungi alla Libreria] → card appears in "I Miei Giochi"

I Miei Giochi:
  → MeepleCard grid (personal games)
  → Tap card → Detail with tabs [Info | KB | Sessioni | Agente]

  KB tab:
    → List of MeepleCard entity="kb" (uploaded documents)
    → [Carica PDF] → upload flow
    → Each kb card shows the authoritative PdfProcessingState enum:
      Pending → Uploading → Extracting → Chunking → Embedding → Indexing → Ready
      (Failed = 99 can occur at any stage)
    → Animated badge on card (pulsing for active states: Uploading, Extracting, Chunking, Embedding, Indexing)
    → "Ready" = ✅ RAG ready, green badge
    → "Failed" = ❌ red badge with retry option
    → Source: Api/BoundedContexts/DocumentProcessing/Domain/Enums/PdfProcessingState.cs
    → Note: ProcessingStatus (4-state) is deprecated — use PdfProcessingState only

Custom game:
  → [+ Nuovo Gioco] (FAB or EmptyStateCard)
  → 3-step wizard: Name/Description → Image → Confirm
  → Card appears in "I Miei Giochi"
```

### 4.3 Game Session (Create → Play → Save → Resume)

```
Play tab → [Sessioni Attive | Storico]

ROUTING: The Play tab panel lives inside SwipeableContainer (AlphaShell).
It shows session cards in a grid. Tapping a session card:
  - setup/paused → navigates to /sessions/live/[id] (EXITS AlphaShell, dedicated layout)
  - completed → opens CardDetailModal (stays in AlphaShell)
Creating a new session → wizard modal in Play tab → on confirm → navigates to /sessions/live/[id]

Active Sessions:
  → MeepleCard entity="session" with status badge (setup/inProgress/paused)
  → Tap paused session → resumes game

Create new session:
  → [Nuova Sessione] FAB
  → Step 1: Choose game (compact card list from library)
  → Step 2: Add players (name + color, no account required — guest players only for alpha)
  → Step 3: Confirm → session created, status "setup"

Active session screen (REPLACES AlphaTabBar — dedicated full-screen layout):
  → Navigates to /sessions/live/[sessionId] which uses its own layout
  → AlphaTabBar and AlphaTopNav are hidden during active session
  → Session has its own TopBar with game name + back button
  → Back button returns to Play tab in AlphaShell
  ┌─────────────────────────┐
  │ TopBar: Game name + ⏱️   │
  │ Status: Round 3 · Turn  │
  ├─────────────────────────┤
  │                         │
  │   Scoreboard (grid)     │
  │   Player1: 12  [+][-]  │
  │   Player2: 8   [+][-]  │
  │   Player3: 15  [+][-]  │
  │                         │
  ├─────────────────────────┤
  │ Tool Rail (h-scroll):   │
  │ [🎲Dice][⏱️Timer][📝Notes][💬AI] │
  ├─────────────────────────┤
  │ [⏸️ Pause]  [✅ End]    │
  └─────────────────────────┘

Tool Rail expanded:
  → Tap "🎲 Dice" → dice panel slides above scoreboard
  → Tap "💬 AI" → inline RAG chat (ask rules on the fly)
  → Tap "📝 Notes" → session notes
  → Game-specific tools (if configured)

Save and resume:
  → [⏸️ Pause] → creates checkpoint, session → "paused"
  → Card in "Active Sessions" shows paused status + last round
  → Tap → resumes from exact checkpoint
  → [✅ End] → finalize, calculate winner, save to history
```

### 4.4 AI Chat (RAG In and Out of Session)

```
Chat tab → MeepleCard list entity="chatSession"
  → Each card: agent name, linked game, last message, unread badge

New chat:
  → [Nuova Chat] → choose agent (compact card list)
  → Chat view: messages + input at bottom
  → SSE streaming for AI responses
  → KB citations inline (tap to expand source)

From active session:
  → Tool Rail → [💬 AI] → contextual chat opens
  → Agent has context of current game
  → Ask specific rules while playing
```

### 4.5 Admin (Populate Catalog)

```
Admin (accessible from avatar menu → "Admin"):
  → Wizard: Import from BGG → Upload PDF → Create Agent
  → Each step uses same MeepleCard components
  → Processing queue visible with KB cards + pipeline status
```

---

## 5. Feature List

### 5.1 Tier 1 — MUST HAVE

| ID | Feature | Backend | Frontend | Description |
|----|---------|---------|----------|-------------|
| F1 | Email invitation registration | ✅ Ready | ⚠️ Refine | Admin sends email → link → register form |
| F2 | Post-registration onboarding | ✅ Ready | ⚠️ Refine | Display name + avatar + guided home |
| F3 | Personalized home feed | ✅ Ready | 🔨 New | Recent cards + active sessions + guided placeholders |
| F4 | Shared game catalog browse | ✅ Ready | ⚠️ Refine | MeepleCard grid, search, filter by mechanic/category |
| F5 | Add game to personal library | ✅ Ready | ⚠️ Refine | From shared catalog + custom games (3-step wizard) |
| F6 | PDF upload + processing status | ✅ Ready | ⚠️ Refine | Upload → KB card with 7 pipeline states visible |
| F7 | Create game session | ✅ Ready | ⚠️ Refine | Choose game → add players → confirm |
| F8 | Live scoreboard with scoring | ✅ Ready | ⚠️ Refine | Round-by-round scores, +/-, turns |
| F9 | Tool rail (dice, timer, notes, AI chat) | ✅ Ready | ⚠️ Refine | Horizontal scroll, expandable panels |
| F10 | Pause/Resume session | ✅ Ready | ⚠️ Refine | Checkpoint → paused card → tap resumes |
| F11 | RAG chat with AI agent | ✅ Ready | ⚠️ Refine | SSE streaming, inline citations |
| F12 | AI chat from active session | ✅ Ready | ⚠️ Refine | Tool rail → 💬 → contextual game chat |

### 5.2 Tier 2 — SHOULD HAVE

| ID | Feature | Backend | Frontend | Description |
|----|---------|---------|----------|-------------|
| F13 | Wishlist | ✅ Ready | ⚠️ Refine | Tab in Library, toggle from card |
| F14 | Collections/Playlists | ✅ Ready | ⚠️ Refine | Create collection, add games |
| F15 | Session history | ✅ Ready | ⚠️ Refine | Past games list with results |
| F16 | User profile | ✅ Ready | ⚠️ Refine | Stats, games, sessions |
| F17 | In-app notifications | ✅ API Ready | 🔨 Frontend New | Badge on TopNav + notification list page. Backend endpoints exist (GET, mark-read, unread-count, SSE stream). |
| F18 | Admin wizard (BGG import + PDF) | ✅ Ready | ⚠️ Refine | Admin flow to populate catalog |
| F19 | Game nights | ✅ Ready | ⚠️ Refine | Create event, invite players |

### 5.3 Tier 3 — NICE TO HAVE

| ID | Feature | Backend | Frontend | Description |
|----|---------|---------|----------|-------------|
| F20 | Badges/Achievements | ✅ Ready | ⚠️ Refine | Basic gamification |
| F21 | Similar games (recommendations) | ✅ Ready | ⚠️ Refine | Content-based filtering |
| F22 | Game reviews | ✅ Ready | ⚠️ Refine | Ratings + comments |
| F23 | Entity links (Mana system) | ✅ Ready | ⚠️ Refine | Navigation between linked entities |
| F24 | Flip card (stats on back) | ✅ Ready | ⚠️ Refine | Tap/button to rotate card |
| F25 | Hover preview | ✅ Ready | ⚠️ Refine | Rich tooltip on desktop |

### 5.4 Cross-Cutting Work Items

| ID | Area | Scope |
|----|------|-------|
| W1 | Layout redesign mobile-first | Remove CardStack/HandDrawer, new 4-tab AlphaTabBar, swipe navigation |
| W2 | Design system consolidation | Spacing scale, typography scale, glassmorphism rules, color coerency |
| W3 | Empty states | EmptyStateCard for every empty section |
| W4 | Transitions | `framer-motion` layoutId animations card → detail, skeleton loading cards |
| W5 | Simplified TopNav | Logo + section title + notifications + avatar |
| W6 | Mobile detail view | Full-screen slide-up modal via CardDetailModal |
| W7 | Desktop adaptation | Minimal icon sidebar + responsive grid 1→4 columns |
| W8 | Responsive audit | Verify every page at 375px, 768px, 1024px, 1440px |
| W9 | Feature flag setup | Add `alpha-layout` flag to SystemConfiguration. Wire `FeatureGate` in authenticated layout.tsx to switch AlphaShell vs UnifiedShell. |
| W10 | Session dedicated layout | `/sessions/live/[sessionId]` uses own layout outside AlphaShell. Verify existing `SessionNavConfig` works independently. Add back-to-Play navigation. |

---

## 6. Mock Page Analysis

Analysis of 12 existing placeholder/incomplete pages and their alpha disposition.

### 6.1 Mock Pages to COMPLETE for Alpha

| # | Page | Location | Current State | Alpha Action |
|---|------|----------|---------------|--------------|
| M1 | **Custom Tool Placeholder** | `toolkit/[sessionId]/_content.tsx:94-105` | "Custom tool — coming soon" | **Complete**: Wire existing toolkit widgets (`DiceRoller.tsx`, `Timer.tsx`, `Counter.tsx`, etc. in `components/toolkit/`) to the `tool.type` enum paths that currently fall through to `CustomToolPlaceholder`. NOT building new tools — auditing which enum values hit the placeholder and connecting them to existing components. Required for F9 (tool rail). |
| M2 | **Player Manager User Search** | `components/play-records/PlayerManager.tsx` | "User search coming soon" | **Defer to Tier 3**: Alpha sessions use guest players only (name + color, no account required per Section 4.3). Registered user autocomplete is not needed for the alpha flow. Move to Nice-to-Have. |
| M3 | **Game Rules Tab - RuleSpec** | `games/detail/GameRulesTab.tsx` | "RuleSpec integration coming soon" | **Complete**: Connect to KB documents. Required for F6 (PDF → KB flow). |
| M4 | **Game Sessions Tab** | `games/detail/GameSessionsTab.tsx` | "Session tracking coming soon" | **Complete**: Show session history for game. Required for F15 (session history). |

### 6.2 Mock Pages to HIDE for Alpha

| # | Page | Location | Current State | Alpha Action |
|---|------|----------|---------------|--------------|
| M5 | **Game Community Tab** | `games/detail/GameCommunityTab.tsx:29-33` | "Community features coming soon" | **Hide**: Remove tab from game detail. Community explicitly excluded from alpha. |
| M6 | **Search Language Filter** | `search/SearchFilters.tsx:252` | "All languages (coming soon)" | **Hide**: Remove language filter option. Alpha is Italian-only. |
| M7 | **Pricing Premium CTA** | `pricing/page.tsx:1-6` | "Coming Soon" disabled button | **Hide**: Remove pricing page from alpha navigation. No Stripe integration. |

### 6.3 Mock Pages ACCEPTABLE as-is for Alpha

| # | Page | Location | Current State | Alpha Action |
|---|------|----------|---------------|--------------|
| M8 | **Slot Usage Analytics** | `agent/slots/page.tsx:40-48` | "Slot usage analytics coming soon" | **Keep**: Admin-only, non-critical. Low visibility for alpha users. |
| M9 | **Profile Activity Feed** | `profile/page.tsx:226-245` | "Activity Feed Coming Soon" | **Keep**: Nice-to-have. Profile works without activity tab. |
| M10 | **Admin Reports** | `admin/analytics/ReportsTab.tsx:55-57` | "Report generation coming soon" | **Keep**: Admin-only analytics. Not user-facing. |
| M11 | **Admin Edit Shared Game** | `admin/shared-games/[id]/client.tsx:307` | "Edit functionality coming soon" | **Keep**: Admin can recreate games. Edit is convenience. |
| M12 | **Debug Waterfall Chart** | `admin/sandbox/DebugSidePanel.tsx:127-134` | "Waterfall chart — coming soon" | **Keep**: Debug tool, not user-facing. |

### 6.4 Summary

| Action | Count | IDs |
|--------|-------|-----|
| **Complete** (required for alpha flows) | 3 | M1, M3, M4 |
| **Defer** (not needed for alpha) | 1 | M2 |
| **Hide** (excluded from alpha scope) | 3 | M5, M6, M7 |
| **Keep as-is** (acceptable placeholder) | 5 | M8, M9, M10, M11, M12 |

---

## 7. Technical Architecture

### 7.1 Frontend — New Components

| Component | Responsibility |
|-----------|----------------|
| `AlphaShell` | Layout wrapper (server component, auth check) |
| `AlphaShellClient` | Client layout (TopNav + SwipeableContainer + TabBar) |
| `AlphaTopNav` | Minimal header (logo + title + notifications + avatar) |
| `AlphaTabBar` | 4-tab bottom navigation with entity-colored active state |
| `SwipeableContainer` | Horizontal swipe gesture handler between tab panels |
| `CardDetailModal` | Entity detail view. Single component, responsive: full-screen slide-up modal on mobile (< 1024px), side panel (right, 40% width) on desktop (>= 1024px). Uses `useResponsive()` internally for presentation switching. |
| `HomeFeed` | Personalized feed with recent cards + EmptyStateCard placeholders |
| `EmptyStateCard` | Standalone component mimicking MeepleCard visual style (NOT a MeepleCard variant). Shows illustration + CTA. Rendered in card grids, replaces with real cards as data appears. |
| `SessionScreen` | Optimized mobile session layout (scoreboard + tool rail). Renders at `/sessions/live/[sessionId]` in its own dedicated layout — OUTSIDE AlphaShell. AlphaTabBar and AlphaTopNav are not visible during active session. Has own TopBar with game name + back-to-Play button. |

### 7.2 Frontend — State Management

```
NOT INITIALIZED by AlphaShell (but store still exists for legacy/admin pages):
- useCardHand() Zustand store — 37 consumers exist, NOT migrated for alpha.
  AlphaShell simply doesn't render CardStack/HandDrawer that trigger the store.
  Pages that directly call useCardHand() outside of AlphaShell (e.g., admin)
  continue to work normally.
- useContextualEntity()
- useContextualSheetActions()
- usePlaceholderActions()

KEEP:
- React Query (data fetching, cache)
- Auth Zustand store
- Theme store
- FeatureFlagProvider (for alpha layout toggle)

ADD:
- useAlphaNav() — active tab index + detail modal state (lightweight Zustand store)
```

### 7.3 Frontend — File Structure

```
apps/web/src/
├── app/
│   └── (authenticated)/
│       └── layout.tsx              # Points to AlphaShell (feature-flagged)
│
├── components/
│   └── layout/
│       ├── alpha/                  # NEW: alpha layout
│       │   ├── AlphaShell.tsx
│       │   ├── AlphaShellClient.tsx
│       │   ├── AlphaTopNav.tsx
│       │   ├── AlphaTabBar.tsx
│       │   └── SwipeableContainer.tsx
│       │
│       └── unified-shell/          # UNCHANGED (rollback ready)
│
│   └── features/
│       ├── home/
│       │   └── HomeFeed.tsx        # NEW
│       ├── session/
│       │   └── SessionScreen.tsx   # NEW (optimized mobile layout)
│       └── common/
│           ├── CardDetailModal.tsx  # NEW
│           └── EmptyStateCard.tsx   # NEW
│
├── hooks/
│   └── useAlphaNav.ts             # NEW (replaces useCardHand for alpha)
│
└── styles/
    └── design-tokens.css           # NEW: consolidated spacing/typography
```

### 7.4 Migration Strategy

1. **Non-destructive**: AlphaShell is parallel to UnifiedShell, switch via layout.tsx
2. **Feature flag**: Use the existing `FeatureFlagProvider` / `useFeatureFlag` / `FeatureGate` system backed by the `SystemConfiguration` bounded context — NOT a raw `NEXT_PUBLIC_*` env var. This enables per-user toggling (relevant for invite-only alpha where not all users may see the new layout) and runtime switching without redeploy. Flag name: `alpha-layout`. Server-side: `FeatureFlagMiddleware` checks the flag. Client-side: `<FeatureGate flag="alpha-layout">` wraps AlphaShell vs UnifiedShell in `layout.tsx`.
3. **Rollback**: Toggle flag in admin → all users revert to previous layout instantly
4. **Incremental**: Each tab panel can be developed independently

### 7.5 Backend Changes

Minimal. The backend is largely ready. Specific interventions:

| Area | Change | Detail |
|------|--------|--------|
| **In-app notifications** (F17) | **Frontend only** | Backend endpoints already exist in `NotificationEndpoints.cs`: `GET /notifications`, `GET /notifications/unread-count`, mark-read, mark-all-read, SSE notification stream. The 30% completion refers to push notification delivery and email template system — neither needed for alpha (in-app only). Frontend needs: notification list page + TopNav badge consuming existing endpoints. |
| **Home feed** (F3) | **Frontend composition** | Existing endpoints cover the data: `GET /dashboard` (aggregated), `GET /sessions/recent`, `GET /users/me/games`, `GET /dashboard/activity-timeline`. No new backend endpoint needed — `HomeFeed` component composes these existing endpoints via React Query parallel queries. |
| **Email invite** (F1) | **Verify SMTP** | Invitation endpoints exist. Verify email template renders correctly and SMTP delivery works end-to-end in staging. |
| **Feature flag** | **Add flag** | Add `alpha-layout` flag to `SystemConfiguration` seed data. |

### 7.6 Responsive Breakpoints

| Breakpoint | Layout |
|------------|--------|
| < 640px (mobile) | 1-column grid, full-width cards, TabBar, swipe nav |
| 640-1023px (tablet) | 2-column grid, TabBar, swipe nav |
| >= 1024px (desktop) | Icon sidebar + 3-4 column grid, side panel detail |
| >= 1440px (wide) | Icon sidebar expanded + 4 column grid |

---

## 8. Explicit Exclusions

The following are explicitly OUT OF SCOPE for the alpha:

- OAuth (Google/Discord/GitHub) — email+password only
- Two-Factor Authentication — not needed for invite-only
- Push notifications — in-app only
- PWA offline mode
- Community features (discussions, Q&A)
- n8n / webhook integration
- Pipeline builder
- Chess mini-game
- Business simulations
- API key authentication
- Multi-language support (Italian only)
- Stripe/payment integration
- Advanced admin analytics/reports

---

## 9. Success Criteria

The alpha is ready when a tester can complete this journey on a single smartphone:

1. Receive email invitation → register → complete onboarding
2. Browse shared game catalog → add a game to personal library
3. Upload a PDF rulebook → see processing progress → status reaches "Indexed"
4. Create a game session → add players → score rounds
5. During session: use dice, timer, notes tools
6. During session: ask AI agent a rule question → get RAG-powered answer with citations
7. Pause session → close app → reopen → resume from checkpoint
8. End session → see results in history

---

## 10. Appendix: Current State Assessment

### 10.1 Backend Readiness (by Bounded Context)

| Context | Completion | Alpha Status |
|---------|------------|-------------|
| Authentication | 95% | ✅ Ready (invitations included) |
| Game Management | 90% | ✅ Ready |
| Session Tracking | 85% | ✅ Ready (30+ endpoints) |
| Knowledge Base / RAG | 88% | ✅ Ready (hybrid search, SSE, citations) |
| User Library | 82% | ✅ Ready |
| Shared Game Catalog | 80% | ✅ Ready |
| Document Processing | 88% | ✅ Ready (7-state pipeline) |
| Administration | 70% | ⚠️ Partial (sufficient for alpha) |
| Gamification | 65% | ⚠️ Partial (Tier 3) |
| User Notifications | 75% | ⚠️ API ready (GET, mark-read, unread-count, SSE stream exist). Missing: push delivery, email templates — not needed for alpha. Frontend notification list page needed. |
| Entity Relationships | 40% | ⚠️ Partial (Tier 3) |
| Business Simulations | 60% | ❌ Excluded |
| Workflow Integration | 25% | ❌ Excluded |

**Total API endpoints**: 150+ implemented across 13 bounded contexts.

### 10.2 Frontend Readiness (by Section)

| Section | Status | Alpha Action |
|---------|--------|-------------|
| Auth pages | ✅ Complete | Refine invite flow |
| Dashboard | ✅ Functional | Rebuild as HomeFeed |
| Library | ✅ Functional | Refine with new layout |
| PDF Upload | ✅ Functional | Refine status display |
| Discover/Catalog | ✅ Functional | Integrate into Library tab |
| Agents | ✅ Functional | Refine list/detail |
| Chat | ✅ Functional | Refine with new layout |
| Sessions | ✅ Functional | Rebuild as SessionScreen |
| Game Nights | ✅ Functional | Refine |
| Toolkit | ✅ Functional | Complete custom tools (M1) |
| Profile/Settings | ✅ Functional | Refine |
| Admin | ✅ Functional | Keep existing |
| Onboarding | ✅ Functional | Add guided home |

### 10.3 MeepleCard System

Fully mature. 16 entity types, 6 variants, 19+ opt-in features. **Zero modifications needed** for alpha — the card system is the foundation we build on.
