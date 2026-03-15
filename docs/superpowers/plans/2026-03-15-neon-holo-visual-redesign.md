# Neon Holo Visual Redesign — Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Redesign MeepleAI's frontend visual identity from warm/glassmorphism to Neon Arcade × Holographic Collector, with full holo effects on all MeepleCards, dark-first canvas, Inter typography, and new page layouts (Bento Grid dashboard, Horizontal Shelves catalog).

**Architecture:** Layered approach — design tokens first (dark canvas, colors, fonts), then reusable holo effect components, then MeepleCard integration, then page layouts. Each phase produces independently testable output. Scoped to existing 10 entity types; the 6 new Mana System types are a follow-up after Mana System Phase 1.

**Tech Stack:** React 19, Next.js 16, Tailwind 4, CVA, shadcn/ui (Radix), Vitest, TypeScript

**Spec:** `docs/superpowers/specs/2026-03-15-meepleai-neon-holo-visual-redesign.md`

---

## File Structure Overview

### New Files
```
apps/web/src/components/ui/data-display/
├── holo/                                    # NEW — Holographic effect system
│   ├── HoloOverlay.tsx                      # 3 holo layers as React component
│   ├── holo-styles.ts                       # CVA variants for holo intensity
│   ├── useHoloVisibility.ts                 # Intersection Observer for perf
│   ├── index.ts                             # Barrel export
│   └── __tests__/
│       ├── HoloOverlay.test.tsx
│       └── useHoloVisibility.test.ts
├── meeple-card-features/
│   └── ManaCostBar.tsx                      # NEW — Static MTG mana cost bar
│       └── __tests__/ManaCostBar.test.tsx
├── layouts/                                 # NEW — Page layout components
│   ├── BentoGrid.tsx                        # Bento grid dashboard layout
│   ├── HorizontalShelf.tsx                  # Scrollable entity shelf
│   ├── index.ts
│   └── __tests__/
│       ├── BentoGrid.test.tsx
│       └── HorizontalShelf.test.tsx
```

### Modified Files
```
apps/web/src/app/layout.tsx                  # Nunito → Inter font swap
apps/web/src/styles/design-tokens.css        # Neon Holo dark palette tokens
apps/web/src/styles/globals.css              # Holo keyframes, @theme, @layer
apps/web/tailwind.config.js                  # Inter font, holo animations
apps/web/src/components/ui/data-display/
├── meeple-card-styles.ts                    # Entity glow CVA, custom color fix
├── meeple-card/variants/MeepleCardGrid.tsx  # Add HoloOverlay + ManaCostBar
```

---

## Chunk 1: Phase 1 — Foundation (Tokens + Typography)

### Task 1.1: Swap Nunito → Inter font loading

**Files:**
- Modify: `apps/web/src/app/layout.tsx`
- Modify: `apps/web/tailwind.config.js`

- [ ] **Step 1: Install Inter font — verify it loads**

In `apps/web/src/app/layout.tsx`, replace the Nunito import with Inter:

```typescript
// Replace this:
import { Quicksand, Nunito } from 'next/font/google';
// With this:
import { Quicksand, Inter } from 'next/font/google';
```

Replace the `nunito` const:
```typescript
// Replace this:
const nunito = Nunito({
  subsets: ['latin'],
  weight: ['300', '400', '600', '700'],
  variable: '--font-nunito',
  display: 'swap',
});
// With this:
const inter = Inter({
  subsets: ['latin'],
  weight: ['400', '500', '600', '700'],
  variable: '--font-inter',
  display: 'swap',
});
```

Update the body className:
```typescript
// Replace: className={`${quicksand.variable} ${nunito.variable}`}
// With:
className={`${quicksand.variable} ${inter.variable}`}
```

- [ ] **Step 2: Update Tailwind font families**

In `apps/web/tailwind.config.js`, update the `fontFamily` section:

```javascript
fontFamily: {
  quicksand: ['var(--font-quicksand)', 'sans-serif'],
  inter: ['var(--font-inter)', 'sans-serif'],
  // Keep for backward compat during migration:
  nunito: ['var(--font-inter)', 'sans-serif'],
  heading: ['var(--font-quicksand)', 'sans-serif'],
  body: ['var(--font-inter)', 'sans-serif'],
},
```

Note: `nunito` maps to Inter temporarily so existing `font-nunito` classes don't break.

- [ ] **Step 3: Run typecheck to verify no breakage**

Run: `cd apps/web && pnpm typecheck`
Expected: PASS

- [ ] **Step 4: Visual spot-check — run dev server**

Run: `cd apps/web && pnpm dev`
Check: Open browser, verify text renders in Inter (geometric, not rounded like Nunito). Quicksand headings unchanged.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/app/layout.tsx apps/web/tailwind.config.js
git commit -m "feat(design): swap Nunito → Inter body font for Neon Holo redesign"
```

---

### Task 1.2: Add Neon Holo dark palette tokens

**Files:**
- Modify: `apps/web/src/styles/design-tokens.css`

- [ ] **Step 1: Add Neon Holo platform palette variables**

Inside the `:root` block in `design-tokens.css`, add after the existing shadow variables:

```css
    /* ============================================================================
     * NEON HOLO PALETTE
     * Dark-first visual identity (spec: 2026-03-15-meepleai-neon-holo-visual-redesign)
     * ============================================================================ */
    --nh-bg-base: #0e0c18;
    --nh-bg-surface: #16131f;
    --nh-bg-surface-end: #1a1628;
    --nh-bg-elevated: #1e1a2a;
    --nh-border-default: rgba(255,255,255,0.06);
    --nh-text-primary: #f0ece4;
    --nh-text-secondary: #7a7484;
    --nh-text-muted: #555555;
```

- [ ] **Step 2: Add light mode overrides in `.dark` negation block**

Find or create the light-mode section. Add after the dark variables:

```css
    /* Light mode overrides (fallback) */
    --nh-bg-base-light: #faf9f7;
    --nh-bg-surface-light: #ffffff;
    --nh-bg-surface-end-light: #f8f7f5;
    --nh-bg-elevated-light: #ffffff;
    --nh-border-default-light: rgba(0,0,0,0.08);
    --nh-text-primary-light: #1a1a1a;
    --nh-text-secondary-light: #6b6b6b;
    --nh-text-muted-light: #999999;
```

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/styles/design-tokens.css
git commit -m "feat(design): add Neon Holo dark palette tokens"
```

---

### Task 1.3: Add holo keyframes and @theme animations

**Files:**
- Modify: `apps/web/src/styles/globals.css`
- Modify: `apps/web/tailwind.config.js`

- [ ] **Step 1: Add holo keyframes to globals.css**

After the existing `@keyframes` blocks (after `mc-spin-slow`), add:

```css
/* Neon Holo keyframes (spec: 2026-03-15) */
@keyframes holo-slide {
  0% { background-position: -200% 0; }
  50% { background-position: 200% 0; }
  100% { background-position: -200% 0; }
}

@keyframes holo-rotate {
  from { filter: hue-rotate(0deg); }
  to { filter: hue-rotate(360deg); }
}

@keyframes entity-pulse {
  0%, 100% { box-shadow: 0 4px 20px rgba(0,0,0,0.4), 0 0 15px var(--entity-glow-color, transparent); }
  50% { box-shadow: 0 4px 20px rgba(0,0,0,0.4), 0 0 25px var(--entity-glow-color, transparent); }
}

@keyframes status-blink {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.5; }
}
```

- [ ] **Step 2: Register animations in @theme block**

In the `@theme { }` block in `globals.css`, add after the existing mc-* animations:

```css
  /* Neon Holo animations (spec: 2026-03-15) */
  --animate-holo-slide: holo-slide 4s ease-in-out infinite;
  --animate-holo-rotate: holo-rotate 6s linear infinite;
  --animate-entity-pulse: entity-pulse 3s ease-in-out infinite;
  --animate-status-blink: status-blink 2s ease-in-out infinite;
```

- [ ] **Step 3: Add matching entries to tailwind.config.js**

In `tailwind.config.js` `animation` section, add:

```javascript
        // Neon Holo animations (spec: 2026-03-15)
        'holo-slide': 'holo-slide 4s ease-in-out infinite',
        'holo-rotate': 'holo-rotate 6s linear infinite',
        'entity-pulse': 'entity-pulse 3s ease-in-out infinite',
        'status-blink': 'status-blink 2s ease-in-out infinite',
```

In `keyframes` section, add:

```javascript
        // Neon Holo keyframes
        'holo-slide': {
          '0%': { backgroundPosition: '-200% 0' },
          '50%': { backgroundPosition: '200% 0' },
          '100%': { backgroundPosition: '-200% 0' },
        },
        'holo-rotate': {
          '0%': { filter: 'hue-rotate(0deg)' },
          '100%': { filter: 'hue-rotate(360deg)' },
        },
        'entity-pulse': {
          '0%, 100%': { boxShadow: '0 4px 20px rgba(0,0,0,0.4), 0 0 15px var(--entity-glow-color, transparent)' },
          '50%': { boxShadow: '0 4px 20px rgba(0,0,0,0.4), 0 0 25px var(--entity-glow-color, transparent)' },
        },
        'status-blink': {
          '0%, 100%': { opacity: '1' },
          '50%': { opacity: '0.5' },
        },
```

- [ ] **Step 4: Run typecheck**

Run: `cd apps/web && pnpm typecheck`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/styles/globals.css apps/web/tailwind.config.js
git commit -m "feat(design): add Neon Holo keyframes and Tailwind animation tokens"
```

---

### Task 1.4: Update entityColors.custom to Silver

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card-styles.ts`

- [ ] **Step 1: Update custom color**

In `meeple-card-styles.ts`, change:
```typescript
// From:
custom: { hsl: '220 70% 50%', name: 'Custom' }, // Blue (default)
// To:
custom: { hsl: '220 15% 45%', name: 'Custom' }, // Silver (Neon Holo spec)
```

- [ ] **Step 2: Run existing tests**

Run: `cd apps/web && pnpm vitest run src/components/ui/data-display/ --reporter=verbose`
Expected: All existing tests PASS (color value is not asserted by existing tests)

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card-styles.ts
git commit -m "fix(meeple-card): update custom entity color to Silver per Neon Holo spec"
```

---

## Chunk 2: Phase 2 — Holographic Effect Components

### Task 2.1: Create HoloOverlay component

**Files:**
- Create: `apps/web/src/components/ui/data-display/holo/HoloOverlay.tsx`
- Create: `apps/web/src/components/ui/data-display/holo/index.ts`
- Test: `apps/web/src/components/ui/data-display/holo/__tests__/HoloOverlay.test.tsx`

- [ ] **Step 1: Write failing test**

```typescript
// apps/web/src/components/ui/data-display/holo/__tests__/HoloOverlay.test.tsx
import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { HoloOverlay } from '../HoloOverlay';

describe('HoloOverlay', () => {
  it('renders three holo layers', () => {
    const { container } = render(<HoloOverlay />);
    expect(container.querySelector('.holo-rainbow')).toBeTruthy();
    expect(container.querySelector('.holo-sparkle')).toBeTruthy();
    expect(container.querySelector('.holo-border')).toBeTruthy();
  });

  it('all layers have pointer-events-none', () => {
    const { container } = render(<HoloOverlay />);
    const layers = container.querySelectorAll('[class*="holo-"]');
    layers.forEach((layer) => {
      expect(layer.className).toContain('pointer-events-none');
    });
  });

  it('renders nothing when disabled', () => {
    const { container } = render(<HoloOverlay disabled />);
    expect(container.querySelector('.holo-rainbow')).toBeNull();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/ui/data-display/holo/__tests__/HoloOverlay.test.tsx`
Expected: FAIL — module not found

- [ ] **Step 3: Implement HoloOverlay**

```typescript
// apps/web/src/components/ui/data-display/holo/HoloOverlay.tsx
'use client';

interface HoloOverlayProps {
  disabled?: boolean;
}

/**
 * Holographic overlay for MeepleCards.
 * Renders 3 layers: rainbow gradient, sparkle points, rotating border.
 * All layers are invisible by default and activate via parent's group-hover.
 *
 * @spec docs/superpowers/specs/2026-03-15-meepleai-neon-holo-visual-redesign.md
 */
export function HoloOverlay({ disabled = false }: HoloOverlayProps) {
  if (disabled) return null;

  return (
    <>
      {/* Layer 1: Rainbow gradient — mix-blend-mode: screen */}
      <div
        className="holo-rainbow pointer-events-none absolute inset-0 z-[7] rounded-[inherit] opacity-0 transition-opacity duration-500 group-hover:opacity-100"
        style={{
          background: `linear-gradient(
            115deg,
            transparent 20%,
            rgba(236,110,173,0.12) 32%,
            rgba(52,148,230,0.12) 40%,
            rgba(103,232,138,0.10) 48%,
            rgba(233,211,98,0.12) 56%,
            rgba(236,110,173,0.10) 64%,
            transparent 80%
          )`,
          backgroundSize: '200% 100%',
          mixBlendMode: 'screen',
        }}
        aria-hidden="true"
      />

      {/* Layer 2: Sparkle points */}
      <div
        className="holo-sparkle pointer-events-none absolute inset-0 z-[8] rounded-[inherit] opacity-0 transition-opacity duration-500 group-hover:opacity-100"
        style={{
          background: `
            radial-gradient(circle at 30% 20%, rgba(255,255,255,0.06) 0%, transparent 30%),
            radial-gradient(circle at 70% 60%, rgba(255,255,255,0.04) 0%, transparent 25%),
            radial-gradient(circle at 50% 80%, rgba(255,255,255,0.03) 0%, transparent 20%)
          `,
        }}
        aria-hidden="true"
      />

      {/* Layer 3: Rotating rainbow border via mask-composite */}
      <div
        className="holo-border pointer-events-none absolute z-[9] opacity-0 transition-opacity duration-500 group-hover:opacity-100"
        style={{
          inset: '-2px',
          borderRadius: '20px',
          background: `conic-gradient(
            from 0deg,
            rgba(167,139,250,0.25),
            rgba(96,165,250,0.2),
            rgba(52,211,153,0.18),
            rgba(251,191,36,0.22),
            rgba(255,107,107,0.18),
            rgba(236,72,153,0.2),
            rgba(167,139,250,0.25)
          )`,
          WebkitMask: 'linear-gradient(#fff 0 0) content-box, linear-gradient(#fff 0 0)',
          WebkitMaskComposite: 'xor',
          maskComposite: 'exclude',
          padding: '2px',
        }}
        aria-hidden="true"
      />
    </>
  );
}
```

- [ ] **Step 4: Create barrel export**

```typescript
// apps/web/src/components/ui/data-display/holo/index.ts
export { HoloOverlay } from './HoloOverlay';
```

- [ ] **Step 5: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/components/ui/data-display/holo/__tests__/HoloOverlay.test.tsx`
Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/ui/data-display/holo/
git commit -m "feat(holo): create HoloOverlay component with 3-layer holographic effect"
```

---

### Task 2.2: Create useHoloVisibility hook (performance)

**Files:**
- Create: `apps/web/src/components/ui/data-display/holo/useHoloVisibility.ts`
- Test: `apps/web/src/components/ui/data-display/holo/__tests__/useHoloVisibility.test.ts`

- [ ] **Step 1: Write failing test**

```typescript
// apps/web/src/components/ui/data-display/holo/__tests__/useHoloVisibility.test.ts
import { renderHook } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { useHoloVisibility } from '../useHoloVisibility';

describe('useHoloVisibility', () => {
  beforeEach(() => {
    // Mock IntersectionObserver
    const mockObserve = vi.fn();
    const mockUnobserve = vi.fn();
    const mockDisconnect = vi.fn();

    vi.stubGlobal('IntersectionObserver', vi.fn().mockImplementation((callback) => ({
      observe: mockObserve,
      unobserve: mockUnobserve,
      disconnect: mockDisconnect,
    })));
  });

  it('returns a ref and isVisible boolean', () => {
    const { result } = renderHook(() => useHoloVisibility());
    expect(result.current.ref).toBeDefined();
    expect(typeof result.current.isVisible).toBe('boolean');
  });

  it('defaults to not visible', () => {
    const { result } = renderHook(() => useHoloVisibility());
    expect(result.current.isVisible).toBe(false);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/ui/data-display/holo/__tests__/useHoloVisibility.test.ts`
Expected: FAIL

- [ ] **Step 3: Implement hook**

```typescript
// apps/web/src/components/ui/data-display/holo/useHoloVisibility.ts
'use client';

import { useRef, useState, useEffect } from 'react';

/**
 * Tracks whether a card is in the viewport.
 * Used to pause holographic animations for off-screen cards (perf budget: max 12 active).
 */
export function useHoloVisibility() {
  const ref = useRef<HTMLDivElement>(null);
  const [isVisible, setIsVisible] = useState(false);

  useEffect(() => {
    const el = ref.current;
    if (!el) return;

    const observer = new IntersectionObserver(
      ([entry]) => setIsVisible(entry.isIntersecting),
      { rootMargin: '100px' }
    );

    observer.observe(el);
    return () => observer.disconnect();
  }, []);

  return { ref, isVisible };
}
```

- [ ] **Step 4: Update barrel export**

Add to `apps/web/src/components/ui/data-display/holo/index.ts`:
```typescript
export { useHoloVisibility } from './useHoloVisibility';
```

- [ ] **Step 5: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/components/ui/data-display/holo/__tests__/useHoloVisibility.test.ts`
Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/ui/data-display/holo/
git commit -m "feat(holo): add useHoloVisibility hook for viewport-based animation pausing"
```

---

### Task 2.3: Add holo CSS to globals.css (@layer components)

**Files:**
- Modify: `apps/web/src/styles/globals.css`

- [ ] **Step 1: Add holographic component styles**

After the existing `@layer components` block (or create one if none exists), add:

```css
@layer components {
  /* Neon Holo — holographic hover animations (spec: 2026-03-15) */
  .holo-rainbow.animate {
    animation: holo-slide 4s ease-in-out infinite;
  }
  .holo-border.animate {
    animation: holo-rotate 6s linear infinite;
  }

  /* Reduced motion: disable all holo animations */
  @media (prefers-reduced-motion: reduce) {
    .holo-rainbow,
    .holo-sparkle,
    .holo-border {
      animation: none !important;
      transition: none !important;
    }
    .holo-rainbow.animate,
    .holo-border.animate {
      animation: none !important;
    }
  }

  /* Light mode: disable rainbow and sparkle, soften border */
  :root:not(.dark) .holo-rainbow,
  :root:not(.dark) .holo-sparkle {
    display: none;
  }
  :root:not(.dark) .holo-border {
    opacity: 0 !important;
  }
  :root:not(.dark) .group:hover .holo-border {
    opacity: 0.4 !important;
  }
}
```

- [ ] **Step 2: Commit**

```bash
git add apps/web/src/styles/globals.css
git commit -m "feat(holo): add holographic CSS layer with reduced motion and light mode support"
```

---

## Chunk 3: Phase 3 — ManaCostBar + MeepleCard Integration

### Task 3.1: Create ManaCostBar component

**Files:**
- Create: `apps/web/src/components/ui/data-display/meeple-card-features/ManaCostBar.tsx`
- Test: `apps/web/src/components/ui/data-display/meeple-card-features/__tests__/ManaCostBar.test.tsx`

- [ ] **Step 1: Write failing test**

```typescript
// apps/web/src/components/ui/data-display/meeple-card-features/__tests__/ManaCostBar.test.tsx
import { render } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { ManaCostBar } from '../ManaCostBar';

describe('ManaCostBar', () => {
  it('renders mana pips for given entity types', () => {
    const { container } = render(
      <ManaCostBar relatedTypes={['session', 'kb', 'agent']} />
    );
    const pips = container.querySelectorAll('[data-mana-pip]');
    expect(pips).toHaveLength(3);
  });

  it('renders nothing when relatedTypes is empty', () => {
    const { container } = render(<ManaCostBar relatedTypes={[]} />);
    expect(container.firstChild).toBeNull();
  });

  it('has backdrop blur and dark background', () => {
    const { container } = render(
      <ManaCostBar relatedTypes={['game']} />
    );
    const bar = container.firstChild as HTMLElement;
    expect(bar).toBeTruthy();
    expect(bar.className).toContain('backdrop-blur');
  });

  it('is not interactive (no click handlers)', () => {
    const { container } = render(
      <ManaCostBar relatedTypes={['session', 'kb']} />
    );
    const pips = container.querySelectorAll('[data-mana-pip]');
    pips.forEach((pip) => {
      expect(pip.getAttribute('role')).toBeNull();
      expect(pip.getAttribute('tabindex')).toBeNull();
    });
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/ui/data-display/meeple-card-features/__tests__/ManaCostBar.test.tsx`
Expected: FAIL

- [ ] **Step 3: Implement ManaCostBar**

```typescript
// apps/web/src/components/ui/data-display/meeple-card-features/ManaCostBar.tsx
import type { MeepleEntityType } from '../meeple-card-styles';
import { entityColors } from '../meeple-card-styles';

interface ManaCostBarProps {
  relatedTypes: MeepleEntityType[];
}

/**
 * Static mana cost bar (MTG style) showing which entity types this card relates to.
 * Decorative only — not interactive. Positioned top-right of card cover.
 *
 * @spec ManaCostBar = "what this entity connects to" (entity-type-level, static)
 * @spec Distinct from ManaLinkFooter which shows instance-level dynamic links.
 */
export function ManaCostBar({ relatedTypes }: ManaCostBarProps) {
  if (relatedTypes.length === 0) return null;

  return (
    <div
      className="absolute right-3 top-2.5 z-[6] flex gap-[3px] rounded-[14px] border border-white/[0.08] bg-black/50 p-[3px_5px] backdrop-blur-[8px]"
      aria-label={`Related: ${relatedTypes.map((t) => entityColors[t]?.name ?? t).join(', ')}`}
    >
      {relatedTypes.map((type) => {
        const color = entityColors[type];
        if (!color) return null;
        return (
          <div
            key={type}
            data-mana-pip={type}
            className="h-4 w-4 rounded-full"
            style={{
              background: `radial-gradient(circle at 35% 35%, hsl(${color.hsl}) , hsl(${color.hsl}) )`,
              border: `1px solid hsla(${color.hsl}, 0.3)`,
              boxShadow: `0 1px 4px hsla(${color.hsl}, 0.25)`,
            }}
            title={color.name}
            aria-hidden="true"
          />
        );
      })}
    </div>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/components/ui/data-display/meeple-card-features/__tests__/ManaCostBar.test.tsx`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card-features/ManaCostBar.tsx apps/web/src/components/ui/data-display/meeple-card-features/__tests__/ManaCostBar.test.tsx
git commit -m "feat(mana): create ManaCostBar component (static MTG-style entity type pips)"
```

---

### Task 3.2: Update MeepleCard CVA with entity glow hover

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card-styles.ts`

- [ ] **Step 1: Add CSS variable for entity glow and update grid variant hover**

Update the `grid` variant in `meepleCardVariants`:

```typescript
grid: [
  'flex flex-col rounded-2xl overflow-hidden',
  'bg-card border border-border/50',
  '[box-shadow:var(--shadow-warm-sm)]',
  // Neon Holo: entity glow on hover + 3D tilt
  'hover:[box-shadow:var(--shadow-warm-xl)] hover:-translate-y-2',
  'hover:rotate-x-[2deg] hover:-rotate-y-[3deg]',
  // Performance containment
  '[contain:layout_paint]',
],
```

- [ ] **Step 2: Add similar treatment to featured variant**

```typescript
featured: [
  'flex flex-col rounded-2xl overflow-hidden',
  'bg-card border border-border/50',
  '[box-shadow:var(--shadow-warm-md)]',
  'hover:[box-shadow:var(--shadow-warm-xl)] hover:-translate-y-2',
  '[contain:layout_paint]',
],
```

- [ ] **Step 3: Run existing MeepleCard tests**

Run: `cd apps/web && pnpm vitest run src/components/ui/data-display/ --reporter=verbose`
Expected: All PASS

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card-styles.ts
git commit -m "feat(meeple-card): add Neon Holo entity glow hover and 3D tilt to CVA variants"
```

---

### Task 3.3: Integrate HoloOverlay into MeepleCardGrid

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card/variants/MeepleCardGrid.tsx`

- [ ] **Step 1: Read current MeepleCardGrid implementation**

Read: `apps/web/src/components/ui/data-display/meeple-card/variants/MeepleCardGrid.tsx`
Understand the component structure and where to insert HoloOverlay.

- [ ] **Step 2: Add HoloOverlay import and render inside card**

Add at the top of the file:
```typescript
import { HoloOverlay } from '../../holo';
```

Inside the card's root `<div>` (the one with the CVA variant classes), add as the first child:
```tsx
<HoloOverlay />
```

The card root must already have `group` in its className for `group-hover` to work on the holo layers.

- [ ] **Step 3: Add entity glow CSS variable**

On the card root `<div>`, add the entity glow color as a CSS variable:
```tsx
style={{
  ...existingStyle,
  '--entity-glow-color': `hsla(${entityColors[entity].hsl}, 0.08)`,
} as React.CSSProperties}
```

- [ ] **Step 4: Run MeepleCard tests**

Run: `cd apps/web && pnpm vitest run src/components/ui/data-display/meeple-card/ --reporter=verbose`
Expected: All PASS

- [ ] **Step 5: Visual check**

Run: `cd apps/web && pnpm dev`
Navigate to a page with MeepleCards. Hover a card — should see:
1. Holographic rainbow shimmer
2. Sparkle points
3. Rotating rainbow border
4. Card lifts with 3D tilt

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/variants/MeepleCardGrid.tsx
git commit -m "feat(meeple-card): integrate HoloOverlay into MeepleCardGrid variant"
```

---

## Chunk 4: Phase 4 — Page Layouts

### Task 4.1: Create BentoGrid layout component

**Files:**
- Create: `apps/web/src/components/ui/data-display/layouts/BentoGrid.tsx`
- Create: `apps/web/src/components/ui/data-display/layouts/index.ts`
- Test: `apps/web/src/components/ui/data-display/layouts/__tests__/BentoGrid.test.tsx`

- [ ] **Step 1: Write failing test**

```typescript
// apps/web/src/components/ui/data-display/layouts/__tests__/BentoGrid.test.tsx
import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { BentoGrid, BentoGridItem } from '../BentoGrid';

describe('BentoGrid', () => {
  it('renders children in a grid', () => {
    render(
      <BentoGrid>
        <BentoGridItem area="feat"><div>Featured</div></BentoGridItem>
        <BentoGridItem area="agent"><div>Agent</div></BentoGridItem>
      </BentoGrid>
    );
    expect(screen.getByText('Featured')).toBeTruthy();
    expect(screen.getByText('Agent')).toBeTruthy();
  });

  it('applies grid-template-areas', () => {
    const { container } = render(
      <BentoGrid>
        <BentoGridItem area="feat"><div>F</div></BentoGridItem>
      </BentoGrid>
    );
    const grid = container.firstChild as HTMLElement;
    expect(grid.className).toContain('grid');
  });

  it('BentoGridItem applies grid-area style', () => {
    const { container } = render(
      <BentoGrid>
        <BentoGridItem area="feat"><div>F</div></BentoGridItem>
      </BentoGrid>
    );
    const item = container.querySelector('[style*="grid-area"]');
    expect(item).toBeTruthy();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/ui/data-display/layouts/__tests__/BentoGrid.test.tsx`
Expected: FAIL

- [ ] **Step 3: Implement BentoGrid**

```typescript
// apps/web/src/components/ui/data-display/layouts/BentoGrid.tsx
interface BentoGridProps {
  children: React.ReactNode;
  className?: string;
}

/**
 * Bento Grid layout for dashboard pages.
 * Uses CSS Grid with named areas for asymmetric card placement.
 *
 * Default areas: feat (2×2), agent, session, event, kb, coll (2×1), player, game
 * @spec docs/superpowers/specs/2026-03-15-meepleai-neon-holo-visual-redesign.md
 */
export function BentoGrid({ children, className = '' }: BentoGridProps) {
  return (
    <div
      className={`grid gap-6 ${className}`}
      style={{
        gridTemplateColumns: 'repeat(4, 1fr)',
        gridTemplateAreas: `
          "feat feat agent session"
          "feat feat event kb"
          "coll coll player game"
        `,
      }}
    >
      {children}
    </div>
  );
}

interface BentoGridItemProps {
  area: string;
  children: React.ReactNode;
  className?: string;
}

export function BentoGridItem({ area, children, className = '' }: BentoGridItemProps) {
  return (
    <div style={{ gridArea: area }} className={className}>
      {children}
    </div>
  );
}
```

- [ ] **Step 4: Create barrel export**

```typescript
// apps/web/src/components/ui/data-display/layouts/index.ts
export { BentoGrid, BentoGridItem } from './BentoGrid';
export { HorizontalShelf } from './HorizontalShelf';
```

(HorizontalShelf will be created in next task — export will resolve then.)

- [ ] **Step 5: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/components/ui/data-display/layouts/__tests__/BentoGrid.test.tsx`
Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/ui/data-display/layouts/
git commit -m "feat(layout): create BentoGrid component for dashboard"
```

---

### Task 4.2: Create HorizontalShelf layout component

**Files:**
- Create: `apps/web/src/components/ui/data-display/layouts/HorizontalShelf.tsx`
- Test: `apps/web/src/components/ui/data-display/layouts/__tests__/HorizontalShelf.test.tsx`

- [ ] **Step 1: Write failing test**

```typescript
// apps/web/src/components/ui/data-display/layouts/__tests__/HorizontalShelf.test.tsx
import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { HorizontalShelf } from '../HorizontalShelf';

describe('HorizontalShelf', () => {
  it('renders title with entity name and count', () => {
    render(
      <HorizontalShelf entity="game" title="My Games" count={47}>
        <div>Card 1</div>
        <div>Card 2</div>
      </HorizontalShelf>
    );
    expect(screen.getByText('My Games')).toBeTruthy();
    expect(screen.getByText('47')).toBeTruthy();
  });

  it('does not render when count < 2', () => {
    const { container } = render(
      <HorizontalShelf entity="game" title="My Games" count={1}>
        <div>Card 1</div>
      </HorizontalShelf>
    );
    expect(container.firstChild).toBeNull();
  });

  it('renders children in horizontal scroll container', () => {
    const { container } = render(
      <HorizontalShelf entity="game" title="My Games" count={3}>
        <div>Card 1</div>
        <div>Card 2</div>
        <div>Card 3</div>
      </HorizontalShelf>
    );
    const scrollContainer = container.querySelector('[class*="overflow-x-auto"]');
    expect(scrollContainer).toBeTruthy();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/ui/data-display/layouts/__tests__/HorizontalShelf.test.tsx`
Expected: FAIL

- [ ] **Step 3: Implement HorizontalShelf**

```typescript
// apps/web/src/components/ui/data-display/layouts/HorizontalShelf.tsx
import type { MeepleEntityType } from '../meeple-card-styles';
import { entityColors } from '../meeple-card-styles';

interface HorizontalShelfProps {
  entity: MeepleEntityType;
  title: string;
  count: number;
  children: React.ReactNode;
  className?: string;
}

/**
 * Horizontal scrollable shelf for entity-grouped cards (catalog/library).
 * Header shows mana pip + title in entity color + count.
 * Minimum 2 items to render (1-item shelves hidden).
 *
 * @spec docs/superpowers/specs/2026-03-15-meepleai-neon-holo-visual-redesign.md
 */
export function HorizontalShelf({ entity, title, count, children, className = '' }: HorizontalShelfProps) {
  if (count < 2) return null;

  const color = entityColors[entity];
  if (!color) return null;

  return (
    <div className={`mb-8 ${className}`}>
      {/* Shelf header */}
      <div className="mb-3 flex items-center gap-2">
        <div
          className="h-[18px] w-[18px] rounded-full"
          style={{
            background: `radial-gradient(circle at 35% 35%, hsl(${color.hsl}), hsl(${color.hsl}))`,
            border: `1px solid hsla(${color.hsl}, 0.3)`,
          }}
          aria-hidden="true"
        />
        <span
          className="font-quicksand text-[13px] font-bold"
          style={{ color: `hsl(${color.hsl})` }}
        >
          {title}
        </span>
        <span className="text-[10px] text-[var(--nh-text-muted,#555)]">{count}</span>
      </div>

      {/* Scrollable card row */}
      <div
        className="flex gap-4 overflow-x-auto pb-2 scroll-smooth [scroll-snap-type:x_mandatory] [-webkit-overflow-scrolling:touch]"
        role="list"
      >
        {children}
      </div>
    </div>
  );
}
```

- [ ] **Step 4: Update barrel export**

The barrel export in `index.ts` already has the HorizontalShelf export from Task 4.1.

- [ ] **Step 5: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/components/ui/data-display/layouts/__tests__/HorizontalShelf.test.tsx`
Expected: PASS

- [ ] **Step 6: Run all layout tests together**

Run: `cd apps/web && pnpm vitest run src/components/ui/data-display/layouts/ --reporter=verbose`
Expected: All PASS

- [ ] **Step 7: Commit**

```bash
git add apps/web/src/components/ui/data-display/layouts/
git commit -m "feat(layout): create HorizontalShelf component for entity-grouped catalog"
```

---

## Chunk 5: Phase 5 — Reduced Motion + Final Integration Tests

### Task 5.1: Add reduced-motion support to MeepleCard

**Files:**
- Modify: `apps/web/src/styles/globals.css`

- [ ] **Step 1: Add reduced-motion overrides for card hover**

In the `@media (prefers-reduced-motion: reduce)` block that already exists in `globals.css`, add:

```css
    /* Neon Holo: simplified hover for reduced motion */
    .group:hover .holo-rainbow,
    .group:hover .holo-sparkle,
    .group:hover .holo-border {
      opacity: 0 !important;
      animation: none !important;
    }
```

- [ ] **Step 2: Add focus-visible holo activation**

In the `@layer components` block, add:

```css
  /* Keyboard focus activates holo effects (accessibility) */
  .group:focus-visible .holo-rainbow,
  .group:focus-visible .holo-sparkle {
    opacity: 1;
  }
  .group:focus-visible .holo-border {
    opacity: 1;
  }
```

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/styles/globals.css
git commit -m "feat(a11y): add reduced-motion and focus-visible support for Neon Holo effects"
```

---

### Task 5.2: Run full test suite and typecheck

- [ ] **Step 1: Run full frontend typecheck**

Run: `cd apps/web && pnpm typecheck`
Expected: PASS

- [ ] **Step 2: Run all component tests**

Run: `cd apps/web && pnpm vitest run --reporter=verbose`
Expected: All PASS

- [ ] **Step 3: Run lint**

Run: `cd apps/web && pnpm lint`
Expected: PASS (or only pre-existing warnings)

- [ ] **Step 4: Final commit if any fixes needed**

```bash
git add -A
git commit -m "chore: fix lint/type issues from Neon Holo integration"
```

---

## Implementation Notes

### What this plan does NOT cover (future work)

1. **6 new entity types** (collection, group, location, expansion, achievement, note) — blocked on Mana System Phase 1
2. **SVG mana icons** — The actual 16 SVG glyph files. These are part of the Mana System plan, not this visual plan
3. **ManaBadge component** — Part of Mana System spec, not this visual spec
4. **ManaLinkFooter** — Already exists, will be updated when Mana System Phase 1 lands
5. **Dashboard page integration** — Connecting BentoGrid to actual data (requires page-level work)
6. **Catalog page integration** — Connecting HorizontalShelf to actual data
7. **Dark canvas body override** — Changing `body` background to `--nh-bg-base` (impacts all pages, needs careful rollout)

### Parallel execution opportunities

These tasks can run in parallel (no shared state):
- **Task 2.1 + Task 3.1**: HoloOverlay and ManaCostBar are independent components
- **Task 4.1 + Task 4.2**: BentoGrid and HorizontalShelf are independent layout components

Sequential dependencies:
- Task 1.1–1.4 must complete before anything else (foundation)
- Task 3.3 depends on Task 2.1 (needs HoloOverlay to integrate)
- Task 5.1–5.2 must be last (final integration)
