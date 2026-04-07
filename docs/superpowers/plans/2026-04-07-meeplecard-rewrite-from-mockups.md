# MeepleCard Rewrite from Mockups — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rewrite the entire MeepleCard component system from the 6 HTML mockups in `admin-mockups/`, replacing all legacy code with a clean implementation that pixel-matches the mockups.

**Architecture:** Delete-and-rebuild approach. Create feature branch, remove all legacy MeepleCard code, build new components from mockup CSS as source of truth, update all ~161 consumer files, add mobile hand+focused layout and dark theme.

**Tech Stack:** React 19, TypeScript, Tailwind 4, CSS custom properties, Next.js App Router

**Build Note:** The branch will NOT compile between Task 1 (deletion) and Task 14 (router + public exports). This is expected — all intermediate commits are stepping stones. Full compilation is restored at Task 15.

**Font Variables:** Quicksand and Nunito are already loaded in `layout.tsx` as `--font-quicksand` and `--font-nunito`. All components use these CSS variables.

**Theme System:** next-themes is already configured with `attribute="class"` (adds `.dark` to `<html>`). Dark theme CSS uses `.dark` selector, NOT `.dark`.

**Spec:** `docs/superpowers/specs/2026-04-07-meeplecard-rewrite-from-mockups-design.md`

**Mockup Source Files:**
- `admin-mockups/meeple-card-nav-buttons-mockup.html` — NavFooter states
- `admin-mockups/meeple-card-real-app-render.html` — Production card renders (14 adapters)
- `admin-mockups/meeple-card-summary-render.html` — Variant reference
- `admin-mockups/meeple-card-visual-test.html` — Full features + 3D carousel
- `admin-mockups/mobile-card-entity-types.html` — 7 entity types mobile
- `admin-mockups/mobile-card-layout-mockup.html` — Mobile architecture

---

## Phase 1: Foundation

### Task 1: Create Feature Branch and Delete Legacy Code

**Files:**
- Delete: `apps/web/src/components/ui/data-display/meeple-card/` (entire directory)
- Delete: `apps/web/src/components/ui/data-display/meeple-card-features/` (entire directory)
- Delete: `apps/web/src/components/ui/data-display/meeple-card-styles.ts`
- Delete: `apps/web/src/components/ui/data-display/meeple-card-compound.tsx`
- Delete: `apps/web/src/components/ui/data-display/meeple-card-mobile-tags.tsx`
- Delete: `apps/web/src/components/ui/data-display/meeple-card-info-button.tsx`
- Delete: `apps/web/src/components/ui/data-display/meeple-card-parts.tsx`
- Delete: `apps/web/src/components/ui/data-display/meeple-card-quick-actions.tsx`
- Delete: `apps/web/src/components/ui/data-display/meeple-card.stories.tsx`
- Delete: `apps/web/src/components/ui/data-display/meeple-card-browser/` (entire directory)
- Delete: `apps/web/src/components/ui/data-display/card-back-blocks/` (entire directory)
- Delete: `apps/web/src/components/ui/data-display/holo/` (entire directory)

- [ ] **Step 1: Create feature branch from main-dev**

```bash
cd D:/Repositories/meepleai-monorepo-frontend
git checkout main-dev && git pull
git checkout -b feature/meeplecard-rewrite
git config branch.feature/meeplecard-rewrite.parent main-dev
```

- [ ] **Step 2: Delete all legacy MeepleCard directories and files**

```bash
rm -rf apps/web/src/components/ui/data-display/meeple-card
rm -rf apps/web/src/components/ui/data-display/meeple-card-features
rm -rf apps/web/src/components/ui/data-display/meeple-card-browser
rm -rf apps/web/src/components/ui/data-display/card-back-blocks
rm -rf apps/web/src/components/ui/data-display/holo
rm -f apps/web/src/components/ui/data-display/meeple-card-styles.ts
rm -f apps/web/src/components/ui/data-display/meeple-card-compound.tsx
rm -f apps/web/src/components/ui/data-display/meeple-card-mobile-tags.tsx
rm -f apps/web/src/components/ui/data-display/meeple-card-info-button.tsx
rm -f apps/web/src/components/ui/data-display/meeple-card-parts.tsx
rm -f apps/web/src/components/ui/data-display/meeple-card-quick-actions.tsx
rm -f apps/web/src/components/ui/data-display/meeple-card.stories.tsx
```

- [ ] **Step 3: Commit deletion**

```bash
git add -A
git commit -m "chore: delete legacy MeepleCard system for rewrite

Removes all legacy components, features, styles, compound API,
mobile tags, card browser, card-back blocks, holo overlay,
info button, parts, quick actions, and stories.
Will be rebuilt from admin-mockups."
```

---

### Task 2: Create Type Definitions

**Files:**
- Create: `apps/web/src/components/ui/data-display/meeple-card/types.ts`

- [ ] **Step 1: Write types.ts with all type definitions**

```typescript
// apps/web/src/components/ui/data-display/meeple-card/types.ts

import type { ReactNode } from 'react';

// ── Entity Types (9 only) ──────────────────────────────────────
export type MeepleEntityType =
  | 'game'
  | 'player'
  | 'session'
  | 'agent'
  | 'kb'
  | 'chat'
  | 'event'
  | 'toolkit'
  | 'tool';

// ── Card Variants (5 only) ─────────────────────────────────────
export type MeepleCardVariant = 'grid' | 'list' | 'compact' | 'featured' | 'hero';

// ── Metadata ───────────────────────────────────────────────────
export interface MeepleCardMetadata {
  icon?: string;
  label: string;
  value?: string;
}

// ── Actions ────────────────────────────────────────────────────
export interface MeepleCardAction {
  icon: string;
  label: string;
  onClick: () => void;
  variant?: 'default' | 'primary' | 'danger';
  disabled?: boolean;
}

// ── NavFooter Items ────────────────────────────────────────────
export interface NavFooterItem {
  icon: string;
  label: string;
  entity: MeepleEntityType;
  count?: number;
  showPlus?: boolean;
  disabled?: boolean;
  onClick?: () => void;
}

// ── Status Badge ───────────────────────────────────────────────
export type CardStatus =
  | 'owned'
  | 'wishlist'
  | 'active'
  | 'idle'
  | 'archived'
  | 'processing'
  | 'indexed'
  | 'failed'
  | 'inprogress'
  | 'setup'
  | 'completed'
  | 'paused';

// ── Cover Label (top-left overlay) ─────────────────────────────
export interface CoverLabel {
  text: string;
  color?: string;
}

// ── Main Props ─────────────────────────────────────────────────
export interface MeepleCardProps {
  // Required
  entity: MeepleEntityType;
  title: string;

  // Layout
  variant?: MeepleCardVariant;

  // Content
  id?: string;
  subtitle?: string;
  imageUrl?: string;
  rating?: number;
  ratingMax?: number;
  metadata?: MeepleCardMetadata[];
  tags?: string[];
  status?: CardStatus;
  badge?: string;
  coverLabels?: CoverLabel[];

  // Actions
  actions?: MeepleCardAction[];
  navItems?: NavFooterItem[];
  onClick?: () => void;

  // Features (opt-in)
  flippable?: boolean;
  flipBackContent?: ReactNode;
  flipTrigger?: 'card' | 'button';
  draggable?: boolean;
  showQuickActions?: boolean;

  // Callbacks
  onWishlistToggle?: () => void;
  onDragStart?: () => void;
  onDragEnd?: () => void;

  // Styling
  className?: string;
  customColor?: string; // HSL override e.g. "200 80% 50%"
}

// ── Mobile Layout Props ────────────────────────────────────────
export interface MobileCardLayoutProps {
  cards: MeepleCardProps[];
  activeId?: string;
  onCardSelect?: (id: string) => void;
  className?: string;
}

// ── Carousel Props ─────────────────────────────────────────────
export interface Carousel3DProps {
  cards: MeepleCardProps[];
  activeIndex?: number;
  onNavigate?: (index: number) => void;
  className?: string;
}
```

- [ ] **Step 2: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/types.ts
git commit -m "feat(meeple-card): add type definitions for rewrite

9 entity types, 5 variants, all prop interfaces including
mobile layout and carousel."
```

---

### Task 3: Create Design Tokens

**Files:**
- Create: `apps/web/src/components/ui/data-display/meeple-card/tokens.ts`
- Modify: `apps/web/src/styles/design-tokens.css`

- [ ] **Step 1: Write tokens.ts — entity colors, variant dimensions, helpers**

```typescript
// apps/web/src/components/ui/data-display/meeple-card/tokens.ts

import type { MeepleEntityType, MeepleCardVariant } from './types';

// ── Entity Color Palette (HSL) ─────────────────────────────────
// Source: admin-mockups CSS :root variables
export const entityColors: Record<MeepleEntityType, { h: number; s: string; l: string }> = {
  game:    { h: 25,  s: '95%',  l: '45%' },
  player:  { h: 262, s: '83%',  l: '58%' },
  session: { h: 240, s: '60%',  l: '55%' },
  agent:   { h: 38,  s: '92%',  l: '50%' },
  kb:      { h: 210, s: '40%',  l: '55%' },
  chat:    { h: 220, s: '80%',  l: '55%' },
  event:   { h: 350, s: '89%',  l: '60%' },
  toolkit: { h: 142, s: '70%',  l: '45%' },
  tool:    { h: 195, s: '80%',  l: '50%' },
};

// ── Helper: Get HSL string ─────────────────────────────────────
export function entityHsl(entity: MeepleEntityType, alpha?: number): string {
  const c = entityColors[entity];
  if (alpha !== undefined) {
    return `hsla(${c.h}, ${c.s}, ${c.l}, ${alpha})`;
  }
  return `hsl(${c.h}, ${c.s}, ${c.l})`;
}

// ── Entity Labels ──────────────────────────────────────────────
export const entityLabel: Record<MeepleEntityType, string> = {
  game: 'Game',
  player: 'Player',
  session: 'Session',
  agent: 'Agent',
  kb: 'KB',
  chat: 'Chat',
  event: 'Event',
  toolkit: 'Toolkit',
  tool: 'Tool',
};

// ── Entity Icons (Emoji) ───────────────────────────────────────
export const entityIcon: Record<MeepleEntityType, string> = {
  game: '🎲',
  player: '👤',
  session: '🎯',
  agent: '🤖',
  kb: '📚',
  chat: '💬',
  event: '📅',
  toolkit: '🧰',
  tool: '🔧',
};

// ── Variant Dimensions ─────────────────────────────────────────
export const variantDimensions: Record<MeepleCardVariant, { width: number; height: number }> = {
  grid:     { width: 270, height: 378 },   // 7:10
  list:     { width: 0,   height: 72 },    // full-width, fixed height
  compact:  { width: 160, height: 48 },    // minimal
  featured: { width: 400, height: 225 },   // cover 16:9
  hero:     { width: 360, height: 320 },   // min-height
};

// ── Cover Aspect Ratios ────────────────────────────────────────
export const coverAspectRatio: Record<MeepleCardVariant, string> = {
  grid: '7 / 10',
  list: '1 / 1',
  compact: '1 / 1',
  featured: '16 / 9',
  hero: '16 / 9',
};

// ── Status Badge Colors ────────────────────────────────────────
export const statusColors: Record<string, { bg: string; text: string }> = {
  owned:      { bg: '#dcfce7', text: '#166534' },
  wishlist:   { bg: '#fef3c7', text: '#92400e' },
  active:     { bg: '#dcfce7', text: '#166534' },
  idle:       { bg: '#f1f5f9', text: '#64748b' },
  archived:   { bg: '#f1f5f9', text: '#64748b' },
  processing: { bg: '#dbeafe', text: '#1e40af' },
  indexed:    { bg: '#dcfce7', text: '#166534' },
  failed:     { bg: '#fee2e2', text: '#991b1b' },
  inprogress: { bg: '#dbeafe', text: '#1e40af' },
  setup:      { bg: '#fef3c7', text: '#92400e' },
  completed:  { bg: '#f3e8ff', text: '#6b21a8' },
  paused:     { bg: '#fef3c7', text: '#92400e' },
};
```

- [ ] **Step 2: Add MeepleCard CSS custom properties to design-tokens.css**

Read `apps/web/src/styles/design-tokens.css`, then append MeepleCard tokens inside the existing `:root` block. Add the following CSS variables (find the end of the existing `:root` block and add before the closing brace):

```css
  /* ── MeepleCard Design Tokens ─────────────────────────── */
  /* Source: admin-mockups/*.html */

  /* Surfaces */
  --mc-bg-page: #faf8f5;
  --mc-bg-card: rgba(255, 255, 255, 0.80);
  --mc-bg-card-hover: rgba(255, 255, 255, 0.92);
  --mc-bg-muted: rgba(245, 240, 235, 0.60);
  --mc-bg-muted-dark: rgba(245, 240, 235, 0.40);

  /* Borders */
  --mc-border: rgba(200, 180, 160, 0.25);
  --mc-border-light: rgba(200, 180, 160, 0.15);

  /* Text */
  --mc-text-primary: #1a1612;
  --mc-text-secondary: rgba(26, 22, 18, 0.65);
  --mc-text-muted: rgba(26, 22, 18, 0.45);

  /* Shadows (warm brown-tinted) */
  --mc-shadow-sm: 0 1px 3px rgba(180, 130, 80, 0.06), 0 1px 2px rgba(180, 130, 80, 0.04);
  --mc-shadow-md: 0 4px 12px rgba(180, 130, 80, 0.08), 0 2px 4px rgba(180, 130, 80, 0.04);
  --mc-shadow-lg: 0 10px 30px rgba(180, 130, 80, 0.12), 0 4px 8px rgba(180, 130, 80, 0.06);
  --mc-shadow-xl: 0 20px 50px rgba(180, 130, 80, 0.16), 0 8px 16px rgba(180, 130, 80, 0.08);
  --mc-shadow-2xl: 0 25px 60px rgba(180, 130, 80, 0.20), 0 12px 24px rgba(180, 130, 80, 0.10);

  /* Nav Footer */
  --mc-nav-footer-bg: rgba(245, 240, 235, 0.28);
  --mc-nav-icon-bg: rgba(255, 255, 255, 0.60);
  --mc-nav-icon-border: rgba(200, 180, 160, 0.30);

  /* Entity Colors (HSL components) */
  --e-game-h: 25;    --e-game-s: 95%;    --e-game-l: 45%;
  --e-player-h: 262;  --e-player-s: 83%;   --e-player-l: 58%;
  --e-session-h: 240;  --e-session-s: 60%;  --e-session-l: 55%;
  --e-agent-h: 38;    --e-agent-s: 92%;    --e-agent-l: 50%;
  --e-kb-h: 210;      --e-kb-s: 40%;       --e-kb-l: 55%;
  --e-chat-h: 220;    --e-chat-s: 80%;     --e-chat-l: 55%;
  --e-event-h: 350;   --e-event-s: 89%;    --e-event-l: 60%;
  --e-toolkit-h: 142;  --e-toolkit-s: 70%;  --e-toolkit-l: 45%;
  --e-tool-h: 195;    --e-tool-s: 80%;     --e-tool-l: 50%;
```

Also add the dark theme block. Find or create `.dark` and add:

```css
.dark {
  --mc-bg-page: #0f0d0b;
  --mc-bg-card: rgba(30, 27, 24, 0.80);
  --mc-bg-card-hover: rgba(40, 36, 32, 0.92);
  --mc-bg-muted: rgba(40, 36, 32, 0.60);
  --mc-bg-muted-dark: rgba(40, 36, 32, 0.40);
  --mc-border: rgba(100, 90, 75, 0.30);
  --mc-border-light: rgba(100, 90, 75, 0.20);
  --mc-text-primary: #f0ece8;
  --mc-text-secondary: rgba(240, 236, 232, 0.65);
  --mc-text-muted: rgba(240, 236, 232, 0.40);
  --mc-nav-footer-bg: rgba(40, 36, 32, 0.28);
  --mc-nav-icon-bg: rgba(40, 36, 32, 0.60);
  --mc-nav-icon-border: rgba(100, 90, 75, 0.30);
  --mc-shadow-sm: 0 1px 3px rgba(0, 0, 0, 0.15), 0 1px 2px rgba(0, 0, 0, 0.10);
  --mc-shadow-md: 0 4px 12px rgba(0, 0, 0, 0.20), 0 2px 4px rgba(0, 0, 0, 0.12);
  --mc-shadow-lg: 0 10px 30px rgba(0, 0, 0, 0.25), 0 4px 8px rgba(0, 0, 0, 0.15);
  --mc-shadow-xl: 0 20px 50px rgba(0, 0, 0, 0.30), 0 8px 16px rgba(0, 0, 0, 0.18);
  --mc-shadow-2xl: 0 25px 60px rgba(0, 0, 0, 0.35), 0 12px 24px rgba(0, 0, 0, 0.22);
}
```

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/tokens.ts apps/web/src/styles/design-tokens.css
git commit -m "feat(meeple-card): add design tokens and entity color system

Entity colors, variant dimensions, status badge colors in TS.
CSS custom properties for light/dark theme from mockup source."
```

---

## Phase 2: Parts (Stateless Building Blocks)

### Task 4: Build Cover Component

**Files:**
- Create: `apps/web/src/components/ui/data-display/meeple-card/parts/Cover.tsx`

**Mockup reference:** `meeple-card-real-app-render.html` lines 82-97, `meeple-card-visual-test.html` lines 118-142

- [ ] **Step 1: Write Cover.tsx**

```tsx
// apps/web/src/components/ui/data-display/meeple-card/parts/Cover.tsx
'use client';

import { entityHsl, entityIcon } from '../tokens';
import type { MeepleEntityType, MeepleCardVariant } from '../types';

interface CoverProps {
  entity: MeepleEntityType;
  variant: MeepleCardVariant;
  imageUrl?: string;
  alt?: string;
}

const aspectRatioClass: Record<MeepleCardVariant, string> = {
  grid: 'aspect-[7/10]',
  list: 'aspect-square',
  compact: 'aspect-square',
  featured: 'aspect-video',
  hero: 'aspect-video',
};

export function Cover({ entity, variant, imageUrl, alt }: CoverProps) {
  const gradientColor = entityHsl(entity, 0.15);

  return (
    <div className={`relative overflow-hidden ${aspectRatioClass[variant]}`}>
      {imageUrl ? (
        <img
          src={imageUrl}
          alt={alt ?? ''}
          className="h-full w-full object-cover transition-transform duration-500 ease-out group-hover:scale-[1.06]"
          loading="lazy"
        />
      ) : (
        <div
          className="flex h-full w-full items-center justify-center text-5xl opacity-50"
          style={{ background: entityHsl(entity, 0.08) }}
        >
          {entityIcon[entity]}
        </div>
      )}

      {/* Shimmer overlay */}
      <div
        className="pointer-events-none absolute inset-0 -translate-x-full transition-none group-hover:animate-[shimmer_0.8s_ease-out_forwards]"
        style={{
          background: 'linear-gradient(105deg, transparent 40%, rgba(255,255,255,0.25) 50%, transparent 60%)',
        }}
      />

      {/* Entity gradient overlay */}
      <div
        className="pointer-events-none absolute inset-0"
        style={{
          background: `linear-gradient(to top, ${gradientColor}, transparent 60%)`,
        }}
      />
    </div>
  );
}
```

- [ ] **Step 2: Add shimmer keyframe to tailwind config or global CSS**

In `apps/web/src/styles/design-tokens.css`, add at the end:

```css
@keyframes shimmer {
  to { transform: translateX(100%); }
}
```

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/parts/Cover.tsx apps/web/src/styles/design-tokens.css
git commit -m "feat(meeple-card): add Cover part with shimmer and entity gradient"
```

---

### Task 5: Build EntityBadge, AccentBorder, StatusBadge

**Files:**
- Create: `apps/web/src/components/ui/data-display/meeple-card/parts/EntityBadge.tsx`
- Create: `apps/web/src/components/ui/data-display/meeple-card/parts/AccentBorder.tsx`
- Create: `apps/web/src/components/ui/data-display/meeple-card/parts/StatusBadge.tsx`

**Mockup reference:** `meeple-card-real-app-render.html` lines 110-137

- [ ] **Step 1: Write EntityBadge.tsx**

```tsx
// apps/web/src/components/ui/data-display/meeple-card/parts/EntityBadge.tsx
import { entityHsl, entityLabel } from '../tokens';
import type { MeepleEntityType } from '../types';

interface EntityBadgeProps {
  entity: MeepleEntityType;
  className?: string;
}

export function EntityBadge({ entity, className = '' }: EntityBadgeProps) {
  return (
    <span
      className={`absolute left-2.5 top-2 z-10 rounded-md px-2 py-0.5 font-[var(--font-quicksand)] text-[9px] font-extrabold uppercase tracking-wide text-white shadow-sm ${className}`}
      style={{ background: entityHsl(entity) }}
    >
      {entityLabel[entity]}
    </span>
  );
}
```

- [ ] **Step 2: Write AccentBorder.tsx**

```tsx
// apps/web/src/components/ui/data-display/meeple-card/parts/AccentBorder.tsx
import { entityHsl } from '../tokens';
import type { MeepleEntityType } from '../types';

interface AccentBorderProps {
  entity: MeepleEntityType;
}

export function AccentBorder({ entity }: AccentBorderProps) {
  return (
    <div
      className="absolute bottom-0 left-0 top-0 z-[5] w-[3px] transition-[width] duration-200 group-hover:w-[5px]"
      style={{ background: entityHsl(entity) }}
    />
  );
}
```

- [ ] **Step 3: Write StatusBadge.tsx**

```tsx
// apps/web/src/components/ui/data-display/meeple-card/parts/StatusBadge.tsx
import { statusColors } from '../tokens';
import type { CardStatus } from '../types';

interface StatusBadgeProps {
  status: CardStatus;
  className?: string;
}

export function StatusBadge({ status, className = '' }: StatusBadgeProps) {
  const colors = statusColors[status];
  if (!colors) return null;

  return (
    <span
      className={`absolute left-2.5 top-7 z-10 rounded-[5px] px-[7px] py-[1px] text-[9px] font-bold ${className}`}
      style={{ background: colors.bg, color: colors.text }}
    >
      {status}
    </span>
  );
}
```

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/parts/
git commit -m "feat(meeple-card): add EntityBadge, AccentBorder, StatusBadge parts"
```

---

### Task 6: Build QuickActions, Rating, MetaChips

**Files:**
- Create: `apps/web/src/components/ui/data-display/meeple-card/parts/QuickActions.tsx`
- Create: `apps/web/src/components/ui/data-display/meeple-card/parts/Rating.tsx`
- Create: `apps/web/src/components/ui/data-display/meeple-card/parts/MetaChips.tsx`

**Mockup reference:** `meeple-card-visual-test.html` lines 121-137, `meeple-card-real-app-render.html` lines 105-120

- [ ] **Step 1: Write QuickActions.tsx**

```tsx
// apps/web/src/components/ui/data-display/meeple-card/parts/QuickActions.tsx
import type { MeepleCardAction } from '../types';

interface QuickActionsProps {
  actions: MeepleCardAction[];
}

export function QuickActions({ actions }: QuickActionsProps) {
  if (actions.length === 0) return null;

  return (
    <div className="absolute right-2 top-2 z-10 flex flex-col gap-1.5 opacity-0 transition-opacity duration-300 group-hover:opacity-100">
      {actions.map((action, i) => (
        <button
          key={i}
          onClick={(e) => {
            e.stopPropagation();
            action.onClick();
          }}
          disabled={action.disabled}
          title={action.label}
          className="flex h-[30px] w-[30px] items-center justify-center rounded-full border border-white/60 bg-white/85 text-sm backdrop-blur-lg transition-transform duration-300 hover:scale-110 hover:shadow-[var(--mc-shadow-md)] disabled:cursor-not-allowed disabled:opacity-45"
        >
          {action.icon}
        </button>
      ))}
    </div>
  );
}
```

- [ ] **Step 2: Write Rating.tsx**

```tsx
// apps/web/src/components/ui/data-display/meeple-card/parts/Rating.tsx

interface RatingProps {
  value: number;
  max?: number;
}

export function Rating({ value, max = 5 }: RatingProps) {
  const normalized = (value / max) * 5;
  const fullStars = Math.floor(normalized);
  const hasHalf = normalized - fullStars >= 0.25;
  const emptyStars = 5 - fullStars - (hasHalf ? 1 : 0);

  return (
    <div className="mt-0.5 flex items-center gap-0.5">
      {Array.from({ length: fullStars }, (_, i) => (
        <span key={`f${i}`} className="text-[0.78rem] text-amber-400">★</span>
      ))}
      {hasHalf && <span className="text-[0.78rem] text-amber-400/50">★</span>}
      {Array.from({ length: emptyStars }, (_, i) => (
        <span key={`e${i}`} className="text-[0.78rem] text-black/[0.12]">★</span>
      ))}
      <span className="ml-1 text-[0.7rem] font-semibold text-[var(--mc-text-secondary)]">
        {value.toFixed(1)}
      </span>
    </div>
  );
}
```

- [ ] **Step 3: Write MetaChips.tsx**

```tsx
// apps/web/src/components/ui/data-display/meeple-card/parts/MetaChips.tsx
import type { MeepleCardMetadata } from '../types';

interface MetaChipsProps {
  metadata: MeepleCardMetadata[];
}

export function MetaChips({ metadata }: MetaChipsProps) {
  if (metadata.length === 0) return null;

  return (
    <div className="mt-1 flex flex-wrap gap-1.5">
      {metadata.map((m, i) => (
        <span
          key={i}
          className="flex items-center gap-[3px] rounded-md bg-[var(--mc-bg-muted)] px-2 py-0.5 text-[10px] text-[var(--mc-text-secondary)]"
        >
          {m.icon && <span className="text-[11px]">{m.icon}</span>}
          {m.label}
          {m.value && <span className="font-semibold">{m.value}</span>}
        </span>
      ))}
    </div>
  );
}
```

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/parts/
git commit -m "feat(meeple-card): add QuickActions, Rating, MetaChips parts"
```

---

### Task 7: Build NavFooter and TagStrip

**Files:**
- Create: `apps/web/src/components/ui/data-display/meeple-card/parts/NavFooter.tsx`
- Create: `apps/web/src/components/ui/data-display/meeple-card/parts/TagStrip.tsx`
- Create: `apps/web/src/components/ui/data-display/meeple-card/parts/index.ts`

**Mockup reference:** `meeple-card-nav-buttons-mockup.html` (entire file — the primary NavFooter reference)

- [ ] **Step 1: Write NavFooter.tsx**

```tsx
// apps/web/src/components/ui/data-display/meeple-card/parts/NavFooter.tsx
'use client';

import { entityHsl } from '../tokens';
import type { NavFooterItem } from '../types';

interface NavFooterProps {
  items: NavFooterItem[];
  size?: 'sm' | 'md';
}

export function NavFooter({ items, size = 'sm' }: NavFooterProps) {
  if (items.length === 0) return null;

  const iconSize = size === 'md' ? 'h-8 w-8 text-[15px]' : 'h-7 w-7 text-[13px]';

  return (
    <div className="flex items-center justify-center gap-2 border-t border-[var(--mc-border-light)] bg-[var(--mc-nav-footer-bg)] px-2.5 py-1.5 backdrop-blur-lg">
      {items.map((item, i) => {
        const color = entityHsl(item.entity);
        const glowColor = entityHsl(item.entity, 0.15);
        const borderHover = entityHsl(item.entity, 0.4);

        return (
          <button
            key={i}
            onClick={(e) => {
              e.stopPropagation();
              item.onClick?.();
            }}
            disabled={item.disabled}
            className="group/nav relative flex flex-col items-center gap-0.5 disabled:cursor-not-allowed disabled:opacity-45"
            title={item.label}
          >
            {/* Icon circle */}
            <div
              className={`relative flex ${iconSize} items-center justify-center rounded-full border border-[var(--mc-nav-icon-border)] bg-[var(--mc-nav-icon-bg)] transition-all duration-200 group-hover/nav:scale-[1.08] group-hover/nav:border-[var(--nav-hover-border)] group-hover/nav:bg-[var(--nav-hover-bg)] group-hover/nav:shadow-[var(--nav-hover-shadow)] group-active/nav:scale-95 group-active/nav:shadow-[inset_0_1px_2px_rgba(0,0,0,0.1)]`}
              style={{
                '--nav-hover-border': borderHover,
                '--nav-hover-bg': entityHsl(item.entity, 0.08),
                '--nav-hover-shadow': `0 2px 8px ${glowColor}`,
              } as React.CSSProperties}
            >
              <span className="pointer-events-none">{item.icon}</span>

              {/* Count badge */}
              {item.count !== undefined && item.count > 0 && (
                <span
                  className="absolute -right-1 -top-1 flex h-4 min-w-4 items-center justify-center rounded-full px-[3px] text-[8px] font-bold text-white shadow-sm"
                  style={{ background: color }}
                >
                  {item.count > 99 ? '99+' : item.count}
                </span>
              )}

              {/* Plus overlay */}
              {item.showPlus && (
                <span
                  className="absolute -bottom-0.5 -right-0.5 flex h-3.5 w-3.5 items-center justify-center rounded-full text-[8px] font-extrabold text-white"
                  style={{ background: color }}
                >
                  +
                </span>
              )}
            </div>

            {/* Label */}
            <span className="text-[7px] font-semibold uppercase tracking-wide text-[var(--mc-text-muted)] transition-colors group-hover/nav:text-[var(--mc-text-secondary)]">
              {item.label}
            </span>
          </button>
        );
      })}
    </div>
  );
}
```

- [ ] **Step 2: Write TagStrip.tsx**

```tsx
// apps/web/src/components/ui/data-display/meeple-card/parts/TagStrip.tsx
import { entityHsl } from '../tokens';
import type { MeepleEntityType } from '../types';

interface TagStripProps {
  tags: string[];
  entity: MeepleEntityType;
  maxVisible?: number;
}

export function TagStrip({ tags, entity, maxVisible = 3 }: TagStripProps) {
  if (tags.length === 0) return null;

  const visible = tags.slice(0, maxVisible);
  const overflow = tags.length - maxVisible;

  return (
    <div className="absolute left-2 top-2 z-[9] flex flex-col gap-1">
      {visible.map((tag, i) => (
        <span
          key={i}
          className="rounded px-1.5 py-[1px] text-[9px] font-semibold"
          style={{
            background: entityHsl(entity, 0.12),
            color: entityHsl(entity),
          }}
        >
          {tag}
        </span>
      ))}
      {overflow > 0 && (
        <span className="text-[8px] font-bold text-[var(--mc-text-muted)]">
          +{overflow}
        </span>
      )}
    </div>
  );
}
```

- [ ] **Step 3: Write parts/index.ts**

```typescript
// apps/web/src/components/ui/data-display/meeple-card/parts/index.ts
export { Cover } from './Cover';
export { EntityBadge } from './EntityBadge';
export { AccentBorder } from './AccentBorder';
export { StatusBadge } from './StatusBadge';
export { QuickActions } from './QuickActions';
export { Rating } from './Rating';
export { MetaChips } from './MetaChips';
export { NavFooter } from './NavFooter';
export { TagStrip } from './TagStrip';
```

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/parts/
git commit -m "feat(meeple-card): add NavFooter and TagStrip parts, barrel export"
```

---

## Phase 3: Variant Renderers

### Task 8: Build GridCard Variant

**Files:**
- Create: `apps/web/src/components/ui/data-display/meeple-card/variants/GridCard.tsx`

**Mockup reference:** `meeple-card-real-app-render.html` (Section 1: Grid cards), `meeple-card-summary-render.html`

- [ ] **Step 1: Write GridCard.tsx**

```tsx
// apps/web/src/components/ui/data-display/meeple-card/variants/GridCard.tsx
'use client';

import { entityHsl } from '../tokens';
import type { MeepleCardProps } from '../types';
import { Cover } from '../parts/Cover';
import { EntityBadge } from '../parts/EntityBadge';
import { AccentBorder } from '../parts/AccentBorder';
import { StatusBadge } from '../parts/StatusBadge';
import { QuickActions } from '../parts/QuickActions';
import { Rating } from '../parts/Rating';
import { MetaChips } from '../parts/MetaChips';
import { NavFooter } from '../parts/NavFooter';
import { TagStrip } from '../parts/TagStrip';

export function GridCard(props: MeepleCardProps) {
  const {
    entity, title, subtitle, imageUrl, rating, ratingMax,
    metadata = [], tags = [], status, actions = [], navItems = [],
    showQuickActions, onClick, className = '',
  } = props;

  const glowColor = entityHsl(entity, 0.4);

  return (
    <div
      className={`group relative flex cursor-pointer flex-col overflow-hidden rounded-2xl border border-[var(--mc-border)] bg-[var(--mc-bg-card)] shadow-[var(--mc-shadow-sm)] outline-2 outline-offset-2 outline-transparent backdrop-blur-[12px] backdrop-saturate-[180%] transition-all duration-[350ms] [transition-timing-function:cubic-bezier(0.4,0,0.2,1)] hover:-translate-y-1.5 hover:shadow-[var(--mc-shadow-xl)] hover:outline-[var(--mc-glow)] ${className}`}
      style={{
        '--mc-glow': glowColor,
      } as React.CSSProperties}
      onClick={onClick}
      role={onClick ? 'button' : undefined}
      tabIndex={onClick ? 0 : undefined}
    >
      <AccentBorder entity={entity} />

      {/* Cover area */}
      <div className="relative">
        <Cover entity={entity} variant="grid" imageUrl={imageUrl} alt={title} />
        <EntityBadge entity={entity} />
        {status && <StatusBadge status={status} />}
        {tags.length > 0 && <TagStrip tags={tags} entity={entity} />}
        {showQuickActions && actions.length > 0 && <QuickActions actions={actions} />}
      </div>

      {/* Content */}
      <div className="flex flex-1 flex-col gap-[3px] px-3.5 py-2.5 pb-2">
        <h3 className="font-[var(--font-quicksand)] text-[0.95rem] font-bold leading-tight text-[var(--mc-text-primary)]">
          {title}
        </h3>
        {subtitle && (
          <p className="text-[0.78rem] leading-tight text-[var(--mc-text-secondary)]">
            {subtitle}
          </p>
        )}
        {rating !== undefined && <Rating value={rating} max={ratingMax} />}
        {metadata.length > 0 && <MetaChips metadata={metadata} />}
      </div>

      {/* Nav Footer */}
      {navItems.length > 0 && <NavFooter items={navItems} />}
    </div>
  );
}
```

- [ ] **Step 2: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/variants/GridCard.tsx
git commit -m "feat(meeple-card): add GridCard variant renderer"
```

---

### Task 9: Build ListCard, CompactCard Variants

**Files:**
- Create: `apps/web/src/components/ui/data-display/meeple-card/variants/ListCard.tsx`
- Create: `apps/web/src/components/ui/data-display/meeple-card/variants/CompactCard.tsx`

**Mockup reference:** `meeple-card-real-app-render.html` (list variant), `meeple-card-summary-render.html` (compact)

- [ ] **Step 1: Write ListCard.tsx**

```tsx
// apps/web/src/components/ui/data-display/meeple-card/variants/ListCard.tsx
'use client';

import { entityHsl, entityIcon } from '../tokens';
import type { MeepleCardProps } from '../types';
import { Rating } from '../parts/Rating';
import { MetaChips } from '../parts/MetaChips';
import { NavFooter } from '../parts/NavFooter';

export function ListCard(props: MeepleCardProps) {
  const {
    entity, title, subtitle, imageUrl, rating, ratingMax,
    metadata = [], navItems = [], onClick, className = '',
  } = props;

  return (
    <div
      className={`group flex items-center gap-3.5 rounded-xl border border-[var(--mc-border)] bg-[var(--mc-bg-card)] px-3.5 py-3 shadow-[var(--mc-shadow-sm)] backdrop-blur-[12px] transition-all duration-300 hover:translate-x-1 hover:shadow-[var(--mc-shadow-md)] ${className}`}
      onClick={onClick}
      role={onClick ? 'button' : undefined}
      tabIndex={onClick ? 0 : undefined}
    >
      {/* Entity dot */}
      <div
        className="h-2 w-2 flex-shrink-0 rounded-full"
        style={{ background: entityHsl(entity) }}
      />

      {/* Thumbnail */}
      <div className="h-[52px] w-[52px] flex-shrink-0 overflow-hidden rounded-lg">
        {imageUrl ? (
          <img src={imageUrl} alt={title} className="h-full w-full object-cover" loading="lazy" />
        ) : (
          <div
            className="flex h-full w-full items-center justify-center text-xl opacity-50"
            style={{ background: entityHsl(entity, 0.08) }}
          >
            {entityIcon[entity]}
          </div>
        )}
      </div>

      {/* Content */}
      <div className="flex min-w-0 flex-1 flex-col gap-0.5">
        <h3 className="truncate font-[var(--font-quicksand)] text-[0.88rem] font-bold text-[var(--mc-text-primary)]">
          {title}
        </h3>
        {subtitle && (
          <p className="truncate text-[0.75rem] text-[var(--mc-text-secondary)]">{subtitle}</p>
        )}
        <div className="flex items-center gap-2">
          {rating !== undefined && <Rating value={rating} max={ratingMax} />}
          {metadata.length > 0 && <MetaChips metadata={metadata} />}
        </div>
      </div>

      {/* Inline nav */}
      {navItems.length > 0 && (
        <div className="flex-shrink-0">
          <NavFooter items={navItems} size="sm" />
        </div>
      )}
    </div>
  );
}
```

- [ ] **Step 2: Write CompactCard.tsx**

```tsx
// apps/web/src/components/ui/data-display/meeple-card/variants/CompactCard.tsx
'use client';

import { entityHsl } from '../tokens';
import type { MeepleCardProps } from '../types';

export function CompactCard(props: MeepleCardProps) {
  const { entity, title, onClick, className = '' } = props;

  return (
    <div
      className={`group flex items-center gap-2 rounded-lg border border-[var(--mc-border)] bg-[var(--mc-bg-card)] px-2.5 py-2 shadow-[var(--mc-shadow-sm)] transition-transform duration-200 hover:scale-[1.02] ${className}`}
      onClick={onClick}
      role={onClick ? 'button' : undefined}
      tabIndex={onClick ? 0 : undefined}
    >
      <div
        className="h-1.5 w-1.5 flex-shrink-0 rounded-full"
        style={{ background: entityHsl(entity) }}
      />
      <span className="truncate font-[var(--font-quicksand)] text-[0.85rem] font-semibold text-[var(--mc-text-primary)]">
        {title}
      </span>
    </div>
  );
}
```

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/variants/
git commit -m "feat(meeple-card): add ListCard and CompactCard variants"
```

---

### Task 10: Build FeaturedCard, HeroCard Variants

**Files:**
- Create: `apps/web/src/components/ui/data-display/meeple-card/variants/FeaturedCard.tsx`
- Create: `apps/web/src/components/ui/data-display/meeple-card/variants/HeroCard.tsx`
- Create: `apps/web/src/components/ui/data-display/meeple-card/variants/index.ts`

**Mockup reference:** `meeple-card-nav-buttons-mockup.html` (featured), `meeple-card-summary-render.html` (hero)

- [ ] **Step 1: Write FeaturedCard.tsx**

```tsx
// apps/web/src/components/ui/data-display/meeple-card/variants/FeaturedCard.tsx
'use client';

import { entityHsl } from '../tokens';
import type { MeepleCardProps } from '../types';
import { Cover } from '../parts/Cover';
import { EntityBadge } from '../parts/EntityBadge';
import { AccentBorder } from '../parts/AccentBorder';
import { StatusBadge } from '../parts/StatusBadge';
import { QuickActions } from '../parts/QuickActions';
import { Rating } from '../parts/Rating';
import { MetaChips } from '../parts/MetaChips';
import { NavFooter } from '../parts/NavFooter';

export function FeaturedCard(props: MeepleCardProps) {
  const {
    entity, title, subtitle, imageUrl, rating, ratingMax,
    metadata = [], status, actions = [], navItems = [],
    showQuickActions, onClick, className = '',
  } = props;

  return (
    <div
      className={`group relative flex w-[400px] cursor-pointer flex-col overflow-hidden rounded-[20px] border border-[var(--mc-border)] bg-[var(--mc-bg-card)] shadow-[var(--mc-shadow-lg)] outline-2 outline-offset-2 outline-transparent backdrop-blur-[12px] backdrop-saturate-[180%] transition-all duration-[350ms] [transition-timing-function:cubic-bezier(0.4,0,0.2,1)] hover:-translate-y-1.5 hover:shadow-[var(--mc-shadow-2xl)] hover:outline-[var(--mc-glow)] ${className}`}
      style={{ '--mc-glow': entityHsl(entity, 0.4) } as React.CSSProperties}
      onClick={onClick}
    >
      <AccentBorder entity={entity} />

      <div className="relative">
        <Cover entity={entity} variant="featured" imageUrl={imageUrl} alt={title} />
        <EntityBadge entity={entity} />
        {status && <StatusBadge status={status} />}
        {showQuickActions && actions.length > 0 && <QuickActions actions={actions} />}
      </div>

      <div className="flex flex-1 flex-col gap-1 px-4 py-3">
        <h3 className="font-[var(--font-quicksand)] text-[1.1rem] font-bold leading-tight text-[var(--mc-text-primary)]">
          {title}
        </h3>
        {subtitle && (
          <p className="text-[0.82rem] text-[var(--mc-text-secondary)]">{subtitle}</p>
        )}
        {rating !== undefined && <Rating value={rating} max={ratingMax} />}
        {metadata.length > 0 && <MetaChips metadata={metadata} />}
      </div>

      {navItems.length > 0 && <NavFooter items={navItems} size="md" />}
    </div>
  );
}
```

- [ ] **Step 2: Write HeroCard.tsx**

```tsx
// apps/web/src/components/ui/data-display/meeple-card/variants/HeroCard.tsx
'use client';

import { entityHsl, entityIcon } from '../tokens';
import type { MeepleCardProps } from '../types';

export function HeroCard(props: MeepleCardProps) {
  const {
    entity, title, subtitle, imageUrl, rating, ratingMax,
    onClick, className = '',
  } = props;

  return (
    <div
      className={`group relative min-h-[320px] cursor-pointer overflow-hidden rounded-3xl shadow-[var(--mc-shadow-lg)] transition-transform duration-300 hover:scale-[1.01] ${className}`}
      onClick={onClick}
    >
      {/* Background */}
      {imageUrl ? (
        <img
          src={imageUrl}
          alt={title}
          className="absolute inset-0 h-full w-full object-cover transition-transform duration-500 group-hover:scale-[1.03]"
        />
      ) : (
        <div
          className="absolute inset-0 flex items-center justify-center text-8xl opacity-30"
          style={{ background: entityHsl(entity, 0.15) }}
        >
          {entityIcon[entity]}
        </div>
      )}

      {/* Gradient overlay */}
      <div
        className="absolute inset-0"
        style={{
          background: `linear-gradient(to top, ${entityHsl(entity, 0.85)} 0%, ${entityHsl(entity, 0.4)} 40%, transparent 80%)`,
        }}
      />

      {/* Content */}
      <div className="relative flex h-full min-h-[320px] flex-col justify-end p-6">
        <h3 className="font-[var(--font-quicksand)] text-2xl font-bold leading-tight text-white drop-shadow-md">
          {title}
        </h3>
        {subtitle && (
          <p className="mt-1 text-sm text-white/80">{subtitle}</p>
        )}
        {rating !== undefined && (
          <div className="mt-2 flex items-center gap-1 text-amber-300">
            <span>★</span>
            <span className="text-sm font-bold text-white">{rating.toFixed(1)}</span>
            {ratingMax && <span className="text-xs text-white/60">/ {ratingMax}</span>}
          </div>
        )}
      </div>
    </div>
  );
}
```

- [ ] **Step 3: Write variants/index.ts**

```typescript
// apps/web/src/components/ui/data-display/meeple-card/variants/index.ts
export { GridCard } from './GridCard';
export { ListCard } from './ListCard';
export { CompactCard } from './CompactCard';
export { FeaturedCard } from './FeaturedCard';
export { HeroCard } from './HeroCard';
```

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/variants/
git commit -m "feat(meeple-card): add FeaturedCard, HeroCard variants and barrel export"
```

---

## Phase 4: Features (Interactive Behaviors)

### Task 11: Build FlipCard and HoverPreview Features

**Files:**
- Create: `apps/web/src/components/ui/data-display/meeple-card/features/FlipCard.tsx`
- Create: `apps/web/src/components/ui/data-display/meeple-card/features/HoverPreview.tsx`

- [ ] **Step 1: Write FlipCard.tsx**

```tsx
// apps/web/src/components/ui/data-display/meeple-card/features/FlipCard.tsx
'use client';

import { useState, type ReactNode } from 'react';

interface FlipCardProps {
  front: ReactNode;
  back: ReactNode;
  trigger?: 'card' | 'button';
  className?: string;
}

export function FlipCard({ front, back, trigger = 'card', className = '' }: FlipCardProps) {
  const [flipped, setFlipped] = useState(false);

  const handleFlip = () => setFlipped((f) => !f);

  return (
    <div
      className={`[perspective:1000px] ${className}`}
      onClick={trigger === 'card' ? handleFlip : undefined}
    >
      <div
        className={`relative transition-transform duration-[600ms] [transform-style:preserve-3d] [transition-timing-function:cubic-bezier(0.4,0,0.2,1)] ${flipped ? '[transform:rotateY(180deg)]' : ''}`}
      >
        {/* Front */}
        <div className="[backface-visibility:hidden]">
          {front}
          {trigger === 'button' && (
            <button
              onClick={(e) => {
                e.stopPropagation();
                handleFlip();
              }}
              className="absolute bottom-2 right-2 z-20 flex h-7 w-7 items-center justify-center rounded-full bg-white/85 text-xs shadow-sm backdrop-blur-sm"
            >
              🔄
            </button>
          )}
        </div>
        {/* Back */}
        <div className="absolute inset-0 [backface-visibility:hidden] [transform:rotateY(180deg)]">
          {back}
          <button
            onClick={(e) => {
              e.stopPropagation();
              handleFlip();
            }}
            className="absolute right-2 top-2 z-20 flex h-7 w-7 items-center justify-center rounded-full bg-white/85 text-xs shadow-sm backdrop-blur-sm"
          >
            ✕
          </button>
        </div>
      </div>
    </div>
  );
}
```

- [ ] **Step 2: Write HoverPreview.tsx**

```tsx
// apps/web/src/components/ui/data-display/meeple-card/features/HoverPreview.tsx
'use client';

import { useState, useRef, useCallback, type ReactNode } from 'react';

interface HoverPreviewProps {
  children: ReactNode;
  content: ReactNode;
  delay?: number;
}

export function HoverPreview({ children, content, delay = 500 }: HoverPreviewProps) {
  const [visible, setVisible] = useState(false);
  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const handleEnter = useCallback(() => {
    timerRef.current = setTimeout(() => setVisible(true), delay);
  }, [delay]);

  const handleLeave = useCallback(() => {
    if (timerRef.current) clearTimeout(timerRef.current);
    setVisible(false);
  }, []);

  return (
    <div className="relative" onMouseEnter={handleEnter} onMouseLeave={handleLeave}>
      {children}
      {visible && (
        <div className="absolute left-full top-0 z-50 ml-2 w-64 rounded-xl border border-[var(--mc-border)] bg-[var(--mc-bg-card)] p-3 shadow-[var(--mc-shadow-xl)] backdrop-blur-[12px]">
          {content}
        </div>
      )}
    </div>
  );
}
```

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/features/
git commit -m "feat(meeple-card): add FlipCard and HoverPreview features"
```

---

### Task 12: Build Carousel3D, DragHandle, SwipeGesture

**Files:**
- Create: `apps/web/src/components/ui/data-display/meeple-card/features/Carousel3D.tsx`
- Create: `apps/web/src/components/ui/data-display/meeple-card/features/DragHandle.tsx`
- Create: `apps/web/src/components/ui/data-display/meeple-card/features/SwipeGesture.tsx`
- Create: `apps/web/src/components/ui/data-display/meeple-card/features/index.ts`

**Mockup reference:** `meeple-card-visual-test.html` (3D carousel section)

- [ ] **Step 1: Write Carousel3D.tsx**

```tsx
// apps/web/src/components/ui/data-display/meeple-card/features/Carousel3D.tsx
'use client';

import { useState, useCallback } from 'react';
import { GridCard } from '../variants/GridCard';
import type { MeepleCardProps, Carousel3DProps } from '../types';

export function Carousel3D({ cards, activeIndex: controlledIndex, onNavigate, className = '' }: Carousel3DProps) {
  const [internalIndex, setInternalIndex] = useState(0);
  const activeIndex = controlledIndex ?? internalIndex;

  const navigate = useCallback((idx: number) => {
    const clamped = Math.max(0, Math.min(cards.length - 1, idx));
    setInternalIndex(clamped);
    onNavigate?.(clamped);
  }, [cards.length, onNavigate]);

  if (cards.length === 0) return null;

  return (
    <div className={`relative hidden md:flex items-center justify-center gap-4 py-8 ${className}`}>
      {/* Left arrow */}
      <button
        onClick={() => navigate(activeIndex - 1)}
        disabled={activeIndex === 0}
        className="z-10 flex h-10 w-10 items-center justify-center rounded-full bg-white/80 shadow-md backdrop-blur-sm transition-transform hover:scale-110 disabled:opacity-30"
      >
        ‹
      </button>

      {/* Cards container */}
      <div className="relative flex items-center justify-center" style={{ perspective: '1200px' }}>
        {cards.map((card, i) => {
          const offset = i - activeIndex;
          const isActive = offset === 0;
          const isAdjacent = Math.abs(offset) === 1;
          const isVisible = Math.abs(offset) <= 1;

          if (!isVisible) return null;

          return (
            <div
              key={card.id ?? i}
              className="absolute transition-all duration-400 ease-out"
              style={{
                transform: isActive
                  ? 'translateX(0) rotateY(0deg) scale(1)'
                  : `translateX(${offset * 35}%) rotateY(${offset * -5}deg) scale(0.85)`,
                zIndex: isActive ? 10 : 5,
                filter: isActive ? 'none' : 'blur(2px)',
                opacity: isActive ? 1 : 0.6,
                pointerEvents: isActive ? 'auto' : 'none',
              }}
              onClick={!isActive ? () => navigate(i) : undefined}
            >
              <GridCard {...card} />
            </div>
          );
        })}
      </div>

      {/* Right arrow */}
      <button
        onClick={() => navigate(activeIndex + 1)}
        disabled={activeIndex === cards.length - 1}
        className="z-10 flex h-10 w-10 items-center justify-center rounded-full bg-white/80 shadow-md backdrop-blur-sm transition-transform hover:scale-110 disabled:opacity-30"
      >
        ›
      </button>
    </div>
  );
}
```

- [ ] **Step 2: Write DragHandle.tsx**

```tsx
// apps/web/src/components/ui/data-display/meeple-card/features/DragHandle.tsx
'use client';

interface DragHandleProps {
  onDragStart?: () => void;
  onDragEnd?: () => void;
}

export function DragHandle({ onDragStart, onDragEnd }: DragHandleProps) {
  return (
    <div
      className="absolute left-1 top-1/2 z-20 flex -translate-y-1/2 cursor-grab items-center justify-center opacity-0 transition-opacity group-hover:opacity-60 active:cursor-grabbing"
      draggable
      onDragStart={onDragStart}
      onDragEnd={onDragEnd}
    >
      <span className="text-[10px] text-[var(--mc-text-muted)]">⠿</span>
    </div>
  );
}
```

- [ ] **Step 3: Write SwipeGesture.tsx**

```tsx
// apps/web/src/components/ui/data-display/meeple-card/features/SwipeGesture.tsx
'use client';

import { useRef, useCallback, type ReactNode, type TouchEvent } from 'react';

interface SwipeGestureProps {
  children: ReactNode;
  onSwipeLeft?: () => void;
  onSwipeRight?: () => void;
  threshold?: number;
  className?: string;
}

export function SwipeGesture({ children, onSwipeLeft, onSwipeRight, threshold = 50, className = '' }: SwipeGestureProps) {
  const startX = useRef(0);

  const handleTouchStart = useCallback((e: TouchEvent) => {
    startX.current = e.touches[0].clientX;
  }, []);

  const handleTouchEnd = useCallback((e: TouchEvent) => {
    const diff = e.changedTouches[0].clientX - startX.current;
    if (Math.abs(diff) >= threshold) {
      if (diff > 0) onSwipeRight?.();
      else onSwipeLeft?.();
    }
  }, [threshold, onSwipeLeft, onSwipeRight]);

  return (
    <div
      className={className}
      onTouchStart={handleTouchStart}
      onTouchEnd={handleTouchEnd}
    >
      {children}
    </div>
  );
}
```

- [ ] **Step 4: Write features/index.ts**

```typescript
// apps/web/src/components/ui/data-display/meeple-card/features/index.ts
export { FlipCard } from './FlipCard';
export { HoverPreview } from './HoverPreview';
export { Carousel3D } from './Carousel3D';
export { DragHandle } from './DragHandle';
export { SwipeGesture } from './SwipeGesture';
```

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/features/
git commit -m "feat(meeple-card): add Carousel3D, DragHandle, SwipeGesture features"
```

---

## Phase 5: Mobile Layout

### Task 13: Build Mobile Hand+Focused Card Layout

**Files:**
- Create: `apps/web/src/components/ui/data-display/meeple-card/mobile/HandCard.tsx`
- Create: `apps/web/src/components/ui/data-display/meeple-card/mobile/HandSidebar.tsx`
- Create: `apps/web/src/components/ui/data-display/meeple-card/mobile/FocusedCard.tsx`
- Create: `apps/web/src/components/ui/data-display/meeple-card/mobile/MobileCardLayout.tsx`

**Mockup reference:** `mobile-card-layout-mockup.html` (primary), `mobile-card-entity-types.html`

- [ ] **Step 1: Write HandCard.tsx**

```tsx
// apps/web/src/components/ui/data-display/meeple-card/mobile/HandCard.tsx
'use client';

import { entityHsl, entityIcon } from '../tokens';
import type { MeepleEntityType } from '../types';

interface HandCardProps {
  entity: MeepleEntityType;
  title: string;
  isActive: boolean;
  onClick: () => void;
}

export function HandCard({ entity, title, isActive, onClick }: HandCardProps) {
  const color = entityHsl(entity);

  return (
    <button
      onClick={onClick}
      className={`relative h-10 w-7 flex-shrink-0 cursor-pointer overflow-hidden rounded-md border border-[var(--mc-border)] bg-[var(--mc-bg-card)] shadow-[var(--mc-shadow-sm)] transition-transform duration-200 hover:scale-110 ${
        isActive ? 'scale-[1.12] outline outline-2 outline-offset-1' : ''
      }`}
      style={isActive ? { outlineColor: color } : undefined}
    >
      {/* Entity edge */}
      <div
        className="absolute bottom-0 left-0 top-0 w-[3px]"
        style={{ background: color }}
      />
      {/* Icon */}
      <div className="flex h-[22px] w-full items-center justify-center text-[11px]">
        {entityIcon[entity]}
      </div>
      {/* Mini title */}
      <span className="absolute bottom-[1px] left-[3px] font-[var(--font-quicksand)] text-[4px] font-bold">
        {title.slice(0, 6)}
      </span>
    </button>
  );
}
```

- [ ] **Step 2: Write HandSidebar.tsx**

```tsx
// apps/web/src/components/ui/data-display/meeple-card/mobile/HandSidebar.tsx
'use client';

import { HandCard } from './HandCard';
import type { MeepleCardProps } from '../types';

interface HandSidebarProps {
  cards: MeepleCardProps[];
  activeId?: string;
  onSelect: (id: string) => void;
}

export function HandSidebar({ cards, activeId, onSelect }: HandSidebarProps) {
  return (
    <div className="flex w-9 flex-shrink-0 flex-col items-center gap-[5px] bg-gradient-to-r from-black/[0.02] to-transparent py-1.5">
      <span className="mb-1 font-[var(--font-quicksand)] text-[7px] font-bold uppercase tracking-widest text-[var(--mc-text-muted)] [writing-mode:vertical-rl] [transform:rotate(180deg)]">
        Cards
      </span>
      {cards.map((card) => (
        <HandCard
          key={card.id ?? card.title}
          entity={card.entity}
          title={card.title}
          isActive={(card.id ?? card.title) === activeId}
          onClick={() => onSelect(card.id ?? card.title)}
        />
      ))}
    </div>
  );
}
```

- [ ] **Step 3: Write FocusedCard.tsx**

```tsx
// apps/web/src/components/ui/data-display/meeple-card/mobile/FocusedCard.tsx
'use client';

import { entityHsl, entityLabel } from '../tokens';
import type { MeepleCardProps } from '../types';
import { MetaChips } from '../parts/MetaChips';

export function FocusedCard(props: MeepleCardProps) {
  const {
    entity, title, subtitle, imageUrl, rating, ratingMax,
    metadata = [], actions = [], onClick,
  } = props;

  const color = entityHsl(entity);

  return (
    <div
      className="flex flex-1 flex-col overflow-hidden rounded-[14px] border border-[var(--mc-border)] bg-[var(--mc-bg-card)] shadow-[var(--mc-shadow-xl)] outline-2 outline-offset-2 outline-transparent backdrop-blur-[12px]"
      onClick={onClick}
    >
      {/* Top accent bar */}
      <div className="h-[3px] w-full flex-shrink-0" style={{ background: color }} />

      {/* Cover */}
      {imageUrl && (
        <div className="relative flex-shrink-0 overflow-hidden">
          <img src={imageUrl} alt={title} className="h-40 w-full object-cover" />
          <div
            className="absolute bottom-0 left-0 right-0 h-1/2"
            style={{ background: `linear-gradient(to top, var(--mc-bg-card), transparent)` }}
          />
          {/* Badge */}
          <span
            className="absolute left-2 top-2 rounded-2xl px-2 py-0.5 font-[var(--font-quicksand)] text-[8px] font-bold uppercase tracking-wide text-white backdrop-blur-lg"
            style={{ background: entityHsl(entity, 0.8) }}
          >
            {entityLabel[entity]}
          </span>
          {/* Rating */}
          {rating !== undefined && (
            <span className="absolute right-2 top-2 flex items-center gap-[3px] rounded-[10px] bg-black/50 px-2 py-[3px] text-[10px] font-bold text-amber-300 backdrop-blur-lg">
              ★ {rating.toFixed(1)}
              {ratingMax && <span className="text-white/60">/{ratingMax}</span>}
            </span>
          )}
        </div>
      )}

      {/* Body */}
      <div className="flex flex-1 flex-col gap-[5px] overflow-y-auto p-2.5 px-3">
        <h3 className="font-[var(--font-quicksand)] text-base font-bold leading-tight">
          {title}
        </h3>
        {subtitle && (
          <p className="text-[0.72rem] leading-tight text-[var(--mc-text-secondary)]">
            {subtitle}
          </p>
        )}
        {metadata.length > 0 && <MetaChips metadata={metadata} />}
      </div>

      {/* Quick actions */}
      {actions.length > 0 && (
        <div className="mt-auto flex gap-1.5 border-t border-[var(--mc-border-light)] p-2">
          {actions.map((action, i) => (
            <button
              key={i}
              onClick={(e) => {
                e.stopPropagation();
                action.onClick();
              }}
              disabled={action.disabled}
              className={`flex h-8 w-8 items-center justify-center rounded-full text-[13px] shadow-[var(--mc-shadow-sm)] transition-transform hover:scale-110 ${
                action.variant === 'primary'
                  ? 'border-none text-white'
                  : 'border border-white/60 bg-white/85 backdrop-blur-lg'
              }`}
              style={action.variant === 'primary' ? { background: color } : undefined}
              title={action.label}
            >
              {action.icon}
            </button>
          ))}
        </div>
      )}
    </div>
  );
}
```

- [ ] **Step 4: Write MobileCardLayout.tsx**

```tsx
// apps/web/src/components/ui/data-display/meeple-card/mobile/MobileCardLayout.tsx
'use client';

import { useState, useCallback } from 'react';
import { HandSidebar } from './HandSidebar';
import { FocusedCard } from './FocusedCard';
import { SwipeGesture } from '../features/SwipeGesture';
import type { MobileCardLayoutProps } from '../types';

export function MobileCardLayout({ cards, activeId: controlledId, onCardSelect, className = '' }: MobileCardLayoutProps) {
  const [internalId, setInternalId] = useState(cards[0]?.id ?? cards[0]?.title ?? '');
  const activeId = controlledId ?? internalId;

  const activeCard = cards.find((c) => (c.id ?? c.title) === activeId) ?? cards[0];
  const activeIndex = cards.findIndex((c) => (c.id ?? c.title) === activeId);

  const selectCard = useCallback((id: string) => {
    setInternalId(id);
    onCardSelect?.(id);
  }, [onCardSelect]);

  const navigateByOffset = useCallback((offset: number) => {
    const newIndex = Math.max(0, Math.min(cards.length - 1, activeIndex + offset));
    const card = cards[newIndex];
    if (card) selectCard(card.id ?? card.title);
  }, [activeIndex, cards, selectCard]);

  if (cards.length === 0) return null;

  return (
    <div className={`flex h-full md:hidden ${className}`}>
      <HandSidebar
        cards={cards}
        activeId={activeId}
        onSelect={selectCard}
      />
      <div className="flex flex-1 flex-col p-1.5 pl-[3px]">
        <SwipeGesture
          onSwipeLeft={() => navigateByOffset(1)}
          onSwipeRight={() => navigateByOffset(-1)}
          className="flex flex-1"
        >
          {activeCard && <FocusedCard {...activeCard} />}
        </SwipeGesture>
      </div>
    </div>
  );
}
```

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/mobile/
git commit -m "feat(meeple-card): add mobile hand+focused card layout

HandCard, HandSidebar, FocusedCard, MobileCardLayout.
Activates on < 768px (md:hidden), swipe navigation between cards."
```

---

## Phase 6: Main Router, Compound API, Public Exports

### Task 14: Build MeepleCard Router, Compound API, Skeleton, Index

**Files:**
- Create: `apps/web/src/components/ui/data-display/meeple-card/MeepleCard.tsx`
- Create: `apps/web/src/components/ui/data-display/meeple-card/compound.tsx`
- Create: `apps/web/src/components/ui/data-display/meeple-card/skeleton.tsx`
- Create: `apps/web/src/components/ui/data-display/meeple-card/index.ts`

- [ ] **Step 1: Write MeepleCard.tsx (router)**

```tsx
// apps/web/src/components/ui/data-display/meeple-card/MeepleCard.tsx
'use client';

import { memo } from 'react';
import type { MeepleCardProps } from './types';
import { GridCard } from './variants/GridCard';
import { ListCard } from './variants/ListCard';
import { CompactCard } from './variants/CompactCard';
import { FeaturedCard } from './variants/FeaturedCard';
import { HeroCard } from './variants/HeroCard';

const variantMap = {
  grid: GridCard,
  list: ListCard,
  compact: CompactCard,
  featured: FeaturedCard,
  hero: HeroCard,
} as const;

export const MeepleCard = memo(function MeepleCard(props: MeepleCardProps) {
  const variant = props.variant ?? 'grid';
  const Renderer = variantMap[variant];
  return <Renderer {...props} />;
});
```

- [ ] **Step 2: Write compound.tsx**

```tsx
// apps/web/src/components/ui/data-display/meeple-card/compound.tsx
'use client';

import { MeepleCard } from './MeepleCard';
import type { MeepleCardProps, MeepleEntityType } from './types';

function createEntityCard(entity: MeepleEntityType) {
  return function EntityCard(props: Omit<MeepleCardProps, 'entity'>) {
    return <MeepleCard {...props} entity={entity} />;
  };
}

export const MeepleCards = {
  Game: createEntityCard('game'),
  Player: createEntityCard('player'),
  Session: createEntityCard('session'),
  Agent: createEntityCard('agent'),
  Kb: createEntityCard('kb'),
  Chat: createEntityCard('chat'),
  Event: createEntityCard('event'),
  Toolkit: createEntityCard('toolkit'),
  Tool: createEntityCard('tool'),
  Base: MeepleCard,
};
```

- [ ] **Step 3: Write skeleton.tsx**

```tsx
// apps/web/src/components/ui/data-display/meeple-card/skeleton.tsx
import type { MeepleCardVariant } from './types';

interface MeepleCardSkeletonProps {
  variant?: MeepleCardVariant;
  className?: string;
}

const skeletonHeight: Record<MeepleCardVariant, string> = {
  grid: 'h-[378px]',
  list: 'h-[72px]',
  compact: 'h-[48px]',
  featured: 'h-[340px]',
  hero: 'h-[320px]',
};

export function MeepleCardSkeleton({ variant = 'grid', className = '' }: MeepleCardSkeletonProps) {
  return (
    <div
      className={`animate-pulse rounded-2xl bg-[var(--mc-bg-muted)] ${skeletonHeight[variant]} ${className}`}
    />
  );
}
```

- [ ] **Step 4: Write index.ts (public API)**

```typescript
// apps/web/src/components/ui/data-display/meeple-card/index.ts

// Main component
export { MeepleCard } from './MeepleCard';

// Compound API
export { MeepleCards } from './compound';

// Types
export type {
  MeepleCardProps,
  MeepleEntityType,
  MeepleCardVariant,
  MeepleCardMetadata,
  MeepleCardAction,
  NavFooterItem,
  CardStatus,
  CoverLabel,
  MobileCardLayoutProps,
  Carousel3DProps,
} from './types';

// Mobile layout
export { MobileCardLayout } from './mobile/MobileCardLayout';

// Features
export { FlipCard } from './features/FlipCard';
export { HoverPreview } from './features/HoverPreview';
export { Carousel3D } from './features/Carousel3D';

// Skeleton
export { MeepleCardSkeleton } from './skeleton';

// Tokens (for consumers needing entity colors)
export { entityColors, entityHsl, entityLabel, entityIcon } from './tokens';
```

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/
git commit -m "feat(meeple-card): add router, compound API, skeleton, public exports

MeepleCard dispatches to 5 variant renderers.
MeepleCards compound: .Game, .Player, .Session, .Agent, .Kb, .Chat, .Event, .Toolkit, .Tool
Public API exports all components, types, and tokens."
```

---

## Phase 7: Consumer Updates

Phase 7 is split into sub-tasks to reduce blast radius and enable parallel work.

### Task 15a: Update Adjacent Component Directories

These directories import from deleted meeple-card files and must be updated to use the new API.

**Files:**
- Modify: `apps/web/src/components/ui/data-display/extra-meeple-card/` (32 files — drawer, entity detail hooks, tabs)
- Modify: `apps/web/src/components/ui/data-display/deck-stack/` (7 files)
- Modify: `apps/web/src/components/ui/data-display/entity-list-view/` (26 files)
- Modify: `apps/web/src/components/ui/data-display/game-carousel/` (14 files)
- Modify: `apps/web/src/components/ui/data-display/mana/` (6 files)

- [ ] **Step 1: Update extra-meeple-card/ imports and entity types**

Update `ExtraMeepleCardDrawer.tsx`, `EntityExtraMeepleCard.tsx`, entity detail hooks, and all tab components to:
- Import `MeepleEntityType` from `@/components/ui/data-display/meeple-card`
- Rename `chatSession` → `chat`, `document` → `kb`
- Remove references to deleted entity types (`collection`, `group`, `location`, `expansion`, `achievement`, `note`, `custom`)
- Remove `CollectionExtraMeepleCard.tsx` (collection entity deleted)

- [ ] **Step 2: Update deck-stack/ imports**

Update `deck-stack-types.ts` and components to use new `MeepleEntityType` import.

- [ ] **Step 3: Update entity-list-view/ entity type references**

Update `entity-list-view.types.ts`, filter utilities, and components to use new 9-entity type system.

- [ ] **Step 4: Update game-carousel/ imports**

Update `types.ts` and `GameCarousel.tsx` to use new `MeepleCard` import.

- [ ] **Step 5: Update mana/ entity type references**

Update `mana-types.ts` and `mana-config.ts` to use new 9-entity type system.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "refactor: update adjacent card directories for new MeepleCard API

extra-meeple-card, deck-stack, entity-list-view, game-carousel, mana
updated to new 9-entity type system and import paths."
```

---

### Task 15b: Update Card Mappers and Config Files

**Files:**
- Modify: `apps/web/src/lib/card-mappers/kb-card-mapper.ts`
- Modify: `apps/web/src/lib/card-mappers/session-card-mapper.ts`
- Modify: `apps/web/src/lib/card-mappers/game-card-mapper.ts`
- Modify: `apps/web/src/lib/card-mappers/player-card-mapper.ts`
- Modify: `apps/web/src/config/entity-actions.ts`
- Modify: `apps/web/src/config/entity-navigation.ts`
- Modify: `apps/web/src/config/component-registry.ts`
- Modify: `apps/web/src/components/icons/entities/EntityIcon.tsx`
- Modify: `apps/web/src/components/icons/entity-types/EntityTypeIcon.tsx`

- [ ] **Step 1: Update card mappers — import paths and entity type renames**

```typescript
// Old: import { MeepleEntityType } from '@/components/ui/data-display/meeple-card-styles';
// New: import { MeepleEntityType } from '@/components/ui/data-display/meeple-card';
```

In kb-card-mapper: rename `entity: 'document'` → `entity: 'kb'`.

- [ ] **Step 2: Update config files — remove deleted entity types**

In `entity-actions.ts`, `entity-navigation.ts`, `component-registry.ts`:
- Remove cases/entries for: `collection`, `group`, `location`, `expansion`, `achievement`, `note`, `custom`
- Rename `chatSession` → `chat`

- [ ] **Step 3: Update icon components**

In `EntityIcon.tsx` and `EntityTypeIcon.tsx`:
- Remove cases for deleted entity types
- Rename `chatSession` → `chat`, `document` → `kb`

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "refactor: update card mappers, configs, icons for new entity types

Rename chatSession→chat, document→kb. Remove 7 deleted entity types."
```

---

### Task 15c: Rename Entity Types Across Remaining Consumers

**SAFETY NOTE on `document` → `kb` rename:**
Do NOT use global find/replace on `document`. Use these SAFE patterns only:

```bash
# Safe patterns (match entity context only):
grep -rn "entity.*['\"]document['\"]" apps/web/src/ --include="*.ts" --include="*.tsx"
grep -rn "'document'" apps/web/src/ --include="*.ts" --include="*.tsx" | grep -i "entity\|type\|meeple"
grep -rn "chatSession" apps/web/src/ --include="*.ts" --include="*.tsx"
```

- [ ] **Step 1: Rename `chatSession` → `chat` (safe — unique string)**

```bash
# This is safe — chatSession is unique to MeepleCard context
grep -rn "chatSession" apps/web/src/ --include="*.ts" --include="*.tsx" -l
```

For each file: replace `'chatSession'` with `'chat'` and `chatSession` with `chat` in type contexts.

- [ ] **Step 2: Rename `document` → `kb` (CAREFUL — context-specific)**

Only replace `document` when it appears as an entity type value:
- `entity: 'document'` → `entity: 'kb'`
- `entity === 'document'` → `entity === 'kb'`
- `'document' |` → `'kb' |` (in type unions)
- `case 'document':` → `case 'kb':` (in switch statements)

**NEVER replace**: `document.getElementById`, `document.querySelector`, `document.body`, etc.

- [ ] **Step 3: Remove deleted entity types from all switch/case and maps**

Search each pattern and remove matching code:
```bash
grep -rn "'collection'\|'group'\|'location'\|'expansion'\|'achievement'\|'note'\|'custom'" apps/web/src/ --include="*.ts" --include="*.tsx" | grep -i "entity\|type\|meeple\|case"
```

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "refactor: rename chatSession→chat, document→kb across codebase

Safe context-specific rename. Remove 7 deleted entity types from
all switch/case, type unions, and entity maps."
```

---

### Task 15d: Fix Remaining Import Paths and Remove Old Props

- [ ] **Step 1: Run TypeScript compiler to get remaining errors**

```bash
cd apps/web && pnpm typecheck 2>&1 | head -200
```

- [ ] **Step 2: Fix all remaining import paths**

```typescript
// Old: import { ... } from '@/components/ui/data-display/meeple-card-styles';
// New: import { ... } from '@/components/ui/data-display/meeple-card';

// Old: import { ... } from '@/components/ui/data-display/meeple-card-compound';
// New: import { ... } from '@/components/ui/data-display/meeple-card';

// Old: import { ... } from '@/components/ui/data-display/meeple-card-features';
// New: import { ... } from '@/components/ui/data-display/meeple-card';

// Old: import { ... } from '@/components/ui/data-display/meeple-card-features/...';
// New: import { ... } from '@/components/ui/data-display/meeple-card';

// Old: import { ... } from '@/components/ui/data-display/meeple-card-browser';
// New: REMOVE (deleted) — consumers must remove these imports

// Old: import { ... } from '@/components/ui/data-display/card-back-blocks';
// New: REMOVE (deleted)

// Old: import { ... } from '@/components/ui/data-display/holo';
// New: REMOVE (deleted)
```

- [ ] **Step 3: Remove old feature-specific prop references**

The following old props NO LONGER EXIST on MeepleCardProps and must be removed from consumer call sites:

| Old Prop | Migration |
|----------|-----------|
| `agentStatus` | Map to `status` prop (e.g., `status="active"`) |
| `agentModel` | Map to `metadata` array entry |
| `agentStats` | Map to `metadata` array entries |
| `chatStatus` | Map to `status` prop |
| `chatAgent` | Map to `metadata` array entry |
| `chatGame` | Map to `subtitle` prop |
| `chatStats` | Map to `metadata` array entries |
| `unreadCount` | Map to `badge` prop (e.g., `badge="3 unread"`) |
| `sessionPlayers` | Map to `metadata` array entries |
| `sessionBackData` | Map to `flipBackContent` prop |
| `sessionSnapshots` | Remove (not in new API) |
| `gameBackData` | Map to `flipBackContent` prop |
| `gameBackActions` | Map to `actions` array |
| `hasAgent` | Map to `navItems` entry |
| `hasKb` | Map to `navItems` entry |
| `kbCards` | Remove (not in new API) |
| `documentStatus` | Map to `status` prop |
| `showWishlist` | Remove (use `actions` with wishlist action) |
| `showPreview` | Remove (wrap with `HoverPreview` explicitly) |
| `selectable` | Remove (not in new API) |
| `showStatusIcon` | Remove (use `status` prop) |
| `showTagStrip` | Remove (tags shown when `tags` prop provided) |
| `showInfoButton` | Remove (not in new API) |
| `capabilities` | Remove (not in new API) |

- [ ] **Step 4: Run typecheck to verify zero errors**

```bash
cd apps/web && pnpm typecheck
```

Expected: 0 errors.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "refactor(meeple-card): fix remaining imports and remove old props

All consumers now use new MeepleCard public API.
Old feature-specific props mapped to generic props.
Zero TypeScript errors."
```

---

## Phase 8: Dark Theme Verification

### Task 16: Verify Dark Theme Works with MeepleCard Tokens

**Existing infrastructure (already in place — NO new code needed):**
- `apps/web/src/components/providers/ThemeProvider.tsx` — next-themes with `attribute="class"`, `defaultTheme="light"`, `enableSystem`
- `apps/web/src/components/ui/navigation/ThemeToggle.tsx` — toggle component in header
- `apps/web/src/app/layout.tsx` — fonts loaded, providers wrapped

The CSS tokens added in Task 3 use `.dark` selector which matches next-themes `attribute="class"` behavior.

- [ ] **Step 1: Verify dark theme CSS tokens are applied**

Start dev server, toggle to dark mode, inspect a MeepleCard element. Check that:
- `--mc-bg-card` resolves to `rgba(30, 27, 24, 0.80)` (dark value)
- `--mc-text-primary` resolves to `#f0ece8` (dark value)
- `--mc-shadow-sm` uses `rgba(0, 0, 0, ...)` (dark shadow)

- [ ] **Step 2: Fix any dark-mode-specific issues**

If MetaChips or other parts use hardcoded colors, replace with CSS variables.
If entity gradients don't look right on dark backgrounds, adjust alpha values.

- [ ] **Step 3: Commit any dark mode fixes**

```bash
git add -A
git commit -m "fix(meeple-card): dark theme adjustments for MeepleCard tokens"
```

---

## Phase 9: Testing

### Task 17: Write Unit Tests for Core Components

**Files:**
- Create: `apps/web/src/components/ui/data-display/meeple-card/__tests__/MeepleCard.test.tsx`
- Create: `apps/web/src/components/ui/data-display/meeple-card/__tests__/tokens.test.ts`

- [ ] **Step 1: Write MeepleCard.test.tsx**

```tsx
// apps/web/src/components/ui/data-display/meeple-card/__tests__/MeepleCard.test.tsx
import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { MeepleCard } from '../MeepleCard';
import type { MeepleEntityType, MeepleCardVariant } from '../types';

const entities: MeepleEntityType[] = ['game', 'player', 'session', 'agent', 'kb', 'chat', 'event', 'toolkit', 'tool'];
const variants: MeepleCardVariant[] = ['grid', 'list', 'compact', 'featured', 'hero'];

describe('MeepleCard', () => {
  it.each(entities)('renders %s entity type without error', (entity) => {
    const { container } = render(
      <MeepleCard entity={entity} title={`Test ${entity}`} />
    );
    expect(container.firstChild).toBeTruthy();
  });

  it.each(variants)('renders %s variant without error', (variant) => {
    const { container } = render(
      <MeepleCard entity="game" title="Test" variant={variant} />
    );
    expect(container.firstChild).toBeTruthy();
  });

  it('displays title text', () => {
    render(<MeepleCard entity="game" title="Catan" />);
    expect(screen.getByText('Catan')).toBeTruthy();
  });

  it('displays subtitle when provided', () => {
    render(<MeepleCard entity="game" title="Catan" subtitle="Kosmos" />);
    expect(screen.getByText('Kosmos')).toBeTruthy();
  });

  it('renders entity badge with correct label', () => {
    render(<MeepleCard entity="game" title="Test" />);
    expect(screen.getByText('Game')).toBeTruthy();
  });

  it('defaults to grid variant', () => {
    const { container } = render(<MeepleCard entity="game" title="Test" />);
    // Grid variant has aspect-[7/10] cover
    expect(container.querySelector('[class*="aspect-"]')).toBeTruthy();
  });
});
```

- [ ] **Step 2: Write tokens.test.ts**

```typescript
// apps/web/src/components/ui/data-display/meeple-card/__tests__/tokens.test.ts
import { describe, it, expect } from 'vitest';
import { entityColors, entityHsl, entityLabel, entityIcon } from '../tokens';
import type { MeepleEntityType } from '../types';

const allEntities: MeepleEntityType[] = ['game', 'player', 'session', 'agent', 'kb', 'chat', 'event', 'toolkit', 'tool'];

describe('entityColors', () => {
  it.each(allEntities)('has color definition for %s', (entity) => {
    const color = entityColors[entity];
    expect(color).toBeDefined();
    expect(color.h).toBeTypeOf('number');
    expect(color.s).toMatch(/%$/);
    expect(color.l).toMatch(/%$/);
  });

  it('has exactly 9 entity types', () => {
    expect(Object.keys(entityColors)).toHaveLength(9);
  });
});

describe('entityHsl', () => {
  it('returns hsl string without alpha', () => {
    expect(entityHsl('game')).toMatch(/^hsl\(/);
  });

  it('returns hsla string with alpha', () => {
    expect(entityHsl('game', 0.5)).toMatch(/^hsla\(/);
  });
});

describe('entityLabel', () => {
  it.each(allEntities)('has label for %s', (entity) => {
    expect(entityLabel[entity]).toBeTypeOf('string');
    expect(entityLabel[entity].length).toBeGreaterThan(0);
  });
});

describe('entityIcon', () => {
  it.each(allEntities)('has icon for %s', (entity) => {
    expect(entityIcon[entity]).toBeTypeOf('string');
  });
});
```

- [ ] **Step 3: Run tests**

```bash
cd apps/web && pnpm test -- --run src/components/ui/data-display/meeple-card/__tests__/
```

Expected: All tests pass.

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/__tests__/
git commit -m "test(meeple-card): add unit tests for router, tokens, all entity types"
```

---

### Task 18: Write Mobile Layout Tests

**Files:**
- Create: `apps/web/src/components/ui/data-display/meeple-card/__tests__/MobileCardLayout.test.tsx`

- [ ] **Step 1: Write MobileCardLayout.test.tsx**

```tsx
// apps/web/src/components/ui/data-display/meeple-card/__tests__/MobileCardLayout.test.tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { MobileCardLayout } from '../mobile/MobileCardLayout';
import type { MeepleCardProps } from '../types';

const mockCards: MeepleCardProps[] = [
  { entity: 'game', title: 'Catan', id: '1' },
  { entity: 'player', title: 'Alice', id: '2' },
  { entity: 'session', title: 'Friday Night', id: '3' },
];

describe('MobileCardLayout', () => {
  it('renders hand sidebar with all cards', () => {
    render(<MobileCardLayout cards={mockCards} />);
    // Hand cards show mini titles
    expect(screen.getByText('Cards')).toBeTruthy();
  });

  it('shows first card as active by default', () => {
    render(<MobileCardLayout cards={mockCards} />);
    expect(screen.getByText('Catan')).toBeTruthy();
  });

  it('calls onCardSelect when hand card clicked', () => {
    const onSelect = vi.fn();
    render(<MobileCardLayout cards={mockCards} onCardSelect={onSelect} />);
    // Click the second hand card
    const buttons = screen.getAllByRole('button');
    fireEvent.click(buttons[1]); // Second hand card
    expect(onSelect).toHaveBeenCalledWith('2');
  });

  it('returns null for empty cards array', () => {
    const { container } = render(<MobileCardLayout cards={[]} />);
    expect(container.firstChild).toBeNull();
  });
});
```

- [ ] **Step 2: Run tests**

```bash
cd apps/web && pnpm test -- --run src/components/ui/data-display/meeple-card/__tests__/MobileCardLayout.test.tsx
```

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/__tests__/
git commit -m "test(meeple-card): add mobile layout tests"
```

---

## Phase 10: Visual Verification

### Task 19: Build and Verify Locally

- [ ] **Step 1: Run full typecheck**

```bash
cd apps/web && pnpm typecheck
```

Expected: 0 errors.

- [ ] **Step 2: Run all tests**

```bash
cd apps/web && pnpm test -- --run
```

Expected: All pass (some unrelated tests may need updating if they imported old MeepleCard).

- [ ] **Step 3: Run lint**

```bash
cd apps/web && pnpm lint
```

- [ ] **Step 4: Start dev server and visually verify**

```bash
cd D:/Repositories/meepleai-monorepo-frontend/infra && make dev-core
```

Open browser to `http://localhost:3000` and verify card rendering on:
- Library page (grid cards)
- Sessions page (session cards)
- Chat page (chat cards)
- Agents page (agent cards)

- [ ] **Step 5: Open mockup HTML files and compare side-by-side**

Open `admin-mockups/meeple-card-real-app-render.html` in browser.
Open app at same viewport width.
Compare visual output — entity colors, shadows, hover effects, typography, badges, nav footer.

- [ ] **Step 6: Test mobile layout at < 768px**

Resize browser to 375px width. Verify hand+focused card layout appears on all pages with cards.

- [ ] **Step 7: Test dark theme**

Toggle to dark theme. Verify all MeepleCard tokens switch correctly.

---

### Task 20: Final Cleanup and PR

- [ ] **Step 1: Remove any remaining dead code**

```bash
cd apps/web && grep -r "expanded\|MeepleCardExpanded\|CartaEstesa" src/ --include="*.ts" --include="*.tsx" -l
```

Delete any remaining references to removed components.

- [ ] **Step 2: Final commit**

```bash
git add -A
git commit -m "chore(meeple-card): final cleanup — remove dead code references"
```

- [ ] **Step 3: Push and create PR**

```bash
git push -u origin feature/meeplecard-rewrite
```

Create PR to `main-dev` with title: `feat: MeepleCard rewrite from mockups (#XX)`

Body should cover:
- 9 entity types (removed 7, renamed 2)
- 5 variants (removed expanded)
- Mobile hand+focused layout (< 768px)
- Dark theme support
- 3D carousel (desktop only)
- Full rewrite from admin-mockup source of truth

---

## Summary

| Phase | Tasks | Description |
|-------|-------|-------------|
| 1. Foundation | 1-3 | Branch, delete legacy (expanded list), types, tokens |
| 2. Parts | 4-7 | Cover, badges, actions, rating, meta, nav footer, tags |
| 3. Variants | 8-10 | Grid, List, Compact, Featured, Hero (with entity glow via CSS var) |
| 4. Features | 11-12 | FlipCard, HoverPreview, Carousel3D, DragHandle, Swipe |
| 5. Mobile | 13 | Hand sidebar, focused card, mobile layout |
| 6. Router | 14 | MeepleCard router, compound API, skeleton, exports |
| 7. Consumers | 15a-15d | Adjacent dirs → mappers/configs → entity renames → import fixes |
| 8. Dark Theme | 16 | Verify existing next-themes + new CSS tokens work |
| 9. Testing | 17-18 | Unit tests for components, tokens, mobile layout |
| 10. Verify | 19-20 | Build, visual compare, mobile test, dark theme, PR |

**Total: 23 tasks (20 + 3 sub-tasks), ~10 phases**

### Review Fixes Applied

| Issue | Severity | Fix Applied |
|-------|----------|-------------|
| NavFooter hover CSS not applied | Critical | Added `group-hover/nav:border-[var(--nav-hover-border)]`, `bg`, `shadow` classes |
| GridCard glow uses dynamic Tailwind (purged) | Critical | Changed to `hover:outline-[var(--mc-glow)]` with CSS variable |
| Missing files in deletion list | Critical | Added 4 standalone files + 3 directories (browser, card-back, holo) |
| Adjacent directories would break | Critical | Added Task 15a for extra-meeple-card, deck-stack, entity-list-view, game-carousel, mana |
| `document`→`kb` rename risk | Critical | Added safe grep patterns and context-specific-only rules in Task 15c |
| Font loading task missing | Important | Documented: already loaded in layout.tsx as `--font-quicksand`/`--font-nunito` |
| `--font-quicksand` vs `--mc-font-heading` mismatch | Important | Plan uses `--font-quicksand` (matches layout.tsx) |
| Theme uses `.dark` not `[data-theme]` | Important | Changed all CSS to use `.dark` selector (matches next-themes) |
| MetaChips `bg-slate-100` not theme-aware | Important | Changed to `bg-[var(--mc-bg-muted)]` |
| Task 15 too large | Important | Split into 15a, 15b, 15c, 15d sub-tasks |
| Theme toggle location TBD | Important | Resolved: `ThemeToggle.tsx` already exists, Task 16 = verify only |
| Old props migration vague | Important | Added explicit prop migration table in Task 15d |
| FeaturedCard missing entity glow | Suggestion | Added `hover:outline-[var(--mc-glow)]` with CSS variable |
| NavFooter active inset shadow missing | Important | Added `group-active/nav:shadow-[inset_0_1px_2px_rgba(0,0,0,0.1)]` |
| Build ordering note missing | Important | Added "Build Note" to plan header |
