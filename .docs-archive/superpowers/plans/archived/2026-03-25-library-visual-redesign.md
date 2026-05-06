# Library Visual Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix MeepleCard flicker, improve icon sizes, adopt Hybrid Warm palette, replace tab bar with dropdown, integrate Meeple Mana SVG icons.

**Architecture:** Incremental 5-step approach on a single feature branch. Steps 1→2→3 sequential (palette → flicker fix → icon resize). Step 4 (dropdown) and Step 5 (SVG icons) independent. All changes are frontend-only CSS/component modifications.

**Tech Stack:** Next.js 16, React 19, Tailwind CSS 4, CVA (class-variance-authority), Framer Motion, shadcn/ui

**Spec:** `docs/superpowers/specs/2026-03-25-library-visual-redesign-design.md`

---

## Task 0: Setup

**Files:**
- None (branch setup only)

- [ ] **Step 1: Create feature branch**

```bash
git checkout main-dev && git pull
git checkout -b feature/library-visual-redesign
git config branch.feature/library-visual-redesign.parent main-dev
```

- [ ] **Step 2: Verify clean state**

```bash
cd apps/web && pnpm build
```

Expected: Build succeeds with no errors.

---

## Task 1: Hybrid Warm-Modern Color Palette

**Files:**
- Modify: `apps/web/src/styles/design-tokens.css:164-181`

- [ ] **Step 1: Update dark mode Neon Holo tokens**

In `apps/web/src/styles/design-tokens.css`, replace lines 160-171 (including the section comment):

```css
    /* ============================================================================
     * HYBRID WARM-MODERN PALETTE
     * Warm-neutral visual identity (spec: 2026-03-25-library-visual-redesign)
     * ============================================================================ */
    --nh-bg-base: #14120e;
    --nh-bg-surface: #1e1b16;
    --nh-bg-surface-end: #27231c;
    --nh-bg-elevated: #302b22;
    --nh-border-default: rgba(200,160,100,0.08);
    --nh-text-primary: #f0ece4;
    --nh-text-secondary: #a09080;
    --nh-text-muted: #6a5d4e;
```

- [ ] **Step 2: Update light mode fallback tokens**

Replace lines 173-181:

```css
    /* Light mode values */
    --nh-bg-base-light: #faf8f5;
    --nh-bg-surface-light: #fffcf8;
    --nh-bg-surface-end-light: #f5f0e8;
    --nh-bg-elevated-light: #fffcf8;
    --nh-border-default-light: rgba(160,120,60,0.08);
    --nh-text-primary-light: #1a1a1a;
    --nh-text-secondary-light: #5a4a35;
    --nh-text-muted-light: #8a7a65;
```

- [ ] **Step 3: Verify build**

```bash
cd apps/web && pnpm build
```

Expected: Build succeeds. No visual change yet (tokens have no consumers until Task 2).

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/styles/design-tokens.css
git commit -m "feat(design): replace Neon Holo palette with Hybrid Warm-Modern tokens"
```

---

## Task 2: Fix MeepleCard Flicker — Remove Glassmorphism

**Files:**
- Modify: `apps/web/src/styles/globals.css:363-383`

- [ ] **Step 1: Remove glassmorphism CSS rules**

In `apps/web/src/styles/globals.css`, delete lines 363-383 (the entire glassmorphism block):

```css
  /* DELETE THIS ENTIRE BLOCK: */
  /* MeepleCard glassmorphism via ::before pseudo (matches mockup) */
  [data-variant="grid"],
  [data-variant="featured"] {
    isolation: isolate;
  }
  [data-variant="grid"]::before,
  [data-variant="featured"]::before {
    content: '';
    position: absolute;
    inset: 0;
    background: rgba(255, 255, 255, 0.6);
    backdrop-filter: blur(12px) saturate(180%);
    z-index: -1;
    border-radius: inherit;
    pointer-events: none;
  }
  .dark [data-variant="grid"]::before,
  .dark [data-variant="featured"]::before {
    background: rgba(30, 25, 20, 0.7);
  }
```

- [ ] **Step 2: Verify build**

```bash
cd apps/web && pnpm build
```

Expected: Build succeeds.

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/styles/globals.css
git commit -m "fix(meeple-card): remove glassmorphism backdrop-filter from grid and featured variants"
```

---

## Task 3: Fix MeepleCard Flicker — Remove Parchment Texture & Update Backgrounds

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card-styles.ts:102-149`

- [ ] **Step 1: Update CVA grid variant**

In `meeple-card-styles.ts`, replace the `grid` variant (lines 102-111):

```typescript
        grid: [
          'flex flex-col rounded-2xl overflow-hidden',
          'bg-[var(--nh-bg-surface)] border border-[var(--nh-border-default)]',
          'border-l-[3px] [border-left-color:var(--mc-entity-color,transparent)]',
          '[box-shadow:var(--shadow-warm-sm)] hover:[box-shadow:var(--shadow-warm-xl)]',
          'hover:-translate-y-2',
          '[contain:layout_paint]',
        ],
```

- [ ] **Step 2: Update CVA list variant**

Replace the `list` variant (lines 112-119):

```typescript
        list: [
          'flex flex-row items-center gap-4 p-3 rounded-xl',
          'bg-[var(--nh-bg-surface)] border border-[var(--nh-border-default)]',
          'border-l-[3px] [border-left-color:var(--mc-entity-color,transparent)]',
          '[box-shadow:var(--shadow-warm-sm)] hover:[box-shadow:var(--shadow-warm-md)]',
          'hover:translate-x-1',
        ],
```

- [ ] **Step 3: Update CVA featured variant**

Replace the `featured` variant (lines 126-135):

```typescript
        featured: [
          'flex flex-col rounded-2xl overflow-hidden',
          'bg-[var(--nh-bg-surface)] border border-[var(--nh-border-default)]',
          'border-l-[3px] [border-left-color:var(--mc-entity-color,transparent)]',
          '[box-shadow:var(--shadow-warm-md)] hover:[box-shadow:var(--shadow-warm-xl)]',
          'hover:-translate-y-2',
          '[contain:layout_paint]',
        ],
```

- [ ] **Step 4: Update CVA expanded variant**

Replace the `expanded` variant (lines 143-149):

```typescript
        expanded: [
          'flex flex-col rounded-2xl overflow-hidden',
          'bg-[var(--nh-bg-surface)] border border-[var(--nh-border-default)]',
          'border-l-[3px] [border-left-color:var(--mc-entity-color,transparent)]',
          '[box-shadow:var(--shadow-warm-md)]',
        ],
```

- [ ] **Step 5: Keep hero variant parchment**

The `hero` variant (lines 136-142) keeps `[background-image:var(--texture-parchment)]`. No change.

- [ ] **Step 6: Verify build**

```bash
cd apps/web && pnpm build
```

- [ ] **Step 7: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card-styles.ts
git commit -m "feat(meeple-card): replace bg-card with warm tokens, remove parchment texture from grid/list/featured/expanded"
```

---

## Task 4: Fix MeepleCard Flicker — HoloOverlay Opt-in & Remove willChange

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card/types.ts`
- Modify: `apps/web/src/components/ui/data-display/meeple-card/variants/MeepleCardGrid.tsx:60-134,250-256,276`

- [ ] **Step 1: Add `showHolo` prop to types.ts**

In `apps/web/src/components/ui/data-display/meeple-card/types.ts`, find the main `MeepleCardProps` interface and add:

```typescript
  /** Opt-in holographic overlay effect. Default: false. Use for hero/featured cards. */
  showHolo?: boolean;
```

Add it near the other visual props (after `glowState` or similar).

- [ ] **Step 2: Destructure `showHolo` in MeepleCardGrid.tsx**

In `MeepleCardGrid.tsx`, add `showHolo = false` to the destructuring block (after line ~134, before `const variant`):

```typescript
    showHolo = false,
```

Add it after `subtypeIcons,` at line ~134.

- [ ] **Step 3: Make HoloOverlay conditional**

In `MeepleCardGrid.tsx` line ~276, change:

```diff
-      <HoloOverlay />
+      {showHolo && <HoloOverlay />}
```

- [ ] **Step 4: Remove static willChange**

In `MeepleCardGrid.tsx` lines 250-256, remove the `willChange` property from the inline style:

```typescript
      style={
        {
          '--mc-entity-color': `hsl(${color})`,
          outlineColor: `hsla(${color}, 0.4)`,
          viewTransitionName: entityId ? `meeple-card-${entityId}` : undefined,
        } as React.CSSProperties
      }
```

- [ ] **Step 5: Verify build**

```bash
cd apps/web && pnpm build
```

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/types.ts apps/web/src/components/ui/data-display/meeple-card/variants/MeepleCardGrid.tsx
git commit -m "fix(meeple-card): make HoloOverlay opt-in, remove static willChange for performance"
```

---

## Task 5: Resize Metadata Icons to 20px

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card/variants/MeepleCardGrid.tsx:498-528`

- [ ] **Step 1: Update metadata item text classes**

In `MeepleCardGrid.tsx`, find the metadata footer section (~line 498-528). Update the `className` for the metadata item containers (both `<button>` and `<span>` variants):

```diff
- "flex items-center gap-1.5 text-[0.7rem] font-semibold text-foreground/65 dark:text-foreground/60"
+ "flex items-center gap-2 text-[0.78rem] font-semibold text-foreground/70 dark:text-[rgba(200,180,140,0.75)]"
```

Apply this to BOTH the `button` version (line ~510) and `span` version (line ~521).

- [ ] **Step 2: Update icon size in metadata items**

In the same section, update the icon rendering:

```diff
- {item.icon && <item.icon className="w-3.5 h-3.5" aria-hidden="true" />}
+ {item.icon && <item.icon className="w-5 h-5 text-[hsl(25,80%,55%)] opacity-[0.85]" aria-hidden="true" />}
```

Apply to BOTH the `button` and `span` versions.

- [ ] **Step 3: Verify build**

```bash
cd apps/web && pnpm build
```

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/variants/MeepleCardGrid.tsx
git commit -m "feat(meeple-card): resize metadata icons to 20px with warm orange accent"
```

---

## Task 6: Add `small` Mana Size & Bump `mini` to 24px

**Files:**
- Modify: `apps/web/src/components/ui/data-display/mana/mana-types.ts`
- Modify: `apps/web/src/components/ui/data-display/mana/ManaSymbol.tsx:10-20`

- [ ] **Step 1: Add `small` to ManaSize type**

In `mana-types.ts`, update line 3:

```diff
- export type ManaSize = 'full' | 'medium' | 'mini';
+ export type ManaSize = 'full' | 'medium' | 'small' | 'mini';
```

- [ ] **Step 2: Update SIZE_CLASSES in ManaSymbol.tsx**

In `ManaSymbol.tsx`, update SIZE_CLASSES (lines 10-14):

```typescript
const SIZE_CLASSES: Record<ManaSize, string> = {
  full: 'w-16 h-16 text-[1.6rem]',
  medium: 'w-7 h-7 text-sm',
  small: 'w-5 h-5 text-xs',
  mini: 'w-6 h-6 text-sm',
};
```

- [ ] **Step 3: Update LABEL_CLASSES in ManaSymbol.tsx**

Update LABEL_CLASSES (lines 16-20):

```typescript
const LABEL_CLASSES: Record<ManaSize, string> = {
  full: 'text-xs mt-2',
  medium: 'text-[9px] ml-1.5',
  small: 'text-[8px] ml-1',
  mini: 'text-[8px] ml-1',
};
```

- [ ] **Step 4: Update ManaSymbol glow boxShadow**

In `ManaSymbol.tsx` line ~67, update the boxShadow value:

```diff
- boxShadow: `0 4px 16px hsl(${color} / 0.35)`,
+ boxShadow: `0 4px 14px hsl(${color} / 0.4)`,
```

- [ ] **Step 5: Verify build**

```bash
cd apps/web && pnpm build
```

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/ui/data-display/mana/mana-types.ts apps/web/src/components/ui/data-display/mana/ManaSymbol.tsx
git commit -m "feat(mana): add small size (20px), bump mini to 24px, enhance glow"
```

---

## Task 7: Update CoverOverlay — Entity Pip Size & Subtype Icons

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card/parts/CoverOverlay.tsx:54,81-88,98-113`

- [ ] **Step 1: Change entity pip to `small` size**

In `CoverOverlay.tsx`, line ~83, change:

```diff
-          <ManaSymbol
-            entity={entity}
-            size="mini"
+          <ManaSymbol
+            entity={entity}
+            size="small"
```

- [ ] **Step 2: Update label container max-width**

In `CoverOverlay.tsx`, line ~54, update the max-width guard:

```diff
- 'max-w-[calc(100%-50px)]'
+ 'max-w-[calc(100%-52px)]'
```

- [ ] **Step 3: Update subtype icon size and styling**

In `CoverOverlay.tsx`, lines ~101-107, change:

```diff
-                'w-6 h-6 rounded-md',
-                'backdrop-blur-[8px] bg-black/45',
-                'border border-white/[0.12]',
+                'w-7 h-7 rounded-md',
+                'backdrop-blur-[8px] bg-[rgba(20,18,14,0.7)]',
+                'border border-[rgba(200,160,100,0.2)]',
```

- [ ] **Step 4: Verify build**

```bash
cd apps/web && pnpm build
```

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/parts/CoverOverlay.tsx
git commit -m "feat(cover-overlay): entity pip small size, subtype icons 28px with warm borders"
```

---

## Task 8: Replace Tab Bar with Dropdown

**Files:**
- Delete: `apps/web/src/components/library/LibraryContextBar.tsx`
- Modify: `apps/web/src/app/(authenticated)/library/layout.tsx`
- Modify: `apps/web/src/app/(authenticated)/library/CollectionPageClient.tsx:44-51,335-360`

- [ ] **Step 1: Simplify library layout**

Replace `apps/web/src/app/(authenticated)/library/layout.tsx` entirely:

```tsx
/**
 * Library Section Layout
 * Simplified: ContextBar removed, filters inline in page.
 */

import { type ReactNode } from 'react';

export default function LibraryLayout({ children }: { children: ReactNode }) {
  return <>{children}</>;
}
```

- [ ] **Step 2: Add quick filter dropdown to CollectionPageClient**

In `apps/web/src/app/(authenticated)/library/CollectionPageClient.tsx`, add import at the top (after existing imports):

```typescript
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
```

- [ ] **Step 3: Add quick filter state and handler**

After the `viewMode` state (~line 56), add:

```typescript
  // Quick filter dropdown (replaces ContextBar tab pills)
  const quickFilterValue = useMemo(() => {
    const sf = filters.stateFilter;
    if (!sf || sf.length === 0) return 'all';
    if (sf.includes('Owned' as GameStateType)) return 'owned';
    if (sf.includes('Wishlist' as GameStateType)) return 'wishlist';
    if (sf.includes('InPrestito' as GameStateType)) return 'loaned';
    return 'all';
  }, [filters.stateFilter]);

  const handleQuickFilter = (value: string) => {
    const stateMap: Record<string, GameStateType[]> = {
      all: [],
      owned: ['Owned' as GameStateType],
      wishlist: ['Wishlist' as GameStateType],
      loaned: ['InPrestito' as GameStateType],
    };
    handleStateFilterChange(stateMap[value] ?? []);
  };
```

- [ ] **Step 4: Add dropdown to page header**

In `CollectionPageClient.tsx`, find the header section (~line 335-360, the `<div>` with the title). Add the Select dropdown after the title `<h2>`:

```tsx
          <Select value={quickFilterValue} onValueChange={handleQuickFilter}>
            <SelectTrigger className="w-[130px] h-8 text-sm">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">Tutti</SelectItem>
              <SelectItem value="owned">Posseduti</SelectItem>
              <SelectItem value="wishlist">Wishlist</SelectItem>
              <SelectItem value="loaned">Prestati</SelectItem>
            </SelectContent>
          </Select>
```

- [ ] **Step 5: Delete LibraryContextBar.tsx**

**IMPORTANT**: Step 1 (layout replacement) MUST be completed before this step. The old layout imports `LibraryContextBar` directly — deleting the file before replacing the layout will break the build.

```bash
rm apps/web/src/components/library/LibraryContextBar.tsx
```

Note: `LibraryContextBar` is NOT in the library barrel export (`index.ts`), so no barrel changes needed.

- [ ] **Step 6: Verify build**

```bash
cd apps/web && pnpm build
```

- [ ] **Step 8: Run existing LibraryFilters tests**

```bash
cd apps/web && pnpm test -- --run LibraryFilters
```

Expected: All existing filter tests pass (LibraryFilters component is untouched).

- [ ] **Step 9: Commit**

```bash
git add -A
git commit -m "feat(library): replace ContextBar tab pills with inline dropdown filter"
```

---

## Task 9: Meeple Mana SVG Icons — Types & Config

**Files:**
- Modify: `apps/web/src/components/ui/data-display/mana/mana-types.ts`
- Create: `apps/web/src/components/icons/entities/icons/GameIcon.tsx` (and 15 more)
- Create: `apps/web/src/components/icons/entities/EntityIcon.tsx`
- Create: `apps/web/src/components/icons/entities/index.ts`

- [ ] **Step 1: Update ManaDisplayConfig type**

In `mana-types.ts`, add Icon field:

Add the `Icon` field and `ComponentType` import to the existing file. Do NOT replace the whole file — only add the new import and field:

```diff
+ import type { ComponentType } from 'react';
  import type { MeepleEntityType } from '../meeple-card-styles';

  export type ManaSize = 'full' | 'medium' | 'small' | 'mini';

  export interface ManaDisplayConfig {
    key: MeepleEntityType;
    displayName: string;
    symbol: string;
    tier: 'core' | 'social' | 'ai' | 'tools';
+   Icon?: ComponentType<{ size?: number; className?: string }>;
  }

  export type EntityRelationshipMap = Record<MeepleEntityType, MeepleEntityType[]>;
```

**IMPORTANT**: Keep `tier: 'core' | 'social' | 'ai' | 'tools'` as the union literal — do NOT widen to `string`. The `ManaSize` type should already have `'small'` from Task 6.

- [ ] **Step 2: Create icon directory and copy reference asset**

```bash
mkdir -p apps/web/src/components/icons/entities/icons
mkdir -p apps/web/src/assets/design
cp "D:/Repositories/meepleai-monorepo-dev/data/design/meeplemana1_2.png" apps/web/src/assets/design/
```

- [ ] **Step 3: Create all 16 entity icon components**

Create each icon file in `apps/web/src/components/icons/entities/icons/`. Each follows this pattern (example for GameIcon):

```tsx
// apps/web/src/components/icons/entities/icons/GameIcon.tsx
interface IconProps {
  size?: number;
  className?: string;
}

export function GameIcon({ size = 24, className = '' }: IconProps) {
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="currentColor"
      className={className}
      role="img"
      aria-label="Game"
    >
      <path d="M17.5 3a2.5 2.5 0 0 1 2.45 2h.05a2 2 0 0 1 2 2v10a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V7a2 2 0 0 1 2-2h.05A2.5 2.5 0 0 1 6.5 3h11ZM12 8a1 1 0 1 0 0 2 1 1 0 0 0 0-2Zm-4 0a1 1 0 1 0 0 2 1 1 0 0 0 0-2Zm8 0a1 1 0 1 0 0 2 1 1 0 0 0 0-2Zm-4 4a1 1 0 1 0 0 2 1 1 0 0 0 0-2Zm-4 0a1 1 0 1 0 0 2 1 1 0 0 0 0-2Zm8 0a1 1 0 1 0 0 2 1 1 0 0 0 0-2Z"/>
    </svg>
  );
}
```

Create all 16: `GameIcon`, `SessionIcon`, `PlayerIcon`, `EventIcon`, `CollectionIcon`, `GroupIcon`, `LocationIcon`, `ExpansionIcon`, `AgentIcon`, `KnowledgeIcon`, `ChatIcon`, `NoteIcon`, `ToolkitIcon`, `ToolIcon`, `AchievementIcon`, `CustomIcon`.

**Note**: SVG paths should be traced from the Meeple Mana design assets. Use simple, recognizable shapes that work at 16-32px. Start with clean geometric paths — they can be refined later when final vector assets are delivered.

- [ ] **Step 4: Create EntityIcon router**

Create `apps/web/src/components/icons/entities/EntityIcon.tsx`:

```tsx
import type { MeepleEntityType } from '@/components/ui/data-display/meeple-card-styles';
import { GameIcon } from './icons/GameIcon';
import { SessionIcon } from './icons/SessionIcon';
import { PlayerIcon } from './icons/PlayerIcon';
import { EventIcon } from './icons/EventIcon';
import { CollectionIcon } from './icons/CollectionIcon';
import { GroupIcon } from './icons/GroupIcon';
import { LocationIcon } from './icons/LocationIcon';
import { ExpansionIcon } from './icons/ExpansionIcon';
import { AgentIcon } from './icons/AgentIcon';
import { KnowledgeIcon } from './icons/KnowledgeIcon';
import { ChatIcon } from './icons/ChatIcon';
import { NoteIcon } from './icons/NoteIcon';
import { ToolkitIcon } from './icons/ToolkitIcon';
import { ToolIcon } from './icons/ToolIcon';
import { AchievementIcon } from './icons/AchievementIcon';
import { CustomIcon } from './icons/CustomIcon';

import type { ComponentType } from 'react';

interface IconProps {
  size?: number;
  className?: string;
}

const ENTITY_ICONS: Record<MeepleEntityType, ComponentType<IconProps>> = {
  game: GameIcon,
  session: SessionIcon,
  player: PlayerIcon,
  event: EventIcon,
  collection: CollectionIcon,
  group: GroupIcon,
  location: LocationIcon,
  expansion: ExpansionIcon,
  agent: AgentIcon,
  kb: KnowledgeIcon,
  chatSession: ChatIcon,
  note: NoteIcon,
  toolkit: ToolkitIcon,
  tool: ToolIcon,
  achievement: AchievementIcon,
  custom: CustomIcon,
};

interface EntityIconProps {
  entity: MeepleEntityType;
  size?: number;
  className?: string;
}

export function EntityIcon({ entity, size = 24, className }: EntityIconProps) {
  const IconComponent = ENTITY_ICONS[entity] ?? CustomIcon;
  return <IconComponent size={size} className={className} />;
}
```

- [ ] **Step 5: Create barrel export**

Create `apps/web/src/components/icons/entities/index.ts`:

```typescript
export { EntityIcon } from './EntityIcon';
export { GameIcon } from './icons/GameIcon';
export { SessionIcon } from './icons/SessionIcon';
export { PlayerIcon } from './icons/PlayerIcon';
export { EventIcon } from './icons/EventIcon';
export { CollectionIcon } from './icons/CollectionIcon';
export { GroupIcon } from './icons/GroupIcon';
export { LocationIcon } from './icons/LocationIcon';
export { ExpansionIcon } from './icons/ExpansionIcon';
export { AgentIcon } from './icons/AgentIcon';
export { KnowledgeIcon } from './icons/KnowledgeIcon';
export { ChatIcon } from './icons/ChatIcon';
export { NoteIcon } from './icons/NoteIcon';
export { ToolkitIcon } from './icons/ToolkitIcon';
export { ToolIcon } from './icons/ToolIcon';
export { AchievementIcon } from './icons/AchievementIcon';
export { CustomIcon } from './icons/CustomIcon';
```

- [ ] **Step 6: Verify build**

```bash
cd apps/web && pnpm build
```

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat(icons): create 16 Meeple Mana entity SVG icon components"
```

---

## Task 10: Integrate SVG Icons into ManaSymbol

**Files:**
- Modify: `apps/web/src/components/ui/data-display/mana/mana-config.ts`
- Modify: `apps/web/src/components/ui/data-display/mana/ManaSymbol.tsx`

- [ ] **Step 1: Add Icon references to mana-config.ts**

Add imports at top of `mana-config.ts`:

```typescript
import {
  GameIcon, SessionIcon, PlayerIcon, EventIcon,
  CollectionIcon, GroupIcon, LocationIcon, ExpansionIcon,
  AgentIcon, KnowledgeIcon, ChatIcon, NoteIcon,
  ToolkitIcon, ToolIcon, AchievementIcon, CustomIcon,
} from '@/components/icons/entities';
```

Update each entry to add `Icon` field:

```typescript
export const MANA_DISPLAY: Record<MeepleEntityType, ManaDisplayConfig> = {
  game: { key: 'game', displayName: 'Game', symbol: '🎲', tier: 'core', Icon: GameIcon },
  session: { key: 'session', displayName: 'Session', symbol: '⏳', tier: 'core', Icon: SessionIcon },
  player: { key: 'player', displayName: 'Player', symbol: '♟', tier: 'core', Icon: PlayerIcon },
  event: { key: 'event', displayName: 'Event', symbol: '✦', tier: 'core', Icon: EventIcon },
  collection: { key: 'collection', displayName: 'Collection', symbol: '📦', tier: 'social', Icon: CollectionIcon },
  group: { key: 'group', displayName: 'Group', symbol: '👥', tier: 'social', Icon: GroupIcon },
  location: { key: 'location', displayName: 'Location', symbol: '📍', tier: 'social', Icon: LocationIcon },
  expansion: { key: 'expansion', displayName: 'Expansion', symbol: '🃏', tier: 'social', Icon: ExpansionIcon },
  agent: { key: 'agent', displayName: 'Agent', symbol: '⚡', tier: 'ai', Icon: AgentIcon },
  kb: { key: 'kb', displayName: 'Knowledge', symbol: '📜', tier: 'ai', Icon: KnowledgeIcon },
  chatSession: { key: 'chatSession', displayName: 'Chat', symbol: '💬', tier: 'ai', Icon: ChatIcon },
  note: { key: 'note', displayName: 'Note', symbol: '📝', tier: 'ai', Icon: NoteIcon },
  toolkit: { key: 'toolkit', displayName: 'Toolkit', symbol: '⚙', tier: 'tools', Icon: ToolkitIcon },
  tool: { key: 'tool', displayName: 'Tool', symbol: '🔧', tier: 'tools', Icon: ToolIcon },
  achievement: { key: 'achievement', displayName: 'Achievement', symbol: '🏆', tier: 'tools', Icon: AchievementIcon },
  custom: { key: 'custom', displayName: 'Custom', symbol: '✧', tier: 'tools', Icon: CustomIcon },
};
```

- [ ] **Step 2: Update ManaSymbol to render SVG when available**

In `ManaSymbol.tsx`, find the symbol rendering (line ~77-79):

```diff
-        <span className="relative z-[1] drop-shadow-[0_2px_4px_rgba(0,0,0,0.5)]">
-          {config.symbol}
-        </span>
+        <span className="relative z-[1] drop-shadow-[0_2px_4px_rgba(0,0,0,0.5)]">
+          {config.Icon
+            ? <config.Icon size={size === 'full' ? 28 : size === 'medium' ? 16 : size === 'small' ? 12 : 14} className="text-white" />
+            : config.symbol}
+        </span>
```

**Note**: The size mapping is: `full` → 28px, `medium` → 16px, `small` → 12px, `mini` → 14px (default). This ensures icons are proportional within their container at each ManaSize.

- [ ] **Step 3: Verify build**

```bash
cd apps/web && pnpm build
```

- [ ] **Step 4: Run existing mana tests if any**

```bash
cd apps/web && pnpm test -- --run mana
```

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat(mana): integrate SVG entity icons into ManaSymbol, emoji fallback preserved"
```

---

## Task 11: Final Verification & PR

**Files:** None (verification only)

- [ ] **Step 1: Full build check**

```bash
cd apps/web && pnpm build
```

- [ ] **Step 2: Run all frontend tests**

```bash
cd apps/web && pnpm test -- --run
```

- [ ] **Step 3: Type check**

```bash
cd apps/web && pnpm typecheck
```

- [ ] **Step 4: Lint**

```bash
cd apps/web && pnpm lint
```

- [ ] **Step 5: Visual spot-check**

Start dev server and manually verify:
- Library page loads, dropdown filter works
- MeepleCard grid: no flicker, warm backgrounds
- Metadata icons: 20px orange
- Mana pips: 24px vibrant in footer
- 4-corner overlay: pip top-right, labels top-left, subtypes bottom-left, state bottom-right
- Dark and light mode both render warm

```bash
cd apps/web && pnpm dev
```

- [ ] **Step 6: Create PR**

```bash
git push -u origin feature/library-visual-redesign
gh pr create --title "feat(library): visual redesign — warm palette, card performance, icon upgrade" --base main-dev --body "$(cat <<'EOF'
## Summary
- Replace Neon Holo palette with Hybrid Warm-Modern (#14120e dark, #faf8f5 light)
- Fix MeepleCard flicker: remove HoloOverlay default, glassmorphism, static willChange, parchment texture
- Resize metadata icons 14px→20px with warm orange accent
- Bump mana pips 20px→24px, add small (20px) size for cover pip
- Replace LibraryContextBar tab pills with inline dropdown filter mapped to GameStateType API
- Create 16 Meeple Mana SVG entity icon components integrated into ManaSymbol

## Test plan
- [ ] Dark mode: warm-brown backgrounds, readable text
- [ ] Light mode: cream backgrounds, readable text
- [ ] MeepleCard grid: no flicker scrolling 20+ cards
- [ ] Library dropdown filter: Tutti/Posseduti/Wishlist/Prestati all work
- [ ] Metadata icons 20px, orange accent visible
- [ ] Mana pips 24px vibrant in footer
- [ ] 4-corner overlay: no overlap at 320px mobile
- [ ] All existing tests pass

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```
