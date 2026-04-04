# Phase 1: Premium Gaming Design System — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create the "Premium Gaming" design system foundation — theme, tokens, and shared components — that all subsequent user page redesign phases build on.

**Architecture:** Add a new "gaming" theme preset to the existing theme system (`src/lib/themes.ts`), extend design tokens with glass/gradient primitives, create 5 new shared components (GlassCard, GradientButton, MobileBottomNav, MobileHeader, SessionBottomNav), promote BottomSheet to shared, and reskin MeepleCard variants.

**Tech Stack:** Next.js 16, React 19, Tailwind CSS 4, CVA (class-variance-authority), Radix UI, Vitest, Framer Motion

**Spec:** `docs/superpowers/specs/2026-03-28-user-pages-redesign-design.md` — Section 1

---

## File Structure

### New Files

| File | Responsibility |
|------|---------------|
| `src/components/ui/surfaces/GlassCard.tsx` | Glassmorphism container with blur, border, and entity glow variants |
| `src/components/ui/surfaces/GlassCard.test.tsx` | Tests for GlassCard |
| `src/components/ui/buttons/GradientButton.tsx` | Primary CTA button with amber→red gradient and loading/disabled states |
| `src/components/ui/buttons/GradientButton.test.tsx` | Tests for GradientButton |
| `src/components/ui/navigation/MobileBottomNav.tsx` | 5-icon fixed bottom bar for mobile (Home, Library, +, Chat, Profile) |
| `src/components/ui/navigation/MobileBottomNav.test.tsx` | Tests for MobileBottomNav |
| `src/components/ui/navigation/MobileHeader.tsx` | Compact mobile header with title + context actions |
| `src/components/ui/navigation/MobileHeader.test.tsx` | Tests for MobileHeader |
| `src/components/ui/navigation/SessionBottomNav.tsx` | 4-tab session nav replacing app nav during live play |
| `src/components/ui/navigation/SessionBottomNav.test.tsx` | Tests for SessionBottomNav |
| `src/components/ui/overlays/BottomSheet.tsx` | Shared bottom sheet (promoted from session/) with slide-up animation |
| `src/components/ui/overlays/BottomSheet.test.tsx` | Tests for BottomSheet |
| `src/styles/premium-gaming.css` | Premium Gaming theme CSS custom properties and glassmorphism utilities |

### Modified Files

| File | Changes |
|------|---------|
| `src/lib/themes.ts` | Add `gaming` preset (dark-only) to PresetThemeName, add gamingDark ThemeColors |
| `src/styles/globals.css` | Import premium-gaming.css, add glass utility classes |
| `src/styles/design-tokens.css` | Add `--size-bottom-nav`, `--size-mobile-header` sizing tokens |
| `src/components/ui/data-display/meeple-card-styles.ts` | Add `gaming` CVA variant class for glass background |
| `src/components/ui/data-display/meeple-card/variants/MeepleCardGrid.tsx` | Apply glass styling when gaming theme active |
| `src/components/ui/data-display/meeple-card/variants/MeepleCardList.tsx` | Apply glass styling when gaming theme active |
| `src/components/ui/data-display/meeple-card/variants/MeepleCardCompact.tsx` | Apply glass styling when gaming theme active |
| `src/config/navigation.ts` | Add mobile nav items configuration (5 items) |

### All paths are relative to `apps/web/`

---

## Task 1: Add "Gaming" Theme Preset

**Files:**
- Modify: `src/lib/themes.ts`
- Test: Run existing theme tests + manual verification

- [ ] **Step 1: Read existing theme structure**

Open `src/lib/themes.ts` and locate the `PresetThemeName` type (line ~15) and the theme preset definitions. The file has 5 presets (default, forest, sunset, ocean, midnight) each with light/dark ThemeColors. We add a 6th preset: `gaming` (dark-only).

- [ ] **Step 2: Add gaming to PresetThemeName**

In `src/lib/themes.ts`, update the type:

```typescript
export type PresetThemeName =
  | 'default'
  | 'forest'
  | 'sunset'
  | 'ocean'
  | 'midnight'
  | 'gaming';
```

- [ ] **Step 3: Add gamingDark ThemeColors**

Add after the midnight theme definitions in `src/lib/themes.ts`:

```typescript
// ========== GAMING (PREMIUM DARK) ==========
const gamingDark: ThemeColors = {
  primary: '36 96% 53%',              // Amber #f59e0b
  primaryForeground: '0 0% 100%',     // White
  secondary: '262 83% 58%',           // Purple #8b5cf6 (AI accent)
  secondaryForeground: '0 0% 100%',
  accent: '0 84% 60%',               // Red #ef4444 (gradient end)
  accentForeground: '0 0% 100%',
  muted: '260 20% 12%',              // Dark purple-gray #1a1333
  mutedForeground: '215 20% 65%',    // #94a3b8
  destructive: '0 84% 60%',
  destructiveForeground: '0 0% 100%',
};
```

- [ ] **Step 4: Register gaming in presetThemes**

Find the `presetThemes` record (or equivalent mapping) and add the gaming entry. Since gaming is dark-only, register only the dark variant:

```typescript
  {
    id: 'gaming-dark',
    name: 'Gaming',
    description: 'Premium dark theme with amber accents and glassmorphism',
    mode: 'dark' as ThemeMode,
    colors: gamingDark,
  },
```

If the preset system expects both light/dark, use gamingDark for both (the light variant is identical — this is a dark-only theme).

- [ ] **Step 5: Verify compilation**

Run: `cd apps/web && npx tsc --noEmit`
Expected: No errors

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/lib/themes.ts
git commit -m "feat(theme): add Gaming preset — dark amber/purple for Premium Gaming design"
```

---

## Task 2: Add Premium Gaming CSS Tokens & Utilities

**Files:**
- Create: `src/styles/premium-gaming.css`
- Modify: `src/styles/globals.css`
- Modify: `src/styles/design-tokens.css`

- [ ] **Step 1: Create premium-gaming.css**

Create `src/styles/premium-gaming.css`:

```css
/**
 * Premium Gaming Theme — CSS Custom Properties & Utilities
 *
 * Applied when the "gaming" theme is active (body has gaming theme class).
 * Provides glassmorphism primitives and gradient utilities.
 */

@layer tokens {
  :root {
    /* Premium Gaming palette */
    --gaming-bg-base: #0f0a1a;
    --gaming-bg-elevated: #1a1333;
    --gaming-bg-glass: rgba(255, 255, 255, 0.05);
    --gaming-border-glass: rgba(255, 255, 255, 0.1);
    --gaming-border-glass-hover: rgba(255, 255, 255, 0.18);
    --gaming-accent-gradient: linear-gradient(90deg, #f59e0b, #ef4444);
    --gaming-accent-ai: #8b5cf6;
    --gaming-accent-success: #22c55e;
    --gaming-accent-info: #3b82f6;
    --gaming-text-primary: #f8fafc;
    --gaming-text-secondary: #94a3b8;
    --gaming-text-accent: #fbbf24;
    --gaming-blur: 12px;
  }
}

@layer components {
  /* Glass surface */
  .glass {
    background: var(--gaming-bg-glass);
    border: 1px solid var(--gaming-border-glass);
    backdrop-filter: blur(var(--gaming-blur));
    -webkit-backdrop-filter: blur(var(--gaming-blur));
  }

  .glass:hover {
    border-color: var(--gaming-border-glass-hover);
  }

  /* Glass card — standard card surface */
  .glass-card {
    background: var(--gaming-bg-glass);
    border: 1px solid var(--gaming-border-glass);
    backdrop-filter: blur(var(--gaming-blur));
    -webkit-backdrop-filter: blur(var(--gaming-blur));
    border-radius: 12px;
    transition: border-color 0.2s ease, box-shadow 0.2s ease;
  }

  .glass-card:hover {
    border-color: var(--gaming-border-glass-hover);
  }

  /* Gradient button surface */
  .gradient-primary {
    background: var(--gaming-accent-gradient);
    color: white;
    font-weight: 600;
    border: none;
    border-radius: 8px;
    transition: opacity 0.2s ease, transform 0.1s ease;
  }

  .gradient-primary:hover {
    opacity: 0.9;
  }

  .gradient-primary:active {
    transform: scale(0.98);
  }

  .gradient-primary:disabled {
    opacity: 0.5;
    cursor: not-allowed;
    transform: none;
  }

  /* Bottom sheet surface */
  .sheet-surface {
    background: var(--gaming-bg-elevated);
    border-radius: 16px 16px 0 0;
  }

  /* Entity glow — for MeepleCard borders */
  .glass-glow-game {
    box-shadow: 0 0 12px rgba(245, 158, 11, 0.15);
    border-color: rgba(245, 158, 11, 0.25);
  }

  .glass-glow-player {
    box-shadow: 0 0 12px rgba(139, 92, 246, 0.15);
    border-color: rgba(139, 92, 246, 0.25);
  }

  .glass-glow-session {
    box-shadow: 0 0 12px rgba(99, 102, 241, 0.15);
    border-color: rgba(99, 102, 241, 0.25);
  }

  .glass-glow-collection {
    box-shadow: 0 0 12px rgba(20, 184, 166, 0.15);
    border-color: rgba(20, 184, 166, 0.25);
  }

  .glass-glow-event {
    box-shadow: 0 0 12px rgba(244, 63, 94, 0.15);
    border-color: rgba(244, 63, 94, 0.25);
  }
}
```

- [ ] **Step 2: Add sizing tokens to design-tokens.css**

In `src/styles/design-tokens.css`, after the `--size-container-max` line (~58), add:

```css
    /* Mobile navigation */
    --size-bottom-nav: 4rem;          /* 64px */
    --size-mobile-header: 3.5rem;     /* 56px */
    --size-session-bottom-nav: 3.5rem; /* 56px */
```

- [ ] **Step 3: Import premium-gaming.css in globals.css**

In `src/styles/globals.css`, after the existing `@import "./design-tokens.css";` line (~6), add:

```css
@import "./premium-gaming.css";
```

- [ ] **Step 4: Verify compilation**

Run: `cd apps/web && npx tsc --noEmit && pnpm build`
Expected: Build succeeds with no CSS errors

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/styles/premium-gaming.css apps/web/src/styles/globals.css apps/web/src/styles/design-tokens.css
git commit -m "feat(tokens): add Premium Gaming CSS tokens, glass utilities, and sizing tokens"
```

---

## Task 3: Create GlassCard Component

**Files:**
- Create: `src/components/ui/surfaces/GlassCard.tsx`
- Create: `src/components/ui/surfaces/GlassCard.test.tsx`

- [ ] **Step 1: Write the failing test**

Create `src/components/ui/surfaces/GlassCard.test.tsx`:

```tsx
import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { GlassCard } from './GlassCard';

describe('GlassCard', () => {
  it('renders children', () => {
    render(<GlassCard>Hello</GlassCard>);
    expect(screen.getByText('Hello')).toBeInTheDocument();
  });

  it('applies glass-card class by default', () => {
    const { container } = render(<GlassCard>Content</GlassCard>);
    expect(container.firstChild).toHaveClass('glass-card');
  });

  it('applies entity glow class when entity prop is provided', () => {
    const { container } = render(<GlassCard entity="game">Content</GlassCard>);
    expect(container.firstChild).toHaveClass('glass-glow-game');
  });

  it('does not apply glow class when no entity', () => {
    const { container } = render(<GlassCard>Content</GlassCard>);
    expect(container.firstChild).not.toHaveClass('glass-glow-game');
  });

  it('merges custom className', () => {
    const { container } = render(<GlassCard className="p-4">Content</GlassCard>);
    expect(container.firstChild).toHaveClass('glass-card');
    expect(container.firstChild).toHaveClass('p-4');
  });

  it('renders as a different element via as prop', () => {
    render(<GlassCard as="section" data-testid="glass">Content</GlassCard>);
    const el = screen.getByTestId('glass');
    expect(el.tagName).toBe('SECTION');
  });

  it('forwards ref', () => {
    const ref = { current: null } as React.RefObject<HTMLDivElement | null>;
    render(<GlassCard ref={ref}>Content</GlassCard>);
    expect(ref.current).toBeInstanceOf(HTMLElement);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/ui/surfaces/GlassCard.test.tsx`
Expected: FAIL — module not found

- [ ] **Step 3: Write GlassCard implementation**

Create `src/components/ui/surfaces/GlassCard.tsx`:

```tsx
import React from 'react';
import { cn } from '@/lib/utils';

type GlowEntity = 'game' | 'player' | 'session' | 'collection' | 'event';

const glowClasses: Record<GlowEntity, string> = {
  game: 'glass-glow-game',
  player: 'glass-glow-player',
  session: 'glass-glow-session',
  collection: 'glass-glow-collection',
  event: 'glass-glow-event',
};

export interface GlassCardProps extends React.HTMLAttributes<HTMLElement> {
  /** Entity type for colored glow border */
  entity?: GlowEntity;
  /** Render as a different HTML element */
  as?: React.ElementType;
  children: React.ReactNode;
}

export const GlassCard = React.forwardRef<HTMLElement, GlassCardProps>(
  function GlassCard({ entity, as: Component = 'div', className, children, ...props }, ref) {
    return (
      <Component
        ref={ref}
        className={cn(
          'glass-card',
          entity && glowClasses[entity],
          className
        )}
        {...props}
      >
        {children}
      </Component>
    );
  }
);
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/components/ui/surfaces/GlassCard.test.tsx`
Expected: All 7 tests PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/ui/surfaces/GlassCard.tsx apps/web/src/components/ui/surfaces/GlassCard.test.tsx
git commit -m "feat(ui): add GlassCard component with entity glow variants"
```

---

## Task 4: Create GradientButton Component

**Files:**
- Create: `src/components/ui/buttons/GradientButton.tsx`
- Create: `src/components/ui/buttons/GradientButton.test.tsx`

- [ ] **Step 1: Write the failing test**

Create `src/components/ui/buttons/GradientButton.test.tsx`:

```tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { GradientButton } from './GradientButton';

describe('GradientButton', () => {
  it('renders children', () => {
    render(<GradientButton>Click me</GradientButton>);
    expect(screen.getByRole('button', { name: 'Click me' })).toBeInTheDocument();
  });

  it('applies gradient-primary class', () => {
    render(<GradientButton>Action</GradientButton>);
    expect(screen.getByRole('button')).toHaveClass('gradient-primary');
  });

  it('handles click events', () => {
    const onClick = vi.fn();
    render(<GradientButton onClick={onClick}>Action</GradientButton>);
    fireEvent.click(screen.getByRole('button'));
    expect(onClick).toHaveBeenCalledOnce();
  });

  it('renders disabled state', () => {
    render(<GradientButton disabled>Action</GradientButton>);
    expect(screen.getByRole('button')).toBeDisabled();
  });

  it('shows loading spinner and disables button', () => {
    render(<GradientButton loading>Action</GradientButton>);
    const button = screen.getByRole('button');
    expect(button).toBeDisabled();
    expect(button).toHaveAttribute('aria-busy', 'true');
  });

  it('renders full width when fullWidth is true', () => {
    render(<GradientButton fullWidth>Action</GradientButton>);
    expect(screen.getByRole('button')).toHaveClass('w-full');
  });

  it('supports size variants', () => {
    const { rerender } = render(<GradientButton size="sm">Small</GradientButton>);
    expect(screen.getByRole('button')).toHaveClass('h-9');

    rerender(<GradientButton size="lg">Large</GradientButton>);
    expect(screen.getByRole('button')).toHaveClass('h-12');
  });

  it('merges custom className', () => {
    render(<GradientButton className="mt-4">Action</GradientButton>);
    expect(screen.getByRole('button')).toHaveClass('gradient-primary');
    expect(screen.getByRole('button')).toHaveClass('mt-4');
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/ui/buttons/GradientButton.test.tsx`
Expected: FAIL — module not found

- [ ] **Step 3: Write GradientButton implementation**

Create `src/components/ui/buttons/GradientButton.tsx`:

```tsx
import React from 'react';
import { cn } from '@/lib/utils';

const sizeClasses = {
  sm: 'h-9 px-4 text-sm',
  md: 'h-10 px-6 text-sm',
  lg: 'h-12 px-8 text-base',
} as const;

export interface GradientButtonProps extends React.ButtonHTMLAttributes<HTMLButtonElement> {
  /** Button size */
  size?: 'sm' | 'md' | 'lg';
  /** Show loading spinner */
  loading?: boolean;
  /** Full width button */
  fullWidth?: boolean;
}

export const GradientButton = React.forwardRef<HTMLButtonElement, GradientButtonProps>(
  function GradientButton(
    { size = 'md', loading = false, fullWidth = false, disabled, className, children, ...props },
    ref
  ) {
    const isDisabled = disabled || loading;

    return (
      <button
        ref={ref}
        disabled={isDisabled}
        aria-busy={loading || undefined}
        className={cn(
          'gradient-primary inline-flex items-center justify-center gap-2',
          sizeClasses[size],
          fullWidth && 'w-full',
          className
        )}
        {...props}
      >
        {loading && (
          <svg
            className="h-4 w-4 animate-spin"
            viewBox="0 0 24 24"
            fill="none"
            aria-hidden="true"
          >
            <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
            <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
          </svg>
        )}
        {children}
      </button>
    );
  }
);
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/components/ui/buttons/GradientButton.test.tsx`
Expected: All 8 tests PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/ui/buttons/GradientButton.tsx apps/web/src/components/ui/buttons/GradientButton.test.tsx
git commit -m "feat(ui): add GradientButton component with loading and size variants"
```

---

## Task 5: Create Shared BottomSheet Component

**Files:**
- Create: `src/components/ui/overlays/BottomSheet.tsx`
- Create: `src/components/ui/overlays/BottomSheet.test.tsx`

- [ ] **Step 1: Write the failing test**

Create `src/components/ui/overlays/BottomSheet.test.tsx`:

```tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { BottomSheet } from './BottomSheet';

describe('BottomSheet', () => {
  it('renders children when open', () => {
    render(
      <BottomSheet open onOpenChange={() => {}}>
        <p>Sheet content</p>
      </BottomSheet>
    );
    expect(screen.getByText('Sheet content')).toBeInTheDocument();
  });

  it('does not render children when closed', () => {
    render(
      <BottomSheet open={false} onOpenChange={() => {}}>
        <p>Sheet content</p>
      </BottomSheet>
    );
    expect(screen.queryByText('Sheet content')).not.toBeInTheDocument();
  });

  it('renders title when provided', () => {
    render(
      <BottomSheet open onOpenChange={() => {}} title="My Sheet">
        <p>Content</p>
      </BottomSheet>
    );
    expect(screen.getByText('My Sheet')).toBeInTheDocument();
  });

  it('calls onOpenChange when overlay is clicked', () => {
    const onOpenChange = vi.fn();
    render(
      <BottomSheet open onOpenChange={onOpenChange}>
        <p>Content</p>
      </BottomSheet>
    );
    // Click the overlay (data-testid for reliability)
    const overlay = screen.getByTestId('bottom-sheet-overlay');
    fireEvent.click(overlay);
    expect(onOpenChange).toHaveBeenCalledWith(false);
  });

  it('renders drag handle', () => {
    render(
      <BottomSheet open onOpenChange={() => {}}>
        <p>Content</p>
      </BottomSheet>
    );
    expect(screen.getByTestId('drag-handle')).toBeInTheDocument();
  });

  it('applies sheet-surface class to content', () => {
    render(
      <BottomSheet open onOpenChange={() => {}}>
        <p>Content</p>
      </BottomSheet>
    );
    const content = screen.getByTestId('bottom-sheet-content');
    expect(content).toHaveClass('sheet-surface');
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/ui/overlays/BottomSheet.test.tsx`
Expected: FAIL — module not found

- [ ] **Step 3: Write BottomSheet implementation**

Create `src/components/ui/overlays/BottomSheet.tsx`:

```tsx
'use client';

import React, { useCallback, useEffect } from 'react';
import { cn } from '@/lib/utils';
import { AnimatePresence, motion } from 'framer-motion';

export interface BottomSheetProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title?: string;
  /** Height constraint: 'auto' fits content, 'half' = 50vh, 'full' = 90vh */
  height?: 'auto' | 'half' | 'full';
  children: React.ReactNode;
  className?: string;
}

const heightClasses = {
  auto: 'max-h-[85vh]',
  half: 'h-[50vh]',
  full: 'h-[90vh]',
} as const;

export function BottomSheet({
  open,
  onOpenChange,
  title,
  height = 'auto',
  children,
  className,
}: BottomSheetProps) {
  // Close on Escape
  const handleKeyDown = useCallback(
    (e: KeyboardEvent) => {
      if (e.key === 'Escape') onOpenChange(false);
    },
    [onOpenChange]
  );

  useEffect(() => {
    if (open) {
      document.addEventListener('keydown', handleKeyDown);
      document.body.style.overflow = 'hidden';
    }
    return () => {
      document.removeEventListener('keydown', handleKeyDown);
      document.body.style.overflow = '';
    };
  }, [open, handleKeyDown]);

  return (
    <AnimatePresence>
      {open && (
        <>
          {/* Overlay */}
          <motion.div
            data-testid="bottom-sheet-overlay"
            className="fixed inset-0 z-50 bg-black/60"
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            transition={{ duration: 0.2 }}
            onClick={() => onOpenChange(false)}
          />

          {/* Sheet */}
          <motion.div
            data-testid="bottom-sheet-content"
            role="dialog"
            aria-modal="true"
            aria-label={title}
            className={cn(
              'sheet-surface fixed inset-x-0 bottom-0 z-50 flex flex-col overflow-hidden',
              heightClasses[height],
              className
            )}
            initial={{ y: '100%' }}
            animate={{ y: 0 }}
            exit={{ y: '100%' }}
            transition={{ type: 'spring', damping: 30, stiffness: 300 }}
          >
            {/* Drag handle */}
            <div className="flex justify-center py-3" data-testid="drag-handle">
              <div className="h-1 w-10 rounded-full bg-white/20" />
            </div>

            {/* Title */}
            {title && (
              <div className="px-4 pb-3">
                <h2 className="text-lg font-semibold text-[var(--gaming-text-primary)]">
                  {title}
                </h2>
              </div>
            )}

            {/* Content */}
            <div className="flex-1 overflow-y-auto px-4 pb-8">
              {children}
            </div>
          </motion.div>
        </>
      )}
    </AnimatePresence>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/components/ui/overlays/BottomSheet.test.tsx`
Expected: All 6 tests PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/ui/overlays/BottomSheet.tsx apps/web/src/components/ui/overlays/BottomSheet.test.tsx
git commit -m "feat(ui): add shared BottomSheet component with spring animation"
```

---

## Task 6: Create MobileBottomNav Component

**Files:**
- Create: `src/components/ui/navigation/MobileBottomNav.tsx`
- Create: `src/components/ui/navigation/MobileBottomNav.test.tsx`

- [ ] **Step 1: Write the failing test**

Create `src/components/ui/navigation/MobileBottomNav.test.tsx`:

```tsx
import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { MobileBottomNav } from './MobileBottomNav';

// Mock next/navigation
vi.mock('next/navigation', () => ({
  usePathname: () => '/dashboard',
  useRouter: () => ({ push: vi.fn() }),
}));

// Mock next/link
vi.mock('next/link', () => ({
  default: ({ children, href, ...props }: { children: React.ReactNode; href: string; [key: string]: unknown }) => (
    <a href={href} {...props}>{children}</a>
  ),
}));

describe('MobileBottomNav', () => {
  it('renders 5 navigation items', () => {
    render(<MobileBottomNav />);
    const nav = screen.getByRole('navigation', { name: /bottom/i });
    const links = nav.querySelectorAll('a, button');
    expect(links).toHaveLength(5);
  });

  it('renders Home, Libreria, Chat, Profilo labels', () => {
    render(<MobileBottomNav />);
    expect(screen.getByText('Home')).toBeInTheDocument();
    expect(screen.getByText('Libreria')).toBeInTheDocument();
    expect(screen.getByText('Chat')).toBeInTheDocument();
    expect(screen.getByText('Profilo')).toBeInTheDocument();
  });

  it('highlights active route', () => {
    render(<MobileBottomNav />);
    const homeLink = screen.getByText('Home').closest('a');
    expect(homeLink).toHaveAttribute('aria-current', 'page');
  });

  it('is hidden on desktop (lg:hidden class)', () => {
    const { container } = render(<MobileBottomNav />);
    expect(container.firstChild).toHaveClass('lg:hidden');
  });

  it('has fixed bottom positioning', () => {
    const { container } = render(<MobileBottomNav />);
    expect(container.firstChild).toHaveClass('fixed');
    expect(container.firstChild).toHaveClass('bottom-0');
  });

  it('is not rendered when hidden prop is true', () => {
    const { container } = render(<MobileBottomNav hidden />);
    expect(container.firstChild).toBeNull();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/ui/navigation/MobileBottomNav.test.tsx`
Expected: FAIL — module not found

- [ ] **Step 3: Write MobileBottomNav implementation**

Create `src/components/ui/navigation/MobileBottomNav.tsx`:

```tsx
'use client';

import React from 'react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { cn } from '@/lib/utils';
import { Home, Library, MessageCircle, User, Plus } from 'lucide-react';

interface NavItem {
  href: string;
  label: string;
  icon: React.ElementType;
  matchPaths: string[];
}

const navItems: NavItem[] = [
  { href: '/dashboard', label: 'Home', icon: Home, matchPaths: ['/dashboard'] },
  { href: '/library', label: 'Libreria', icon: Library, matchPaths: ['/library'] },
  // Center FAB is handled separately
  { href: '/chat', label: 'Chat', icon: MessageCircle, matchPaths: ['/chat'] },
  { href: '/profile', label: 'Profilo', icon: User, matchPaths: ['/profile', '/settings'] },
];

export interface MobileBottomNavProps {
  /** Hide the nav (e.g., during live session) */
  hidden?: boolean;
}

export function MobileBottomNav({ hidden = false }: MobileBottomNavProps) {
  const pathname = usePathname();

  if (hidden) return null;

  const isActive = (item: NavItem) =>
    item.matchPaths.some((p) => pathname.startsWith(p));

  return (
    <nav
      aria-label="Bottom navigation"
      className={cn(
        'fixed bottom-0 inset-x-0 z-40 lg:hidden',
        'flex items-center justify-around',
        'h-[var(--size-bottom-nav)]',
        'bg-[var(--gaming-bg-elevated)] border-t border-[var(--gaming-border-glass)]',
        'safe-area-pb'
      )}
    >
      {/* First two nav items */}
      {navItems.slice(0, 2).map((item) => (
        <NavLink key={item.href} item={item} active={isActive(item)} />
      ))}

      {/* Center FAB */}
      <button
        aria-label="Azione rapida"
        className={cn(
          'flex items-center justify-center',
          'h-12 w-12 -mt-4 rounded-full',
          'gradient-primary shadow-lg shadow-orange-500/20'
        )}
      >
        <Plus className="h-6 w-6 text-white" />
      </button>

      {/* Last two nav items */}
      {navItems.slice(2).map((item) => (
        <NavLink key={item.href} item={item} active={isActive(item)} />
      ))}
    </nav>
  );
}

function NavLink({ item, active }: { item: NavItem; active: boolean }) {
  const Icon = item.icon;
  return (
    <Link
      href={item.href}
      aria-current={active ? 'page' : undefined}
      className={cn(
        'flex flex-col items-center justify-center gap-0.5 px-3 py-1',
        'text-[10px] font-medium transition-colors',
        active
          ? 'text-[var(--gaming-text-accent)]'
          : 'text-[var(--gaming-text-secondary)]'
      )}
    >
      <Icon className="h-5 w-5" />
      <span>{item.label}</span>
    </Link>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/components/ui/navigation/MobileBottomNav.test.tsx`
Expected: All 6 tests PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/ui/navigation/MobileBottomNav.tsx apps/web/src/components/ui/navigation/MobileBottomNav.test.tsx
git commit -m "feat(ui): add MobileBottomNav with 5 items, FAB center, active state"
```

---

## Task 7: Create MobileHeader Component

**Files:**
- Create: `src/components/ui/navigation/MobileHeader.tsx`
- Create: `src/components/ui/navigation/MobileHeader.test.tsx`

- [ ] **Step 1: Write the failing test**

Create `src/components/ui/navigation/MobileHeader.test.tsx`:

```tsx
import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { MobileHeader } from './MobileHeader';

describe('MobileHeader', () => {
  it('renders title', () => {
    render(<MobileHeader title="La Mia Libreria" />);
    expect(screen.getByText('La Mia Libreria')).toBeInTheDocument();
  });

  it('renders subtitle when provided', () => {
    render(<MobileHeader title="Libreria" subtitle="12 giochi" />);
    expect(screen.getByText('12 giochi')).toBeInTheDocument();
  });

  it('renders right actions when provided', () => {
    render(
      <MobileHeader
        title="Test"
        rightActions={<button>Filter</button>}
      />
    );
    expect(screen.getByRole('button', { name: 'Filter' })).toBeInTheDocument();
  });

  it('renders back button when onBack is provided', () => {
    render(<MobileHeader title="Detail" onBack={() => {}} />);
    expect(screen.getByLabelText('Torna indietro')).toBeInTheDocument();
  });

  it('does not render back button when onBack is not provided', () => {
    render(<MobileHeader title="Home" />);
    expect(screen.queryByLabelText('Torna indietro')).not.toBeInTheDocument();
  });

  it('is hidden on desktop (lg:hidden class)', () => {
    const { container } = render(<MobileHeader title="Test" />);
    expect(container.firstChild).toHaveClass('lg:hidden');
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/ui/navigation/MobileHeader.test.tsx`
Expected: FAIL — module not found

- [ ] **Step 3: Write MobileHeader implementation**

Create `src/components/ui/navigation/MobileHeader.tsx`:

```tsx
'use client';

import React from 'react';
import { cn } from '@/lib/utils';
import { ChevronLeft } from 'lucide-react';

export interface MobileHeaderProps {
  title: string;
  subtitle?: string;
  /** Callback for back navigation. If provided, shows back arrow. */
  onBack?: () => void;
  /** Right-side action buttons */
  rightActions?: React.ReactNode;
  className?: string;
}

export function MobileHeader({
  title,
  subtitle,
  onBack,
  rightActions,
  className,
}: MobileHeaderProps) {
  return (
    <header
      className={cn(
        'sticky top-0 z-30 lg:hidden',
        'flex items-center gap-3',
        'h-[var(--size-mobile-header)] px-4',
        'bg-[var(--gaming-bg-base)]/95 backdrop-blur-sm',
        'border-b border-[var(--gaming-border-glass)]',
        className
      )}
    >
      {/* Back button */}
      {onBack && (
        <button
          onClick={onBack}
          aria-label="Torna indietro"
          className="flex h-9 w-9 items-center justify-center rounded-full text-[var(--gaming-text-secondary)] hover:bg-white/5"
        >
          <ChevronLeft className="h-5 w-5" />
        </button>
      )}

      {/* Title area */}
      <div className="flex-1 min-w-0">
        <h1 className="truncate text-base font-semibold text-[var(--gaming-text-primary)]">
          {title}
        </h1>
        {subtitle && (
          <p className="truncate text-xs text-[var(--gaming-text-secondary)]">
            {subtitle}
          </p>
        )}
      </div>

      {/* Right actions */}
      {rightActions && (
        <div className="flex items-center gap-2">
          {rightActions}
        </div>
      )}
    </header>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/components/ui/navigation/MobileHeader.test.tsx`
Expected: All 6 tests PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/ui/navigation/MobileHeader.tsx apps/web/src/components/ui/navigation/MobileHeader.test.tsx
git commit -m "feat(ui): add MobileHeader component with back nav and actions"
```

---

## Task 8: Create SessionBottomNav Component

**Files:**
- Create: `src/components/ui/navigation/SessionBottomNav.tsx`
- Create: `src/components/ui/navigation/SessionBottomNav.test.tsx`

- [ ] **Step 1: Write the failing test**

Create `src/components/ui/navigation/SessionBottomNav.test.tsx`:

```tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { SessionBottomNav } from './SessionBottomNav';

describe('SessionBottomNav', () => {
  it('renders 4 session tabs', () => {
    render(<SessionBottomNav activeTab="game" onTabChange={() => {}} />);
    expect(screen.getByText('Gioco')).toBeInTheDocument();
    expect(screen.getByText('Punteggi')).toBeInTheDocument();
    expect(screen.getByText('Chiedi')).toBeInTheDocument();
    expect(screen.getByText('Giocatori')).toBeInTheDocument();
  });

  it('highlights active tab', () => {
    render(<SessionBottomNav activeTab="scores" onTabChange={() => {}} />);
    const scoresTab = screen.getByText('Punteggi').closest('button');
    expect(scoresTab).toHaveAttribute('aria-selected', 'true');
  });

  it('calls onTabChange when tab is clicked', () => {
    const onTabChange = vi.fn();
    render(<SessionBottomNav activeTab="game" onTabChange={onTabChange} />);
    fireEvent.click(screen.getByText('Chiedi'));
    expect(onTabChange).toHaveBeenCalledWith('chat');
  });

  it('has fixed bottom positioning', () => {
    const { container } = render(
      <SessionBottomNav activeTab="game" onTabChange={() => {}} />
    );
    expect(container.firstChild).toHaveClass('fixed');
    expect(container.firstChild).toHaveClass('bottom-0');
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/ui/navigation/SessionBottomNav.test.tsx`
Expected: FAIL — module not found

- [ ] **Step 3: Write SessionBottomNav implementation**

Create `src/components/ui/navigation/SessionBottomNav.tsx`:

```tsx
'use client';

import React from 'react';
import { cn } from '@/lib/utils';
import { Dice5, BarChart3, MessageCircle, Users } from 'lucide-react';

export type SessionTab = 'game' | 'scores' | 'chat' | 'players';

interface TabItem {
  id: SessionTab;
  label: string;
  icon: React.ElementType;
}

const tabs: TabItem[] = [
  { id: 'game', label: 'Gioco', icon: Dice5 },
  { id: 'scores', label: 'Punteggi', icon: BarChart3 },
  { id: 'chat', label: 'Chiedi', icon: MessageCircle },
  { id: 'players', label: 'Giocatori', icon: Users },
];

export interface SessionBottomNavProps {
  activeTab: SessionTab;
  onTabChange: (tab: SessionTab) => void;
}

export function SessionBottomNav({ activeTab, onTabChange }: SessionBottomNavProps) {
  return (
    <nav
      aria-label="Session navigation"
      className={cn(
        'fixed bottom-0 inset-x-0 z-40',
        'flex items-center justify-around',
        'h-[var(--size-session-bottom-nav)]',
        'bg-[var(--gaming-bg-elevated)] border-t border-[var(--gaming-border-glass)]',
        'safe-area-pb'
      )}
    >
      {tabs.map((tab) => {
        const active = activeTab === tab.id;
        const Icon = tab.icon;
        return (
          <button
            key={tab.id}
            onClick={() => onTabChange(tab.id)}
            role="tab"
            aria-selected={active}
            className={cn(
              'flex flex-col items-center justify-center gap-0.5 px-4 py-1',
              'text-[10px] font-medium transition-colors',
              active
                ? 'text-[var(--gaming-text-accent)]'
                : 'text-[var(--gaming-text-secondary)]'
            )}
          >
            <Icon className="h-5 w-5" />
            <span>{tab.label}</span>
          </button>
        );
      })}
    </nav>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/components/ui/navigation/SessionBottomNav.test.tsx`
Expected: All 4 tests PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/ui/navigation/SessionBottomNav.tsx apps/web/src/components/ui/navigation/SessionBottomNav.test.tsx
git commit -m "feat(ui): add SessionBottomNav with 4 tabs for live play mode"
```

---

## Task 9: Reskin MeepleCard for Premium Gaming

**Files:**
- Modify: `src/components/ui/data-display/meeple-card-styles.ts`
- Modify: `src/components/ui/data-display/meeple-card/variants/MeepleCardGrid.tsx`
- Modify: `src/components/ui/data-display/meeple-card/variants/MeepleCardList.tsx`
- Modify: `src/components/ui/data-display/meeple-card/variants/MeepleCardCompact.tsx`

This task adds glass styling to MeepleCard when the Premium Gaming theme is active. The approach: add a `glass-card` class alongside existing styles. The existing entity colors, ManaPips, links, and all variants remain untouched.

- [ ] **Step 1: Read current MeepleCardGrid variant to understand structure**

Read `src/components/ui/data-display/meeple-card/variants/MeepleCardGrid.tsx` to identify the root element className pattern. Look for the outer `<div>` or wrapper that receives CVA classes.

- [ ] **Step 2: Add gaming variant to meeple-card-styles CVA**

In `src/components/ui/data-display/meeple-card-styles.ts`, find the `meepleCardVariants` CVA definition. Add a `theme` variant:

```typescript
// In the meepleCardVariants CVA definition, add to the variants object:
theme: {
  gaming: 'glass-card',
  default: '',
},
```

And add a default:
```typescript
defaultVariants: {
  // ... existing defaults
  theme: 'default',
},
```

- [ ] **Step 3: Create a theme detection hook**

Create a small utility. In `src/components/ui/data-display/meeple-card/hooks/useCardTheme.ts`:

```typescript
'use client';

import { useEffect, useState } from 'react';

/**
 * Detects if the Premium Gaming theme is active by checking localStorage.
 * Returns 'gaming' or 'default'.
 */
export function useCardTheme(): 'gaming' | 'default' {
  const [theme, setTheme] = useState<'gaming' | 'default'>('default');

  useEffect(() => {
    const stored = localStorage.getItem('meepleai-theme');
    if (stored) {
      try {
        const parsed = JSON.parse(stored);
        if (parsed?.id?.startsWith('gaming')) {
          setTheme('gaming');
        }
      } catch {
        // ignore
      }
    }
  }, []);

  return theme;
}
```

- [ ] **Step 4: Apply glass class in MeepleCardGrid**

In `src/components/ui/data-display/meeple-card/variants/MeepleCardGrid.tsx`, import and use the theme:

```typescript
import { useCardTheme } from '../hooks/useCardTheme';
```

Inside the component, before the return:
```typescript
const cardTheme = useCardTheme();
```

On the root wrapper element, merge the glass class:
```typescript
className={cn(
  // existing classes...
  cardTheme === 'gaming' && 'glass-card',
  cardTheme === 'gaming' && entity && `glass-glow-${entity}`,
)}
```

This keeps all existing styling and adds glass on top when gaming theme is active.

- [ ] **Step 5: Apply same pattern to MeepleCardList and MeepleCardCompact**

Repeat the same import + `useCardTheme()` + conditional class pattern in:
- `src/components/ui/data-display/meeple-card/variants/MeepleCardList.tsx`
- `src/components/ui/data-display/meeple-card/variants/MeepleCardCompact.tsx`

These are the three variants used in the mobile redesign. Featured, Hero, and Expanded can be updated later.

- [ ] **Step 6: Verify no existing tests break**

Run: `cd apps/web && pnpm vitest run src/components/ui/data-display/`
Expected: All existing MeepleCard tests PASS

- [ ] **Step 7: Commit**

```bash
git add apps/web/src/components/ui/data-display/
git commit -m "feat(meeple-card): add Premium Gaming glass reskin for grid, list, compact variants"
```

---

## Task 10: Wire MobileBottomNav into Authenticated Layout

**Files:**
- Modify: `src/app/(authenticated)/layout.tsx`
- Modify: `src/components/layout/UserShell/UserShellClient.tsx` (or equivalent)

- [ ] **Step 1: Read current authenticated layout and UserShell**

Read `src/app/(authenticated)/layout.tsx` and `src/components/layout/UserShell/UserShellClient.tsx` to understand how the shell wraps pages. The HybridSidebar is currently desktop-only (`hidden lg:flex`). We need to add MobileBottomNav for mobile.

- [ ] **Step 2: Add MobileBottomNav to UserShell**

In `UserShellClient.tsx` (or wherever the shell renders), add after the main content area:

```tsx
import { MobileBottomNav } from '@/components/ui/navigation/MobileBottomNav';

// Inside the render, after {children}:
<MobileBottomNav />
```

- [ ] **Step 3: Add bottom padding to main content area**

The main content wrapper needs `pb-[var(--size-bottom-nav)]` on mobile so content isn't hidden behind the fixed nav. Add this class with a `lg:pb-0` override:

```tsx
<main className={cn(
  // existing classes...
  'pb-[var(--size-bottom-nav)] lg:pb-0'
)}>
  {children}
</main>
```

- [ ] **Step 4: Verify visually**

Run: `cd apps/web && pnpm dev`
Open http://localhost:3000/dashboard in mobile viewport (Chrome DevTools → toggle device toolbar → iPhone 14 Pro).
Expected: Bottom nav visible with 5 items, sidebar hidden, content not obscured.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/app/\(authenticated\)/layout.tsx apps/web/src/components/layout/UserShell/
git commit -m "feat(layout): wire MobileBottomNav into authenticated shell with safe area padding"
```

---

## Task 11: Final Integration Verification

- [ ] **Step 1: Run full test suite**

Run: `cd apps/web && pnpm test`
Expected: All tests pass (existing + new)

- [ ] **Step 2: Run typecheck**

Run: `cd apps/web && pnpm typecheck`
Expected: No type errors

- [ ] **Step 3: Run lint**

Run: `cd apps/web && pnpm lint`
Expected: No lint errors

- [ ] **Step 4: Build check**

Run: `cd apps/web && pnpm build`
Expected: Build succeeds

- [ ] **Step 5: Commit any fixes needed, then tag phase completion**

```bash
git commit -m "chore: fix lint/type issues from Phase 1 design system"
```

---

## Summary

| Task | Component | Tests | Status |
|------|-----------|-------|--------|
| 1 | Gaming theme preset | Existing | ☐ |
| 2 | Premium Gaming CSS tokens | Build check | ☐ |
| 3 | GlassCard | 7 tests | ☐ |
| 4 | GradientButton | 8 tests | ☐ |
| 5 | BottomSheet | 6 tests | ☐ |
| 6 | MobileBottomNav | 6 tests | ☐ |
| 7 | MobileHeader | 6 tests | ☐ |
| 8 | SessionBottomNav | 4 tests | ☐ |
| 9 | MeepleCard reskin | Existing | ☐ |
| 10 | Layout wiring | Visual | ☐ |
| 11 | Integration check | Full suite | ☐ |

**Total new tests: 43**
**Total new files: 13** (6 components + 6 test files + 1 CSS)
**Total modified files: 7**

After this phase: the design system foundation is in place. Phase 2 (Discovery & Add Game) can begin building pages using these components.
