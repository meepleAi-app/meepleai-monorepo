# MeepleAI Visual Redesign — Neon Holo

**Date**: 2026-03-15
**Status**: Draft
**Scope**: Full visual redesign — identity, card system, page layouts
**Depends on**: `docs/superpowers/specs/2026-03-15-meeplecard-redesign-mana-system.md` (Phase 1 must complete first for 16 entity types)
**Supersedes**: Typography section of Mana System spec (Nunito → Inter for body/labels)

## Overview

Complete visual redesign of MeepleAI's frontend, moving from the current warm/glassmorphism aesthetic to a **Neon Arcade × Holographic Collector** direction. The design is 70% Playful / 30% Premium — high energy with quality finishes. Every card gets full holographic treatment. The Mana System (16 entity types with SVG symbols) is the visual DNA.

The redesign touches three layers:
1. **Identity** — colors, typography, dark canvas, glow language
2. **Cards** — MeepleCard component with neon glow + holo effects + MTG mana integration
3. **Pages** — Bento Grid dashboard, Horizontal Shelves catalog

## Design Principles

1. **Dark canvas, not dark mode** — `#0e0c18` purple-black is the platform identity, not just a theme toggle
2. **Glow, not borders** — Entity identity communicated through colored glow/shadow, not solid borders
3. **Mana is the DNA** — The same SVG symbol appears at every scale: badge, cost bar, footer, drawer, navigation
4. **Every card is precious** — Full holographic effect on all cards, no rarity tiers. Democratic visual treatment
5. **Navy > pure black** — Blue-shifted dark backgrounds add depth and warmth (research-backed: all premium gaming platforms use this)
6. **60/30/10 rule** — 60% dark surfaces, 30% neutral/muted, 10% vibrant entity accents

## Dark Mode Strategy

**Dark-first, light as fallback.**

The full Neon Holo experience is designed for dark mode. Light mode will be a functional adaptation:
- Mana symbols retain their radial gradients and colors
- Glow effects become warm shadows (existing `--shadow-warm-*` system)
- Holographic shimmer becomes subtle iridescent border
- Card backgrounds become white/warm-white with entity-tinted glassmorphism

Light mode is not degraded — it's a different reading of the same design language. But dark is the primary experience.

## Color System

### Platform Palette

| Token | Value | Usage |
|-------|-------|-------|
| `--bg-base` | `#0e0c18` | App background (purple-black) |
| `--bg-surface` | `#16131f` | Card/panel base |
| `--bg-surface-end` | `#1a1628` | Card gradient end |
| `--bg-elevated` | `#1e1a2a` | Modals, dropdowns, popovers |
| `--border-default` | `rgba(255,255,255,0.06)` | Default borders |
| `--border-hover` | Entity-specific at 30% opacity | Hover state borders |
| `--text-primary` | `#f0ece4` | Headings, titles (warm off-white) |
| `--text-secondary` | `#7a7484` | Subtitles, metadata |
| `--text-muted` | `#555` | Captions, counts |

### Entity Colors (16 Mana Types)

Unchanged from Mana System spec. Each color is used for:
- Mana orb radial gradient (lighter at 35% 35%, darker at edges)
- Outer ring border at 35% opacity
- Drop shadow at 35% opacity
- Card accent bar gradient (top lighter, bottom darker)
- Hover glow box-shadow
- ManaBadge background at 80-85% opacity
- Tag/chip background at 10-12% opacity

| Entity | HSL | Name | Tier |
|--------|-----|------|------|
| Game | `25 95% 45%` | Orange | Core |
| Session | `240 60% 55%` | Indigo | Core |
| Player | `262 83% 58%` | Purple | Core |
| Event | `350 89% 60%` | Rose | Core |
| Collection | `20 70% 42%` | Copper | Social |
| Group | `280 50% 48%` | Warm Violet | Social |
| Location | `200 55% 45%` | Slate Cyan | Social |
| Expansion | `290 65% 50%` | Magenta | Social |
| Agent | `38 92% 50%` | Amber | AI |
| KB | `174 60% 40%` | Teal | AI |
| Chat | `220 80% 55%` | Blue | AI |
| Note | `40 30% 42%` | Warm Gray | AI |
| Toolkit | `142 70% 45%` | Green | Tools |
| Tool | `195 80% 50%` | Sky | Tools |
| Achievement | `45 90% 48%` | Gold | Tools |
| Custom | `220 15% 45%` | Silver | Meta |

## Typography

| Role | Font | Weight | Usage |
|------|------|--------|-------|
| **Titles** | Quicksand | 700 | Card titles, section headings, page titles |
| **Subtitles** | Quicksand | 600 | Badge labels, shelf titles |
| **Body** | Inter | 400 | Descriptions, metadata, UI text |
| **Labels** | Inter | 500-600 | Tags, chips, status indicators |
| **Captions** | Inter | 400 | Muted small text, counts |

> **Note**: This supersedes the Mana System spec's typography section which uses Nunito for body/labels.
> The Mana System spec's Quicksand usage for titles is unchanged.

### Font Loading (implementation detail)

Replace Nunito with Inter in `apps/web/src/app/layout.tsx`:

```typescript
import { Quicksand, Inter } from 'next/font/google';

const quicksand = Quicksand({ subsets: ['latin'], variable: '--font-quicksand' });
const inter = Inter({ subsets: ['latin'], variable: '--font-inter' });

// In <body>: className={`${quicksand.variable} ${inter.variable}`}
```

Update `apps/web/tailwind.config.js`:
```javascript
fontFamily: {
  quicksand: ['var(--font-quicksand)', ...defaultTheme.fontFamily.sans],
  inter: ['var(--font-inter)', ...defaultTheme.fontFamily.sans],
  sans: ['var(--font-inter)', ...defaultTheme.fontFamily.sans], // Inter as default body
}
```

Update `apps/web/src/styles/design-tokens.css`:
```css
--font-body: var(--font-inter);
/* --font-nunito: removed */
```

### Type Scale (dark mode)

| Element | Size | Weight | Color | Letter-spacing |
|---------|------|--------|-------|---------------|
| Page title | 24px | QS 700 | `--text-primary` | 0 |
| Section heading | 16px | QS 700 | Entity color (lightened) | 2px uppercase |
| Card title | 17px | QS 700 | `--text-primary` | 0 |
| Card subtitle | 12px | Inter 400 | `--text-secondary` | 0 |
| ManaBadge label | 9px | QS 700 | `#fff` | 1.5px uppercase |
| Tag chip | 9px | Inter 600 | Entity color (lightened) | 0 |
| Mana link count | 11px | Inter 600 | `--text-muted` | 0 |

## Mana Symbol System

### SVG Icon Set (16 icons)

Each entity has a dedicated SVG glyph rendered inside a circular mana orb:

| Entity | Icon | Description |
|--------|------|-------------|
| Game | D20 polyhedron | Strategy and chance |
| Session | Hourglass | Time and play |
| Player | Chess pawn | The player piece |
| Event | Star burst | Special moments |
| Collection | Treasure chest | Your deck of games |
| Group | People silhouettes | Your fixed party |
| Location | Map pin | Where you play |
| Expansion | Fanned cards | Game expansions |
| Agent | Lightning bolt | Active AI intelligence |
| KB | Scroll | Rules and lore |
| Chat | Speech bubble | AI conversation |
| Note | Pencil | Personal annotations |
| Toolkit | Gear | Tool set |
| Tool | Wrench | Single tool |
| Achievement | Trophy | Earned reward |
| Custom | Diamond | Wildcard type |

### Mana Orb Rendering

```
┌─────────────────────────┐
│  radial-gradient          │
│  center: 35% 35%         │
│  inner: entity HSL +10L  │
│  outer: entity HSL -10L  │
│                           │
│  outer ring: 2px border   │
│    entity color @ 35%     │
│                           │
│  inner highlight: 1px     │
│    rgba(255,255,255,0.08) │
│                           │
│  drop shadow:             │
│    0 3px 12px             │
│    entity color @ 35%     │
│                           │
│  icon: centered SVG       │
│    fill: #fff             │
│    drop-shadow: 0 1px 2px │
│    rgba(0,0,0,0.5)        │
└─────────────────────────┘
```

### Sizes

> **Canonical sizes** — these supersede the Mana System spec's sizes (64px/28px/20px) for visual consistency.

| Size | Diameter | Border | Shadow | Usage |
|------|----------|--------|--------|-------|
| Full | 56px | 2px | `0 3px 12px` | Mana grid, drawer header |
| Medium | 26px | 1.5px | `0 2px 8px` | ManaBadge, entity tag |
| Mini | 18px | 1px | `0 1px 4px` | Cost bar, link footer, inline |

## Card Anatomy — Front

```
┌──────────────────────────────┐
│ [ManaBadge]    [ManaCostBar] │  ← top-left: entity mana + label
│                [StatusChip]  │  ← top-right: related entity mana pips (MTG style)
│▌                              │  ← Entity accent bar (4px, left edge)
│▌    ┌──────────────────┐      │
│▌    │                  │      │  ← Cover image (aspect 7:10 portrait, grid variant)
│▌    │   COVER IMAGE    │      │     + gradient fade to card bg
│▌    │   + HOLO LAYERS  │      │     + holographic overlay layers
│▌    │                  │      │
│▌    └──────────────────┘      │
│                                │
│  [tag] [tag] [tag]            │  ← Colored entity-tinted chips
│  Title                        │  ← Quicksand 700, 17px
│  Subtitle                     │  ← Inter 400, 12px, muted
│  ★★★★☆ 8.4                   │  ← Rating (gold stars + number)
│───────────────────────────────│
│  (⏳)(📜)(⚡)(🃏)       +2   │  ← ManaLinkFooter: mini pips
└──────────────────────────────┘
```

### ManaBadge (top-left)

- Entity mana orb (Medium, 26px) + uppercase label
- Background: entity color at 80-85% opacity
- `backdrop-filter: blur(10px)`
- Border: `1px solid rgba(255,255,255,0.1)`
- Border-radius: 20px

### ManaCostBar (top-right, MTG style)

- Row of Mini mana pips (18px) showing related entity types
- Background: `rgba(0,0,0,0.5)` with `backdrop-filter: blur(8px)`
- Border-radius: 14px
- **Static declaration**: Shows which entity types this card CAN relate to (entity-type-level, not instance-level). Defined per entity type in `mana-config.ts` relationship map.
- **Not interactive**: Purely decorative/informational — no click handler, no deck stack
- **Distinction from ManaLinkFooter**: ManaCostBar = "what this entity connects to" (static). ManaLinkFooter = "actual linked entities for this instance" (dynamic, clickable, opens deck stack)
- Example: Game card always shows Session + KB + Agent pips (because games relate to those types). ManaLinkFooter shows the specific 3 sessions, 1 KB, and 1 agent linked to this particular game.

### StatusChip (below cost bar, conditional)

- Only visible for active states (Owned, Active, In Progress, Error)
- Background: entity color at 12-15% opacity
- Border: entity color at 25% opacity
- Font: Inter 600, 9px, uppercase
- Reinforced by StatusGlow border animation

### Entity Accent Bar

- 4px vertical bar, left edge, full height
- Gradient: entity color lighter (top) → darker (bottom)
- Expands to 5px on hover

### ManaLinkFooter

- Row of Mini mana pips (18px) for related entities
- Each pip is clickable → opens deck stack (from Mana System spec)
- `+N` count for overflow
- Separator: `1px solid rgba(255,255,255,0.05)` top border
- Pips scale to 1.3x on individual hover

## Holographic Effect System

All cards get the full holographic treatment. Three overlay layers activate on hover:

### Layer 1: Rainbow Gradient (z-index: 7)

Implemented as `::before` pseudo-element on `.meeple-card`, positioned `absolute inset 0`.

```css
.meeple-card .holo-rainbow {
  position: absolute;
  inset: 0;
  border-radius: inherit;
  pointer-events: none;
  z-index: 7;
  background: linear-gradient(
    115deg,
    transparent 20%,
    rgba(236,110,173,0.12) 32%,
    rgba(52,148,230,0.12) 40%,
    rgba(103,232,138,0.10) 48%,
    rgba(233,211,98,0.12) 56%,
    rgba(236,110,173,0.10) 64%,
    transparent 80%
  );
  /* NOTE: Use `screen` blend mode, not `color-dodge`.
     color-dodge is imperceptible on dark canvas (#0e0c18).
     screen additively lightens, producing visible rainbow on dark backgrounds. */
  mix-blend-mode: screen;
  opacity: 0;
  transition: opacity 0.5s;
  animation: holo-slide 4s ease-in-out infinite paused;
}
.meeple-card:hover .holo-rainbow {
  opacity: 1;
  animation-play-state: running;
}
```

> **Stacking context note**: The card uses `overflow: hidden` and `border-radius`. Holo layers must be children of the card (not pseudo-elements on ancestors) to composite correctly within the card's stacking context. This is intentional — the holo effect blends against the card surface, not the page background.

### Layer 2: Sparkle Points (z-index: 8)

```css
background:
  radial-gradient(circle at 30% 20%, rgba(255,255,255,0.06) 0%, transparent 30%),
  radial-gradient(circle at 70% 60%, rgba(255,255,255,0.04) 0%, transparent 25%),
  radial-gradient(circle at 50% 80%, rgba(255,255,255,0.03) 0%, transparent 20%);
```

### Layer 3: Rotating Rainbow Border (z-index: 9)

Implemented as a dedicated `<div className="holo-border">` child element. Uses the mask-composite technique to create an animated border from a conic-gradient:

```css
.meeple-card .holo-border {
  position: absolute;
  inset: -2px;                    /* overflow by border width */
  border-radius: 20px;            /* card radius + 2px */
  pointer-events: none;
  z-index: 9;
  opacity: 0;
  transition: opacity 0.5s;

  /* The conic gradient IS the border color */
  background: conic-gradient(
    from 0deg,
    rgba(167,139,250,0.25),
    rgba(96,165,250,0.2),
    rgba(52,211,153,0.18),
    rgba(251,191,36,0.22),
    rgba(255,107,107,0.18),
    rgba(236,72,153,0.2),
    rgba(167,139,250,0.25)
  );

  /* Mask trick: show only the 2px border ring, punch out the interior */
  -webkit-mask:
    linear-gradient(#fff 0 0) content-box,
    linear-gradient(#fff 0 0);
  -webkit-mask-composite: xor;
  mask-composite: exclude;
  padding: 2px;                   /* this IS the border width */

  animation: holo-rotate 6s linear infinite paused;
}
.meeple-card:hover .holo-border {
  opacity: 1;
  animation-play-state: running;
}

@keyframes holo-rotate {
  from { filter: hue-rotate(0deg); }
  to { filter: hue-rotate(360deg); }
}
```

> **Browser support**: `mask-composite: exclude` is supported in all modern browsers (Chrome 120+, Firefox 53+, Safari 15.4+). The `-webkit-mask-composite: xor` fallback covers older WebKit.

### Hover Transform

```css
transform: translateY(-8px) rotateX(2deg) rotateY(-3deg);
transition: all 0.5s cubic-bezier(0.4,0,0.2,1);
```

### Entity Glow (hover box-shadow)

```css
box-shadow:
  0 12px 40px rgba(ENTITY_COLOR, 0.15),
  0 0 40px rgba(ENTITY_COLOR, 0.08),
  0 0 80px rgba(167,139,250, 0.05),  /* holo violet ambient */
  0 0 100px rgba(96,165,250, 0.04);  /* holo blue ambient */
```

### StatusGlow (active entities)

```css
/* Pulsing glow for active states */
@keyframes entity-pulse {
  0%, 100% { box-shadow: 0 4px 20px rgba(0,0,0,0.4), 0 0 15px rgba(ENTITY_COLOR, 0.04); }
  50% { box-shadow: 0 4px 20px rgba(0,0,0,0.4), 0 0 25px rgba(ENTITY_COLOR, 0.1); }
}
animation: entity-pulse 3s ease-in-out infinite;
```

### Reduced Motion

All holographic animations respect `prefers-reduced-motion: reduce`:
- Rainbow gradient: static position (no slide)
- Border rotation: paused
- Hover transform: translateY only (no 3D tilt)
- StatusGlow: static glow (no pulse)

## Page Layouts

### Dashboard — Bento Grid

The dashboard uses an asymmetric bento grid layout that creates visual hierarchy:

```
grid-template-columns: repeat(4, 1fr);
grid-template-areas:
  "feat feat   agent  session"
  "feat feat   event  kb"
  "coll coll   player game";

┌──────────────────┬──────────┬──────────┐
│                   │  AGENT   │ SESSION  │
│   FEATURED GAME   │  card    │ card     │
│   (2col × 2row)   │          │          │
│                   ├──────────┼──────────┤
│                   │  EVENT   │   KB     │
│                   │  card    │  card    │
├──────────────────┼──────────┼──────────┤
│  COLLECTION       │ PLAYER   │  GAME    │
│  (2col, horiz)    │  card    │  card    │
└──────────────────┴──────────┴──────────┘
```

**Rules:**
- Featured card: `grid-area: feat` (span 2 cols, 2 rows) — uses `featured` variant (16:9 cover aspect)
- Standard cards: default `grid` variant (7:10 portrait cover aspect)
- Wide cards (Collection): `grid-area: coll` (span 2 cols) — horizontal layout with mini card stack
- Grid: `grid-template-columns: repeat(4, 1fr)` with `gap: 24px`
- Responsive: 4 cols (desktop) → 2 cols (tablet, featured keeps span 2) → 1 col (mobile, all full-width)
- Content: Mix of entity types, dynamically populated from user's most active/recent data

### Catalog/Library — Horizontal Shelves

The catalog uses horizontal shelf rows, each representing an entity type or category:

```
🎲 My Games (47) ──────────────────────────────→
┌────┐ ┌────┐ ┌────┐ ┌────┐ ┌────┐ ┌────┐  →
│    │ │    │ │    │ │    │ │    │ │    │
└────┘ └────┘ └────┘ └────┘ └────┘ └────┘

⏳ Recent Sessions (12) ───────────────────────→
┌────┐ ┌────┐ ┌────┐ ┌────┐  →
│    │ │    │ │    │ │    │
└────┘ └────┘ └────┘ └────┘

⚡ AI Agents (3) ──────────────────────────────→
┌────┐ ┌────┐ ┌────┐
│    │ │    │ │    │
└────┘ └────┘ └────┘
```

**Rules:**
- Shelf header: Mana orb (Mini, 18px) + Quicksand 700 title in entity color + item count
- Cards: horizontal scroll with `overflow-x: auto`, `scroll-snap-type: x mandatory`
- Card width: fixed `min-width: 280px` for consistency
- Scroll indicators: fade gradient on right edge when more content available
- Shelf order: User-configurable, default by activity recency
- Empty shelves: Hidden (don't show empty categories)

## Light Mode Adaptation

Activated via absence of `.dark` class (existing `darkMode: ['class']` in Tailwind config). Dark is the default; light is opt-in.

### Light Mode Token Values

| Token | Dark Value | Light Value |
|-------|-----------|-------------|
| `--bg-base` | `#0e0c18` | `#faf9f7` (warm white) |
| `--bg-surface` | `#16131f` | `#ffffff` |
| `--bg-surface-end` | `#1a1628` | `#f8f7f5` |
| `--bg-elevated` | `#1e1a2a` | `#ffffff` |
| `--border-default` | `rgba(255,255,255,0.06)` | `rgba(0,0,0,0.08)` |
| `--border-hover` | Entity color @ 30% | Entity color @ 20% |
| `--text-primary` | `#f0ece4` | `#1a1a1a` |
| `--text-secondary` | `#7a7484` | `#6b6b6b` (WCAG AA on white) |
| `--text-muted` | `#555` | `#999` (WCAG AA at 16px+) |

### Light Mode Effect Substitutions

| Effect | Dark | Light |
|--------|------|-------|
| Entity glow box-shadow | `0 0 40px rgba(EC, 0.08)` | `--shadow-warm-lg` (existing) |
| Holo rainbow layer | `mix-blend-mode: screen` | Disabled (hidden) |
| Holo sparkle layer | Radial white glows | Disabled (hidden) |
| Holo rotating border | Conic rainbow at 25% | Conic rainbow at 8% (subtle iridescence) |
| `entity-pulse` glow | `rgba(EC, 0.04–0.1)` | `rgba(EC, 0.08–0.15)` (stronger for visibility on white) |
| ManaBadge backdrop | Entity color @ 85% | Entity color @ 90% + `box-shadow: 0 1px 3px rgba(0,0,0,0.1)` |
| Card surface | `backdrop-filter` glass on dark | `rgba(255,255,255,0.9)` + `--shadow-warm-md` |

### CSS Selector Strategy

```css
/* Dark mode (default) */
:root {
  --bg-base: #0e0c18;
  /* ... all dark tokens ... */
}

/* Light mode override */
:root:not(.dark) {
  --bg-base: #faf9f7;
  /* ... all light tokens ... */
}

/* Disable heavy holo effects in light mode */
:root:not(.dark) .holo-rainbow,
:root:not(.dark) .holo-sparkle {
  display: none;
}
:root:not(.dark) .holo-border {
  opacity: 0;
}
:root:not(.dark) .meeple-card:hover .holo-border {
  opacity: 0.4; /* subtle iridescent border only */
}
```

The mana symbols, card anatomy, page layouts, and typography remain identical. Only the surface treatment changes.

## Animation Keyframes Summary

| Animation | Duration | Easing | Purpose |
|-----------|----------|--------|---------|
| `holo-slide` | 4s | ease-in-out infinite | Rainbow gradient position shift |
| `holo-rotate` | 6s | linear infinite | Border conic-gradient rotation |
| `entity-pulse` | 3s | ease-in-out infinite | Active entity glow pulse |
| `status-blink` | 2s | ease-in-out infinite | Status chip opacity pulse (see below) |
| Card hover lift | 0.5s | `cubic-bezier(0.4,0,0.2,1)` | translateY + 3D tilt |
| Cover zoom | 0.6s | ease | scale(1.05) on card hover |
| Accent bar expand | 0.3s | ease | 4px → 5px on hover |
| Mana pip scale | 0.2s | ease | 1x → 1.3x on individual pip hover |

### Missing Keyframe Definitions

```css
@keyframes holo-slide {
  0% { background-position: -200% 0; }
  50% { background-position: 200% 0; }
  100% { background-position: -200% 0; }
}

@keyframes status-blink {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.5; }
}
```

### Tailwind 4 Integration

All keyframes must be registered in `@theme` for utility class generation:

```css
/* In globals.css, inside @theme block */
@theme {
  --animate-holo-slide: holo-slide 4s ease-in-out infinite;
  --animate-holo-rotate: holo-rotate 6s linear infinite;
  --animate-entity-pulse: entity-pulse 3s ease-in-out infinite;
  --animate-status-blink: status-blink 2s ease-in-out infinite;
}
```

This generates `animate-holo-slide`, `animate-holo-rotate`, etc. as Tailwind utility classes. Holographic overlay CSS classes should be placed in `@layer components` (not `@layer utilities`) since they represent composite visual patterns, not atomic utilities.

```css
@layer components {
  .holo-rainbow { /* ... */ }
  .holo-sparkle { /* ... */ }
  .holo-border { /* ... */ }
}
```

The `body { background: var(--bg-base) }` rule goes in `@layer base`.

## Accessibility

### Keyboard Focus

All card hover effects must also apply on `:focus-visible`:

```css
.meeple-card:hover,
.meeple-card:focus-visible {
  transform: translateY(-8px) rotateX(2deg) rotateY(-3deg);
  /* + entity glow + holo layers */
}
.meeple-card:focus-visible {
  outline: 2px solid var(--entity-color);
  outline-offset: 2px;
}
```

### Performance Budget

With potentially dozens of cards on screen, perpetual animations must be managed:

- **Intersection Observer**: Pause `holo-slide`, `holo-rotate`, and `entity-pulse` for cards outside the viewport
- **`will-change: transform`**: Apply only on hover (not permanently) to avoid excessive compositing layers
- **`contain: layout paint`**: Apply to each `.meeple-card` to isolate repaint boundaries
- **Target**: Max 12 simultaneously animating cards (viewport limit); cards scrolled off-screen have animations paused

### Horizontal Shelf Mobile Behavior

- Touch: `scroll-snap-type: x mandatory` with `scroll-snap-align: start` per card
- Minimum 2 cards visible before shelf renders (1-card shelves hidden)
- Swipe velocity: CSS `scroll-behavior: smooth` for momentum
- Card width on mobile: `min-width: 240px` (narrower than desktop 280px)
- No scroll arrows on touch devices; fade gradient on right edge indicates scrollability

## Integration with Existing Mana System Spec

This visual redesign works **on top of** the Mana System spec (`2026-03-15-meeplecard-redesign-mana-system.md`). The Mana System defines:
- Entity types, symbols, sizes, card anatomy zones
- Back block system, deck stack navigation, polymorphic drawer
- StatusGlow states, ManaLinkFooter behavior

This spec adds:
- The Neon Holo visual treatment (colors, glow, holographic layers)
- Platform-level identity (dark canvas, typography, page layouts)
- Specific CSS implementations for all effects

**Implementation order**: Mana System first (structural), then Neon Holo (visual).

## Files Impacted

### New/Modified Design Tokens
- `apps/web/src/styles/design-tokens.css` — New dark base palette, glow variables
- `apps/web/tailwind.config.js` — Holo animations, new keyframes, Inter font
- `apps/web/src/app/globals.css` — Holographic overlay CSS, dark canvas body

### New Components
- Holographic overlay layers (can be a shared utility or part of MeepleCard)
- Bento Grid layout component for dashboard
- Horizontal Shelf component for catalog

### Modified Components
- `MeepleCard` variants — add holo layers (`holo-rainbow`, `holo-sparkle`, `holo-border` children), update hover effects
- `meeple-card-styles.ts` — update shadow system from warm to neon glow, update `entityColors.custom.hsl` from `220 70% 50%` to `220 15% 45%` (Silver, per Mana System spec)
- Dashboard page — switch to Bento Grid layout
- Library/Catalog page — switch to Horizontal Shelves layout

### Font Loading
- `apps/web/src/app/layout.tsx` — Replace `Nunito` import with `Inter` from `next/font/google`
- `apps/web/tailwind.config.js` — Add `inter` to `fontFamily`, set as default `sans`
- `apps/web/src/styles/design-tokens.css` — Update `--font-body` from `var(--font-nunito)` to `var(--font-inter)`
- Remove Nunito from font loading (fully replaced by Inter)

### Dependency: Mana System Phase 1

This spec requires the 6 new entity types (`collection`, `group`, `location`, `expansion`, `achievement`, `note`) to be added to `MeepleEntityType` and `entityColors` before all 16 types can receive visual treatment. Implementation of Neon Holo for the existing 10 entity types can proceed in parallel with Mana System Phase 1.

## Success Criteria

1. All 16 mana symbols render as SVG at all 3 sizes with correct radial gradients
2. Full holographic effect activates on hover for all card variants
3. Entity glow colors are distinct and recognizable at a glance
4. Dark mode is the default, visually coherent experience
5. Light mode is functional with adapted treatment (warm shadows, subtle iridescence)
6. Dashboard renders in Bento Grid with featured card prominence
7. Catalog renders in Horizontal Shelves with entity-type sections
8. All animations respect `prefers-reduced-motion: reduce`
9. Performance: no jank on hover transitions (60fps target)
10. Quicksand + Inter typography renders correctly across all components
