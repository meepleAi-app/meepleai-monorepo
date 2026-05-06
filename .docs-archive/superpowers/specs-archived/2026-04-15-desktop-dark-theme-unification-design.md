# Desktop Dark Theme Unification

**Date**: 2026-04-15
**Status**: Approved
**Scope**: Authenticated pages only (landing/login fuori scope)

## Problem

Mobile has a coherent "Dark Premium Gaming" visual language using `--gaming-*` CSS tokens. Desktop mixes two incompatible themes:
- TopBar uses warm beige (`--nh-*` tokens, `rgba(255,252,248,0.85)`)
- HandRail uses dark blue-gray (`hsl(220,15%,11%)`)
- Main content area uses warm beige (`#faf8f5`)
- No consistent glassmorphism, spacing, or border-radius system on desktop

## Solution

Apply the mobile Dark Premium Gaming theme to all desktop components. Prepare for future light/dark toggle by using CSS variables exclusively (no hardcoded colors).

## Design Tokens

### Dark Theme (default on `:root`)

```css
:root {
  --bg-base: #0f0a1a;
  --bg-elevated: #1a1333;
  --bg-glass: rgba(255, 255, 255, 0.04);
  --bg-glass-hover: rgba(255, 255, 255, 0.08);
  --border-glass: rgba(255, 255, 255, 0.08);
  --border-glass-hover: rgba(255, 255, 255, 0.14);
  --border-glass-strong: rgba(255, 255, 255, 0.18);
  --text-primary: #f8fafc;
  --text-secondary: #94a3b8;
  --text-tertiary: #64748b;
  --text-accent: #fbbf24;
}
```

### Light Theme (future, on `.light` class)

```css
.light {
  --bg-base: #faf8f5;
  --bg-elevated: #fffcf8;
  --bg-glass: rgba(160, 120, 60, 0.04);
  --bg-glass-hover: rgba(160, 120, 60, 0.08);
  --border-glass: rgba(160, 120, 60, 0.08);
  --border-glass-hover: rgba(160, 120, 60, 0.14);
  --border-glass-strong: rgba(160, 120, 60, 0.18);
  --text-primary: #1a1a1a;
  --text-secondary: #5a4a35;
  --text-tertiary: #8a7a65;
  --text-accent: hsl(25, 95%, 38%);
}
```

### Entity Colors (unchanged)

| Entity | HSL | Usage |
|--------|-----|-------|
| Game | `25 95% 45%` | Orange accent |
| Player | `262 83% 58%` | Purple accent |
| Session | `240 60% 55%` | Blue accent |
| Agent | `38 92% 50%` | Amber accent |
| Document | `210 40% 55%` | Cyan accent |
| Chat | `220 80% 55%` | Blue-chat accent |
| Event | `350 89% 60%` | Red accent |

Entity colors are used at consistent opacities:
- Background: `15-22%` opacity
- Border: `30-55%` opacity
- Text: `60-70%` lightness variant

## Border-Radius Hierarchy

| Element | Value | Token |
|---------|-------|-------|
| Sheet/Drawer (top only) | `20px` | `--radius-sheet` |
| Card | `16px` | `--radius-card` |
| Button/Input | `12px` | `--radius-btn` |
| Chip/Tag/Pill | `50px` | `--radius-pill` |
| Avatar/Icon | `50%` | `--radius-circle` |

## Spacing System

Base unit: 4px. Mobile and desktop use same scale; desktop scales only container padding and grid gap.

| Context | Mobile | Desktop |
|---------|--------|---------|
| Content horizontal padding | `px-4` (16px) | `px-6` (24px) |
| Card grid gap | `gap-3` (12px) | `gap-4` (16px) |
| Card internal padding | `12px 16px` | `16px 20px` |
| TopBar padding | `px-4` | `px-6` |
| HandRail padding | — | `p-2` (8px) |
| Nav item gap | `gap-2` (8px) | `gap-2` (8px) |

No arbitrary values like `p-[5px]`. All spacing uses Tailwind's 4px scale.

## Typography

| Element | Font | Size | Weight |
|---------|------|------|--------|
| Logo | Quicksand | 16px | 700 |
| Heading h1 | Quicksand | 24-32px | 700 |
| Heading h2 | Quicksand | 18-20px | 600 |
| Body | Nunito | 14px | 400 |
| Nav item | Nunito | 13px | 500 |
| Label | Nunito | 12px | 600 |
| Chip/Badge | Nunito | 10-11px | 600-700 |
| HandRail label | Nunito | 7.5px | 800, uppercase |

No font family changes. Only normalize size/weight where mobile and desktop diverge.

## Glassmorphism Specification

| Surface | Background | Blur | Border |
|---------|-----------|------|--------|
| TopBar | `var(--bg-elevated)` at 95% opacity | `16px` | `var(--border-glass)` bottom |
| HandRail | `var(--bg-base)` at 95% opacity | none (solid) | `var(--border-glass)` right (6% opacity) |
| Card | `var(--bg-glass)` | `12px` | `var(--border-glass)` + entity accent 3px left |
| Drawer/Sheet | `var(--bg-elevated)` | none | `var(--border-glass-strong)` top |
| Input | `var(--bg-glass)` | none | `var(--border-glass)`, focus ring `amber-500/40` |
| Tooltip/Popup | `var(--bg-elevated)` | `12px` | `var(--border-glass)` |

## Components to Update

### Layer 1 — Design Tokens (foundation)

| File | Change |
|------|--------|
| `src/styles/design-tokens.css` | Add unified `--bg-*`, `--border-*`, `--text-*` vars on `:root` (dark default). Move `--nh-*` under `.light`. Add `--radius-*` tokens. |
| `src/styles/globals.css` | Update body/html background, text color, border defaults to dark theme. Update `.card`, `.glass`, `.btn-*` component classes. |
| `tailwind.config.js` | Map new token names if needed for Tailwind utility usage. |

### Layer 2 — Shell/Layout (structure)

| File | Change |
|------|--------|
| `src/components/layout/UserShell/DesktopShell.tsx` | Main area background → `var(--bg-base)` |
| `src/components/layout/UserShell/TopBar.tsx` | Background from `rgba(255,252,248,0.85)` → `var(--bg-elevated)` at 95% opacity + `backdrop-blur-[16px]` + border `var(--border-glass)` |
| `src/components/layout/HandRail/HandRail.tsx` | Background from `hsl(220,15%,11%)` → `var(--bg-base)` at 95%. Border from `white/5` → `var(--border-glass)`. Text labels to `var(--text-tertiary)`. |

### Layer 3 — Shared Components (cross-device coherence)

| File | Change |
|------|--------|
| `src/components/session/MobileStatusBar.tsx` | `bg-card` → `var(--bg-glass)`, `border-border` → `var(--border-glass)` |
| `src/components/session/MobileScorebar.tsx` | `bg-card border-border` → `var(--bg-glass)` + `var(--border-glass)` |
| `src/components/layout/ContextualHand/ContextualHandSidebar.tsx` | `--nh-bg-base` → `var(--bg-base)`, `--nh-border-default` → `var(--border-glass)` |
| `src/components/layout/ContextualHand/ContextualHandBottomBar.tsx` | Align blur/border to glassmorphism spec |
| `src/components/layout/CardRack/CardRack.tsx` | `bg-sidebar` → `var(--bg-base)` |
| `src/components/layout/AdminShell/AdminShell.tsx` | Align to dark theme tokens |

### Layer 4 — MeepleCard

| File | Change |
|------|--------|
| `src/components/ui/data-display/meeple-card/tokens.ts` | Update `--mc-*` to derive from `--bg-*` / `--border-*` tokens |
| `src/components/ui/data-display/meeple-card/` (variants) | Ensure glassmorphism uses unified blur/border values, entity accent 3px left border |

### Layer 5 — Page-specific hardcoded colors

| File | Change |
|------|--------|
| `src/components/library/game-table/GameTableLayout.tsx` | Replace `#0d1117`, `#161b22`, `#30363d` hardcoded values with `var(--bg-*)` tokens |
| `src/components/chat-unified/` | Verify desktop chat uses `--gaming-*` / new unified tokens consistently |

## Out of Scope

- Landing page (public, not authenticated)
- Login/Register pages
- Font family changes
- Responsive breakpoints (md: 768px, lg: 1024px)
- Animations/transitions (keep existing)
- JS/business logic
- Light mode implementation (only CSS var preparation)
- Toggle button UI (only placeholder position in TopBar)

## Light/Dark Toggle Preparation

Components must use only CSS variables (`var(--bg-base)`, etc.), never hardcoded color values. The toggle mechanism will be:

1. Default: no class on `<html>` = dark theme (`:root` vars)
2. Light mode: add `class="light"` on `<html>` = light theme overrides
3. Toggle button: placeholder position in TopBar (moon/sun icon), implementation deferred

This means every color change in this spec must use `var()` references, enabling the future toggle to work by simply adding/removing a CSS class.

## Success Criteria

1. All authenticated pages use dark theme consistently (no warm beige remnants)
2. Desktop TopBar, HandRail, content area, and cards share the same color palette as mobile
3. Zero hardcoded color values in updated components
4. Border-radius follows the hierarchy table consistently
5. Spacing uses Tailwind's 4px scale (no arbitrary values)
6. Glassmorphism (blur + transparency) applied per the surface specification table
7. Mobile components (MobileStatusBar, MobileScorebar) also use unified tokens
8. Existing functionality unchanged — visual-only changes
9. No regressions in responsive behavior (mobile/tablet/desktop breakpoints work as before)
