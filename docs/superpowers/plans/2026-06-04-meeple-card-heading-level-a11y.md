# MeepleCard `headingLevel` Prop Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add `headingLevel?: 2 | 3 | 4` prop to `MeepleCard` and migrate 20 grid-below-h1-hero consumer surfaces to close the `heading-order` WCAG suppression debt from #1841 + #1481.

**Architecture:** Additive prop on `MeepleCardProps` (default `3`, backwards-compatible). 5 variants (`GridCard/ListCard/FeaturedCard/HeroCard/FocusCard`) render dynamic `<HeadingTag>` instead of hardcoded `<h3>/<h2>`. 20 consumer files pass `headingLevel={2}` at the call site. CompactCard skipped (uses `<span>`, no heading element). 2 existing axe suppressions removed + 5 new axe scans added.

**Tech Stack:** TypeScript, React 19, Vitest + React Testing Library (RTL), `jest-axe` for a11y assertions, Tailwind 4.

**Spec:** [`docs/superpowers/specs/2026-06-04-meeple-card-heading-level-a11y-design.md`](../specs/2026-06-04-meeple-card-heading-level-a11y-design.md)

**Branch:** `feature/issue-1842-meeple-card-heading-level` (already created, parent: `main-dev`)

**Cwd assumption:** All commands assume `cwd=apps/web` unless otherwise noted. From repo root prefix paths with `apps/web/`.

---

## File map

| Order | Action | File | Responsibility |
|---|---|---|---|
| T1 | Modify | `apps/web/src/components/ui/data-display/meeple-card/types.ts` | Add `headingLevel?: 2 \| 3 \| 4` to `MeepleCardProps` |
| T2 | Modify | `apps/web/src/components/ui/data-display/meeple-card/variants/GridCard.tsx` | Dynamic HeadingTag (default 3) |
| T3 | Modify | `apps/web/src/components/ui/data-display/meeple-card/variants/ListCard.tsx` | Dynamic HeadingTag (default 3) |
| T4 | Modify | `apps/web/src/components/ui/data-display/meeple-card/variants/FeaturedCard.tsx` | Dynamic HeadingTag (default 3) |
| T5 | Modify | `apps/web/src/components/ui/data-display/meeple-card/variants/HeroCard.tsx` | Dynamic HeadingTag (default 3) |
| T6 | Modify | `apps/web/src/components/ui/data-display/meeple-card/variants/FocusCard.tsx` | Dynamic HeadingTag (default 2 — preserve existing h2) |
| T7 | Create | `apps/web/src/components/ui/data-display/meeple-card/__tests__/heading-level.smoke.test.tsx` | DOM smoke for 5 variants — tag swap + className unchanged |
| T8 | Modify | 5 consumer files (batch 1: feature grids) | Pass `headingLevel={2}` |
| T9 | Modify | 5 consumer files (batch 2: library/catalog cards) | Pass `headingLevel={2}` |
| T10 | Modify | 5 consumer files (batch 3: public/auth pages + chat entry) | Pass `headingLevel={2}` |
| T11 | Modify | 5 consumer files (batch 4: game-night planning + paused session) | Pass `headingLevel={2}` |
| T12 | Modify | 2 test files | Remove `heading-order: { enabled: false }` suppression |
| T13 | Modify | 5 test files | Add jest-axe scan with heading-order rule enabled |
| T14 | Verify + PR | Full suite + PR | Sanity check, push, PR |

Total: 14 tasks.

---

## Task 1: Add `headingLevel` prop to `MeepleCardProps`

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card/types.ts`

- [ ] **Step 1.1: Write the failing test**

Create file `apps/web/src/components/ui/data-display/meeple-card/__tests__/headingLevel-prop.contract.test.ts`:

```typescript
import { describe, it, expectTypeOf } from 'vitest';

import type { MeepleCardProps } from '../types';

describe('MeepleCardProps headingLevel contract', () => {
  it('accepts headingLevel as optional 2 | 3 | 4 union', () => {
    expectTypeOf<MeepleCardProps>().toHaveProperty('headingLevel').toEqualTypeOf<2 | 3 | 4 | undefined>();
  });

  it('allows omitting headingLevel (backwards compat)', () => {
    const props: MeepleCardProps = { entity: 'game', title: 'Catan' };
    expectTypeOf(props.headingLevel).toEqualTypeOf<2 | 3 | 4 | undefined>();
  });

  it('allows passing headingLevel=2', () => {
    const props: MeepleCardProps = { entity: 'game', title: 'Catan', headingLevel: 2 };
    expectTypeOf(props.headingLevel).toEqualTypeOf<2 | 3 | 4 | undefined>();
  });
});
```

- [ ] **Step 1.2: Run test to verify it fails**

Run from `apps/web/`:
```bash
pnpm vitest run src/components/ui/data-display/meeple-card/__tests__/headingLevel-prop.contract.test.ts
```
Expected: FAIL with TS error `'headingLevel' does not exist in type 'MeepleCardProps'` on test 3.

- [ ] **Step 1.3: Add `headingLevel` to `MeepleCardProps`**

In `apps/web/src/components/ui/data-display/meeple-card/types.ts`, find the `MeepleCardProps` interface and add `headingLevel` immediately after `coverEmoji?: string`:

Replace:
```typescript
  coverEmoji?: string;
  rating?: number;
```

With:
```typescript
  coverEmoji?: string;
  /**
   * Semantic heading level for the card's title element (2, 3, or 4).
   * Default: 3 for most variants (4: GridCard/ListCard/FeaturedCard/HeroCard);
   * 2 for FocusCard (full-focus detail card).
   *
   * Pass `headingLevel={2}` when this card is rendered in a grid below an
   * `<h1>` hero — without this, axe-core flags `heading-order` (h1→h3 jump
   * skipping h2). See #1842 spec for the audit of 20 grid-below-hero
   * consumer surfaces that need `headingLevel={2}`.
   */
  headingLevel?: 2 | 3 | 4;
  rating?: number;
```

- [ ] **Step 1.4: Run test to verify it passes**

Run from `apps/web/`:
```bash
pnpm vitest run src/components/ui/data-display/meeple-card/__tests__/headingLevel-prop.contract.test.ts
```
Expected: PASS (3 tests).

- [ ] **Step 1.5: Typecheck**

Run from `apps/web/`:
```bash
pnpm typecheck
```
Expected: 0 errors.

- [ ] **Step 1.6: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/types.ts apps/web/src/components/ui/data-display/meeple-card/__tests__/headingLevel-prop.contract.test.ts
git commit -m "feat(meeple-card): #1842 T1 add headingLevel?: 2|3|4 prop to MeepleCardProps"
```

---

## Task 2: GridCard dynamic HeadingTag (default 3)

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card/variants/GridCard.tsx`
- Modify: `apps/web/src/components/ui/data-display/meeple-card/variants/__tests__/GridCard.test.tsx`

- [ ] **Step 2.1: Append failing tests**

Append to `apps/web/src/components/ui/data-display/meeple-card/variants/__tests__/GridCard.test.tsx` (before the closing `});` of the last `describe`):

```tsx
/**
 * #1842 headingLevel prop — dynamic title heading tag.
 */
describe('GridCard headingLevel prop (#1842)', () => {
  it('renders <h3> by default (backwards compat)', () => {
    render(<GridCard entity="game" title="Catan default" />);
    const el = screen.getByText('Catan default');
    expect(el.tagName).toBe('H3');
  });

  it('renders <h2> when headingLevel={2}', () => {
    render(<GridCard entity="game" title="Catan h2" headingLevel={2} />);
    const el = screen.getByText('Catan h2');
    expect(el.tagName).toBe('H2');
  });

  it('renders <h4> when headingLevel={4}', () => {
    render(<GridCard entity="game" title="Catan h4" headingLevel={4} />);
    const el = screen.getByText('Catan h4');
    expect(el.tagName).toBe('H4');
  });

  it('preserves title className across all heading levels (visual stability)', () => {
    const { rerender } = render(<GridCard entity="game" title="X" />);
    const h3Class = screen.getByText('X').className;
    rerender(<GridCard entity="game" title="X" headingLevel={2} />);
    expect(screen.getByText('X').className).toBe(h3Class);
  });
});
```

- [ ] **Step 2.2: Run tests to verify they fail**

Run from `apps/web/`:
```bash
pnpm vitest run src/components/ui/data-display/meeple-card/variants/__tests__/GridCard.test.tsx
```
Expected: 3 of 4 new tests FAIL (h2/h4 variants not supported; current GridCard hardcodes `<h3>`).

- [ ] **Step 2.3: Implement dynamic HeadingTag in GridCard**

In `apps/web/src/components/ui/data-display/meeple-card/variants/GridCard.tsx`:

1. Add `headingLevel` to the destructuring (immediately after `coverEmoji`):
```tsx
    coverEmoji,
    headingLevel,
    rating,
```

2. Replace the hardcoded `<h3>` block (around line 78-80):

Find:
```tsx
        <h3 className="font-[var(--font-quicksand)] text-[0.95rem] font-bold leading-tight text-[var(--mc-text-primary)]">
          {title}
        </h3>
```

Replace with:
```tsx
        {(() => {
          const HeadingTag = `h${headingLevel ?? 3}` as 'h2' | 'h3' | 'h4';
          return (
            <HeadingTag className="font-[var(--font-quicksand)] text-[0.95rem] font-bold leading-tight text-[var(--mc-text-primary)]">
              {title}
            </HeadingTag>
          );
        })()}
```

- [ ] **Step 2.4: Run tests to verify they pass**

Run from `apps/web/`:
```bash
pnpm vitest run src/components/ui/data-display/meeple-card/variants/__tests__/GridCard.test.tsx
```
Expected: ALL PASS (15 existing + 4 new = 19 tests).

- [ ] **Step 2.5: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/variants/GridCard.tsx apps/web/src/components/ui/data-display/meeple-card/variants/__tests__/GridCard.test.tsx
git commit -m "feat(meeple-card): #1842 T2 GridCard dynamic HeadingTag (default 3)"
```

---

## Task 3: ListCard dynamic HeadingTag (default 3)

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card/variants/ListCard.tsx`
- Modify: `apps/web/src/components/ui/data-display/meeple-card/variants/__tests__/ListCard.test.tsx`

- [ ] **Step 3.1: Append failing tests**

Append to `apps/web/src/components/ui/data-display/meeple-card/variants/__tests__/ListCard.test.tsx` (before closing `});` of last describe):

```tsx
describe('ListCard headingLevel prop (#1842)', () => {
  it('renders <h3> by default', () => {
    render(<ListCard entity="game" title="Default title" />);
    expect(screen.getByText('Default title').tagName).toBe('H3');
  });

  it('renders <h2> when headingLevel={2}', () => {
    render(<ListCard entity="game" title="H2 title" headingLevel={2} />);
    expect(screen.getByText('H2 title').tagName).toBe('H2');
  });

  it('preserves className across heading levels', () => {
    const { rerender } = render(<ListCard entity="game" title="X" />);
    const h3Class = screen.getByText('X').className;
    rerender(<ListCard entity="game" title="X" headingLevel={2} />);
    expect(screen.getByText('X').className).toBe(h3Class);
  });
});
```

If `ListCard.test.tsx` does not import `screen` from RTL, add it.

- [ ] **Step 3.2: Run tests to verify failures**

Run from `apps/web/`:
```bash
pnpm vitest run src/components/ui/data-display/meeple-card/variants/__tests__/ListCard.test.tsx
```
Expected: 2 of 3 new tests FAIL (h2 not supported).

- [ ] **Step 3.3: Implement**

In `apps/web/src/components/ui/data-display/meeple-card/variants/ListCard.tsx`:

1. Destructure `headingLevel` from props.
2. Replace `<h3 ...>` block (around line 52-54):

Find:
```tsx
          <h3 className="truncate font-[var(--font-quicksand)] text-[0.88rem] font-bold text-[var(--mc-text-primary)]">
            {title}
          </h3>
```

Replace with:
```tsx
          {(() => {
            const HeadingTag = `h${headingLevel ?? 3}` as 'h2' | 'h3' | 'h4';
            return (
              <HeadingTag className="truncate font-[var(--font-quicksand)] text-[0.88rem] font-bold text-[var(--mc-text-primary)]">
                {title}
              </HeadingTag>
            );
          })()}
```

- [ ] **Step 3.4: Run tests to verify pass**

Run from `apps/web/`:
```bash
pnpm vitest run src/components/ui/data-display/meeple-card/variants/__tests__/ListCard.test.tsx
```
Expected: ALL PASS.

- [ ] **Step 3.5: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/variants/ListCard.tsx apps/web/src/components/ui/data-display/meeple-card/variants/__tests__/ListCard.test.tsx
git commit -m "feat(meeple-card): #1842 T3 ListCard dynamic HeadingTag (default 3)"
```

---

## Task 4: FeaturedCard dynamic HeadingTag (default 3)

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card/variants/FeaturedCard.tsx`
- Modify: `apps/web/src/components/ui/data-display/meeple-card/variants/__tests__/FeaturedCard.test.tsx`

- [ ] **Step 4.1: Append failing tests**

Append to `apps/web/src/components/ui/data-display/meeple-card/variants/__tests__/FeaturedCard.test.tsx`:

```tsx
describe('FeaturedCard headingLevel prop (#1842)', () => {
  it('renders <h3> by default', () => {
    render(<FeaturedCard entity="game" title="Default" />);
    expect(screen.getByText('Default').tagName).toBe('H3');
  });

  it('renders <h2> when headingLevel={2}', () => {
    render(<FeaturedCard entity="game" title="H2" headingLevel={2} />);
    expect(screen.getByText('H2').tagName).toBe('H2');
  });

  it('preserves className across heading levels', () => {
    const { rerender } = render(<FeaturedCard entity="game" title="X" />);
    const h3Class = screen.getByText('X').className;
    rerender(<FeaturedCard entity="game" title="X" headingLevel={2} />);
    expect(screen.getByText('X').className).toBe(h3Class);
  });
});
```

If imports missing, add `import { render, screen } from '@testing-library/react'`.

- [ ] **Step 4.2: Run failing**

```bash
pnpm vitest run src/components/ui/data-display/meeple-card/variants/__tests__/FeaturedCard.test.tsx
```

- [ ] **Step 4.3: Implement**

In `apps/web/src/components/ui/data-display/meeple-card/variants/FeaturedCard.tsx`:

1. Destructure `headingLevel`.
2. Replace `<h3>` block (around line 62-64):

Find:
```tsx
          <h3 className="font-[var(--font-quicksand)] text-[1.1rem] font-bold leading-tight text-[var(--mc-text-primary)]">
            {title}
          </h3>
```

Replace with:
```tsx
          {(() => {
            const HeadingTag = `h${headingLevel ?? 3}` as 'h2' | 'h3' | 'h4';
            return (
              <HeadingTag className="font-[var(--font-quicksand)] text-[1.1rem] font-bold leading-tight text-[var(--mc-text-primary)]">
                {title}
              </HeadingTag>
            );
          })()}
```

- [ ] **Step 4.4: Run pass**

- [ ] **Step 4.5: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/variants/FeaturedCard.tsx apps/web/src/components/ui/data-display/meeple-card/variants/__tests__/FeaturedCard.test.tsx
git commit -m "feat(meeple-card): #1842 T4 FeaturedCard dynamic HeadingTag (default 3)"
```

---

## Task 5: HeroCard dynamic HeadingTag (default 3)

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card/variants/HeroCard.tsx`
- Create OR modify: `apps/web/src/components/ui/data-display/meeple-card/variants/__tests__/HeroCard.test.tsx`

Note: HeroCard.test.tsx may not exist. If it doesn't, create it.

- [ ] **Step 5.1: Verify HeroCard.test.tsx exists**

Run from repo root:
```bash
ls apps/web/src/components/ui/data-display/meeple-card/variants/__tests__/HeroCard.test.tsx 2>&1 | head -3
```

If missing, create it with the test below as the only content (along with imports).

- [ ] **Step 5.2: Write failing tests**

Either append (if file exists) or create with:

```tsx
import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';

import { HeroCard } from '../HeroCard';

describe('HeroCard headingLevel prop (#1842)', () => {
  it('renders <h3> by default', () => {
    render(<HeroCard entity="game" title="Default" />);
    expect(screen.getByText('Default').tagName).toBe('H3');
  });

  it('renders <h2> when headingLevel={2}', () => {
    render(<HeroCard entity="game" title="H2" headingLevel={2} />);
    expect(screen.getByText('H2').tagName).toBe('H2');
  });

  it('preserves className across heading levels', () => {
    const { rerender } = render(<HeroCard entity="game" title="X" />);
    const h3Class = screen.getByText('X').className;
    rerender(<HeroCard entity="game" title="X" headingLevel={2} />);
    expect(screen.getByText('X').className).toBe(h3Class);
  });
});
```

- [ ] **Step 5.3: Run failing**

```bash
pnpm vitest run src/components/ui/data-display/meeple-card/variants/__tests__/HeroCard.test.tsx
```

- [ ] **Step 5.4: Implement**

In `apps/web/src/components/ui/data-display/meeple-card/variants/HeroCard.tsx`:

1. Destructure `headingLevel`.
2. Replace `<h3>` block (around line 81-83):

Find:
```tsx
        <h3 className="font-[var(--font-quicksand)] text-2xl font-bold leading-tight text-white drop-shadow-md">
          {title}
        </h3>
```

Replace with:
```tsx
        {(() => {
          const HeadingTag = `h${headingLevel ?? 3}` as 'h2' | 'h3' | 'h4';
          return (
            <HeadingTag className="font-[var(--font-quicksand)] text-2xl font-bold leading-tight text-white drop-shadow-md">
              {title}
            </HeadingTag>
          );
        })()}
```

- [ ] **Step 5.5: Run pass**

- [ ] **Step 5.6: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/variants/HeroCard.tsx apps/web/src/components/ui/data-display/meeple-card/variants/__tests__/HeroCard.test.tsx
git commit -m "feat(meeple-card): #1842 T5 HeroCard dynamic HeadingTag (default 3)"
```

---

## Task 6: FocusCard dynamic HeadingTag (default 2)

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card/variants/FocusCard.tsx`
- Modify: `apps/web/src/components/ui/data-display/meeple-card/variants/__tests__/FocusCard.test.tsx`

FocusCard differs: it currently renders `<h2>` hardcoded (not `<h3>`). Default for this variant is `2`.

- [ ] **Step 6.1: Append failing tests**

Append to `apps/web/src/components/ui/data-display/meeple-card/variants/__tests__/FocusCard.test.tsx`:

```tsx
describe('FocusCard headingLevel prop (#1842)', () => {
  it('renders <h2> by default (preserves existing behavior — FocusCard is page-hero-level)', () => {
    render(<FocusCard entity="game" title="Default" />);
    expect(screen.getByText('Default').tagName).toBe('H2');
  });

  it('renders <h3> when headingLevel={3}', () => {
    render(<FocusCard entity="game" title="H3" headingLevel={3} />);
    expect(screen.getByText('H3').tagName).toBe('H3');
  });

  it('preserves className across heading levels', () => {
    const { rerender } = render(<FocusCard entity="game" title="X" />);
    const h2Class = screen.getByText('X').className;
    rerender(<FocusCard entity="game" title="X" headingLevel={3} />);
    expect(screen.getByText('X').className).toBe(h2Class);
  });
});
```

- [ ] **Step 6.2: Run failing**

```bash
pnpm vitest run src/components/ui/data-display/meeple-card/variants/__tests__/FocusCard.test.tsx
```
Expected: 1 of 3 new tests FAIL (h3 variant not supported; current renders only h2).

- [ ] **Step 6.3: Implement**

In `apps/web/src/components/ui/data-display/meeple-card/variants/FocusCard.tsx`:

1. Destructure `headingLevel`.
2. Replace `<h2>` block (around line 50):

Find:
```tsx
          <h2 className="font-[var(--font-quicksand)] text-xl font-bold leading-tight text-[var(--mc-text-primary)]">
```

Replace with:
```tsx
          {(() => {
            const HeadingTag = `h${headingLevel ?? 2}` as 'h2' | 'h3' | 'h4';
            return (
              <HeadingTag className="font-[var(--font-quicksand)] text-xl font-bold leading-tight text-[var(--mc-text-primary)]">
```

And close the IIFE block after the closing `</h2>` (around 4 lines later):

Find the closing:
```tsx
          </h2>
```

Replace with:
```tsx
              </HeadingTag>
            );
          })()}
```

- [ ] **Step 6.4: Run pass**

```bash
pnpm vitest run src/components/ui/data-display/meeple-card/variants/__tests__/FocusCard.test.tsx
```
Expected: ALL PASS.

- [ ] **Step 6.5: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/variants/FocusCard.tsx apps/web/src/components/ui/data-display/meeple-card/variants/__tests__/FocusCard.test.tsx
git commit -m "feat(meeple-card): #1842 T6 FocusCard dynamic HeadingTag (default 2)"
```

---

## Task 7: DOM smoke test for 5 variants

**Files:**
- Create: `apps/web/src/components/ui/data-display/meeple-card/__tests__/heading-level.smoke.test.tsx`

This is a meta-test that verifies the API contract works through the top-level `MeepleCard` component for all 5 variants.

- [ ] **Step 7.1: Create smoke test**

Create the file with:

```tsx
/**
 * #1842 headingLevel cross-variant smoke test.
 *
 * Verifies the MeepleCard API contract: passing `headingLevel` produces
 * the correct semantic tag for each of the 5 variants that own a title
 * heading (GridCard, ListCard, FeaturedCard, HeroCard, FocusCard).
 *
 * CompactCard is intentionally skipped — it renders <span> for the title
 * (compact-ticker semantic), not a heading.
 *
 * Per DEC-5: no visual regression test. Assertions cover semantic tag
 * + className stability only.
 */
import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';

import { MeepleCard } from '../MeepleCard';

import type { MeepleCardVariant } from '../types';

interface VariantSpec {
  variant: MeepleCardVariant;
  defaultTag: 'H2' | 'H3';
}

const variants: VariantSpec[] = [
  { variant: 'grid', defaultTag: 'H3' },
  { variant: 'list', defaultTag: 'H3' },
  { variant: 'featured', defaultTag: 'H3' },
  { variant: 'hero', defaultTag: 'H3' },
  { variant: 'focus', defaultTag: 'H2' },
];

describe('MeepleCard headingLevel smoke (#1842)', () => {
  for (const { variant, defaultTag } of variants) {
    it(`${variant} renders ${defaultTag} by default`, () => {
      render(<MeepleCard entity="game" title={`${variant}-default`} variant={variant} />);
      expect(screen.getByText(`${variant}-default`).tagName).toBe(defaultTag);
    });

    it(`${variant} renders H2 when headingLevel={2}`, () => {
      render(
        <MeepleCard entity="game" title={`${variant}-h2`} variant={variant} headingLevel={2} />
      );
      expect(screen.getByText(`${variant}-h2`).tagName).toBe('H2');
    });

    it(`${variant} preserves className when headingLevel changes (visual stability)`, () => {
      const { rerender } = render(
        <MeepleCard entity="game" title={`${variant}-stable`} variant={variant} />
      );
      const defaultClass = screen.getByText(`${variant}-stable`).className;
      rerender(
        <MeepleCard entity="game" title={`${variant}-stable`} variant={variant} headingLevel={4} />
      );
      expect(screen.getByText(`${variant}-stable`).className).toBe(defaultClass);
    });
  }
});
```

- [ ] **Step 7.2: Run smoke test**

```bash
pnpm vitest run src/components/ui/data-display/meeple-card/__tests__/heading-level.smoke.test.tsx
```
Expected: ALL PASS (15 tests = 5 variants × 3 assertions).

- [ ] **Step 7.3: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/__tests__/heading-level.smoke.test.tsx
git commit -m "test(meeple-card): #1842 T7 cross-variant headingLevel smoke (5 variants × 3 assertions)"
```

---

## Task 8: Consumer migration batch 1 — feature grids (5 files)

**Files (5):**
- `apps/web/src/components/features/agents/AgentsResultsGrid.tsx`
- `apps/web/src/components/features/games/GamesResultsGrid.tsx`
- `apps/web/src/components/features/players/PlayersResultsGrid.tsx`
- `apps/web/src/components/features/library/LibraryHybridGrid.tsx`
- `apps/web/src/components/features/home/HomeFeed.tsx`

Each file passes `headingLevel={2}` to `<MeepleCard>` at the call site. The change is 1-line additive — no failing-first test required (the prop is type-checked and the migration is mechanical pass-through).

- [ ] **Step 8.1: Migrate `AgentsResultsGrid.tsx`**

In `apps/web/src/components/features/agents/AgentsResultsGrid.tsx`, find the `<MeepleCard` JSX block and add `headingLevel={2}` as a prop. Example:

Find:
```tsx
<MeepleCard
  entity="agent"
  ...
/>
```

Add `headingLevel={2}`:
```tsx
<MeepleCard
  entity="agent"
  headingLevel={2}
  ...
/>
```

(The exact line varies — search for `<MeepleCard` in the file. If multiple usages exist, update all.)

- [ ] **Step 8.2: Migrate `GamesResultsGrid.tsx`**

Same pattern: add `headingLevel={2}` to `<MeepleCard>` invocations.

- [ ] **Step 8.3: Migrate `PlayersResultsGrid.tsx`**

Same pattern.

- [ ] **Step 8.4: Migrate `LibraryHybridGrid.tsx`**

Same pattern.

- [ ] **Step 8.5: Migrate `HomeFeed.tsx`**

Same pattern. Note HomeFeed may use MeepleCard for multiple sections; update ALL MeepleCard invocations.

- [ ] **Step 8.6: Run typecheck**

```bash
pnpm typecheck
```
Expected: 0 errors.

- [ ] **Step 8.7: Run feature tests for these surfaces**

```bash
pnpm vitest run src/components/features/agents src/components/features/games src/components/features/players src/components/features/library src/components/features/home
```
Expected: ALL PASS (no regressions — additive prop change).

- [ ] **Step 8.8: Commit**

```bash
git add apps/web/src/components/features/agents/AgentsResultsGrid.tsx apps/web/src/components/features/games/GamesResultsGrid.tsx apps/web/src/components/features/players/PlayersResultsGrid.tsx apps/web/src/components/features/library/LibraryHybridGrid.tsx apps/web/src/components/features/home/HomeFeed.tsx
git commit -m "feat(meeple-card): #1842 T8 batch 1 — feature grids pass headingLevel={2} (agents/games/players/library/home)"
```

---

## Task 9: Consumer migration batch 2 — library/catalog cards (5 files)

**Files (5):**
- `apps/web/src/components/library/MeepleLibraryGameCard.tsx`
- `apps/web/src/components/library/MeepleUserLibraryCard.tsx`
- `apps/web/src/components/catalog/MeepleGameCatalogCard.tsx`
- `apps/web/src/components/wishlist/MeepleWishlistCard.tsx`
- `apps/web/src/app/(authenticated)/library/wishlist/page.tsx`

- [ ] **Step 9.1: Migrate `MeepleLibraryGameCard.tsx`**

Find `<MeepleCard` invocations and add `headingLevel={2}`.

- [ ] **Step 9.2: Migrate `MeepleUserLibraryCard.tsx`**

Same pattern.

- [ ] **Step 9.3: Migrate `MeepleGameCatalogCard.tsx`**

Same pattern.

- [ ] **Step 9.4: Migrate `MeepleWishlistCard.tsx`**

Same pattern. Note: this is the card component. The page `library/wishlist/page.tsx` consumes it.

- [ ] **Step 9.5: Migrate `app/(authenticated)/library/wishlist/page.tsx`**

The page already renders `<h1>My Wishlist</h1>` and uses `<MeepleWishlistCard>` below. The card already gets `headingLevel={2}` from Step 9.4 (if MeepleWishlistCard is configured correctly). If MeepleWishlistCard is a thin wrapper, the page itself does not pass MeepleCard prop directly — verify and either skip page OR pass through MeepleWishlistCard prop.

- [ ] **Step 9.6: Typecheck + tests**

```bash
pnpm typecheck && pnpm vitest run src/components/library src/components/catalog src/components/wishlist
```

- [ ] **Step 9.7: Commit**

```bash
git add apps/web/src/components/library/MeepleLibraryGameCard.tsx apps/web/src/components/library/MeepleUserLibraryCard.tsx apps/web/src/components/catalog/MeepleGameCatalogCard.tsx apps/web/src/components/wishlist/MeepleWishlistCard.tsx apps/web/src/app/\(authenticated\)/library/wishlist/page.tsx
git commit -m "feat(meeple-card): #1842 T9 batch 2 — library/catalog/wishlist cards pass headingLevel={2}"
```

---

## Task 10: Consumer migration batch 3 — public/auth pages + chat entry (5 files)

**Files (5):**
- `apps/web/src/app/(public)/library/shared/[token]/page.tsx`
- `apps/web/src/app/(authenticated)/gamebook/upload/_components/GamebookUploadClient.tsx`
- `apps/web/src/components/chat/entry/AgentSelector.tsx`
- `apps/web/src/components/chat/entry/GameSelector.tsx`
- `apps/web/src/components/chat-unified/AgentCreationWizard.tsx`

- [ ] **Step 10.1–10.5: Migrate each file** with `headingLevel={2}` on `<MeepleCard>` invocations.

- [ ] **Step 10.6: Typecheck + tests**

```bash
pnpm typecheck && pnpm vitest run src/components/chat src/components/chat-unified src/app/\(authenticated\)/gamebook src/app/\(public\)/library
```

- [ ] **Step 10.7: Commit**

```bash
git add apps/web/src/app/\(public\)/library/shared/\[token\]/page.tsx apps/web/src/app/\(authenticated\)/gamebook/upload/_components/GamebookUploadClient.tsx apps/web/src/components/chat/entry/AgentSelector.tsx apps/web/src/components/chat/entry/GameSelector.tsx apps/web/src/components/chat-unified/AgentCreationWizard.tsx
git commit -m "feat(meeple-card): #1842 T10 batch 3 — public/auth pages + chat entry pass headingLevel={2}"
```

---

## Task 11: Consumer migration batch 4 — game-night planning + paused session (5 files)

**Files (5):**
- `apps/web/src/components/game-night/MeepleEventCard.tsx`
- `apps/web/src/components/game-night/planning/MeepleGameNightCard.tsx`
- `apps/web/src/components/game-night/planning/MeepleDealtGameCard.tsx`
- `apps/web/src/components/game-night/planning/MeepleAISuggestionCard.tsx`
- `apps/web/src/components/library/private-game-detail/MeeplePausedSessionCard.tsx`

- [ ] **Step 11.1–11.5: Migrate each file** with `headingLevel={2}` on `<MeepleCard>` invocations.

- [ ] **Step 11.6: Typecheck + tests**

```bash
pnpm typecheck && pnpm vitest run src/components/game-night src/components/library/private-game-detail
```

- [ ] **Step 11.7: Commit**

```bash
git add apps/web/src/components/game-night/MeepleEventCard.tsx apps/web/src/components/game-night/planning/MeepleGameNightCard.tsx apps/web/src/components/game-night/planning/MeepleDealtGameCard.tsx apps/web/src/components/game-night/planning/MeepleAISuggestionCard.tsx apps/web/src/components/library/private-game-detail/MeeplePausedSessionCard.tsx
git commit -m "feat(meeple-card): #1842 T11 batch 4 — game-night planning + paused session pass headingLevel={2}"
```

---

## Task 12: Re-enable `heading-order` axe rule on 2 existing suppressions

**Files (2):**
- `apps/web/src/app/(authenticated)/agents/_components/__tests__/AgentsLibraryView.test.tsx`
- `apps/web/src/components/features/gamebook/__tests__/LibroGameDetailView.test.tsx`

These two files contain `rules: { 'heading-order': { enabled: false } }` overrides added in #1841 (P164 pattern) and earlier #1481 cluster. Now that consumers pass `headingLevel={2}`, the rule should pass without suppression.

- [ ] **Step 12.1: Remove suppression in `AgentsLibraryView.test.tsx`**

In `apps/web/src/app/(authenticated)/agents/_components/__tests__/AgentsLibraryView.test.tsx`, find the axe scan at line ~359 with `{ rules: { 'heading-order': { enabled: false } } }`. Remove the `rules` option, leaving just the call:

Find:
```tsx
      await axe(container, { rules: { 'heading-order': { enabled: false } } })
```

Replace with:
```tsx
      await axe(container)
```

Also remove the associated JSDoc/comment block explaining the suppression (if present, look at lines 350-358).

- [ ] **Step 12.2: Remove suppression in `LibroGameDetailView.test.tsx`**

In `apps/web/src/components/features/gamebook/__tests__/LibroGameDetailView.test.tsx`, find the axe scan around line 157-163:

Find:
```tsx
    const results = await axe(container, {
      rules: {
        'heading-order': { enabled: false },
      },
    });
```

Replace with:
```tsx
    const results = await axe(container);
```

Also remove the associated comment block explaining the suppression.

- [ ] **Step 12.3: Run both tests**

```bash
pnpm vitest run src/app/\(authenticated\)/agents/_components/__tests__/AgentsLibraryView.test.tsx src/components/features/gamebook/__tests__/LibroGameDetailView.test.tsx
```

Expected: BOTH PASS without violations (assuming earlier consumer migrations have wired `headingLevel={2}` correctly).

If FAIL with `heading-order` violation: investigate which surface didn't get the prop and add it. The error message will identify the violating element via CSS selector.

- [ ] **Step 12.4: Commit**

```bash
git add apps/web/src/app/\(authenticated\)/agents/_components/__tests__/AgentsLibraryView.test.tsx apps/web/src/components/features/gamebook/__tests__/LibroGameDetailView.test.tsx
git commit -m "test(a11y): #1842 T12 re-enable heading-order axe rule on AgentsLibraryView + LibroGameDetailView"
```

---

## Task 13: Add `heading-order` axe scans to 5 key surfaces

**Files (5):**
- `apps/web/src/app/(authenticated)/library/_components/__tests__/LibraryHub.test.tsx`
- `apps/web/src/components/features/toolkits-index/__tests__/HubToolkitsBody.test.tsx`
- `apps/web/src/components/features/players/__tests__/PlayersResultsGrid.test.tsx`
- `apps/web/src/components/features/games/__tests__/GamesResultsGrid.test.tsx`
- `apps/web/src/components/game-night/planning/__tests__/GameNightPlanningLayout.test.tsx`

For each file, add a new `it('passes heading-order axe rule', ...)` test that renders the component and runs jest-axe with default (heading-order ENABLED) rule.

- [ ] **Step 13.1: Add axe scan to `LibraryHub.test.tsx`**

In the file, check if `import { axe } from 'jest-axe';` exists at the top. If not, add it.

At the end of the existing `describe(...)` block (or in a new `describe('a11y axe (#1842)', ...)` block), add:

```tsx
  it('passes heading-order axe rule (#1842)', async () => {
    const { container } = render(<LibraryHub /* required props */ />);
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });
```

(Replace `<LibraryHub />` with the actual signature including required props. Use existing tests in the same file as a reference for how to set up the render.)

- [ ] **Step 13.2: Add axe scan to `HubToolkitsBody.test.tsx`**

Same pattern.

- [ ] **Step 13.3: Add axe scan to `PlayersResultsGrid.test.tsx`**

Same pattern.

- [ ] **Step 13.4: Add axe scan to `GamesResultsGrid.test.tsx`**

Same pattern.

- [ ] **Step 13.5: Add axe scan to `GameNightPlanningLayout.test.tsx`**

Same pattern.

- [ ] **Step 13.6: Run all 5 new axe scans**

```bash
pnpm vitest run src/app/\(authenticated\)/library/_components/__tests__/LibraryHub.test.tsx src/components/features/toolkits-index/__tests__/HubToolkitsBody.test.tsx src/components/features/players/__tests__/PlayersResultsGrid.test.tsx src/components/features/games/__tests__/GamesResultsGrid.test.tsx src/components/game-night/planning/__tests__/GameNightPlanningLayout.test.tsx
```

Expected: ALL PASS (0 axe violations on each surface).

If any FAIL, the violation will point to a card whose consumer didn't get `headingLevel={2}` — go back to T8-T11 and add the prop.

- [ ] **Step 13.7: Commit**

```bash
git add apps/web/src/app/\(authenticated\)/library/_components/__tests__/LibraryHub.test.tsx apps/web/src/components/features/toolkits-index/__tests__/HubToolkitsBody.test.tsx apps/web/src/components/features/players/__tests__/PlayersResultsGrid.test.tsx apps/web/src/components/features/games/__tests__/GamesResultsGrid.test.tsx apps/web/src/components/game-night/planning/__tests__/GameNightPlanningLayout.test.tsx
git commit -m "test(a11y): #1842 T13 add heading-order axe scans to 5 key surfaces (LibraryHub/HubToolkitsBody/PlayersResultsGrid/GamesResultsGrid/GameNightPlanningLayout)"
```

---

## Task 14: Final verify + push + PR

- [ ] **Step 14.1: Run full meeple-card suite**

```bash
pnpm vitest run src/components/ui/data-display/meeple-card/
```
Expected: ALL PASS (>270 tests — includes the new heading-level tests).

- [ ] **Step 14.2: Run full typecheck**

```bash
pnpm typecheck
```
Expected: 0 errors.

- [ ] **Step 14.3: Run lint**

```bash
pnpm lint
```
Expected: 0 errors. Pre-existing warnings are acceptable.

- [ ] **Step 14.4: Push branch**

From repo root:
```bash
git push -u origin feature/issue-1842-meeple-card-heading-level
```

- [ ] **Step 14.5: Create PR base main-dev**

```bash
gh pr create --base main-dev --title "fix(a11y): #1842 MeepleCard headingLevel prop — close heading-order WCAG suppressions" --body "$(cat <<'EOF'
## Summary

Implements #1842 — adds `headingLevel?: 2 | 3 | 4` prop to `MeepleCard` (default `3`) and migrates 20 grid-below-h1-hero consumer surfaces. Closes the `heading-order` axe suppression debt from #1841 (P164 pattern) and #1481.

Surgical primitive prop addition per spec [`docs/superpowers/specs/2026-06-04-meeple-card-heading-level-a11y-design.md`](docs/superpowers/specs/2026-06-04-meeple-card-heading-level-a11y-design.md) and plan [`docs/superpowers/plans/2026-06-04-meeple-card-heading-level-a11y.md`](docs/superpowers/plans/2026-06-04-meeple-card-heading-level-a11y.md).

## Changes

- **types.ts** — Add `headingLevel?: 2 | 3 | 4` to `MeepleCardProps` (additive, backwards compat).
- **5 variants** — Dynamic `<HeadingTag>` rendering instead of hardcoded `<h3>` (or `<h2>` for FocusCard). CompactCard unchanged (uses `<span>`, no heading element).
- **20 consumers migrated** — Pass `headingLevel={2}` at call sites (4 batches × 5 files each).
- **2 axe suppressions removed** — Re-enable `heading-order` rule on `AgentsLibraryView.test.tsx` + `LibroGameDetailView.test.tsx`.
- **5 new axe scans added** — `LibraryHub`, `HubToolkitsBody`, `PlayersResultsGrid`, `GamesResultsGrid`, `GameNightPlanningLayout`.

## Decisions (locked in brainstorming 2026-06-04)

| ID | Decision |
|---|---|
| DEC-1 | Systematic audit of 59 consumers (issue body underestimated scope 2x) |
| DEC-2 | Approach A — additive `headingLevel` prop, default 3 |
| DEC-3 | Per-consumer migration (NOT default change to 2) |
| DEC-4 | 5 new axe scans + 2 existing suppression re-enable |
| DEC-5 | DOM structure assertions only (no visual regression infra) |
| DEC-6 | Defer 7 NEEDS_INSPECTION files to follow-up |
| DEC-7 | 4 variants migrate (`<h3>` → dynamic); FocusCard already h2 (prop-accept with default 2); CompactCard skip |

## Tests

- 5 new variant tests + 1 cross-variant smoke (15 tests) + 1 contract test = 27+ new assertions
- 2 existing axe suppressions removed → rule re-enabled in production CI
- 5 new axe scans cover key grid surfaces
- Full meeple-card suite: 270+ tests pass, typecheck 0 errors

## Out of scope (deferred follow-up)

- 7 NEEDS_INSPECTION files (`entity-list-view` polymorphic primitive, `shared-games-grid` passthrough, `MeepleChatCard` adapter, `MeepleContributorCard` deep-section, `MeepleParticipantCard` Scoreboard label, `PrivateGamesClient` unused-h1, `LibraryPanel` unused) — tracked here for follow-up issue.

## Related

- Closes #1842
- Source debt: #1841 (P164 pattern), #1481 (earlier cluster)
- Origin coverage: #1569 (jest-axe agents-index)
- Spec: `docs/superpowers/specs/2026-06-04-meeple-card-heading-level-a11y-design.md`
- Plan: `docs/superpowers/plans/2026-06-04-meeple-card-heading-level-a11y.md`
EOF
)"
```

- [ ] **Step 14.6: Watch CI**

```bash
gh pr checks --watch
```
Expected: All checks green. If `Frontend Tests` shard fails per #1851 baseline (`AdvancedFilterPanel` flake), document in PR comment.

- [ ] **Step 14.7: Admin merge (P145 pattern: user direttiva richiede merge + cleanup)**

```bash
gh pr merge --squash --admin --delete-branch
```

- [ ] **Step 14.8: Sync main-dev + cleanup**

```bash
git checkout main-dev && git pull --ff-only origin main-dev
```

Local branch should already be deleted by `--delete-branch`. If not:
```bash
git branch -D feature/issue-1842-meeple-card-heading-level
```

---

## Self-review checklist

**1. Spec coverage**:
- §2 DEC-1..DEC-7 → T1-T13 implement all 7 decisions
- §3 Architecture (dynamic HeadingTag, default 3, per-consumer) → T2-T6 + T8-T11
- §4 File map (5 primitive + 20 consumer + 2 test re-enable + 5 axe scans + 1 smoke) → T1-T7 + T8-T11 + T12 + T13
- §5 Data flow (HeadingTag dispatch) → T2-T6 implementation
- §6 Error handling (TS literal union + fallback) → T1 type definition
- §7 Testing strategy → T7 smoke + T12 re-enable + T13 new scans
- §8 Out of scope (NEEDS_INSPECTION 7 files) → documented in PR body, not tasked
- §9 Rollout & risk → T14 verify + designer-review-deferred via PR description

✅ All sections covered.

**2. Placeholder scan**: no "TBD", "TODO", or "implement later" directives in any step. Every step has concrete code or commands.

**3. Type consistency**:
- `MeepleCardProps.headingLevel?: 2 | 3 | 4` defined in T1, used consistently in T2-T7 + T9 contract test
- `HeadingTag = \`h${headingLevel ?? N}\` as 'h2' | 'h3' | 'h4'` — identical pattern across T2-T6 (variants), only default level varies (3 for most, 2 for FocusCard)
- `MeepleCardVariant` type used in T7 smoke loop
- Consumer migration commands consistent across T8-T11 (always `headingLevel={2}`)

✅ Type consistency verified.

If any spec requirement is missing from the task list, add a task. Otherwise, plan is complete.
