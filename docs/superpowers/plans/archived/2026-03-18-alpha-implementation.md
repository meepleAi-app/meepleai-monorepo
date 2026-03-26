# MeepleAI Alpha Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver the alpha release with mobile-first layout redesign, full end-to-end user flow (invite → library → PDF/RAG → session with scoring + toolkit + chat AI), and design system consolidation.

**Architecture:** Non-destructive migration via AlphaShell parallel to UnifiedShell, toggled by existing FeatureFlagProvider. Each phase produces working, testable software. Backend requires near-zero changes — work is 95% frontend.

**Tech Stack:** Next.js 16 (App Router), React 19, Tailwind 4, Zustand, React Query, framer-motion, shadcn/ui

**Spec:** `docs/superpowers/specs/2026-03-18-alpha-feature-spec-design.md`

---

## Phase Overview

| Phase | Description | Depends On | Delivers |
|-------|-------------|------------|----------|
| 0 | Foundation (flag, tokens, store) | — | W9, W2 (partial) |
| 1 | Layout Shell | Phase 0 | W1, W5, W7 |
| 2 | Core UI Components | Phase 0 | W3, W4, W6 |
| 3 | Tab Panels + Onboarding (Home, Library, Play, Chat) | Phase 1, 2 | F1-F6, F11 |
| 4 | Session Screen + Verification | Phase 1 | F7-F10, F12, W10, M1 |
| 5 | Mock Page Fixes | Phase 3 | M3, M4, M5, M6, M7 |
| 6 | Tier 2 Features | Phase 3 | F13-F19 |
| 7 | Polish & Responsive Audit | Phase 3-6 | W2 (final), W8 |
| 8 | E2E Verification | Phase 0-7 | Success Criteria 1-8 |

---

## Phase 0: Foundation

### Task 0.1: Feature Flag Setup (W9)

**Files:**
- Modify: `apps/web/src/app/(authenticated)/layout.tsx`
- Reference: `apps/web/src/providers/FeatureFlagProvider.tsx`
- Reference: `apps/web/src/components/ui/gates/FeatureGate.tsx`
- Reference: `apps/web/src/hooks/queries/useFeatureFlags.ts`

**Context:** The existing `FeatureFlagProvider` provides `useFeatureFlag(flagName)` hook and `<FeatureGate flag="...">` component, backed by `SystemConfiguration` backend context. We add a flag `alpha-layout` to toggle between AlphaShell and UnifiedShell.

- [ ] **Step 1:** Read the existing `FeatureFlagProvider.tsx`, `FeatureGate.tsx`, and `useFeatureFlags.ts` to understand the flag system interface.

- [ ] **Step 2:** Read the current `(authenticated)/layout.tsx` to understand how `UnifiedShell` is imported and used.

- [ ] **Step 3:** Modify `(authenticated)/layout.tsx` to conditionally render `AlphaShell` when the `alpha-layout` flag is enabled. For now, import a placeholder `AlphaShell` that renders `{children}` with a div wrapper. Use `FeatureGate` or `useFeatureFlag('alpha-layout')`.

```tsx
// In layout.tsx, add conditional rendering:
// If alpha-layout flag: <AlphaShell>{children}</AlphaShell>
// Else: <UnifiedShell>{children}</UnifiedShell>
// AlphaShell is a stub for now — Task 1.1 will build it
```

- [ ] **Step 4:** Create stub `apps/web/src/components/layout/alpha/AlphaShell.tsx` — server component that just wraps children in a `<div>`.

- [ ] **Step 5:** Create `apps/web/src/components/layout/alpha/index.ts` exporting AlphaShell.

- [ ] **Step 6:** Verify the app builds: `cd apps/web && pnpm build`. With the flag off (default), UnifiedShell should render as before.

- [ ] **Step 7:** Commit: `feat(alpha): add alpha-layout feature flag with stub AlphaShell`

---

### Task 0.2: Design Tokens Verification (W2 partial)

**Files:**
- Verify: `apps/web/src/styles/design-tokens.css` (ALREADY EXISTS)

**Context:** `design-tokens.css` already exists with comprehensive tokens under `@layer tokens`: spacing (`--space-1` through `--space-12`), typography (`--text-xs` through `--text-xl`), font references (`--font-heading: var(--font-quicksand)`, `--font-body: var(--font-nunito)`), glassmorphism utilities (`.glass-nav`, `.glass-card`, `.glass-modal`), entity colors, and z-index layers. Do NOT recreate or overwrite this file.

- [ ] **Step 1:** Read `apps/web/src/styles/design-tokens.css` to verify it already defines the tokens from spec Section 3.4 (spacing) and Section 3.5 (typography).

- [ ] **Step 2:** Verify the existing `--text-*` and `--space-*` values. Note: existing values may differ slightly from spec (e.g., `--text-lg` might be 18px not 20px). Use the existing values — they are already integrated across the codebase.

- [ ] **Step 3:** Verify `design-tokens.css` is already imported in `globals.css`. If not, add the import.

- [ ] **Step 4:** Verify glassmorphism utilities (`.glass-nav`, `.glass-card`, `.glass-modal`) exist for the 3 elements specified in spec Section 3.6.

- [ ] **Step 5:** No commit needed unless changes were made. If changes were needed: `fix(alpha): align design tokens with alpha spec`

---

### Task 0.3: useAlphaNav Zustand Store

**Files:**
- Create: `apps/web/src/lib/stores/alphaNavStore.ts` (store definition — follows project convention)
- Create: `apps/web/src/hooks/useAlphaNav.ts` (re-export hook for consumer convenience)
- Reference: `apps/web/src/lib/stores/` (existing stores for pattern)

**Context:** Lightweight Zustand store replacing the complex `useCardHand` for alpha navigation. Manages: active tab index, detail modal state.

- [ ] **Step 1:** Read one existing Zustand store (e.g., `apps/web/src/lib/stores/toolboxStore.ts`) to understand the project's Zustand pattern (create vs createWithEqualityFn, persist middleware, etc.).

- [ ] **Step 2:** Create `alphaNavStore.ts` in `lib/stores/` and `useAlphaNav.ts` as re-export hook in `hooks/`:

```typescript
import { create } from 'zustand';

export type AlphaTab = 'home' | 'library' | 'play' | 'chat';

interface AlphaNavState {
  activeTab: AlphaTab;
  setActiveTab: (tab: AlphaTab) => void;

  // Detail modal
  detailEntityId: string | null;
  detailEntityType: string | null;
  openDetail: (entityId: string, entityType: string) => void;
  closeDetail: () => void;

  // Section title (shown in TopNav)
  sectionTitle: string;
  setSectionTitle: (title: string) => void;
}

export const useAlphaNav = create<AlphaNavState>((set) => ({
  activeTab: 'home',
  setActiveTab: (tab) => set({ activeTab: tab }),

  detailEntityId: null,
  detailEntityType: null,
  openDetail: (entityId, entityType) =>
    set({ detailEntityId: entityId, detailEntityType: entityType }),
  closeDetail: () =>
    set({ detailEntityId: null, detailEntityType: null }),

  sectionTitle: 'Home',
  setSectionTitle: (title) => set({ sectionTitle: title }),
}));
```

- [ ] **Step 3:** Verify build: `cd apps/web && pnpm build`.

- [ ] **Step 4:** Commit: `feat(alpha): add useAlphaNav Zustand store for tab navigation`

---

## Phase 1: Layout Shell

### Task 1.1: AlphaShell Server + Client Components (W1)

**Files:**
- Modify: `apps/web/src/components/layout/alpha/AlphaShell.tsx` (replace stub)
- Create: `apps/web/src/components/layout/alpha/AlphaShellClient.tsx`

**Context:** Mirror the UnifiedShell pattern: server component handles auth check + passes props, client component handles layout rendering. Reference `UnifiedShell.tsx` and `UnifiedShellClient.tsx` for the pattern.

- [ ] **Step 1:** Read `apps/web/src/components/layout/UnifiedShell/UnifiedShell.tsx` to understand server-side auth check pattern (`getServerUser()`, `isAdmin()`).

- [ ] **Step 2:** Read `apps/web/src/components/layout/UnifiedShell/UnifiedShellClient.tsx` to understand the overall layout structure (top nav + content + bottom nav).

- [ ] **Step 3:** Rewrite `AlphaShell.tsx` as a server component that:
  - Calls `getServerUser()` for auth
  - Calls `isAdmin()` for admin check
  - Renders `<AlphaShellClient isAdmin={isAdmin}>{children}</AlphaShellClient>`

- [ ] **Step 4:** Create `AlphaShellClient.tsx` as client component with basic structure:

```tsx
'use client';

export function AlphaShellClient({
  children,
  isAdmin,
}: {
  children: React.ReactNode;
  isAdmin: boolean;
}) {
  return (
    <div className="flex flex-col h-screen">
      {/* AlphaTopNav will go here — Phase 1, Task 1.2 */}
      <main className="flex-1 overflow-y-auto">
        {children}
      </main>
      {/* AlphaTabBar will go here — Phase 1, Task 1.3 */}
    </div>
  );
}
```

- [ ] **Step 5:** Update `apps/web/src/components/layout/alpha/index.ts` to also export `AlphaShellClient`.

- [ ] **Step 6:** Verify build. With `alpha-layout` flag enabled, pages should render without the old navigation chrome (just content in a full-height flex column).

- [ ] **Step 7:** Commit: `feat(alpha): implement AlphaShell server/client layout components`

---

### Task 1.2: AlphaTopNav (W5)

**Files:**
- Create: `apps/web/src/components/layout/alpha/AlphaTopNav.tsx`
- Modify: `apps/web/src/components/layout/alpha/AlphaShellClient.tsx`

**Context:** Minimal top navigation per spec Section 2.1: Logo + section title + notifications + avatar. Glassmorphism: `bg-background/90 backdrop-blur-xl`. Hide on scroll down (mobile), always visible (desktop).

- [ ] **Step 1:** Read `apps/web/src/components/layout/UnifiedShell/UnifiedTopNav.tsx` to understand existing TopNav patterns (scroll hide behavior, notification badge, user menu).

- [ ] **Step 2:** Read `apps/web/src/hooks/useResponsive.ts` to understand the responsive hook API.

- [ ] **Step 3:** Create `AlphaTopNav.tsx`:
  - Left: Logo (MeepleAI text or icon, link to `/dashboard`)
  - Center: `sectionTitle` from `useAlphaNav()` + entity color icon
  - Right: Notification bell with unread count badge + user avatar (dropdown: Profile, Settings, Admin (if isAdmin), Logout)
  - Sticky top, h-14, z-40
  - Glassmorphism: `bg-background/90 backdrop-blur-xl border-b border-border/40`
  - Mobile scroll hide: reuse `useScrollHideNav()` hook if it exists, or implement basic scroll direction detection
  - Desktop: always visible via `md:translate-y-0`

- [ ] **Step 4:** Wire `AlphaTopNav` into `AlphaShellClient.tsx` above `<main>`.

- [ ] **Step 5:** Verify visually in browser with alpha flag on. TopNav should show logo + title + avatar.

- [ ] **Step 6:** Commit: `feat(alpha): add AlphaTopNav with minimal header`

---

### Task 1.3: AlphaTabBar

**Files:**
- Create: `apps/web/src/components/layout/alpha/AlphaTabBar.tsx`
- Modify: `apps/web/src/components/layout/alpha/AlphaShellClient.tsx`

**Context:** 4-tab bottom navigation (Home, Libreria, Play, Chat). Fixed bottom, z-40, glassmorphism. Entity-colored active tab. 44×44px touch targets.

- [ ] **Step 1:** Read existing MobileTabBar component (search for `MobileTabBar` in `apps/web/src/components/layout/`) to understand the existing bottom tab pattern.

- [ ] **Step 2:** Create `AlphaTabBar.tsx`:
  - 4 tabs with Lucide icons: Home (House), Libreria (BookOpen), Play (Dice5), Chat (MessageCircle)
  - Uses `useAlphaNav()` for activeTab state
  - Active tab: icon filled + entity color + label visible
  - Inactive tab: icon outline + muted + label smaller
  - Entity colors per tab: Home uses `--color-entity-custom` (220 15% 45% silver), Library uses `--color-entity-game` (25 95% 45% orange), Play uses `--color-entity-session` (240 60% 55% indigo), Chat uses `--color-entity-chat` (220 80% 55% blue). Check `design-tokens.css` for exact values.
  - Fixed bottom-0, z-40
  - Style: `bg-card/90 backdrop-blur-md backdrop-saturate-150 border-t border-border/40`
  - Touch targets: `min-w-[44px] min-h-[44px]`
  - Hidden on desktop: `md:hidden` (desktop uses icon sidebar)

- [ ] **Step 3:** Wire `AlphaTabBar` into `AlphaShellClient.tsx` below `<main>`.

- [ ] **Step 4:** Add padding-bottom to `<main>` to account for TabBar height (e.g., `pb-16`).

- [ ] **Step 5:** Verify visually: 4 tabs visible at bottom, tapping changes active tab state.

- [ ] **Step 6:** Commit: `feat(alpha): add AlphaTabBar with 4-tab bottom navigation`

---

### Task 1.4: SwipeableContainer

**Files:**
- Create: `apps/web/src/components/layout/alpha/SwipeableContainer.tsx`
- Modify: `apps/web/src/components/layout/alpha/AlphaShellClient.tsx`

**Context:** Horizontal swipe between tab panels. Each panel maintains its own scroll position. Uses framer-motion for gesture detection (already a project dependency).

- [ ] **Step 1:** Check framer-motion is available: `grep "framer-motion" apps/web/package.json`.

- [ ] **Step 2:** Create `SwipeableContainer.tsx`:
  - Renders 4 panel slots (children passed as array or render prop)
  - Uses `useAlphaNav()` for `activeTab` and `setActiveTab`
  - Maps tab names to indices: home=0, library=1, play=2, chat=3
  - framer-motion `AnimatePresence` with `mode="wait"` for cross-fade transitions
  - Animation: opacity-based cross-fade (`initial: {opacity: 0}` / `animate: {opacity: 1}` / `exit: {opacity: 0}`) — NOT horizontal slide. Spec Section 3.7 requires "cross-fade".
  - Swipe detection via `onPanEnd` gesture: swipe left → next tab, swipe right → previous tab
  - Each panel wrapped in a div that preserves scroll position (using `overflow-y-auto`)
  - 250ms ease-out transition duration

- [ ] **Step 3:** Update `AlphaShellClient.tsx` to use `SwipeableContainer` wrapping 4 panel placeholders:

```tsx
<SwipeableContainer>
  <div key="home">Home Panel (placeholder)</div>
  <div key="library">Library Panel (placeholder)</div>
  <div key="play">Play Panel (placeholder)</div>
  <div key="chat">Chat Panel (placeholder)</div>
</SwipeableContainer>
```

- [ ] **Step 4:** Verify: swiping left/right switches panels, TabBar active state syncs.

- [ ] **Step 5:** Commit: `feat(alpha): add SwipeableContainer with gesture-based tab switching`

---

### Task 1.5: Desktop Icon Sidebar (W7)

**Files:**
- Create: `apps/web/src/components/layout/alpha/AlphaDesktopSidebar.tsx`
- Modify: `apps/web/src/components/layout/alpha/AlphaShellClient.tsx`

**Context:** On desktop (>= 1024px), show a minimal icon rail on the left instead of the bottom TabBar. Same 4 tabs, same icons, same entity colors. Expanded label on hover.

- [ ] **Step 1:** Create `AlphaDesktopSidebar.tsx`:
  - `hidden md:flex flex-col` — hidden on mobile, flex column on desktop
  - Width: 56px collapsed, 180px on hover (via group-hover or CSS transition)
  - 4 icon buttons matching AlphaTabBar tabs
  - Active state: left border accent + entity color icon
  - Bottom: user avatar + admin link (if admin)
  - Profile/Settings accessible from avatar click

- [ ] **Step 2:** Update `AlphaShellClient.tsx` layout to include sidebar on desktop:

```tsx
<div className="flex h-screen">
  <AlphaDesktopSidebar isAdmin={isAdmin} className="hidden lg:flex" />
  <div className="flex flex-col flex-1">
    <AlphaTopNav />
    <SwipeableContainer>...</SwipeableContainer>
    <AlphaTabBar className="lg:hidden" />
  </div>
</div>
```

- [ ] **Step 3:** Verify at 1024px+: sidebar visible, TabBar hidden. At <1024px: TabBar visible, sidebar hidden.

- [ ] **Step 4:** Commit: `feat(alpha): add desktop icon sidebar for lg+ breakpoints`

---

## Phase 2: Core UI Components

### Task 2.1: EmptyStateCard (W3)

**Files:**
- Create: `apps/web/src/components/features/common/EmptyStateCard.tsx`

**Context:** Standalone component mimicking MeepleCard visual style. NOT a MeepleCard variant. Shows contextual message + CTA button. Used in every empty section.

- [ ] **Step 1:** Read `apps/web/src/components/ui/data-display/meeple-card-styles.ts` to understand card styling patterns (border radius, shadows, entity colors).

- [ ] **Step 2:** Create `EmptyStateCard.tsx`:

```tsx
interface EmptyStateCardProps {
  entity?: MeepleEntityType;
  title: string;
  description: string;
  ctaLabel: string;
  onCtaClick: () => void;
  icon?: LucideIcon;
}
```

  - Mimics MeepleCard grid variant dimensions and styling
  - Uses entity color for subtle border and CTA button accent
  - Icon (Lucide) centered in card body area
  - Title + description below icon
  - CTA button at bottom (entity-colored)
  - Same border-radius, shadow, hover behavior as MeepleCard grid

- [ ] **Step 3:** Verify component renders correctly in isolation (can test by temporarily placing it in a page).

- [ ] **Step 4:** Commit: `feat(alpha): add EmptyStateCard component for empty sections`

---

### Task 2.2: CardDetailModal (W4, W6)

**Files:**
- Create: `apps/web/src/components/features/common/CardDetailModal.tsx`
- Reference: `apps/web/src/components/ui/data-display/extra-meeple-card/ExtraMeepleCardDrawer.tsx`
- Reference: `apps/web/src/hooks/useResponsive.ts`

**Context:** Single responsive component. Mobile: full-screen slide-up modal. Desktop: side panel (right, 40% width). Wraps the existing `ExtraMeepleCardDrawer` / `EntityExtraMeepleCard` system.

- [ ] **Step 1:** Read `ExtraMeepleCardDrawer.tsx` to understand how entity detail views are rendered (Sheet component, entity type routing).

- [ ] **Step 2:** Create `CardDetailModal.tsx`:
  - Uses `useAlphaNav()` for `detailEntityId`, `detailEntityType`, `closeDetail`
  - Uses `useResponsive()` to determine presentation mode
  - Mobile (< 1024px): framer-motion `AnimatePresence` + `motion.div` sliding up from bottom, full screen, z-50
  - Desktop (>= 1024px): side panel on right, 40% width, z-30
  - Both: overlay backdrop (click to dismiss), swipe-down/Escape to close
  - Renders `EntityExtraMeepleCard` inside (the existing component that routes to Game/Agent/Chat/KB detail views)
  - Close button in top-right corner
  - **Transition (W4)**: Use simple opacity fade animation (`initial: {opacity: 0}` → `animate: {opacity: 1}`), NOT `layoutId` pairing. Spec allows "Fallback: simple opacity fade" and MeepleCard has zero modifications. The `layoutId` approach would require modifying MeepleCard which is out of scope.

- [ ] **Step 3:** Wire `CardDetailModal` into `AlphaShellClient.tsx` (render once, always present, visibility controlled by `detailEntityId !== null`).

- [ ] **Step 4:** Verify: calling `openDetail(id, 'game')` from console/dev tools should open the modal. Closing should work via backdrop click, Escape key, and close button.

- [ ] **Step 5:** Commit: `feat(alpha): add responsive CardDetailModal for entity details`

---

### Task 2.3: Skeleton Loading Cards (W4)

**Files:**
- Create: `apps/web/src/components/features/common/SkeletonCard.tsx`

**Context:** Loading skeleton matching MeepleCard grid shape. Pulsing animation. Used while data is loading in any tab panel.

- [ ] **Step 1:** Create `SkeletonCard.tsx`:
  - Same dimensions as MeepleCard grid variant
  - Tailwind `animate-pulse` on gray background blocks
  - Cover image area (7:10 aspect ratio, rounded-t)
  - Title line (60% width)
  - Subtitle line (40% width)
  - Metadata footer (3 small blocks)

- [ ] **Step 2:** Create `SkeletonCardGrid` that renders 6 `SkeletonCard` in a responsive grid (1→2→3→4 columns).

- [ ] **Step 3:** Commit: `feat(alpha): add skeleton loading cards for grid views`

---

## Phase 3: Tab Panels + Onboarding

### Task 3.0: Invite Registration + Onboarding Refinement (F1, F2)

**Files:**
- Verify: `apps/web/src/app/(auth)/register/page.tsx`
- Verify: `apps/web/src/app/(auth)/login/page.tsx`
- Verify: `apps/web/src/app/(authenticated)/onboarding/page.tsx`
- Verify: email invite template (backend)

**Context:** The invite and onboarding flows exist but need verification and refinement for the alpha. F1: email invite links must pre-fill the email in the register form. F2: onboarding must flow into the guided home with 3 ordered EmptyStateCards.

- [ ] **Step 1:** Read the register page to understand how it handles invite tokens. Check if it reads the token from URL params and pre-fills the email field (via `validateInvitation` endpoint).

- [ ] **Step 2:** Test the invite flow manually: use the admin invitation endpoint to send an invite → click the link → verify register page shows pre-filled email.

- [ ] **Step 3:** Read the onboarding page to understand the post-registration flow. Verify it redirects to `/dashboard` (or home) after completion.

- [ ] **Step 4:** Verify the SMTP email template renders correctly. Check `apps/api/src/Api/BoundedContexts/Authentication/` for email template configuration.

- [ ] **Step 5:** If any issues found, fix them. Common issues: invite token not parsed from URL, email not pre-filled, redirect after onboarding pointing to wrong route.

- [ ] **Step 6:** Commit if changes made: `fix(alpha): refine invite registration and onboarding flow (F1, F2)`

---

### Task 3.1: HomeFeed Panel (F2, F3)

**Files:**
- Create: `apps/web/src/components/features/home/HomeFeed.tsx`
- Modify: `apps/web/src/components/layout/alpha/AlphaShellClient.tsx`

**Context:** Personalized home feed composing existing endpoints via React Query. Shows recent games, active sessions, and guided EmptyStateCard placeholders for first-time users.

- [ ] **Step 1:** Read existing dashboard data fetching hooks. Search for hooks that call `/dashboard`, `/sessions/recent`, `/users/me/games` in `apps/web/src/hooks/queries/`.

- [ ] **Step 2:** Create `HomeFeed.tsx`:
  - Uses React Query `useQueries` for parallel fetching: dashboard summary, recent games, active sessions
  - Sections (top to bottom):
    1. "Sessioni Attive" — MeepleCard list (entity="session") for active/paused sessions. EmptyStateCard if none.
    2. "I Tuoi Giochi Recenti" — MeepleCard grid (entity="game") for recently accessed games. EmptyStateCard if none.
    3. "Serate di Gioco" — MeepleCard list (entity="event") for upcoming game nights (F19). EmptyStateCard if none.
    4. "Chat Recenti" — MeepleCard compact list (entity="chatSession") for recent chats. EmptyStateCard if none.
  - First-time user: all sections show EmptyStateCard with guided CTAs (spec Section 4.1)
  - Each card taps → `openDetail(id, type)` via `useAlphaNav()`
  - Pull-to-refresh: use React Query `refetch` on scroll-to-top gesture

- [ ] **Step 3:** Replace the "Home Panel (placeholder)" in `AlphaShellClient.tsx` with `<HomeFeed />`.

- [ ] **Step 4:** Set `sectionTitle` to "Home" when Home tab is active (via useEffect on activeTab).

- [ ] **Step 5:** Verify: HomeFeed renders with real data (if authenticated) or empty states (if new user).

- [ ] **Step 6:** Commit: `feat(alpha): add HomeFeed panel with personalized card feed`

---

### Task 3.2: Library Panel (F4, F5, F6)

**Files:**
- Create: `apps/web/src/components/features/library/LibraryPanel.tsx`
- Modify: `apps/web/src/components/layout/alpha/AlphaShellClient.tsx`

**Context:** Library tab with sub-tabs [I Miei Giochi | Catalogo | Wishlist]. Reuses existing library and catalog components within the alpha layout.

- [ ] **Step 1:** Read existing library page content: `apps/web/src/app/(authenticated)/library/page.tsx` and its child components. Identify the tab system and content components used.

- [ ] **Step 2:** Read existing catalog/discover page: `apps/web/src/app/(authenticated)/discover/page.tsx` to understand how catalog browsing works.

- [ ] **Step 3:** Create `LibraryPanel.tsx`:
  - Internal tab bar at top: [I Miei Giochi | Catalogo | Wishlist] — use shadcn Tabs component
  - "I Miei Giochi" tab: reuse existing library content component (game grid from library page)
  - "Catalogo" tab: reuse existing catalog/discover content (CatalogContent or equivalent)
  - "Wishlist" tab: reuse existing wishlist content
  - Each game card: tap → `openDetail(id, 'game')` via `useAlphaNav()`
  - Search bar at top of each sub-tab (reuse existing search components)
  - FAB button: [+ Nuovo Gioco] for custom game creation (opens existing wizard modal)
  - EmptyStateCard for each empty sub-tab

- [ ] **Step 4:** Replace "Library Panel (placeholder)" in `AlphaShellClient.tsx` with `<LibraryPanel />`.

- [ ] **Step 5:** Set `sectionTitle` to "Libreria" when Library tab active.

- [ ] **Step 6:** Verify: can browse catalog, see personal games, switch sub-tabs.

- [ ] **Step 7:** Commit: `feat(alpha): add Library panel with catalog, personal games, and wishlist tabs`

---

### Task 3.3: Play Panel (F7, F10, F15)

**Files:**
- Create: `apps/web/src/components/features/play/PlayPanel.tsx`
- Modify: `apps/web/src/components/layout/alpha/AlphaShellClient.tsx`

**Context:** Play tab with [Sessioni Attive | Storico]. Session cards open live session (exits AlphaShell) or detail modal (completed sessions).

- [ ] **Step 1:** Read existing sessions page: `apps/web/src/app/(authenticated)/sessions/` to understand session list rendering and data hooks.

- [ ] **Step 2:** Create `PlayPanel.tsx`:
  - Internal tabs: [Sessioni Attive | Storico]
  - "Sessioni Attive": MeepleCard grid (entity="session") filtered by status: setup, inProgress, paused
    - Tap on setup/paused → `router.push('/sessions/live/${id}')` (exits AlphaShell)
    - Status badge on each card (colored: green=inProgress, yellow=paused, gray=setup)
  - "Storico": MeepleCard grid filtered by status: completed
    - Tap → `openDetail(id, 'session')` (stays in AlphaShell, CardDetailModal)
  - FAB: [Nuova Sessione] → opens session creation wizard modal
    - Step 1: Choose game (compact MeepleCard list from user library)
    - Step 2: Add guest players (name + color picker)
    - Step 3: Confirm → creates session → navigates to `/sessions/live/${newId}`
  - EmptyStateCard: "Nessuna partita. Inizia una serata!" → triggers FAB wizard

- [ ] **Step 3:** Replace "Play Panel (placeholder)" in `AlphaShellClient.tsx` with `<PlayPanel />`.

- [ ] **Step 4:** Set `sectionTitle` to "Gioca" when Play tab active.

- [ ] **Step 5:** Verify: can see sessions, create new session, tap opens live view.

- [ ] **Step 6:** Commit: `feat(alpha): add Play panel with active sessions, history, and creation wizard`

---

### Task 3.4: Chat Panel (F11)

**Files:**
- Create: `apps/web/src/components/features/chat/ChatPanel.tsx`
- Modify: `apps/web/src/components/layout/alpha/AlphaShellClient.tsx`

**Context:** Chat tab showing conversation list. Reuses existing chat components.

- [ ] **Step 1:** Read existing chat page: `apps/web/src/app/(chat)/chat/` to understand chat list and thread rendering.

- [ ] **Step 2:** Create `ChatPanel.tsx`:
  - MeepleCard list (entity="chatSession") — each card shows: agent name, linked game, last message preview, unread badge, timestamp
  - Tap → navigates to `/chat/${threadId}` (existing chat thread page, which has its own layout)
  - FAB: [Nuova Chat] → opens agent selection modal (compact MeepleCard list of available agents)
    - On select → creates chat thread → navigates to `/chat/${newThreadId}`
  - EmptyStateCard: "Nessuna conversazione. Chiedi al tuo assistente AI!"

- [ ] **Step 3:** Replace "Chat Panel (placeholder)" in `AlphaShellClient.tsx` with `<ChatPanel />`.

- [ ] **Step 4:** Set `sectionTitle` to "Chat" when Chat tab active.

- [ ] **Step 5:** Verify: chat list renders, can create new chat, tap opens thread.

- [ ] **Step 6:** Commit: `feat(alpha): add Chat panel with conversation list and agent selection`

---

## Phase 4: Session Screen

### Task 4.1: Session Dedicated Layout (W10)

**Files:**
- Modify: `apps/web/src/app/(authenticated)/sessions/live/[sessionId]/layout.tsx`

**Context:** The live session screen must work independently of AlphaShell. The existing layout at this path already has a `SessionNavConfig`. We need to verify it works without UnifiedShell's navigation providers and add a back-to-Play button.

- [ ] **Step 1:** Read the existing live session layout at `sessions/live/[sessionId]/layout.tsx`.

- [ ] **Step 2:** Check if it depends on `NavigationContext` or `useCardHand` — if so, ensure it has fallback behavior when those providers are absent (AlphaShell doesn't provide them).

- [ ] **Step 3:** Add a back button in the session TopBar that navigates to `/dashboard` (Play tab will be the default active tab when returning, handled by `useAlphaNav` detecting the route).

- [ ] **Step 4:** Verify: navigate to `/sessions/live/test-id` directly — the page should render without errors even without UnifiedShell context.

- [ ] **Step 5:** Commit: `feat(alpha): ensure session live layout works independently of UnifiedShell`

---

### Task 4.2: Verify Live Scoreboard and Tool Rail (F8, F9)

**Files:**
- Verify: `apps/web/src/app/(authenticated)/sessions/live/[sessionId]/` (session page)
- Verify: `apps/web/src/components/toolkit/` (tool rail components)
- Reference: Session scoring components (Scoreboard, LiveIndicator)

**Context:** F8 (live scoreboard) and F9 (tool rail) are listed as "Retained Without Modification" in spec Section 2.4. They already work in the existing session screen. This task verifies they work correctly when accessed from the alpha layout (via Play tab → navigate to `/sessions/live/[id]`).

- [ ] **Step 1:** Navigate to a live session from the alpha Play tab. Verify the scoreboard renders correctly with round-by-round scoring, +/- buttons, and turn tracking.

- [ ] **Step 2:** Verify the tool rail renders at the bottom of the session screen with base tools (dice, timer, notes, scoreboard). Test each tool opens its panel correctly.

- [ ] **Step 3:** Verify the AI chat tool in the tool rail opens the contextual chat panel (F12).

- [ ] **Step 4:** If any issues found (especially related to missing UnifiedShell context), fix them.

- [ ] **Step 5:** Commit if changes made: `fix(alpha): ensure scoreboard and tool rail work in alpha layout (F8, F9)`

---

### Task 4.3: Wire Custom Tools to Existing Widgets (M1)

**Files:**
- Modify: `apps/web/src/app/(authenticated)/toolkit/[sessionId]/_content.tsx`
- Reference: `apps/web/src/components/toolkit/` (DiceRoller.tsx, Timer.tsx, Counter.tsx, etc.)

**Context:** Base tools (`dice`, `turn-order`, `whiteboard`, `scoreboard`) are already wired in `_content.tsx`. The `CustomToolPlaceholder` only appears for **custom toolkit tool types** (under the `custom-*` prefix branch, around line 377-382) — specifically `card`, `timer`, and `dice` as custom overrides. The scope is narrow: only wire the custom tool types that fall through.

- [ ] **Step 1:** Read `_content.tsx` fully. Identify the `custom-*` prefix branch (around line 377) where `CustomToolPlaceholder` is rendered. Confirm base tools (`dice`, `turn-order`, `whiteboard`, `scoreboard`) are already wired to their components.

- [ ] **Step 2:** For the custom tool types that hit `CustomToolPlaceholder` (`timer`, `card`, and `dice` as custom overrides): wire `timer` to the existing `Timer` widget, wire `dice` (custom) to `DiceRoller` if not already delegating. For `card` type: if no existing component serves it, keep the placeholder.

- [ ] **Step 3:** Verify: in a session with custom toolkit tools, `timer` and `dice` custom types render their widgets instead of "coming soon".

- [ ] **Step 5:** Commit: `fix(alpha): wire custom tool types to existing toolkit widgets (M1)`

---

## Phase 5: Mock Page Fixes

### Task 5.1: Game Rules Tab — Connect KB Documents (M3)

**Files:**
- Modify: `apps/web/src/components/games/detail/GameRulesTab.tsx`

- [ ] **Step 1:** Read `GameRulesTab.tsx` to understand current placeholder content.

- [ ] **Step 2:** Read the game detail page to understand how the `gameId` is passed to tabs.

- [ ] **Step 3:** Replace "coming soon" with:
  - Query KB documents for this game (reuse existing hooks for document listing)
  - Show MeepleCard list (entity="kb") with `PdfProcessingState` badge
  - Upload button linking to PDF upload flow
  - If no documents: EmptyStateCard with upload CTA

- [ ] **Step 4:** Verify: game detail → Rules tab shows documents or empty state.

- [ ] **Step 5:** Commit: `fix(alpha): connect GameRulesTab to KB documents (M3)`

---

### Task 5.2: Game Sessions Tab — Show Session History (M4)

**Files:**
- Modify: `apps/web/src/components/games/detail/GameSessionsTab.tsx`

- [ ] **Step 1:** Read `GameSessionsTab.tsx` to understand current placeholder.

- [ ] **Step 2:** Replace "coming soon" with:
  - Query sessions for this game (reuse existing session list hooks, filter by gameId)
  - Show MeepleCard list (entity="session") with status badges
  - If no sessions: EmptyStateCard "Nessuna partita per questo gioco"

- [ ] **Step 3:** Verify: game detail → Sessions tab shows history.

- [ ] **Step 4:** Commit: `fix(alpha): show session history in GameSessionsTab (M4)`

---

### Task 5.3: Hide Excluded Mock Pages (M5, M6, M7)

**Files:**
- Modify: `apps/web/src/components/games/detail/GameCommunityTab.tsx` (or its parent tab config)
- Modify: `apps/web/src/components/search/SearchFilters.tsx`
- Note: Pricing page (M7) — simply not linked in alpha navigation (AlphaShell doesn't route to it)

- [ ] **Step 1:** For M5 (Community tab): find where game detail tabs are configured and remove the Community tab entry. The component can stay, just don't show the tab.

- [ ] **Step 2:** For M6 (Language filter): read `SearchFilters.tsx` line ~252 and conditionally hide or remove the language filter select.

- [ ] **Step 3:** For M7 (Pricing): verify that AlphaShell doesn't link to `/pricing` anywhere. No code change needed if it's not in the alpha navigation.

- [ ] **Step 4:** Commit: `fix(alpha): hide Community tab, language filter, and pricing page (M5, M6, M7)`

---

## Phase 6: Tier 2 Features

### Task 6.1: Notification Badge + List (F17)

**Files:**
- Modify: `apps/web/src/components/layout/alpha/AlphaTopNav.tsx`
- Create: `apps/web/src/components/features/notifications/NotificationList.tsx`

**Context:** Backend endpoints already exist. Frontend needs: notification count badge on TopNav bell icon + dropdown/page showing notification list.

- [ ] **Step 1:** Find existing notification hooks/queries in `apps/web/src/hooks/queries/` that call the notification endpoints.

- [ ] **Step 2:** Add unread count badge to the bell icon in `AlphaTopNav.tsx`. Use the existing `GET /notifications/unread-count` endpoint.

- [ ] **Step 3:** Create `NotificationList.tsx` — a dropdown (or slide-down panel) from the bell icon showing recent notifications. Each notification item: icon + message + timestamp + read/unread state. Tap → mark as read.

- [ ] **Step 4:** Verify: notifications show in the bell dropdown, unread count decrements on read.

- [ ] **Step 5:** Commit: `feat(alpha): add notification badge and list to AlphaTopNav (F17)`

---

### Task 6.2: Profile Access from Avatar Menu (F16)

**Files:**
- Modify: `apps/web/src/components/layout/alpha/AlphaTopNav.tsx`

**Context:** Profile is no longer a tab — it's accessed from the avatar in TopNav. The avatar menu should link to Profile, Settings, and Admin (if admin).

- [ ] **Step 1:** Ensure the avatar dropdown in AlphaTopNav includes:
  - Profile → `/profile`
  - Settings → `/settings`
  - Admin (if isAdmin) → `/admin`
  - Logout

- [ ] **Step 2:** Verify navigation works from the menu.

- [ ] **Step 3:** Commit: `feat(alpha): add profile/settings/admin access from avatar menu (F16)`

---

### Task 6.3: Remaining Tier 2 Features (F13, F14, F18, F19)

**Context:** These features (Wishlist, Collections, Admin wizard, Game nights) already exist in the frontend. They need to work within the alpha layout but don't need new components — they're accessible via:
- F13 (Wishlist): Already in LibraryPanel as a sub-tab
- F14 (Collections): Accessible from library detail view
- F18 (Admin wizard): Accessible from avatar menu → Admin
- F19 (Game nights): Already included in HomeFeed (Task 3.1 adds "Serate di Gioco" section)

- [ ] **Step 1:** Verify F13 (Wishlist) works in the Library panel Wishlist sub-tab.

- [ ] **Step 2:** Verify F14 (Collections) is accessible from game detail views.

- [ ] **Step 3:** Verify F18 (Admin wizard) is accessible from Admin link in avatar menu.

- [ ] **Step 4:** Verify F19 (Game nights) section appears in HomeFeed with upcoming events.

- [ ] **Step 5:** Commit if fixes needed: `fix(alpha): verify and fix Tier 2 features (F13, F14, F18, F19)`

---

## Phase 7: Polish & Responsive Audit

### Task 7.1: Design System Application (W2 final)

**Files:**
- Various components created in Phases 1-6

**Context:** Apply the design tokens consistently across all alpha components. Ensure spacing scale, typography, and glassmorphism rules are followed.

- [ ] **Step 1:** Audit all alpha components for spacing consistency:
  - Card gaps should use `gap-6` (24px / var(--space-6))
  - Section separators should use `mb-8` (32px / var(--space-8))
  - Page margins should use `pt-12` (48px / var(--space-12))

- [ ] **Step 2:** Audit typography:
  - Page titles: `font-quicksand font-bold text-xl` (24px)
  - Section headers: `font-quicksand font-bold text-lg` (20px)
  - Body/metadata: `font-nunito text-base` (16px) or `text-sm` (14px)
  - Badges/timestamps: `font-nunito text-xs` (12px)

- [ ] **Step 3:** Verify glassmorphism is ONLY on: AlphaTopNav, AlphaTabBar, CardDetailModal overlay. Remove from any other component.

- [ ] **Step 4:** Commit: `style(alpha): apply design system tokens consistently (W2)`

---

### Task 7.2: Responsive Audit (W8)

**Context:** Test every alpha screen at 4 breakpoints: 375px, 768px, 1024px, 1440px.

- [ ] **Step 1:** At 375px (mobile): verify TabBar visible, sidebar hidden, 1-column card grid, TopNav hides on scroll, CardDetailModal full-screen, session screen full-width.

- [ ] **Step 2:** At 768px (tablet): verify TabBar visible, sidebar hidden, 2-column card grid.

- [ ] **Step 3:** At 1024px (desktop): verify sidebar visible, TabBar hidden, 3-column card grid, CardDetailModal as side panel.

- [ ] **Step 4:** At 1440px (wide): verify sidebar expanded labels visible, 4-column card grid.

- [ ] **Step 5:** Fix any layout issues found.

- [ ] **Step 6:** Commit: `style(alpha): responsive audit fixes across breakpoints (W8)`

---

## Phase 8: End-to-End Verification

### Task 8.1: Success Criteria Walkthrough

**Context:** Walk through all 8 success criteria from spec Section 9 on a mobile viewport.

- [ ] **Step 1: (SC1)** Verify invite → register → onboarding flow works with alpha-layout flag on. Email invite link → `/register` with pre-filled email → verify email → onboarding → home with guided empty states.

- [ ] **Step 2: (SC2)** Verify catalog browse → add game. Library tab → Catalogo sub-tab → search → tap game card → CardDetailModal → [Aggiungi alla Libreria] → card appears in "I Miei Giochi".

- [ ] **Step 3: (SC3)** Verify PDF upload → processing → Ready. Game detail → KB tab → upload PDF → KB card shows `PdfProcessingState` enum progression (Pending → Uploading → Extracting → Chunking → Embedding → Indexing → Ready) → badge turns green at terminal `Ready` state (enum value 6). Note: the enum uses `Ready` not `Indexed` as the terminal success state.

- [ ] **Step 4: (SC4)** Verify create session → score. Play tab → Nuova Sessione → choose game → add players → confirm → live session screen → add scores.

- [ ] **Step 5: (SC5)** Verify toolkit. In live session → tool rail → tap Dice (DiceRoller renders) → tap Timer (Timer renders) → tap Notes (notes panel renders).

- [ ] **Step 6: (SC6)** Verify AI chat from session. Tool rail → tap AI → chat panel opens → type question → SSE response with citations.

- [ ] **Step 7: (SC7)** Verify pause/resume. Session → Pause → card shows "paused" in Play tab → tap → resumes from checkpoint.

- [ ] **Step 8: (SC8)** Verify end → history. Session → End → session appears in Play → Storico tab with results.

- [ ] **Step 9:** Document any issues found and create follow-up tasks.

- [ ] **Step 10:** Commit any final fixes: `fix(alpha): e2e verification fixes`

---

## Dependency Graph

```
Phase 0 (Foundation)
  ├── Task 0.1: Feature Flag ─────────┐
  ├── Task 0.2: Design Tokens ────────┤
  └── Task 0.3: useAlphaNav store ────┤
                                      │
Phase 1 (Layout Shell)                │
  ├── Task 1.1: AlphaShell ←──────────┘
  ├── Task 1.2: AlphaTopNav ←── 1.1
  ├── Task 1.3: AlphaTabBar ←── 1.1
  ├── Task 1.4: SwipeableContainer ←── 1.1, 1.3
  └── Task 1.5: Desktop Sidebar ←── 1.1

Phase 2 (Core Components)
  ├── Task 2.1: EmptyStateCard ←── 0.2
  ├── Task 2.2: CardDetailModal ←── 0.3
  └── Task 2.3: SkeletonCard ←── 0.2

Phase 3 (Tab Panels + Onboarding) ←── Phase 1, 2
  ├── Task 3.0: Invite + Onboarding (F1, F2) ←── 0.1
  ├── Task 3.1: HomeFeed (F3, F19) ←── 2.1, 2.2
  ├── Task 3.2: LibraryPanel (F4-F6) ←── 2.1, 2.2
  ├── Task 3.3: PlayPanel (F7, F10, F15) ←── 2.1, 2.2
  └── Task 3.4: ChatPanel (F11) ←── 2.1

Phase 4 (Session + Verification) ←── Phase 1
  ├── Task 4.1: Session Layout (W10) ←── 1.1
  ├── Task 4.2: Verify Scoreboard + Tool Rail (F8, F9) ←── 4.1
  └── Task 4.3: Custom Tools (M1) ←── 4.1

Phase 5 (Mock Fixes) ←── Phase 3
  ├── Task 5.1: GameRulesTab (M3)
  ├── Task 5.2: GameSessionsTab (M4)
  └── Task 5.3: Hide pages (M5-M7)

Phase 6 (Tier 2) ←── Phase 3
  ├── Task 6.1: Notifications (F17) ←── 1.2
  ├── Task 6.2: Profile menu (F16) ←── 1.2
  └── Task 6.3: Verify F13,14,18,19

Phase 7 (Polish) ←── Phase 3-6
  ├── Task 7.1: Design system audit
  └── Task 7.2: Responsive audit

Phase 8 (E2E) ←── Phase 0-7
  └── Task 8.1: Success criteria walkthrough
```

## Parallelization Guide

Tasks that can run in parallel (independent of each other):

| Parallel Group | Tasks |
|---------------|-------|
| **Foundation** | 0.1, 0.2, 0.3 (all independent) |
| **Layout components** | 1.2 → 1.3 → 1.4 → 1.5 (SEQUENTIAL — all modify AlphaShellClient.tsx) |
| **Core components** | 2.1, 2.2, 2.3 (all independent) |
| **Tab panels** | 3.1, 3.2, 3.3, 3.4 (all independent once Phase 1+2 done) |
| **Session + Mock fixes** | Phase 4 and Phase 5 (independent of each other) |
| **Tier 2** | 6.1, 6.2, 6.3 (all independent) |
