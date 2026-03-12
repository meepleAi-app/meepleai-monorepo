# Desktop UX: Card Flip Enhancement, Sidebar Styling, View Transitions

**Date**: 2026-03-12
**Status**: Approved
**Repo**: meepleai-monorepo-dev (frontend at `apps/web/`)

## Overview

Three improvements to the desktop UX experience, building on the existing MeepleCard system and "Game Table" metaphor:

1. **Card Flip Enhanced** — Evolve `GameBackContent` with contextual actions, inline info chips, temporal context, EntityLinks, and enriched footer
2. **Sidebar Mini-Card Style** — Add left-border active styling, tooltips on collapsed items, and micro-interactions to the existing `Sidebar` + `SidebarContextNav` system
3. **View Transitions** — SPA-driven page transitions via View Transitions API, including shared-element MeepleCard morph

---

## 1. Card Flip — Enhanced GameBackContent

### Current State

`GameBackContent.tsx` renders the back of game MeepleCards with:
- Entity-colored header (HSL + diagonal stripe pattern)
- Stats grid 3-col: Peso, Durata, Giocatori, Voto, Partite (as `StatChip` components)
- KB document preview: up to 3 docs with green/amber status dots
- Quick actions 4-col: Chat, KB, Note, Preferito (as `ActionButton` components)
- Detail page link

`FlipCard.tsx` provides the 3D flip animation with:
- Framer Motion `rotateY` animation (700ms, ease [0.4, 0, 0.2, 1])
- Two trigger modes: `'card'` (click anywhere) and `'button'` (dedicated rotate-ccw icon)
- Touch detection: always uses button mode on touch devices (WCAG 44px target)
- Controlled/uncontrolled state management
- `customBackContent` prop for entity-specific back sides
- 5 variant configs controlling content density (compact → hero)

`SessionBackContent.tsx` renders session-specific back with:
- Status-adaptive sections (setup/inProgress/paused/completed)
- Player ranking with medals and progress bars
- Timeline of last 5 events
- Media counts (photos/videos/audio)

### Changes

#### 1.1 Enriched Header

Add temporal context and personal stats below the title:

```tsx
// Current
<h2 className="...">Catan</h2>

// Proposed
<h2 className="...">Catan</h2>
<p className="relative z-[1] text-xs text-white/75 mt-0.5">
  {lastPlayedLabel} · Win rate {winRate}%
</p>
```

New props on `GameBackData`:
```typescript
lastPlayedAt?: string;   // ISO date → displayed as "3 giorni fa"
                         // Uses: formatDistanceToNow(new Date(lastPlayedAt), { locale: it, addSuffix: true })
                         // Requires: import { it } from 'date-fns/locale'
winRate?: number;         // 0-100, displayed as percentage
timesPlayed?: number;     // already exists on interface, reused in action context
```

**Data sources** (all frontend-computable from existing API responses):
- `lastPlayedAt`: mapped from `lastPlayed` field in `library.schemas.ts:287` (`z.string().datetime().nullable()`). Already used in `recent-games-section.tsx:39` with `formatDistanceToNow`. Consumer (`MeepleLibraryGameCard`) must map: `lastPlayedAt: game.lastPlayed ?? undefined`
- `winRate`: computed client-side from session history (`wins / totalGames * 100`). If session data is unavailable, pass `undefined` (omitted from header).
- `timesPlayed`: already on `GameBackData` interface but currently hardcoded to `0` in `MeepleLibraryGameCard.tsx:341` (`// TODO: wire from game data`). Must be wired from `game.playCount` or session count. If still 0: action context shows "Nessuna partita" instead of "0 partite giocate".

Display rules:
- If `lastPlayedAt` is null/undefined → show "Mai giocato"
- If `winRate` is null/undefined → omit "Win rate" text
- If `timesPlayed === 0` → action context shows "Nessuna partita"
- Header subtitle only renders if at least one value is present

#### 1.2 Info Chips (replacing Stats Grid)

Replace the 3-column `StatChip` grid with inline compact chips:

```tsx
// Current: grid of 5 StatChip components (Peso, Durata, Giocatori, Voto, Partite)
// Proposed: horizontal chip row
<div className="flex gap-1.5 flex-wrap">
  <InfoChip><Users className="w-3 h-3" /> {playerRange}</InfoChip>
  <InfoChip><Clock className="w-3 h-3" /> {playingTimeMinutes}min</InfoChip>
  <InfoChip>{complexityDots}</InfoChip>
  <InfoChip><Star className="w-3 h-3" /> {averageRating}</InfoChip>
</div>
```

`InfoChip` is an internal sub-component:
```tsx
function InfoChip({ children }: { children: React.ReactNode }) {
  return (
    <span className="inline-flex items-center gap-1 bg-muted/40 px-2 py-0.5 rounded-md text-xs text-card-foreground">
      {children}
    </span>
  );
}
```

`complexityDots` renders filled/empty circles based on `complexityRating`:
```tsx
// Render as styled spans for cross-platform consistency (Unicode circles vary across fonts)
const complexityDots = complexityRating != null
  ? Array.from({ length: 5 }, (_, i) => (
      <span key={i} className={`inline-block w-2 h-2 rounded-full ${i < Math.round(complexityRating) ? 'bg-current' : 'bg-current/20'}`} />
    ))
  : null;
```

Rationale: chips use less vertical space, freeing room for contextual actions. Uses Lucide icons (consistent with codebase) instead of emoji.

#### 1.3 Contextual Actions (replacing Quick Actions Grid)

Replace the 4-column icon-only grid with full-width action buttons that show inline context:

```tsx
<div className="flex flex-col gap-1.5">
  <ContextualAction
    icon={Play}
    label="Nuova Sessione"
    context={`${timesPlayed} partite giocate`}
    colorHsl="25 95% 45%"
    onClick={onNewSession}
  />
  <ContextualAction
    icon={Bot}
    label="Chiedi all'AI"
    context={kbContextLabel}  // "KB pronta · 2 doc" or "Nessuna KB"
    colorHsl="262 83% 58%"
    onClick={onChatAgent}
  />
  {onAddToGameNight && (
    <ContextualAction
      icon={Calendar}
      label="Aggiungi a serata"
      context={nextGameNightLabel}  // "Sab 21:00" or null
      colorHsl="217 91% 60%"
      onClick={onAddToGameNight}
    />
  )}
  {entityLinkCount > 0 && (
    <ContextualAction
      icon={Link2}
      label="Espansioni"
      context={`${entityLinkCount} collegate`}
      colorHsl="142 70% 45%"
      onClick={onViewLinks}
    />
  )}
</div>
```

`ContextualAction` component (internal sub-component):
```tsx
function ContextualAction({
  icon: Icon, label, context, colorHsl, onClick
}: {
  icon: React.ElementType; label: string; context?: string | null;
  colorHsl: string; onClick?: () => void;
}) {
  return (
    <button
      type="button"
      className="flex items-center gap-2 w-full rounded-lg px-3 py-2 text-xs font-medium transition-colors"
      style={{
        backgroundColor: `hsla(${colorHsl}, 0.08)`,
        borderColor: `hsla(${colorHsl}, 0.15)`,
        color: `hsl(${colorHsl})`,
      }}
      onClick={(e) => { e.stopPropagation(); onClick?.(); }}
    >
      <Icon className="w-4 h-4 shrink-0" />
      <span>{label}</span>
      {context && (
        <span className="ml-auto text-[10px] text-muted-foreground">{context}</span>
      )}
    </button>
  );
}
```

Color values use the existing `entityColors` map from `meeple-card-styles.ts` where possible:
- `"25 95% 45%"` = `entityColors.game` (orange)
- `"262 83% 58%"` = `entityColors.player` (purple, used for AI agent actions)
- `"217 91% 60%"` = custom blue for calendar/game-night action (no matching entity color — closest is `chatSession: '220 80% 55%'`)
- `"142 70% 45%"` = `entityColors.toolkit` (green)

New props on `GameBackActions`:
```typescript
onNewSession?: () => void;
onAddToGameNight?: () => void;    // conditionally rendered — if null, action hidden
onViewLinks?: () => void;
```

New props on `GameBackData`:
```typescript
nextGameNight?: string;      // displayed as context for "Aggiungi a serata"
                             // Data source: from game-nights API (if feature exists)
                             // If null/undefined: entire action button hidden
entityLinkCount?: number;    // count of linked entities (expansions etc.)
                             // Data source: GET /api/v1/library/entity-links/count?entityType=Game&entityId={id}
                             // (existing endpoint in entityLinksClient.ts → getEntityLinkCount)
                             // If 0: entire action button hidden
```

#### 1.4 KB Preview Handling

The existing KB preview section (up to 3 docs with status dots) is **removed** from the main content area. Its information is condensed into the "Chiedi all'AI" action's context label:

```typescript
const kbContextLabel = hasKb
  ? `KB pronta · ${kbCardCount} doc`
  : kbDocuments?.some(d => d.status !== 'Ready')
    ? 'KB in elaborazione'
    : null;
```

Rationale: the KB preview was 3-4 lines of vertical space. The status information (ready/processing) is now communicated via the action's inline context, saving space while preserving the signal.

#### 1.5 Compact Footer

Replace the standalone detail link with a compact footer row:

```tsx
<div className="mt-auto border-t border-border/10 pt-2 flex items-center justify-between text-xs">
  <div className="flex gap-3 text-muted-foreground">
    {actions?.onToggleFavorite && (
      <span>{actions.isFavorite ? <Heart className="w-3 h-3 fill-current" /> : <Heart className="w-3 h-3" />} Pref</span>
    )}
    {noteCount != null && noteCount > 0 && <span><StickyNote className="w-3 h-3" /> {noteCount}</span>}
  </div>
  {detailHref && isSafeHref(detailHref) && (
    <Link href={detailHref} className="font-medium font-nunito" style={{ color: `hsl(${entityColor})` }}
      onClick={(e) => e.stopPropagation()}>
      Dettaglio →
    </Link>
  )}
</div>
```

Uses Lucide icons (Heart, StickyNote) consistent with existing codebase — no emoji.

New props on `GameBackData`:
```typescript
noteCount?: number;    // Data source: local state or GET /api/v1/games/{id}/notes/count
                       // If 0 or undefined: hidden
```

#### 1.6 Removed Actions Migration

Two existing actions from the 4-col `ActionButton` grid are **removed from the card back**:

| Removed Action | Current Behavior | Migration |
|---------------|-----------------|-----------|
| `onViewKb` (KB button) | Opens KB drawer (`setKbDrawerOpen(true)`) | **Absorbed into "Chiedi all'AI" action**. The AI action's context label shows KB status. Clicking it navigates to chat (existing behavior of `onChatAgent`). The KB drawer remains accessible via the KB badge on the card front (existing feature). |
| `onEditNotes` (Note button) | Opens notes modal | **Moved to footer**. The `noteCount` indicator in the footer links to notes. Alternatively, notes are accessible via the card's quick-actions popover (existing `entityQuickActions` feature on MeepleCard). |

**`onToggleFavorite` is kept** but moves from the action grid to the compact footer (section 1.5). It remains on the `GameBackActions` interface.

#### 1.7 Complete Interface (Before → After)

**`GameBackData` — Before:**
```typescript
export interface GameBackData {
  complexityRating?: number | null;
  playingTimeMinutes?: number | null;
  minPlayers?: number | null;
  maxPlayers?: number | null;
  averageRating?: number | null;
  timesPlayed?: number;
  kbDocuments?: Array<{ id: string; fileName: string; status: string }>;
  hasKb?: boolean;
  kbCardCount?: number;
}
```

**`GameBackData` — After:**
```typescript
export interface GameBackData {
  // Kept (used by InfoChips)
  complexityRating?: number | null;
  playingTimeMinutes?: number | null;
  minPlayers?: number | null;
  maxPlayers?: number | null;
  averageRating?: number | null;
  timesPlayed?: number;

  // Kept (used by kbContextLabel computation)
  kbDocuments?: Array<{ id: string; fileName: string; status: string }>;
  hasKb?: boolean;
  kbCardCount?: number;

  // NEW — enriched header
  lastPlayedAt?: string;     // ISO date → "3 giorni fa"
  winRate?: number;           // 0-100

  // NEW — contextual actions
  nextGameNight?: string;     // display label for game night context
  entityLinkCount?: number;   // count of linked entities

  // NEW — footer
  noteCount?: number;         // note indicator in footer
}
```

**`GameBackActions` — Before:**
```typescript
export interface GameBackActions {
  onChatAgent?: () => void;
  onViewKb?: () => void;       // REMOVED from UI (KB drawer still accessible via card front badge)
  onEditNotes?: () => void;    // REMOVED from action grid (accessible via quick-actions popover)
  onToggleFavorite?: () => void; // MOVED to footer
  isFavorite?: boolean;          // MOVED to footer
}
```

**`GameBackActions` — After:**
```typescript
export interface GameBackActions {
  // Kept
  onChatAgent?: () => void;
  onToggleFavorite?: () => void;  // rendered in footer, not action grid
  isFavorite?: boolean;

  // REMOVED (see section 1.6 for migration)
  // onViewKb — no longer on interface
  // onEditNotes — no longer on interface

  // NEW
  onNewSession?: () => void;
  onAddToGameNight?: () => void;
  onViewLinks?: () => void;
}
```

**Consumer update required**: `MeepleLibraryGameCard.tsx` must update its `gameBackActions` useMemo (line ~362) to remove `onViewKb`/`onEditNotes` and add `onNewSession`/`onViewLinks`. The `gameBackData` useMemo (line ~333) must add `lastPlayedAt: game.lastPlayed ?? undefined` and wire `timesPlayed` from actual data.

#### 1.8 Rendering Examples

**Example A — New game, never played, no KB:**
```
┌──────────────────────────┐
│ 🎲 Catan                 │  ← header
│ Mai giocato              │  ← no win rate (omitted)
├──────────────────────────┤
│ [👥 3-4] [⏱ 90min] [●●●○○] [⭐ 7.2] │ ← InfoChips
├──────────────────────────┤
│ ▶ Nuova Sessione    Nessuna partita │ ← action (timesPlayed=0)
│ 🤖 Chiedi all'AI    Nessuna KB     │ ← action (hasKb=false)
├──────────────────────────┤
│ ♡ Pref              Dettaglio → │ ← footer
└──────────────────────────┘
```
- Only 2 actions: "Aggiungi a serata" hidden (onAddToGameNight=null), "Espansioni" hidden (entityLinkCount=0)

**Example B — Active game with KB and links:**
```
┌──────────────────────────┐
│ 🎲 Catan                 │
│ 3 giorni fa · Win rate 33% │
├──────────────────────────┤
│ [👥 3-4] [⏱ 90min] [●●●○○] [⭐ 7.2] │
├──────────────────────────┤
│ ▶ Nuova Sessione    12 partite giocate │
│ 🤖 Chiedi all'AI    KB pronta · 2 doc │
│ 📅 Aggiungi a serata    Sab 21:00    │
│ 🔗 Espansioni           3 collegate  │
├──────────────────────────┤
│ ♥ Pref   📝 2           Dettaglio → │
└──────────────────────────┘
```
- All 4 actions visible, noteCount=2 shown in footer

**Example C — Game with KB processing, no game night feature:**
```
┌──────────────────────────┐
│ 🎲 Gloomhaven            │
│ 2 settimane fa            │  ← no win rate (undefined)
├──────────────────────────┤
│ [👥 1-4] [⏱ 120min] [●●●●○] [⭐ 8.8] │
├──────────────────────────┤
│ ▶ Nuova Sessione    5 partite giocate │
│ 🤖 Chiedi all'AI    KB in elaborazione │ ← kbDocuments has non-Ready items
├──────────────────────────┤
│ ♡ Pref              Dettaglio → │
└──────────────────────────┘
```
- "Aggiungi a serata" hidden (feature not wired), "Espansioni" hidden (entityLinkCount=0), "Chiedi all'AI" still clickable even with processing KB

### File Changes

| File | Change |
|------|--------|
| `components/ui/data-display/meeple-card-features/GameBackContent.tsx` | Replace StatChip grid → InfoChip row, replace ActionButton grid → ContextualAction list, remove standalone KB preview, remove `StatChip`/`ActionButton` sub-components, enriched header, compact footer. New internal sub-components: `InfoChip`, `ContextualAction`. Update `GameBackData` and `GameBackActions` interfaces per section 1.7 |
| `components/ui/data-display/meeple-card.tsx` | Pass new props through to GameBackContent, remove `onViewKb`/`onEditNotes` from prop threading |
| `components/library/MeepleLibraryGameCard.tsx` | Update `gameBackData` useMemo: add `lastPlayedAt`, wire `timesPlayed` from real data. Update `gameBackActions` useMemo: remove `onViewKb`/`onEditNotes`, add `onNewSession`/`onViewLinks` |

### Test Impact

**Existing tests affected:**
- `components/library/__tests__/MeepleLibraryGameCard.test.tsx` — constructs `GameBackData` and `GameBackActions` objects; must update to match new interface (remove `onViewKb`/`onEditNotes`, add new fields)
- Any snapshot tests that render GameBackContent will break due to structural changes

**New test scenarios needed:**
- InfoChip rendering with various data combinations (all present, some null, complexity=null)
- ContextualAction click handlers fire with `stopPropagation` (must NOT trigger card flip)
- Conditional rendering: "Espansioni" hidden when `entityLinkCount === 0`
- Conditional rendering: "Aggiungi a serata" hidden when `onAddToGameNight` is null
- Conditional rendering: "Nessuna partita" when `timesPlayed === 0`
- Header subtitle: "Mai giocato" when `lastPlayedAt` is null
- Header subtitle: win rate omitted when `winRate` is undefined
- KB context label variations: "KB pronta · N doc" / "KB in elaborazione" / "Nessuna KB"
- Footer: favorite icon filled when `isFavorite=true`, noteCount hidden when 0
- `prefers-reduced-motion` disables view transition animations (Section 3)

### Not in Scope

- New BackContent components for other entity types (Player, Agent, Document, etc.)
- Template-slot architecture refactor (future: option B from brainstorm)
- SessionBackContent changes (already feature-complete)
- New backend API endpoints — all data sources use existing APIs or client-side computation
- KB drawer removal — it remains accessible via the KB badge on the card front

---

## 2. Sidebar Styling Improvements

### Current State

> **Note**: The active sidebar is `Sidebar` + `SidebarContextNav` (Issue #4936), NOT the legacy `CardRack` component. `CardRack` exists in the codebase but is not rendered in the current layout chain.

**Layout chain**: `(authenticated)/layout.tsx` → `AppShell` → `AppShellClient.tsx` → `Sidebar` → `SidebarContextNav`

`Sidebar.tsx`:
- Fixed left sidebar with toggle button (not hover-based)
- `--sidebar-width-collapsed` / `--sidebar-width-expanded` CSS variables
- Contains: `SidebarToggle` + `SidebarContextNav` + `SidebarUser`

`SidebarContextNav.tsx`:
- **Two zones**: Fixed Nav Zone (6 primary links) + Contextual Zone (route-specific panels)
- Fixed Nav: Dashboard, Libreria, Scopri, Chat AI, Sessioni, Giocatori
- Contextual panels: DashboardContextPanel, LibraryContextPanel, GamesFilterPanel
- `SectionLabel` component already exists: `text-[10px] uppercase tracking-wider text-sidebar-foreground/40`
- `AnimatePresence` with slide/fade for contextual panel transitions
- `SidebarLink` component: icon + label, active state with game-orange bg tint

**Already has** (no changes needed):
- Semantic grouping with `SectionLabel` in contextual panels
- Animated contextual panel transitions via Framer Motion
- Collapsed `<hr>` separators

### Changes

#### 2.1 Left Border Active Indicator

Add a left border accent to active `SidebarLink` items for stronger visual signal:

```tsx
// In SidebarLink component
// Current active classes:
'bg-[hsl(25_95%_45%/0.12)] text-[hsl(25_95%_42%)] font-semibold'

// Proposed active classes (add border):
'bg-[hsl(25_95%_45%/0.08)] text-[hsl(25_95%_42%)] font-semibold',
'border-l-[3px] border-[hsl(25_95%_45%)]'

// All items get transparent left border for alignment:
'border-l-[3px] border-transparent'  // inactive
'border-l-[3px] border-[hsl(25_95%_45%)]'  // active
```

#### 2.2 Tooltip on Collapsed

When sidebar is collapsed, hovering a `SidebarLink` shows a tooltip to the right:

```tsx
// In SidebarLink, add group class to the Link wrapper
<Link href={href} className={cn('group relative', /* existing classes */)}>
  <Icon className="..." />
  {!isCollapsed && <span>...</span>}

  {/* Tooltip — only when collapsed */}
  {isCollapsed && (
    <span className={cn(
      'absolute left-full ml-2 top-1/2 -translate-y-1/2',
      'px-2 py-1 rounded-md',
      'bg-popover text-popover-foreground text-xs font-medium',
      'shadow-md border border-border/50',
      'opacity-0 group-hover:opacity-100 pointer-events-none',
      'transition-opacity duration-150',
      'whitespace-nowrap z-50'
    )}>
      {label}
    </span>
  )}
</Link>
```

#### 2.3 Micro-Interactions

- Icon hover scale: add `group-hover:scale-105 transition-transform duration-150` to the Icon element
- Active icon: no animation change (the existing `AnimatePresence` on contextual panels is sufficient)

### File Changes

| File | Change |
|------|--------|
| `components/layout/Sidebar/SidebarContextNav.tsx` | `SidebarLink`: add `group` class, left border active indicator, tooltip when collapsed, icon hover scale |

### Test Impact

Existing tests at `Sidebar/__tests__/` may need updates:
- Tooltip visibility assertion when collapsed
- Left border class assertion on active items
- No structural changes to test (same component, same props)

---

## 3. View Transitions

### Current State

No View Transitions API usage in the codebase. Framer Motion used for:
- `fadeUp` in `gaming-hub-client.tsx` (dashboard sections)
- `FlipCard` 3D rotation animation
- `AnimatePresence` in `SidebarContextNav` (panel transitions)

### Changes

#### 3.1 useViewTransition Hook (P0)

```typescript
// lib/hooks/useViewTransition.ts
'use client';

import { useRouter } from 'next/navigation';
import { useCallback } from 'react';

/**
 * Wraps Next.js router.push() with the View Transitions API.
 * Falls back to instant navigation on unsupported browsers.
 */
export function useViewTransition() {
  const router = useRouter();

  const navigateWithTransition = useCallback(
    (href: string) => {
      // Graceful fallback: no View Transitions API → instant navigation
      if (!document.startViewTransition) {
        router.push(href);
        return;
      }
      document.startViewTransition(() => {
        router.push(href);
      });
    },
    [router]
  );

  return { navigateWithTransition };
}
```

**Error handling**: If navigation fails (404, network error), the View Transition resolves normally and the user sees the error page rendered by Next.js error boundaries. No additional error handling needed in the hook.

**TypeScript note**: `document.startViewTransition` may require a type declaration if not in the project's `lib` target. Add if needed:
```typescript
// types/view-transitions.d.ts
interface ViewTransition {
  finished: Promise<void>;
  ready: Promise<void>;
  updateCallbackDone: Promise<void>;
}

interface Document {
  startViewTransition?(callback: () => void | Promise<void>): ViewTransition;
}
```

#### 3.2 CSS Transition Rules (P0)

```css
/* globals.css — SPA View Transitions (triggered by useViewTransition hook) */

/* Default crossfade for page content */
::view-transition-old(root) {
  animation: vt-fade-out 150ms ease-out;
}

::view-transition-new(root) {
  animation: vt-fade-in 150ms ease-in;
}

@keyframes vt-fade-out {
  from { opacity: 1; }
  to { opacity: 0; }
}

@keyframes vt-fade-in {
  from { opacity: 0; }
  to { opacity: 1; }
}

/* Named transition: page-content slides */
::view-transition-old(page-content) {
  animation: vt-slide-out 200ms ease-out;
}

::view-transition-new(page-content) {
  animation: vt-slide-in 200ms ease-in;
}

@keyframes vt-slide-out {
  from { transform: translateX(0); opacity: 1; }
  to { transform: translateX(-20px); opacity: 0; }
}

@keyframes vt-slide-in {
  from { transform: translateX(20px); opacity: 0; }
  to { transform: translateX(0); opacity: 1; }
}

/* Respect reduced motion */
@media (prefers-reduced-motion: reduce) {
  ::view-transition-old(root),
  ::view-transition-new(root),
  ::view-transition-old(page-content),
  ::view-transition-new(page-content) {
    animation: none;
  }
}
```

**Note**: `@view-transition { navigation: auto; }` is intentionally **not** included. That rule is for MPA (multi-page) cross-document transitions. Next.js App Router uses SPA navigation via `router.push()`, so transitions are triggered programmatically by the `useViewTransition` hook calling `document.startViewTransition()`.

#### 3.3 Page Content Transition Name (P1)

In `AppShellClient.tsx` (where `<main id="main-content">` is rendered at line ~100), add `viewTransitionName`:

```tsx
<main
  id="main-content"
  style={{ viewTransitionName: 'page-content' }}
  className={/* existing classes */}
>
  {children}
</main>
```

**File**: `components/layout/AppShell/AppShellClient.tsx` (NOT `(authenticated)/layout.tsx` — that file just renders `<AppShell>`)

#### 3.4 MeepleCard Shared Element Morph (P2)

On `meeple-card.tsx`, wire the existing `entityId` prop to `view-transition-name`:

```tsx
// In the card wrapper div (already has entityId prop on MeepleCardProps)
style={{
  viewTransitionName: entityId ? `meeple-card-${entityId}` : undefined,
}}
```

On the game detail page, match the transition name in the header:

```tsx
// In the game detail page's client component (e.g., game-detail-client.tsx or equivalent).
// Note: app/(authenticated)/games/[id]/page.tsx may not exist yet — apply to whichever
// client component renders the game header (image + title + metadata).
<div style={{ viewTransitionName: `meeple-card-${gameId}` }}>
  {/* game header: image + title + metadata */}
</div>
```

The browser automatically morphs between the two elements during navigation when `useViewTransition` triggers the transition.

**No new prop needed**: `entityId` already exists on `MeepleCardProps`.

#### 3.5 Sidebar Integration (P1)

Use `useViewTransition` in `SidebarLink` for sidebar navigation:

```tsx
// In SidebarContextNav.tsx → SidebarLink
import { useViewTransition } from '@/lib/hooks/useViewTransition';

function SidebarLink({ href, icon: Icon, label, isActive, isCollapsed }) {
  const { navigateWithTransition } = useViewTransition();

  return (
    <Link
      href={href}
      onClick={(e) => {
        e.preventDefault();
        navigateWithTransition(href);
      }}
      className={/* same as current Link classes */}
    >
      {/* ... */}
    </Link>
  );
}
```

This keeps `<Link>` (preserving Next.js prefetching) but intercepts the click to wrap navigation in `document.startViewTransition()`. Using `<a>` instead would work but loses prefetching — always prefer `<Link>` + `onClick`.

### Priority & Risk

| Priority | Feature | Effort | Risk |
|----------|---------|--------|------|
| **P0** | Crossfade between pages (hook + CSS) | Low | None — CSS only, graceful fallback |
| **P1** | Slide from sidebar + page-content name | Low | None — CSS only |
| **P2** | MeepleCard shared-element morph | Medium | View Transitions API browser support |
| **P3** | Sidebar micro-interactions | Low | None |

### Browser Support

- Chrome 111+, Edge 111+: full support
- Safari 18+: full support (same-document only)
- Firefox 130+: partial support (no cross-document transitions, but SPA works)
- Older browsers: graceful fallback — instant navigation, no animation, zero breaking

### File Changes

| File | Change |
|------|--------|
| `lib/hooks/useViewTransition.ts` | **NEW** — hook wrapper for SPA view transitions |
| `types/view-transitions.d.ts` | **NEW** (if needed) — TypeScript type declarations |
| `app/globals.css` | Add view transition keyframes and pseudo-element rules |
| `components/layout/AppShell/AppShellClient.tsx` | Add `viewTransitionName: 'page-content'` to `<main>` |
| `components/ui/data-display/meeple-card.tsx` | Wire `entityId` → `viewTransitionName` style |
| `components/layout/Sidebar/SidebarContextNav.tsx` | Use `useViewTransition` in `SidebarLink` |
| `app/(authenticated)/games/[id]/` | Add matching `viewTransitionName` to detail header |

---

## Architecture Summary

```
Modified files (6):
  components/ui/data-display/meeple-card-features/GameBackContent.tsx
  components/ui/data-display/meeple-card.tsx
  components/library/MeepleLibraryGameCard.tsx
  components/layout/Sidebar/SidebarContextNav.tsx
  components/layout/AppShell/AppShellClient.tsx
  app/globals.css

New files (1-2):
  lib/hooks/useViewTransition.ts
  types/view-transitions.d.ts  (if TS types needed)

Detail page (1):
  app/(authenticated)/games/[id]/  (add viewTransitionName to header)
```

## Out of Scope

- BackContent for entity types other than Game/Session
- Template-slot architecture for generic entity backs
- Dashboard "Game Table" layout redesign
- SSE / real-time data integration
- Mobile-specific layout changes
- Next.js experimental `unstable_ViewTransition` component (use native API instead)
- Legacy `CardRack` component changes (not used in active layout)
- New backend API endpoints — all data uses existing APIs or client-side computation
- Game nights scheduling feature (if `onAddToGameNight` is null, action is hidden)

## NFR

- All transitions < 300ms (except existing flip 700ms)
- `prefers-reduced-motion: reduce` disables all view transition animations
- Touch target minimum 44px maintained on all interactive elements
- View Transitions: graceful fallback on unsupported browsers (no animation, no error)
- No new npm dependencies required (View Transitions API is browser-native)
- `date-fns` locale: use `import { it } from 'date-fns/locale'` for Italian relative dates
