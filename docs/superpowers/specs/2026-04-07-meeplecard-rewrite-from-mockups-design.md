# MeepleCard Rewrite from Mockups — Design Spec

**Date**: 2026-04-07
**Branch**: `feature/meeplecard-rewrite` from `main-dev`
**Source of Truth**: `admin-mockups/*.html` (6 files)
**Approach**: Full rewrite — mockups define the target, legacy code is replaced

---

## 1. Overview

Rewrite the entire MeepleCard component system from scratch using the 6 HTML mockup files in `admin-mockups/` as the single source of truth for visual design, interactions, and component architecture. Remove all legacy MeepleCard code and entity types not defined in the mockups.

### Goals

- Pixel-level match between app components and mockup HTML renders
- Clean, minimal codebase with no legacy baggage
- Mobile-first hand+focused card layout on all pages (< 768px)
- Dark theme support built-in from day one
- 3D carousel on desktop only

### Non-Goals

- Adding new features beyond what mockups define
- Backend API changes
- Changing routing or page structure (only card rendering changes)

---

## 2. Entity Type System

### Definitive Entity Types (9)

| Entity | HSL Color | CSS Variable | Origin |
|--------|-----------|-------------|--------|
| `game` | 25 95% 45% | `--e-game` | Mockup |
| `player` | 262 83% 58% | `--e-player` | Mockup |
| `session` | 240 60% 55% | `--e-session` | Mockup |
| `agent` | 38 92% 50% | `--e-agent` | Mockup |
| `kb` | 210 40% 55% | `--e-kb` | Mockup (rename document→kb) |
| `chat` | 220 80% 55% | `--e-chat` | Mockup (rename chatSession→chat) |
| `event` | 350 89% 60% | `--e-event` | Mockup |
| `toolkit` | 142 70% 45% | `--e-toolkit` | Existing codebase |
| `tool` | 195 80% 50% | `--e-tool` | Existing codebase |

### Entity Types to Delete

Remove from entire codebase (types, components, references, tests):
- `collection`, `group`, `location`, `expansion`, `achievement`, `note`, `custom`

### Rename Mapping

| Old Name | New Name | Reason |
|----------|----------|--------|
| `document` | `kb` | A document is a KB of type document |
| `chatSession` | `chat` | Mockup uses `chat` |

---

## 3. Card Variants (5)

### Variant Specifications

#### Grid (default)
- **Width**: ~270px (responsive via CSS grid auto-fill)
- **Cover aspect ratio**: 7:10
- **Layout**: Flex column
- **Border radius**: 16px
- **Hover**: translateY(-6px), shadow warm-sm → warm-xl, entity-colored outline glow (2px, 0.4 alpha)
- **Left accent border**: 3-4px → 5-6px on hover, entity-colored
- **Cover effects**: Image scale(1.06) on hover, shimmer animation (800ms linear-gradient sweep)
- **Content**: Entity badge (top-left), title (Quicksand 700), subtitle (Nunito), rating stars, meta chips, quick actions (hover-reveal), nav footer
- **Source**: `meeple-card-real-app-render.html`, `meeple-card-summary-render.html`

#### List
- **Layout**: Flex row, full-width
- **Thumbnail**: 52-56px square
- **Entity indicator**: 8px colored dot (left)
- **Border radius**: 12-14px
- **Hover**: translateX(4px), shadow increase
- **Nav footer**: Inline, right-aligned
- **Content**: Title, subtitle, meta inline, quick actions on hover
- **Source**: `meeple-card-real-app-render.html`, `meeple-card-summary-render.html`

#### Compact
- **Layout**: Flex row, minimal height
- **Entity indicator**: 6px dot
- **Border radius**: 8px
- **Hover**: scale(1.02)
- **Content**: Title only, small typography (0.85rem)
- **Source**: `meeple-card-summary-render.html`

#### Featured
- **Width**: ~400px
- **Cover aspect ratio**: 16:9
- **Layout**: Flex column
- **Border radius**: 20px
- **Left accent border**: 4px → 6px on hover
- **Hover**: translateY(-6px), shadow warm-lg → warm-2xl
- **Content**: Full entity badge, title, subtitle, rating, meta, quick actions, nav footer with larger buttons
- **Source**: `meeple-card-nav-buttons-mockup.html`

#### Hero
- **Layout**: Flex column, full-bleed image
- **Min height**: 320px
- **Border radius**: 24px
- **Cover**: Full-height with bottom gradient overlay (entity-colored → transparent)
- **Text**: White on gradient, large typography
- **Hover**: scale(1.01)
- **Content**: Overlay text, accent ribbon
- **Source**: `meeple-card-summary-render.html`

### Variant to Delete

- `expanded` — remove variant, renderer, and all references

---

## 4. Design Token System

Extracted directly from mockup CSS. These replace current design-tokens.css MeepleCard-related tokens.

### Light Theme

```css
:root {
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
}
```

### Dark Theme

```css
[data-theme="dark"] {
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

### Typography

```css
:root {
  --mc-font-heading: 'Quicksand', sans-serif;
  --mc-font-body: 'Nunito', sans-serif;
}
```

Font weights used: 400, 500, 600, 700, 800 (badges/tags).

### Entity Colors (CSS Variables)

```css
:root {
  --e-game-h: 25;   --e-game-s: 95%;   --e-game-l: 45%;
  --e-player-h: 262; --e-player-s: 83%;  --e-player-l: 58%;
  --e-session-h: 240; --e-session-s: 60%; --e-session-l: 55%;
  --e-agent-h: 38;   --e-agent-s: 92%;   --e-agent-l: 50%;
  --e-kb-h: 210;     --e-kb-s: 40%;      --e-kb-l: 55%;
  --e-chat-h: 220;   --e-chat-s: 80%;    --e-chat-l: 55%;
  --e-event-h: 350;  --e-event-s: 89%;   --e-event-l: 60%;
  --e-toolkit-h: 142; --e-toolkit-s: 70%; --e-toolkit-l: 45%;
  --e-tool-h: 195;   --e-tool-s: 80%;    --e-tool-l: 50%;
}
```

---

## 5. Component Architecture

### Directory Structure

```
apps/web/src/components/ui/data-display/meeple-card/
├── MeepleCard.tsx              # Router → dispatches to variant
├── types.ts                    # MeepleCardProps, MeepleEntityType (9), MeepleCardVariant (5)
├── tokens.ts                   # Entity color map, variant dimensions, design token helpers
├── index.ts                    # Public API re-exports
│
├── variants/
│   ├── GridCard.tsx            # 7:10 grid card
│   ├── ListCard.tsx            # Horizontal list row
│   ├── CompactCard.tsx         # Minimal inline card
│   ├── FeaturedCard.tsx        # 16:9 featured card
│   └── HeroCard.tsx            # Full-bleed hero
│
├── parts/                      # Pure visual building blocks (stateless)
│   ├── Cover.tsx               # Image + shimmer + entity gradient overlay + placeholder
│   ├── EntityBadge.tsx         # Top-left entity type label (9px uppercase Quicksand 800)
│   ├── AccentBorder.tsx        # Left entity-colored border (3-4px → 5-6px hover)
│   ├── QuickActions.tsx        # Hover-reveal circular glass buttons (top-right)
│   ├── Rating.tsx              # Star rating (full/half/empty, 0.78rem)
│   ├── MetaChips.tsx           # Metadata chips (10px, bg #f1f5f9, border-radius 6px)
│   ├── TagStrip.tsx            # Vertical tag stack (left edge, max 3 visible + overflow)
│   ├── StatusBadge.tsx         # Status indicator (owned/wishlist/active/idle/etc.)
│   └── NavFooter.tsx           # Navigation footer with entity icon buttons + badges
│
├── features/                   # Interactive behaviors (stateful)
│   ├── FlipCard.tsx            # 3D flip animation wrapper
│   ├── HoverPreview.tsx        # Lazy-load preview popover (500ms delay)
│   ├── Carousel3D.tsx          # Desktop-only (>=768px) perspective carousel
│   ├── DragHandle.tsx          # Drag & drop handler
│   └── SwipeGesture.tsx        # Touch swipe detection (used by mobile layout)
│
├── mobile/                     # Mobile layout (< 768px)
│   ├── MobileCardLayout.tsx    # Orchestrator: hand + focused + swipe
│   ├── HandSidebar.tsx         # 36-44px vertical mini-card stack
│   ├── FocusedCard.tsx         # Expanded card in main area
│   └── HandCard.tsx            # Individual mini-card (28x40px) in sidebar
│
├── compound.tsx                # MeepleCards.Game, .Player, etc. (9 entity wrappers)
└── skeleton.tsx                # Loading skeleton
```

### Files to Delete (Legacy)

All current files under:
- `apps/web/src/components/ui/data-display/meeple-card/` (entire directory — replaced)
- `apps/web/src/components/ui/data-display/meeple-card-features/` (all 28 components)
- `apps/web/src/components/ui/data-display/meeple-card-styles.ts`
- `apps/web/src/components/ui/data-display/meeple-card-compound.tsx`
- `apps/web/src/components/ui/data-display/meeple-card-mobile-tags.tsx`
- `apps/web/src/components/ui/data-display/meeple-card/__tests__/` (replaced by new tests)

---

## 6. Parts Specifications

### 6.1 Cover

From `meeple-card-real-app-render.html` and `meeple-card-visual-test.html`:

```
Structure:
  <div class="cover" style="aspect-ratio: {ratio}">
    <img /> OR <div class="placeholder">{emoji}</div>
    <div class="shimmer" />          — hover animation
    <div class="gradient-overlay" /> — entity-colored bottom gradient
    <EntityBadge />                  — top-left
    <StatusBadge />                  — below entity badge
    <QuickActions />                 — top-right, hover-reveal
  </div>

Shimmer: linear-gradient(105deg, transparent 40%, rgba(255,255,255,0.25) 50%, transparent 60%)
         translateX(-100%) → translateX(100%) in 800ms ease-out

Gradient: linear-gradient(to top, hsla(entity, 0.15), transparent 60%)

Placeholder: Centered emoji (3rem, opacity 0.5) on entity-tinted background
```

### 6.2 EntityBadge

```
Position: absolute top:8px left:10px z-index:10
Padding: 2px 8px
Border-radius: 6px
Font: Quicksand 800, 9px, uppercase, letter-spacing 0.04em
Color: white
Background: hsl(entity color)
Box-shadow: 0 1px 3px rgba(0,0,0,0.2)
```

### 6.3 AccentBorder

```
Position: absolute left:0 top:0 bottom:0
Width: 3-4px (variant-dependent), grows to 5-6px on card hover
Background: hsl(entity color)
Transition: width 0.25s ease
Z-index: 5
```

### 6.4 QuickActions

From `meeple-card-visual-test.html`:

```
Position: absolute top:8px right:8px z-index:10
Direction: column, gap:6px
Opacity: 0 → 1 on card hover (transition 0.3s)

Button:
  Width/Height: 30px
  Border-radius: 50%
  Background: rgba(255,255,255,0.85), backdrop-filter blur(8px)
  Border: 1px solid rgba(255,255,255,0.6)
  Font-size: 14px (emoji/icon)
  Hover: scale(1.1), shadow-md
```

### 6.5 Rating

```
Display: flex, align-items center, gap 2px
Star size: 0.78rem
Full star: #f59e0b
Half star: rgba(245,158,11,0.5)
Empty star: rgba(0,0,0,0.12)
Value label: 0.7rem, font-weight 600, text-secondary, margin-left 4px
```

### 6.6 MetaChips

```
Display: flex, flex-wrap, gap 6px
Chip:
  Font-size: 10px
  Color: text-secondary
  Background: #f1f5f9
  Padding: 2px 8px
  Border-radius: 6px
  Display: flex, align-items center, gap 3px
  Icon: 11px emoji/icon before text
```

### 6.7 NavFooter

From `meeple-card-nav-buttons-mockup.html` (comprehensive):

```
Container:
  Background: var(--mc-nav-footer-bg)
  Backdrop-filter: blur(8px)
  Border-top: 1px solid var(--mc-border-light)
  Padding: 6px 10px
  Display: flex, justify-content center, gap varies by variant

Button Structure:
  <button class="nav-btn" data-entity="{type}">
    <div class="nav-btn-icon">{icon}</div>  — 28-32px circle
    <span class="nav-btn-label">{label}</span> — 7-8px uppercase
    <span class="nav-badge">{count}</span>  — optional count badge
    <span class="nav-plus">+</span>         — optional plus indicator
  </button>

Icon circle:
  Width/Height: 28px (grid), 32px (featured)
  Border-radius: 50%
  Background: var(--mc-nav-icon-bg)
  Border: 1px solid var(--mc-nav-icon-border)
  Font-size: 13-15px (emoji)
  Transition: transform 0.2s, box-shadow 0.2s, border-color 0.2s

Hover:
  Border-color: hsl(entity, 0.4)
  Background: hsl(entity, 0.08)
  Transform: scale(1.08)
  Box-shadow: 0 2px 8px hsl(entity, 0.15)

Active (pressed):
  Transform: scale(0.95)
  Box-shadow: inset 0 1px 2px rgba(0,0,0,0.1)

Disabled:
  Opacity: 0.45
  Cursor: not-allowed
  No hover effects

Badge:
  Position: absolute top:-4px right:-4px
  Min-width: 16px, height: 16px
  Border-radius: 8px
  Background: hsl(entity)
  Color: white
  Font-size: 8px, font-weight 700
  Box-shadow: 0 1px 3px rgba(0,0,0,0.15)

Plus overlay:
  Position: absolute bottom:-2px right:-2px
  Width: 14px, height: 14px
  Background: hsl(entity)
  Color: white
  Font-size: 8px, font-weight 800
  Border-radius: 50%

Label:
  Font-size: 7-8px
  Font-weight: 600-700
  Text-transform: uppercase
  Letter-spacing: 0.04em
  Color: text-muted → text-secondary on hover
```

### 6.8 StatusBadge

From `meeple-card-real-app-render.html`:

```
Position: absolute top:28px left:10px z-index:10
Padding: 1px 7px
Border-radius: 5px
Font-size: 9px, font-weight 700

Status colors:
  owned:      bg #dcfce7, color #166534
  wishlist:   bg #fef3c7, color #92400e
  active:     bg #dcfce7, color #166534
  idle:       bg #f1f5f9, color #64748b
  archived:   bg #f1f5f9, color #64748b
  processing: bg #dbeafe, color #1e40af
  indexed:    bg #dcfce7, color #166534
  failed:     bg #fee2e2, color #991b1b
  inprogress: bg #dbeafe, color #1e40af
  setup:      bg #fef3c7, color #92400e
  completed:  bg #f3e8ff, color #6b21a8
  paused:     bg #fef3c7, color #92400e
```

### 6.9 TagStrip

```
Position: left edge of card, top-left
Max visible: 3 (configurable)
Overflow: "+N" counter
Tag:
  Font-size: 9px
  Font-weight: 600
  Padding: 1px 6px
  Border-radius: 4px
  Background: entity-tinted (low alpha)
```

---

## 7. Mobile Layout (< 768px)

From `mobile-card-layout-mockup.html` and `mobile-card-entity-types.html`.

### 7.1 Activation

Whenever a MeepleCard grid or list appears on a page, and viewport < 768px, the layout automatically switches to hand+focused mode.

### 7.2 MobileCardLayout

```
Display: flex
Height: 100% of available space (flex:1 in parent)

Structure:
  <MobileCardLayout cards={cards} activeId={selectedId}>
    <HandSidebar />        — left, 36-44px width
    <FocusedCard />        — right, flex:1
  </MobileCardLayout>
```

### 7.3 HandSidebar

```
Width: 36-44px
Flex-shrink: 0
Display: flex, flex-direction column, align-items center
Padding: 6px 0
Gap: 5px
Background: linear-gradient(90deg, rgba(0,0,0,0.02), transparent)

Label (top):
  Writing-mode: vertical-rl
  Transform: rotate(180deg)
  Font: Quicksand 7px, 700, uppercase, letter-spacing 0.08em
  Color: text-muted
```

### 7.4 HandCard

```
Width: 28px
Height: 40px
Border-radius: 6px
Background: var(--mc-bg-card)
Border: 1px solid var(--mc-border)
Box-shadow: var(--mc-shadow-sm)
Cursor: pointer
Overflow: hidden
Transition: transform 0.2s

Left edge indicator:
  Position: absolute left:0 top:0 bottom:0
  Width: 3px
  Background: hsl(entity)

Mini icon: centered, 11px
Mini title: absolute bottom:1px left:3px, 4px font, Quicksand 700

Hover: scale(1.1)
Active (selected): outline 2px solid hsl(entity), outline-offset 1px, scale(1.12)
```

### 7.5 FocusedCard

```
Flex: 1
Display: flex, flex-direction column
Padding: 6px 8px 6px 3px

Card container:
  Border-radius: 14px
  Overflow: hidden
  Background: var(--mc-bg-card)
  Backdrop-filter: blur(12px)
  Border: 1px solid var(--mc-border)
  Box-shadow: var(--mc-shadow-xl)
  Outline: 2px solid transparent, offset 2px

Top accent bar: height 3px, full-width, entity-colored

Cover:
  Flex-shrink: 0
  Overflow: hidden
  Gradient overlay: linear-gradient(to top, var(--mc-bg-card), transparent) bottom 50%

Badge: top:8px left:8px, border-radius 16px, Quicksand 8px 700 uppercase
Rating: top:8px right:8px, bg rgba(0,0,0,0.5), color #fbbf24

Body:
  Flex: 1
  Padding: 10px 12px
  Display: flex, flex-direction column, gap 5px
  Overflow-y: auto

Title: Quicksand 1rem 700, line-height 1.2
Subtitle: 0.72rem, text-secondary
Meta chips: 10px, bg bg-muted, border-radius 16px, padding 3px 8px

Quick actions:
  Display: flex, gap 6px
  Margin-top: auto
  Padding-top: 6px
  Button: 32px circle, glass morphism, hover scale(1.1)
  Primary button: entity-colored, white text
```

### 7.6 Swipe Navigation

Touch gesture: horizontal swipe on FocusedCard area changes active card in hand.
- Threshold: 50px minimum swipe distance
- Animation: slide transition (200ms ease-out)
- Feedback: active hand card updates immediately

---

## 8. Features (Interactive Behaviors)

### 8.1 FlipCard

Click-to-rotate 3D animation:
- Container: `perspective(1000px)`
- Front/Back: `backface-visibility: hidden`, `transform: rotateY(0/180deg)`
- Transition: 600ms cubic-bezier(0.4, 0, 0.2, 1)
- Trigger: click on card or dedicated button (configurable via prop)
- Back content: entity-specific (session stats, game KB preview)

### 8.2 HoverPreview

Lazy-loaded preview popover on hover:
- Delay: 500ms before showing
- Content: fetched via callback prop
- Position: adjacent to card (auto-positioned to avoid viewport edges)
- Dismiss: on mouse leave

### 8.3 Carousel3D (Desktop Only, >=768px)

From `meeple-card-visual-test.html`:
- Container: `perspective(1200px)`
- Active card: centered, full size
- Adjacent cards: `translateX(±35%)`, `rotateY(∓5deg)`, `filter: blur(2px)`, reduced opacity
- Navigation: arrow buttons or keyboard left/right
- Transition: 400ms ease

### 8.4 DragHandle

Drag & drop for reordering cards:
- Handle icon visible on hover
- Ghost preview during drag
- Drop zone highlighting

### 8.5 SwipeGesture

Shared touch gesture utility:
- Horizontal swipe detection with configurable threshold
- Used by mobile layout and carousel
- Passive touch listeners for performance

---

## 9. Dark Theme Implementation

### Approach

CSS custom properties with `[data-theme="dark"]` selector, matching mockup definitions exactly.

### Theme Toggle

- Attribute-based: `<html data-theme="light|dark">`
- Persisted in localStorage
- System preference detection via `prefers-color-scheme`
- Toggle component in app header

### Coverage

All MeepleCard tokens are theme-aware (see Section 4). Components use only CSS variables, never hardcoded colors.

---

## 10. Cleanup Scope

### Delete Entirely

| Path | Reason |
|------|--------|
| `meeple-card/` (current) | Replaced by rewrite |
| `meeple-card-features/` (all 28 files) | Replaced by new parts/ and features/ |
| `meeple-card-styles.ts` | Replaced by tokens.ts + CSS |
| `meeple-card-compound.tsx` | Replaced by new compound.tsx |
| `meeple-card-mobile-tags.tsx` | Replaced by mobile layout |
| `meeple-card/__tests__/` | Replaced by new tests |

### Entity Type References to Remove

Search and remove all references to deleted entity types across:
- TypeScript type definitions
- Component props and switch cases
- CSS/style entity color maps
- Test fixtures and mock data
- Storybook stories (if any)

### Rename References

Update all codebase references:
- `document` entity → `kb`
- `chatSession` entity → `chat`

---

## 11. Public API

### Exports from `index.ts`

```typescript
// Main component
export { MeepleCard } from './MeepleCard';

// Compound API
export { MeepleCards } from './compound';
// Usage: <MeepleCards.Game />, <MeepleCards.Player />, etc.

// Types
export type { MeepleCardProps, MeepleEntityType, MeepleCardVariant } from './types';

// Mobile layout (for page-level integration)
export { MobileCardLayout } from './mobile/MobileCardLayout';

// Features (opt-in wrappers)
export { FlipCard } from './features/FlipCard';
export { HoverPreview } from './features/HoverPreview';
export { Carousel3D } from './features/Carousel3D';

// Skeleton
export { MeepleCardSkeleton } from './skeleton';

// Tokens (for consumers that need entity colors)
export { entityColors, entityLabel } from './tokens';
```

### MeepleCardProps (Core)

```typescript
type MeepleEntityType = 'game' | 'player' | 'session' | 'agent' | 'kb' | 'chat' | 'event' | 'toolkit' | 'tool';
type MeepleCardVariant = 'grid' | 'list' | 'compact' | 'featured' | 'hero';

interface MeepleCardProps {
  // Required
  entity: MeepleEntityType;
  title: string;
  variant?: MeepleCardVariant; // default: 'grid'

  // Content
  id?: string;
  subtitle?: string;
  imageUrl?: string;
  rating?: number;
  ratingMax?: number;
  metadata?: MeepleCardMetadata[];
  tags?: string[];
  status?: string;
  badge?: string;

  // Actions
  actions?: MeepleCardAction[];
  navItems?: NavFooterItem[];
  onClick?: () => void;

  // Features (opt-in)
  flippable?: boolean;
  flipBackContent?: React.ReactNode;
  draggable?: boolean;
  showQuickActions?: boolean;

  // Styling
  className?: string;
  customColor?: string; // HSL override
}
```

---

## 12. Integration Points

### Pages That Use MeepleCard

All pages rendering entity cards must be updated to:
1. Use new `MeepleCard` import
2. Map to new entity type names (`document` → `kb`, `chatSession` → `chat`)
3. Remove references to deleted entity types
4. Wrap card grids with responsive mobile layout detection

### Grid Container

Desktop (>=768px): standard CSS grid with `auto-fill, minmax(230px, 1fr)`
Mobile (<768px): `MobileCardLayout` automatically replaces grid

### Font Loading

Ensure Quicksand and Nunito are loaded in `_app.tsx` or `layout.tsx`:
```html
<link href="https://fonts.googleapis.com/css2?family=Quicksand:wght@400;500;600;700;800&family=Nunito:wght@400;500;600;700;800&display=swap" rel="stylesheet">
```

---

## 13. Testing Strategy

### Unit Tests (Vitest)

Per-component tests for:
- Each variant renders correctly with all entity types
- Hover states apply correct transforms and shadows
- Entity badge shows correct color and label
- NavFooter renders correct buttons with badges
- Status badges show correct colors
- Dark theme token application

### Integration Tests

- Mobile layout activates at < 768px
- Hand sidebar selection updates focused card
- Swipe gesture navigates between cards
- Carousel3D only renders on desktop
- FlipCard animation triggers correctly
- Entity type rename (document→kb, chatSession→chat) works across app

### Visual Regression

- Open mockup HTML files in browser, screenshot
- Open app locally at same viewport sizes, screenshot
- Compare pixel-level match

---

## 14. Acceptance Criteria

1. All 6 mockup HTML files can be opened side-by-side with the app, and the visual output matches
2. 9 entity types only — no references to deleted types compile
3. 5 variants only — no `expanded` references
4. Mobile (< 768px): hand+focused layout on all pages with cards
5. Desktop (>= 768px): grid/list/featured/hero with 3D carousel option
6. Dark theme toggleable and visually matching mockup dark tokens
7. All existing app functionality (navigation, data loading, actions) preserved
8. Zero TypeScript compilation errors
9. All tests pass
