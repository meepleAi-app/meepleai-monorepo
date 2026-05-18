# Gamebook Checkout Modal Implementation Plan (Issue #953)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship a 4-step checkout modal + 47/50 soft warning component for the libro-game quota/credits flow (mockup `sp6-libro-game-quota-credits.jsx`), 100% frontend (no backend, no real Stripe), wired into the existing `QuotaWidget` CTA on `/gamebook`.

**Architecture:** Pure-presentational step components (Step1/Step2/Step3/Step4) orchestrated by a `CheckoutModal` that owns the FSM (`step` + `paymentState`) and the simulated 2s payment latency. Modal uses shadcn/ui `Dialog` for focus trap + ESC + ARIA. Soft warning is an independent component with `toast-mobile` / `modal-desktop` variants, gated by a `useSoftWarningDismissal` hook that reads/writes `sessionStorage`. Pack catalog hardcoded in a TS constant. Replacement seam: `onPurchaseSuccess(packId, credits)` callback fired on Step 4 entry.

**Tech Stack:** Next.js 16 / React 19 / TypeScript / Tailwind 4 / shadcn/ui (Dialog) / Vitest + Testing Library + userEvent / Playwright / Storybook + Chromatic / axe-core

**Spec reference:** `docs/superpowers/specs/2026-05-18-issue-953-gamebook-checkout-modal-design.md`
**Mockup reference:** `admin-mockups/design_files/sp6-libro-game-quota-credits.jsx`
**Branch:** `feature/issue-953-quota-credits-checkout-modal` (already created from `main-dev`)

---

## File Structure

### New files
```
apps/web/src/lib/gamebook/
├── checkout-packs.ts                            (constant + formatEur + types)
└── hooks/
    └── useSoftWarningDismissal.ts               (sessionStorage hook)

apps/web/src/components/features/gamebook/
├── CheckoutModal.tsx                            (orchestrator)
├── SoftWarningCredits.tsx                       (toast/modal variants)
└── checkout/
    ├── CheckoutStepIndicator.tsx                (4-step variant; existing StepIndicator is 3-step hardcoded)
    ├── Step1QuotaReached.tsx
    ├── Step2PackPicker.tsx
    ├── Step3CheckoutForm.tsx
    └── Step4Success.tsx

apps/web/src/components/features/gamebook/__tests__/
├── CheckoutModal.test.tsx
├── SoftWarningCredits.test.tsx
└── checkout/
    ├── CheckoutStepIndicator.test.tsx
    ├── Step1QuotaReached.test.tsx
    ├── Step2PackPicker.test.tsx
    ├── Step3CheckoutForm.test.tsx
    └── Step4Success.test.tsx

apps/web/src/lib/gamebook/__tests__/
├── checkout-packs.test.ts
└── hooks/
    └── useSoftWarningDismissal.test.ts

apps/web/src/stories/features/gamebook/
└── CheckoutModal.stories.tsx                    (Chromatic: 7 states × 2 themes × 2 viewports)

apps/web/e2e/
└── gamebook-checkout-modal.spec.ts              (Playwright @ci happy path)
```

### Modified files
```
apps/web/src/components/features/gamebook/index.ts     (re-export new components)
apps/web/src/app/(authenticated)/gamebook/_components/GamebookIndexView.tsx
  → wire CheckoutModal + SoftWarningCredits + useSoftWarningDismissal
docs/for-developers/frontend/v2-migration-matrix.md     (status=done + PR link)
```

### Single-responsibility map

| File | One-sentence responsibility |
|------|----------------------------|
| `checkout-packs.ts` | Provide the 3-pack catalog + EUR formatter — pure data, no React, no DOM |
| `useSoftWarningDismissal.ts` | Decide whether the 47/50 warning should render and persist dismissal to sessionStorage |
| `CheckoutStepIndicator.tsx` | Render 4-step horizontal progress bar (decoration); pure presentational |
| `Step1QuotaReached.tsx` | Render "Quota raggiunta 50/50" announcement card with two CTAs |
| `Step2PackPicker.tsx` | Render 3-pack picker with single-select and fire `onSelect(packId)` + `onContinue()` |
| `Step3CheckoutForm.tsx` | Render visual-only checkout form in `filled`/`loading`/`failed` sub-states |
| `Step4Success.tsx` | Render success recap + confetti + "Torna al gioco" CTA |
| `CheckoutModal.tsx` | Own FSM, mount shadcn Dialog, route step rendering, simulate payment latency |
| `SoftWarningCredits.tsx` | Render 47/50 warning as bottom-toast (mobile) or center-modal (desktop) |
| `CheckoutModal.stories.tsx` | Chromatic visual snapshots for all 7 mockup states |
| `gamebook-checkout-modal.spec.ts` | Playwright E2E happy path (soft warning → modal → success → close) |

---

## Conventions

- **Test framework:** Vitest + `@testing-library/react` + `@testing-library/user-event` + `vitest/globals`
- **Mocking callbacks:** `vi.fn()`
- **Imports:** `'use client'` directive on every React component that uses hooks/state
- **Token discipline (DS-15):** Only semantic tokens (`bg-background`, `bg-card`, `text-foreground`, `border-border`) and entity utilities (`bg-entity-toolkit`, `text-entity-event`, `bg-entity-event/10`). NEVER `bg-white`, `bg-slate-*`, `text-gray-*`, etc. The ESLint rule `local/no-hardcoded-color-utility` is `error`-level since DS-15.
- **i18n pattern (Wave D.3):** Every component takes a `labels` prop; no Italian string literals inside components except in test mocks.
- **data-slot:** Every meaningful element gets `data-slot="<component-name>"` (kebab-case) for E2E selectors.
- **a11y minimums:** `aria-modal`, `aria-labelledby`, `role`, `aria-current="step"` on active step, `aria-busy` on loading button, `prefers-reduced-motion` guarded animations.
- **Commit style:** `feat(gamebook): description (#953)` or `test(gamebook): description (#953)`. Sub-Claude commit suffix per CLAUDE.md (`Co-Authored-By: Claude...`). One commit per task.

---

## Tasks

### Task 0: Pre-flight check

**Files:** none (verification only)

- [ ] **Step 1: Verify branch + spec already committed**

```bash
git branch --show-current
# expect: feature/issue-953-quota-credits-checkout-modal

git log --oneline -3
# expect first line: "docs(spec): #953 gamebook checkout modal design"

git status --short
# expect: empty (clean tree)
```

If any expectation fails, STOP and ask for guidance.

- [ ] **Step 2: Verify dependencies present**

```bash
grep '"@radix-ui/react-dialog"' apps/web/package.json && echo "OK: radix-dialog"
grep '"clsx"' apps/web/package.json && echo "OK: clsx"
grep '"@testing-library/react"' apps/web/package.json && echo "OK: RTL"
grep '"@testing-library/user-event"' apps/web/package.json && echo "OK: user-event"
```

Each line must echo `OK: <dep>`. If any missing, STOP and report.

---

### Task 1: Pack catalog (`checkout-packs.ts`)

**Files:**
- Create: `apps/web/src/lib/gamebook/checkout-packs.ts`
- Test: `apps/web/src/lib/gamebook/__tests__/checkout-packs.test.ts`

- [ ] **Step 1: Write the failing test**

Create `apps/web/src/lib/gamebook/__tests__/checkout-packs.test.ts`:

```typescript
import { describe, it, expect } from 'vitest';

import { CHECKOUT_PACKS, formatEur, type CheckoutPack } from '../checkout-packs';

describe('CHECKOUT_PACKS', () => {
  it('has exactly 3 entries', () => {
    expect(CHECKOUT_PACKS).toHaveLength(3);
  });

  it('contains starter, mid, pro with correct pricing', () => {
    const ids = CHECKOUT_PACKS.map((p) => p.id);
    expect(ids).toEqual(['starter', 'mid', 'pro']);
  });

  it('each pack has consistent priceEur / credits ratio close to perParagraphEur', () => {
    for (const pack of CHECKOUT_PACKS) {
      const ratio = pack.priceEur / pack.credits;
      expect(Math.abs(ratio - pack.perParagraphEur)).toBeLessThan(0.001);
    }
  });

  it('readonly array type at compile time (smoke check)', () => {
    // @ts-expect-error - cannot mutate readonly array
    CHECKOUT_PACKS.push({} as CheckoutPack);
  });

  it('starter has popular badge, pro has save badge, mid has no badge', () => {
    const find = (id: string) => CHECKOUT_PACKS.find((p) => p.id === id)!;
    expect(find('starter').badge).toBe('popular');
    expect(find('mid').badge).toBeNull();
    expect(find('pro').badge).toBe('save');
  });
});

describe('formatEur', () => {
  it('formats integer values without decimals (it-IT)', () => {
    const result = formatEur(5);
    expect(result).toMatch(/5\s?€|€\s?5/); // it-IT uses "5 €"
  });

  it('formats fractional values with comma decimal (it-IT)', () => {
    const result = formatEur(5.5);
    expect(result).toMatch(/5,50\s?€|€\s?5,50/);
  });

  it('formats zero', () => {
    const result = formatEur(0);
    expect(result).toMatch(/0\s?€|€\s?0/);
  });
});
```

- [ ] **Step 2: Run test, verify it fails**

```bash
cd apps/web && pnpm vitest run src/lib/gamebook/__tests__/checkout-packs.test.ts
```

Expected: FAIL with `Cannot find module '../checkout-packs'`.

- [ ] **Step 3: Implement**

Create `apps/web/src/lib/gamebook/checkout-packs.ts`:

```typescript
/**
 * Pack catalog for the libro-game quota/credits checkout modal (Issue #953).
 *
 * Mockup: `admin-mockups/design_files/sp6-libro-game-quota-credits.jsx` lines 50-54.
 * Visual-only: pricing is hardcoded; no backend endpoint exists. When real Stripe
 * + billing BC land, replace this constant with a `GET /api/v1/budget/packs` fetch.
 *
 * Locale: it-IT, currency EUR. Locale hardcoded by design (single-market product).
 */

export type CheckoutPackId = 'starter' | 'mid' | 'pro';
export type CheckoutPackBadge = 'popular' | 'save' | null;

export interface CheckoutPack {
  readonly id: CheckoutPackId;
  readonly name: string;
  readonly priceEur: number;
  readonly credits: number;
  readonly perParagraphEur: number;
  readonly badge: CheckoutPackBadge;
}

export const CHECKOUT_PACKS: readonly CheckoutPack[] = [
  { id: 'starter', name: 'Starter', priceEur: 5,  credits: 100,  perParagraphEur: 0.05,  badge: 'popular' },
  { id: 'mid',     name: 'Mid',     priceEur: 20, credits: 500,  perParagraphEur: 0.04,  badge: null      },
  { id: 'pro',     name: 'Pro',     priceEur: 35, credits: 1000, perParagraphEur: 0.035, badge: 'save'    },
] as const;

/**
 * Format EUR value using Italian locale. Integer values render without decimals
 * ("5 €"); fractional values render with two decimals and comma separator
 * ("5,50 €").
 */
export function formatEur(value: number): string {
  return new Intl.NumberFormat('it-IT', {
    style: 'currency',
    currency: 'EUR',
    minimumFractionDigits: value % 1 === 0 ? 0 : 2,
  }).format(value);
}
```

- [ ] **Step 4: Run test, verify it passes**

```bash
cd apps/web && pnpm vitest run src/lib/gamebook/__tests__/checkout-packs.test.ts
```

Expected: 8 tests passing.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/lib/gamebook/checkout-packs.ts apps/web/src/lib/gamebook/__tests__/checkout-packs.test.ts
git commit -m "feat(gamebook): #953 checkout pack catalog + formatEur

Hardcoded 3-pack catalog (Starter/Mid/Pro) + Intl.NumberFormat it-IT
EUR formatter. No backend endpoint by design (mockup is visual-only)."
```

---

### Task 2: Soft warning dismissal hook (`useSoftWarningDismissal`)

**Files:**
- Create: `apps/web/src/lib/gamebook/hooks/useSoftWarningDismissal.ts`
- Test: `apps/web/src/lib/gamebook/__tests__/hooks/useSoftWarningDismissal.test.ts`

- [ ] **Step 1: Write the failing test**

Create `apps/web/src/lib/gamebook/__tests__/hooks/useSoftWarningDismissal.test.ts`:

```typescript
import { renderHook, act } from '@testing-library/react';
import { describe, it, expect, beforeEach } from 'vitest';

import { useSoftWarningDismissal, SOFT_WARNING_STORAGE_KEY } from '../../hooks/useSoftWarningDismissal';

describe('useSoftWarningDismissal', () => {
  beforeEach(() => {
    sessionStorage.clear();
  });

  it('shouldShow=true when usage is 90% and not dismissed', () => {
    const { result } = renderHook(() => useSoftWarningDismissal(45, 50));
    expect(result.current.shouldShow).toBe(true);
  });

  it('shouldShow=true at the exact 0.9 boundary', () => {
    const { result } = renderHook(() => useSoftWarningDismissal(9, 10));
    expect(result.current.shouldShow).toBe(true);
  });

  it('shouldShow=false when usage < 90%', () => {
    const { result } = renderHook(() => useSoftWarningDismissal(44, 50));
    expect(result.current.shouldShow).toBe(false);
  });

  it('shouldShow=false when usage = 100% (hard limit, not soft)', () => {
    const { result } = renderHook(() => useSoftWarningDismissal(50, 50));
    expect(result.current.shouldShow).toBe(false);
  });

  it('shouldShow=false after dismiss()', () => {
    const { result } = renderHook(() => useSoftWarningDismissal(47, 50));
    expect(result.current.shouldShow).toBe(true);
    act(() => result.current.dismiss());
    expect(result.current.shouldShow).toBe(false);
  });

  it('dismiss() persists ISO timestamp to sessionStorage', () => {
    const { result } = renderHook(() => useSoftWarningDismissal(47, 50));
    act(() => result.current.dismiss());
    const stored = sessionStorage.getItem(SOFT_WARNING_STORAGE_KEY);
    expect(stored).not.toBeNull();
    expect(() => new Date(stored!).toISOString()).not.toThrow();
  });

  it('shouldShow=false on remount when dismissal already in sessionStorage', () => {
    sessionStorage.setItem(SOFT_WARNING_STORAGE_KEY, new Date().toISOString());
    const { result } = renderHook(() => useSoftWarningDismissal(47, 50));
    expect(result.current.shouldShow).toBe(false);
  });

  it('shouldShow=false defensively when total <= 0', () => {
    const { result } = renderHook(() => useSoftWarningDismissal(0, 0));
    expect(result.current.shouldShow).toBe(false);
  });
});
```

- [ ] **Step 2: Run test, verify it fails**

```bash
cd apps/web && pnpm vitest run src/lib/gamebook/__tests__/hooks/useSoftWarningDismissal.test.ts
```

Expected: FAIL with `Cannot find module '../../hooks/useSoftWarningDismissal'`.

- [ ] **Step 3: Implement**

Create `apps/web/src/lib/gamebook/hooks/useSoftWarningDismissal.ts`:

```typescript
'use client';

import { useCallback, useState } from 'react';

export const SOFT_WARNING_STORAGE_KEY = 'gamebook.soft-warning.dismissed-at';

const SOFT_THRESHOLD = 0.9;

/**
 * Hook that decides whether to render the SoftWarningCredits component
 * and exposes a dismiss callback.
 *
 * - shouldShow = true ⇔ 0.9 <= used/total < 1.0 AND no prior dismissal in
 *   the current browser session.
 * - dismiss() writes an ISO timestamp to sessionStorage; subsequent mounts
 *   in the same session will return shouldShow=false.
 * - Cross-session: opening a new tab resets dismissal (sessionStorage, not local).
 * - Defensive: returns shouldShow=false when total <= 0.
 *
 * Must be invoked from a client component ('use client').
 */
export function useSoftWarningDismissal(
  used: number,
  total: number
): { readonly shouldShow: boolean; readonly dismiss: () => void } {
  const initialDismissed =
    typeof sessionStorage !== 'undefined' &&
    sessionStorage.getItem(SOFT_WARNING_STORAGE_KEY) !== null;
  const [dismissed, setDismissed] = useState<boolean>(initialDismissed);

  const dismiss = useCallback(() => {
    if (typeof sessionStorage !== 'undefined') {
      sessionStorage.setItem(SOFT_WARNING_STORAGE_KEY, new Date().toISOString());
    }
    setDismissed(true);
  }, []);

  if (total <= 0) {
    return { shouldShow: false, dismiss };
  }
  const ratio = used / total;
  const inSoftRange = ratio >= SOFT_THRESHOLD && ratio < 1;
  return { shouldShow: inSoftRange && !dismissed, dismiss };
}
```

- [ ] **Step 4: Run test, verify it passes**

```bash
cd apps/web && pnpm vitest run src/lib/gamebook/__tests__/hooks/useSoftWarningDismissal.test.ts
```

Expected: 8 tests passing.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/lib/gamebook/hooks/useSoftWarningDismissal.ts apps/web/src/lib/gamebook/__tests__/hooks/useSoftWarningDismissal.test.ts
git commit -m "feat(gamebook): #953 useSoftWarningDismissal hook

sessionStorage-backed dismissal for 47/50 soft warning. Auto-shows at
[90%, 100%) usage. Defensive against total=0. Per-tab persistence."
```

---

### Task 3: `CheckoutStepIndicator` (4-step variant)

**Files:**
- Create: `apps/web/src/components/features/gamebook/checkout/CheckoutStepIndicator.tsx`
- Test: `apps/web/src/components/features/gamebook/__tests__/checkout/CheckoutStepIndicator.test.tsx`

The existing `StepIndicator.tsx` is hardcoded to 3 steps. Build a 4-step sibling for the checkout (does not modify the existing component).

- [ ] **Step 1: Write the failing test**

Create `apps/web/src/components/features/gamebook/__tests__/checkout/CheckoutStepIndicator.test.tsx`:

```typescript
import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { CheckoutStepIndicator, type CheckoutStepIndicatorLabels } from '../../checkout/CheckoutStepIndicator';

const LABELS: CheckoutStepIndicatorLabels = {
  step1: 'Quota',
  step2: 'Pacchetto',
  step3: 'Pagamento',
  step4: 'Fatto',
  ariaCurrent: 'Passo 2 di 4',
};

describe('CheckoutStepIndicator', () => {
  it('renders 4 step circles with correct labels', () => {
    render(<CheckoutStepIndicator currentStep={1} labels={LABELS} />);
    expect(screen.getByText('Quota')).toBeInTheDocument();
    expect(screen.getByText('Pacchetto')).toBeInTheDocument();
    expect(screen.getByText('Pagamento')).toBeInTheDocument();
    expect(screen.getByText('Fatto')).toBeInTheDocument();
  });

  it('active step has aria-current="step"', () => {
    render(<CheckoutStepIndicator currentStep={2} labels={LABELS} />);
    const active = document.querySelector('[data-step-number="2"]');
    expect(active?.getAttribute('aria-current')).toBe('step');
  });

  it('done steps render ✓ glyph', () => {
    render(<CheckoutStepIndicator currentStep={3} labels={LABELS} />);
    const step1 = document.querySelector('[data-step-number="1"] [data-slot="checkout-step-circle"]');
    const step2 = document.querySelector('[data-step-number="2"] [data-slot="checkout-step-circle"]');
    expect(step1?.textContent).toBe('✓');
    expect(step2?.textContent).toBe('✓');
  });

  it('pending steps render the number', () => {
    render(<CheckoutStepIndicator currentStep={2} labels={LABELS} />);
    const step4 = document.querySelector('[data-step-number="4"] [data-slot="checkout-step-circle"]');
    expect(step4?.textContent).toBe('4');
  });

  it('data-current-step matches prop', () => {
    render(<CheckoutStepIndicator currentStep={3} labels={LABELS} />);
    expect(document.querySelector('[data-slot="checkout-step-indicator"]')?.getAttribute('data-current-step')).toBe('3');
  });

  it('sr-only ariaCurrent rendered on active step', () => {
    render(<CheckoutStepIndicator currentStep={2} labels={LABELS} />);
    expect(screen.getByText('Passo 2 di 4')).toHaveClass('sr-only');
  });

  it('connecting lines have aria-hidden', () => {
    render(<CheckoutStepIndicator currentStep={2} labels={LABELS} />);
    const lines = document.querySelectorAll('[data-slot="checkout-step-line"]');
    expect(lines).toHaveLength(3);
    lines.forEach((line) => expect(line.getAttribute('aria-hidden')).toBe('true'));
  });
});
```

- [ ] **Step 2: Run test, verify it fails**

```bash
cd apps/web && pnpm vitest run src/components/features/gamebook/__tests__/checkout/CheckoutStepIndicator.test.tsx
```

Expected: FAIL with module not found.

- [ ] **Step 3: Implement**

Create `apps/web/src/components/features/gamebook/checkout/CheckoutStepIndicator.tsx`:

```typescript
/**
 * CheckoutStepIndicator — 4-step variant for the SP6 checkout modal (#953).
 *
 * Mirror of `StepIndicator.tsx` (3-step, #789) but parameterised to 4 steps.
 * Uses entity-toolkit color for active/done circles per mockup
 * (sp6-libro-game-quota-credits.jsx StepIndicator).
 */

'use client';

import type { ReactElement } from 'react';

import clsx from 'clsx';

export interface CheckoutStepIndicatorLabels {
  readonly step1: string;
  readonly step2: string;
  readonly step3: string;
  readonly step4: string;
  /** Pre-resolved sr-only text e.g. "Passo 2 di 4". */
  readonly ariaCurrent: string;
}

export interface CheckoutStepIndicatorProps {
  readonly currentStep: 1 | 2 | 3 | 4;
  readonly labels: CheckoutStepIndicatorLabels;
  readonly className?: string;
}

type StepState = 'done' | 'active' | 'pending';

function deriveState(stepNumber: number, currentStep: number): StepState {
  if (stepNumber < currentStep) return 'done';
  if (stepNumber === currentStep) return 'active';
  return 'pending';
}

interface StepCircleProps {
  readonly stepNumber: 1 | 2 | 3 | 4;
  readonly state: StepState;
  readonly label: string;
  readonly ariaCurrent: string;
}

function StepCircle({ stepNumber, state, label, ariaCurrent }: StepCircleProps): ReactElement {
  const isDone = state === 'done';
  const isActive = state === 'active';
  return (
    <div
      data-step-number={stepNumber}
      data-state={state}
      aria-current={isActive ? 'step' : undefined}
      className="flex flex-col items-center gap-1"
    >
      <span
        data-slot="checkout-step-circle"
        className={clsx(
          'flex h-6 w-6 items-center justify-center rounded-full border-2 text-xs font-bold tabular-nums',
          'transition-colors motion-reduce:transition-none',
          isDone || isActive
            ? 'border-entity-toolkit bg-entity-toolkit text-white'
            : 'border-border bg-transparent text-muted-foreground'
        )}
      >
        {isDone ? '✓' : stepNumber}
      </span>
      <span
        className={clsx(
          'text-[11px] font-semibold whitespace-nowrap',
          isActive ? 'text-foreground' : 'text-muted-foreground'
        )}
      >
        {label}
      </span>
      {isActive && <span className="sr-only">{ariaCurrent}</span>}
    </div>
  );
}

function StepLine({ highlighted }: { readonly highlighted: boolean }): ReactElement {
  return (
    <span
      data-slot="checkout-step-line"
      aria-hidden="true"
      className={clsx(
        'h-0.5 flex-1 self-start mt-3 min-w-2 rounded-full transition-colors motion-reduce:transition-none',
        highlighted ? 'bg-entity-toolkit/80' : 'bg-border'
      )}
    />
  );
}

export function CheckoutStepIndicator({
  currentStep,
  labels,
  className,
}: CheckoutStepIndicatorProps): ReactElement {
  const stepLabels: Record<1 | 2 | 3 | 4, string> = {
    1: labels.step1,
    2: labels.step2,
    3: labels.step3,
    4: labels.step4,
  };
  return (
    <nav
      data-slot="checkout-step-indicator"
      data-current-step={currentStep}
      aria-label="Checkout progress"
      role="navigation"
      className={clsx('flex items-start gap-1.5 px-2 py-1', className)}
    >
      <StepCircle stepNumber={1} state={deriveState(1, currentStep)} label={stepLabels[1]} ariaCurrent={labels.ariaCurrent} />
      <StepLine highlighted={currentStep > 1} />
      <StepCircle stepNumber={2} state={deriveState(2, currentStep)} label={stepLabels[2]} ariaCurrent={labels.ariaCurrent} />
      <StepLine highlighted={currentStep > 2} />
      <StepCircle stepNumber={3} state={deriveState(3, currentStep)} label={stepLabels[3]} ariaCurrent={labels.ariaCurrent} />
      <StepLine highlighted={currentStep > 3} />
      <StepCircle stepNumber={4} state={deriveState(4, currentStep)} label={stepLabels[4]} ariaCurrent={labels.ariaCurrent} />
    </nav>
  );
}
```

- [ ] **Step 4: Run test, verify it passes**

```bash
cd apps/web && pnpm vitest run src/components/features/gamebook/__tests__/checkout/CheckoutStepIndicator.test.tsx
```

Expected: 7 tests passing.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/features/gamebook/checkout/CheckoutStepIndicator.tsx apps/web/src/components/features/gamebook/__tests__/checkout/CheckoutStepIndicator.test.tsx
git commit -m "feat(gamebook): #953 CheckoutStepIndicator (4-step)

4-step variant of the existing 3-step StepIndicator. Entity-toolkit color
for active/done. Pure presentational, ARIA-compliant. Existing
StepIndicator unchanged."
```

---

### Task 4: `Step1QuotaReached`

**Files:**
- Create: `apps/web/src/components/features/gamebook/checkout/Step1QuotaReached.tsx`
- Test: `apps/web/src/components/features/gamebook/__tests__/checkout/Step1QuotaReached.test.tsx`

- [ ] **Step 1: Write the failing test**

```typescript
// apps/web/src/components/features/gamebook/__tests__/checkout/Step1QuotaReached.test.tsx
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';

import { Step1QuotaReached, type Step1Labels } from '../../checkout/Step1QuotaReached';

const LABELS: Step1Labels = {
  heading: 'Quota raggiunta',
  subheading: 'Hai tradotto 50 paragrafi questo mese.',
  quotaLabel: 'Quota mensile',
  resetIn: 'Reset tra 12 giorni',
  primaryCta: '💎 Acquista 100 crediti (€5)',
  secondaryCta: '⏸️ Continua senza traduzione',
  explainLink: 'Cosa sono i crediti? →',
};

describe('Step1QuotaReached', () => {
  it('renders heading + subheading + quota label', () => {
    render(
      <Step1QuotaReached
        used={50}
        total={50}
        labels={LABELS}
        onPrimaryClick={vi.fn()}
        onSecondaryClick={vi.fn()}
      />
    );
    expect(screen.getByRole('heading', { name: 'Quota raggiunta' })).toBeInTheDocument();
    expect(screen.getByText(LABELS.subheading)).toBeInTheDocument();
    expect(screen.getByText('Quota mensile')).toBeInTheDocument();
  });

  it('renders used/total counter with tabular nums', () => {
    render(
      <Step1QuotaReached
        used={50}
        total={50}
        labels={LABELS}
        onPrimaryClick={vi.fn()}
        onSecondaryClick={vi.fn()}
      />
    );
    const counter = screen.getByTestId('quota-counter');
    expect(counter.textContent).toMatch(/50.*50/);
  });

  it('progress bar width matches used/total ratio', () => {
    render(
      <Step1QuotaReached
        used={50}
        total={50}
        labels={LABELS}
        onPrimaryClick={vi.fn()}
        onSecondaryClick={vi.fn()}
      />
    );
    const fill = document.querySelector('[data-slot="quota-card-fill"]') as HTMLElement;
    expect(fill.style.width).toBe('100%');
  });

  it('fires onPrimaryClick on primary CTA', async () => {
    const user = userEvent.setup();
    const onPrimary = vi.fn();
    render(
      <Step1QuotaReached
        used={50}
        total={50}
        labels={LABELS}
        onPrimaryClick={onPrimary}
        onSecondaryClick={vi.fn()}
      />
    );
    await user.click(screen.getByRole('button', { name: LABELS.primaryCta }));
    expect(onPrimary).toHaveBeenCalledOnce();
  });

  it('fires onSecondaryClick on secondary CTA', async () => {
    const user = userEvent.setup();
    const onSecondary = vi.fn();
    render(
      <Step1QuotaReached
        used={50}
        total={50}
        labels={LABELS}
        onPrimaryClick={vi.fn()}
        onSecondaryClick={onSecondary}
      />
    );
    await user.click(screen.getByRole('button', { name: LABELS.secondaryCta }));
    expect(onSecondary).toHaveBeenCalledOnce();
  });

  it('has data-slot="checkout-step-1"', () => {
    render(
      <Step1QuotaReached
        used={50}
        total={50}
        labels={LABELS}
        onPrimaryClick={vi.fn()}
        onSecondaryClick={vi.fn()}
      />
    );
    expect(document.querySelector('[data-slot="checkout-step-1"]')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test, verify it fails**

```bash
cd apps/web && pnpm vitest run src/components/features/gamebook/__tests__/checkout/Step1QuotaReached.test.tsx
```

Expected: FAIL with module not found.

- [ ] **Step 3: Implement**

```typescript
// apps/web/src/components/features/gamebook/checkout/Step1QuotaReached.tsx
'use client';

import type { ReactElement } from 'react';

import clsx from 'clsx';

export interface Step1Labels {
  readonly heading: string;
  readonly subheading: string;
  readonly quotaLabel: string;
  readonly resetIn: string;
  readonly primaryCta: string;
  readonly secondaryCta: string;
  readonly explainLink: string;
}

export interface Step1QuotaReachedProps {
  readonly used: number;
  readonly total: number;
  readonly labels: Step1Labels;
  readonly onPrimaryClick: () => void;
  readonly onSecondaryClick: () => void;
}

export function Step1QuotaReached({
  used,
  total,
  labels,
  onPrimaryClick,
  onSecondaryClick,
}: Step1QuotaReachedProps): ReactElement {
  const fillPercent = total > 0 ? Math.min(100, Math.round((used / total) * 100)) : 0;
  return (
    <div data-slot="checkout-step-1" className="flex flex-col gap-4 p-5">
      <div
        aria-hidden="true"
        className="mx-auto flex h-24 w-24 items-center justify-center rounded-3xl bg-entity-event/15 ring-1 ring-entity-event/30"
      >
        <span className="text-5xl">💎</span>
      </div>
      <div className="flex flex-col gap-1">
        <h2 className="text-xl font-bold text-foreground">{labels.heading}</h2>
        <p className="text-sm text-muted-foreground">{labels.subheading}</p>
      </div>
      <section
        data-slot="quota-card"
        className="flex flex-col gap-2 rounded-lg border border-entity-event/25 bg-entity-event/10 p-3"
      >
        <div className="flex items-end justify-between">
          <span className="font-mono text-[11px] uppercase tracking-wider text-muted-foreground">
            {labels.quotaLabel}
          </span>
          <span
            data-testid="quota-counter"
            className="font-mono text-2xl font-extrabold tabular-nums text-entity-event"
          >
            {used}
            <span className="text-[60%] text-muted-foreground"> / {total}</span>
          </span>
        </div>
        <div
          aria-label={`Quota ${used} su ${total} paragrafi`}
          className="h-2 overflow-hidden rounded-full bg-muted"
        >
          <div
            data-slot="quota-card-fill"
            style={{ width: `${fillPercent}%` }}
            className="h-full bg-entity-event/70 transition-[width] duration-300 motion-reduce:transition-none"
          />
        </div>
        <p className="font-mono text-[11px] text-muted-foreground">{labels.resetIn}</p>
      </section>
      <div className="flex flex-col gap-2">
        <button
          type="button"
          onClick={onPrimaryClick}
          className={clsx(
            'w-full rounded-lg bg-entity-event px-5 py-3 text-sm font-bold text-white',
            'transition-[filter] duration-150 hover:brightness-110 active:scale-[0.98]',
            'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2'
          )}
        >
          {labels.primaryCta}
        </button>
        <button
          type="button"
          onClick={onSecondaryClick}
          className={clsx(
            'w-full rounded-lg border border-border-strong bg-card px-5 py-3 text-sm font-semibold text-foreground',
            'transition-colors hover:bg-muted',
            'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2'
          )}
        >
          {labels.secondaryCta}
        </button>
        <button
          type="button"
          className="self-center text-xs text-muted-foreground hover:text-foreground"
        >
          {labels.explainLink}
        </button>
      </div>
    </div>
  );
}
```

- [ ] **Step 4: Run test, verify it passes**

```bash
cd apps/web && pnpm vitest run src/components/features/gamebook/__tests__/checkout/Step1QuotaReached.test.tsx
```

Expected: 6 tests passing.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/features/gamebook/checkout/Step1QuotaReached.tsx apps/web/src/components/features/gamebook/__tests__/checkout/Step1QuotaReached.test.tsx
git commit -m "feat(gamebook): #953 Step1QuotaReached component

Pure presentational quota-reached announcement card. Two CTAs (acquista
crediti / continua senza traduzione) + explain link. Entity-event color
palette per mockup. Tabular-nums counter."
```

---

### Task 5: `Step2PackPicker`

**Files:**
- Create: `apps/web/src/components/features/gamebook/checkout/Step2PackPicker.tsx`
- Test: `apps/web/src/components/features/gamebook/__tests__/checkout/Step2PackPicker.test.tsx`

- [ ] **Step 1: Write the failing test**

```typescript
// apps/web/src/components/features/gamebook/__tests__/checkout/Step2PackPicker.test.tsx
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';

import { Step2PackPicker, type Step2Labels } from '../../checkout/Step2PackPicker';

const LABELS: Step2Labels = {
  heading: 'Scegli il tuo pacchetto',
  subheading: 'Più paragrafi = miglior prezzo. I crediti non scadono.',
  disclaimer: 'I crediti non scadono. La quota free si resetta mensilmente.',
  totalLabel: 'Totale',
  continueCta: 'Continua →',
  packBadges: { popular: 'Più popolare', save: 'Risparmia 30%' },
  packNames: { starter: 'Starter', mid: 'Mid', pro: 'Pro' },
  packCreditsSuffix: 'crediti',
  perParagraphSuffix: '/ paragrafo',
};

describe('Step2PackPicker', () => {
  it('renders 3 pack cards', () => {
    render(<Step2PackPicker selectedId="starter" labels={LABELS} onSelect={vi.fn()} onContinue={vi.fn()} />);
    expect(document.querySelectorAll('[data-slot="pack-card"]')).toHaveLength(3);
  });

  it('starter pack has popular badge', () => {
    render(<Step2PackPicker selectedId="starter" labels={LABELS} onSelect={vi.fn()} onContinue={vi.fn()} />);
    expect(screen.getByText('Più popolare')).toBeInTheDocument();
  });

  it('pro pack has save badge', () => {
    render(<Step2PackPicker selectedId="starter" labels={LABELS} onSelect={vi.fn()} onContinue={vi.fn()} />);
    expect(screen.getByText('Risparmia 30%')).toBeInTheDocument();
  });

  it('default selection is starter; renders €5 in total', () => {
    render(<Step2PackPicker selectedId="starter" labels={LABELS} onSelect={vi.fn()} onContinue={vi.fn()} />);
    expect(screen.getByTestId('checkout-total')).toHaveTextContent(/5/);
  });

  it('mid selection updates total to €20', () => {
    render(<Step2PackPicker selectedId="mid" labels={LABELS} onSelect={vi.fn()} onContinue={vi.fn()} />);
    expect(screen.getByTestId('checkout-total')).toHaveTextContent(/20/);
  });

  it('fires onSelect with packId on radio change', async () => {
    const user = userEvent.setup();
    const onSelect = vi.fn();
    render(<Step2PackPicker selectedId="starter" labels={LABELS} onSelect={onSelect} onContinue={vi.fn()} />);
    await user.click(screen.getByLabelText(/Pacchetto Pro/i));
    expect(onSelect).toHaveBeenCalledWith('pro');
  });

  it('continue button fires onContinue', async () => {
    const user = userEvent.setup();
    const onContinue = vi.fn();
    render(<Step2PackPicker selectedId="starter" labels={LABELS} onSelect={vi.fn()} onContinue={onContinue} />);
    await user.click(screen.getByRole('button', { name: LABELS.continueCta }));
    expect(onContinue).toHaveBeenCalledOnce();
  });

  it('radiogroup role for accessibility', () => {
    render(<Step2PackPicker selectedId="starter" labels={LABELS} onSelect={vi.fn()} onContinue={vi.fn()} />);
    expect(screen.getByRole('radiogroup', { name: /pacchetto/i })).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test, verify it fails**

```bash
cd apps/web && pnpm vitest run src/components/features/gamebook/__tests__/checkout/Step2PackPicker.test.tsx
```

Expected: FAIL with module not found.

- [ ] **Step 3: Implement**

```typescript
// apps/web/src/components/features/gamebook/checkout/Step2PackPicker.tsx
'use client';

import type { ReactElement } from 'react';

import clsx from 'clsx';

import { CHECKOUT_PACKS, formatEur, type CheckoutPackId } from '@/lib/gamebook/checkout-packs';

export interface Step2Labels {
  readonly heading: string;
  readonly subheading: string;
  readonly disclaimer: string;
  readonly totalLabel: string;
  readonly continueCta: string;
  readonly packBadges: { readonly popular: string; readonly save: string };
  readonly packNames: { readonly starter: string; readonly mid: string; readonly pro: string };
  readonly packCreditsSuffix: string;
  readonly perParagraphSuffix: string;
}

export interface Step2PackPickerProps {
  readonly selectedId: CheckoutPackId;
  readonly labels: Step2Labels;
  readonly onSelect: (id: CheckoutPackId) => void;
  readonly onContinue: () => void;
}

export function Step2PackPicker({
  selectedId,
  labels,
  onSelect,
  onContinue,
}: Step2PackPickerProps): ReactElement {
  const selectedPack = CHECKOUT_PACKS.find((p) => p.id === selectedId)!;
  return (
    <div data-slot="checkout-step-2" className="flex flex-col gap-4 p-5">
      <div className="flex flex-col gap-1">
        <h2 className="text-xl font-bold text-foreground">{labels.heading}</h2>
        <p className="text-sm text-muted-foreground">{labels.subheading}</p>
      </div>
      <div
        role="radiogroup"
        aria-label="Scelta pacchetto crediti"
        className="flex flex-col gap-2.5"
      >
        {CHECKOUT_PACKS.map((pack) => {
          const selected = pack.id === selectedId;
          const badgeKind = pack.badge;
          const badgeLabel = badgeKind ? labels.packBadges[badgeKind] : null;
          return (
            <label
              key={pack.id}
              data-slot="pack-card"
              data-selected={selected || undefined}
              className={clsx(
                'relative cursor-pointer rounded-lg border-2 px-4 py-3.5 transition-transform',
                'hover:translate-y-[-1px]',
                'grid grid-cols-[1fr_auto_auto] grid-rows-[auto_auto_auto] gap-x-3 gap-y-0.5',
                selected
                  ? 'border-border-strong bg-entity-toolkit/10 shadow-[0_0_0_3px_hsl(var(--entity-toolkit)/0.15)]'
                  : 'border-border bg-card shadow-xs'
              )}
            >
              <input
                type="radio"
                name="pack"
                checked={selected}
                onChange={() => onSelect(pack.id)}
                aria-label={`Pacchetto ${labels.packNames[pack.id]}`}
                className="sr-only"
              />
              {badgeLabel && (
                <span
                  className={clsx(
                    'absolute -top-2 right-3 rounded-full px-2 py-0.5 text-[10px] font-bold uppercase tracking-wider text-white',
                    badgeKind === 'popular' ? 'bg-entity-toolkit' : 'bg-entity-event'
                  )}
                >
                  {badgeLabel}
                </span>
              )}
              <span className="col-start-1 row-start-1 font-bold uppercase tracking-wider text-xs text-muted-foreground">
                {labels.packNames[pack.id]}
              </span>
              <span className="col-start-1 row-start-2 flex items-baseline gap-1.5 text-2xl font-extrabold text-entity-toolkit">
                <span className="tabular-nums">{pack.credits.toLocaleString('it')}</span>
                <span className="text-xs font-semibold text-muted-foreground lowercase">{labels.packCreditsSuffix}</span>
              </span>
              <span className="col-start-2 row-start-2 text-2xl font-extrabold text-foreground tabular-nums">
                {formatEur(pack.priceEur)}
              </span>
              <span className="col-start-1 row-start-3 font-mono text-[11px] text-muted-foreground">
                {formatEur(pack.perParagraphEur)} {labels.perParagraphSuffix}
              </span>
              <span
                aria-hidden="true"
                className={clsx(
                  'col-start-3 row-span-3 self-center justify-self-end flex h-5 w-5 items-center justify-center rounded-full border-2 transition-colors',
                  selected
                    ? 'border-entity-toolkit bg-entity-toolkit'
                    : 'border-border bg-transparent'
                )}
              >
                {selected && <span className="h-2 w-2 rounded-full bg-white" />}
              </span>
            </label>
          );
        })}
      </div>
      <p className="rounded-md bg-muted px-3 py-2.5 text-[11px] leading-relaxed text-muted-foreground">
        {labels.disclaimer}
      </p>
      <div className="flex items-center gap-2.5">
        <div className="flex flex-col">
          <span className="font-mono text-[10px] uppercase tracking-wider text-muted-foreground">
            {labels.totalLabel}
          </span>
          <span data-testid="checkout-total" className="text-2xl font-extrabold text-foreground tabular-nums">
            {formatEur(selectedPack.priceEur)}
          </span>
        </div>
        <button
          type="button"
          onClick={onContinue}
          className={clsx(
            'flex-1 rounded-lg bg-entity-toolkit px-5 py-3.5 text-sm font-bold text-white',
            'transition-[filter] duration-150 hover:brightness-110 active:scale-[0.98]',
            'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2'
          )}
        >
          {labels.continueCta}
        </button>
      </div>
    </div>
  );
}
```

- [ ] **Step 4: Run test, verify it passes**

```bash
cd apps/web && pnpm vitest run src/components/features/gamebook/__tests__/checkout/Step2PackPicker.test.tsx
```

Expected: 8 tests passing.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/features/gamebook/checkout/Step2PackPicker.tsx apps/web/src/components/features/gamebook/__tests__/checkout/Step2PackPicker.test.tsx
git commit -m "feat(gamebook): #953 Step2PackPicker component

3-pack radiogroup picker with single-select. Popular/save badges,
per-pack pricing, total recap. Tabular-nums everywhere. Italian locale."
```

---

### Task 6: `Step3CheckoutForm`

**Files:**
- Create: `apps/web/src/components/features/gamebook/checkout/Step3CheckoutForm.tsx`
- Test: `apps/web/src/components/features/gamebook/__tests__/checkout/Step3CheckoutForm.test.tsx`

Step 3 is visual-only: the form fields render fixed placeholder values (card "•••• •••• •••• 4242", expiry "12 / 27", etc.) per mockup §709. The component takes a `subState` prop ('filled' | 'loading' | 'failed') and the `onPay`/`onBack` callbacks. The parent (`CheckoutModal`) handles the actual setTimeout-based transition.

- [ ] **Step 1: Write the failing test**

```typescript
// apps/web/src/components/features/gamebook/__tests__/checkout/Step3CheckoutForm.test.tsx
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';

import { Step3CheckoutForm, type Step3Labels } from '../../checkout/Step3CheckoutForm';

const LABELS: Step3Labels = {
  heading: 'Pagamento',
  summary: 'Pacchetto Starter · 100 crediti · 5 €',
  fieldLabels: {
    card: 'Numero carta',
    expiry: 'Scadenza',
    cvc: 'CVC',
    name: 'Nome sulla carta',
    country: 'Paese',
  },
  trustChips: ['SSL secure', 'Stripe powered', 'Politica rimborso 14gg'],
  payCta: 'Paga 5 €',
  loadingCta: 'Elaborazione…',
  backLink: '← Torna ai pacchetti',
  failedBanner: { title: 'Pagamento rifiutato', detail: 'Carta scaduta · prova un altro metodo.' },
  recapLabels: { credits: '100 crediti', vat: 'IVA inclusa', total: 'Totale' },
  recapValues: { credits: '5 €', total: '5 €' },
};

describe('Step3CheckoutForm', () => {
  it('renders heading + summary', () => {
    render(<Step3CheckoutForm subState="filled" labels={LABELS} onPay={vi.fn()} onBack={vi.fn()} />);
    expect(screen.getByRole('heading', { name: 'Pagamento' })).toBeInTheDocument();
    expect(screen.getByText(LABELS.summary)).toBeInTheDocument();
  });

  it('shows form fields with labels in filled state', () => {
    render(<Step3CheckoutForm subState="filled" labels={LABELS} onPay={vi.fn()} onBack={vi.fn()} />);
    expect(screen.getByText('Numero carta')).toBeInTheDocument();
    expect(screen.getByText('Scadenza')).toBeInTheDocument();
    expect(screen.getByText('CVC')).toBeInTheDocument();
  });

  it('pay button enabled + clickable in filled state', async () => {
    const user = userEvent.setup();
    const onPay = vi.fn();
    render(<Step3CheckoutForm subState="filled" labels={LABELS} onPay={onPay} onBack={vi.fn()} />);
    const btn = screen.getByRole('button', { name: 'Paga 5 €' });
    expect(btn).not.toBeDisabled();
    await user.click(btn);
    expect(onPay).toHaveBeenCalledOnce();
  });

  it('pay button shows loading state with aria-busy in loading sub-state', () => {
    render(<Step3CheckoutForm subState="loading" labels={LABELS} onPay={vi.fn()} onBack={vi.fn()} />);
    const btn = screen.getByRole('button', { name: /Elaborazione/i });
    expect(btn).toHaveAttribute('aria-busy', 'true');
    expect(btn).toBeDisabled();
  });

  it('shows red banner in failed sub-state', () => {
    render(<Step3CheckoutForm subState="failed" labels={LABELS} onPay={vi.fn()} onBack={vi.fn()} />);
    const banner = screen.getByRole('alert');
    expect(banner).toHaveTextContent('Pagamento rifiutato');
  });

  it('back link fires onBack', async () => {
    const user = userEvent.setup();
    const onBack = vi.fn();
    render(<Step3CheckoutForm subState="filled" labels={LABELS} onPay={vi.fn()} onBack={onBack} />);
    await user.click(screen.getByRole('button', { name: LABELS.backLink }));
    expect(onBack).toHaveBeenCalledOnce();
  });

  it('renders 3 trust chips', () => {
    render(<Step3CheckoutForm subState="filled" labels={LABELS} onPay={vi.fn()} onBack={vi.fn()} />);
    LABELS.trustChips.forEach((chip) => {
      expect(screen.getByText(chip)).toBeInTheDocument();
    });
  });

  it('renders recap rows', () => {
    render(<Step3CheckoutForm subState="filled" labels={LABELS} onPay={vi.fn()} onBack={vi.fn()} />);
    expect(screen.getByText('100 crediti')).toBeInTheDocument();
    expect(screen.getByText('Totale')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test, verify it fails**

```bash
cd apps/web && pnpm vitest run src/components/features/gamebook/__tests__/checkout/Step3CheckoutForm.test.tsx
```

Expected: FAIL with module not found.

- [ ] **Step 3: Implement**

```typescript
// apps/web/src/components/features/gamebook/checkout/Step3CheckoutForm.tsx
'use client';

import type { ReactElement } from 'react';

import clsx from 'clsx';

export type Step3SubState = 'filled' | 'loading' | 'failed';

export interface Step3Labels {
  readonly heading: string;
  /** Pre-resolved summary line e.g. "Pacchetto Starter · 100 crediti · 5 €". */
  readonly summary: string;
  readonly fieldLabels: {
    readonly card: string;
    readonly expiry: string;
    readonly cvc: string;
    readonly name: string;
    readonly country: string;
  };
  readonly trustChips: readonly string[];
  readonly payCta: string;
  readonly loadingCta: string;
  readonly backLink: string;
  readonly failedBanner: { readonly title: string; readonly detail: string };
  readonly recapLabels: { readonly credits: string; readonly vat: string; readonly total: string };
  readonly recapValues: { readonly credits: string; readonly total: string };
}

export interface Step3CheckoutFormProps {
  readonly subState: Step3SubState;
  readonly labels: Step3Labels;
  readonly onPay: () => void;
  readonly onBack: () => void;
}

const FIXED_PLACEHOLDERS = {
  card: '•••• •••• •••• 4242',
  cardBrand: 'VISA',
  expiry: '12 / 27',
  cvc: '•••',
  name: 'Sara Bianchi',
  country: '🇮🇹 Italia',
} as const;

export function Step3CheckoutForm({
  subState,
  labels,
  onPay,
  onBack,
}: Step3CheckoutFormProps): ReactElement {
  const isLoading = subState === 'loading';
  const isFailed = subState === 'failed';
  return (
    <div data-slot="checkout-step-3" className="flex flex-col gap-4 p-5">
      <div className="flex flex-col gap-1">
        <h2 className="text-xl font-bold text-foreground">{labels.heading}</h2>
        <p className="text-sm text-muted-foreground">{labels.summary}</p>
      </div>
      {isFailed && (
        <div
          role="alert"
          data-slot="checkout-error-banner"
          className="flex items-start gap-2.5 rounded-md border border-entity-event/40 bg-entity-event/10 px-3 py-2.5"
        >
          <span
            aria-hidden="true"
            className="flex h-5 w-5 items-center justify-center rounded-full bg-entity-event text-xs font-bold text-white"
          >
            !
          </span>
          <div className="flex-1">
            <p className="text-sm font-bold text-entity-event">{labels.failedBanner.title}</p>
            <p className="text-xs text-muted-foreground">{labels.failedBanner.detail}</p>
          </div>
        </div>
      )}
      <div data-slot="checkout-form" className="flex flex-col gap-2.5">
        <Field label={labels.fieldLabels.card}>
          <span className="font-mono text-sm text-foreground">{FIXED_PLACEHOLDERS.card}</span>
          <span aria-hidden="true" className="font-mono text-[10px] font-bold tracking-widest text-muted-foreground">
            {FIXED_PLACEHOLDERS.cardBrand}
          </span>
        </Field>
        <div className="flex gap-2.5">
          <Field label={labels.fieldLabels.expiry} className="flex-1">
            <span className="font-mono text-sm text-foreground">{FIXED_PLACEHOLDERS.expiry}</span>
          </Field>
          <Field label={labels.fieldLabels.cvc} className="flex-1">
            <span className="font-mono text-sm text-foreground">{FIXED_PLACEHOLDERS.cvc}</span>
          </Field>
        </div>
        <Field label={labels.fieldLabels.name}>
          <span className="text-sm text-foreground">{FIXED_PLACEHOLDERS.name}</span>
        </Field>
        <Field label={labels.fieldLabels.country}>
          <span className="text-sm text-foreground">{FIXED_PLACEHOLDERS.country}</span>
          <span aria-hidden="true" className="text-[10px] text-muted-foreground">▾</span>
        </Field>
      </div>
      <section className="flex flex-col gap-1.5 rounded-md border border-border bg-card px-3.5 py-3">
        <div className="flex items-center justify-between text-sm text-foreground">
          <span>{labels.recapLabels.credits}</span>
          <span className="tabular-nums">{labels.recapValues.credits}</span>
        </div>
        <div className="flex items-center justify-between text-[11px] text-muted-foreground">
          <span>{labels.recapLabels.vat}</span>
          <span>—</span>
        </div>
        <div className="flex items-center justify-between border-t border-border pt-1.5 text-base font-extrabold text-foreground">
          <span>{labels.recapLabels.total}</span>
          <span className="tabular-nums">{labels.recapValues.total}</span>
        </div>
      </section>
      <div className="flex flex-wrap justify-center gap-1.5">
        {labels.trustChips.map((chip) => (
          <span
            key={chip}
            className="inline-flex items-center gap-1 rounded-full bg-muted px-2.5 py-1 font-mono text-[10px] text-muted-foreground"
          >
            <span aria-hidden="true">✓</span>
            <span>{chip}</span>
          </span>
        ))}
      </div>
      <div className="flex flex-col gap-2">
        <button
          type="button"
          onClick={onPay}
          disabled={isLoading}
          aria-busy={isLoading || undefined}
          className={clsx(
            'flex w-full items-center justify-center gap-2 rounded-lg bg-entity-toolkit px-5 py-3.5 text-sm font-bold text-white',
            'transition-[filter] duration-150 hover:brightness-110 active:scale-[0.98]',
            'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2',
            'disabled:cursor-wait disabled:opacity-85'
          )}
        >
          {isLoading ? (
            <>
              <Spinner />
              <span>{labels.loadingCta}</span>
            </>
          ) : (
            <span>{labels.payCta}</span>
          )}
        </button>
        <button
          type="button"
          onClick={onBack}
          disabled={isLoading}
          className="self-center text-xs text-muted-foreground hover:text-foreground disabled:opacity-50"
        >
          {labels.backLink}
        </button>
      </div>
    </div>
  );
}

function Field({
  label,
  children,
  className,
}: {
  readonly label: string;
  readonly children: React.ReactNode;
  readonly className?: string;
}): ReactElement {
  return (
    <div className={clsx('flex flex-col gap-1', className)}>
      <span className="font-mono text-[10px] uppercase tracking-wider text-muted-foreground">{label}</span>
      <div className="flex min-h-[44px] items-center justify-between rounded-md border border-border bg-card px-3.5 py-3">
        {children}
      </div>
    </div>
  );
}

function Spinner(): ReactElement {
  return (
    <span
      aria-hidden="true"
      className="inline-block h-4 w-4 animate-spin rounded-full border-2 border-white/40 border-t-white motion-reduce:animate-none"
    />
  );
}
```

- [ ] **Step 4: Run test, verify it passes**

```bash
cd apps/web && pnpm vitest run src/components/features/gamebook/__tests__/checkout/Step3CheckoutForm.test.tsx
```

Expected: 8 tests passing.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/features/gamebook/checkout/Step3CheckoutForm.tsx apps/web/src/components/features/gamebook/__tests__/checkout/Step3CheckoutForm.test.tsx
git commit -m "feat(gamebook): #953 Step3CheckoutForm component

Visual-only checkout form with filled/loading/failed sub-states.
Stripe-style placeholders (mockup §709). Trust chips + recap + spinner
with prefers-reduced-motion guard. aria-busy on loading button."
```

---

### Task 7: `Step4Success`

**Files:**
- Create: `apps/web/src/components/features/gamebook/checkout/Step4Success.tsx`
- Test: `apps/web/src/components/features/gamebook/__tests__/checkout/Step4Success.test.tsx`

- [ ] **Step 1: Write the failing test**

```typescript
// apps/web/src/components/features/gamebook/__tests__/checkout/Step4Success.test.tsx
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';

import { Step4Success, type Step4Labels } from '../../checkout/Step4Success';

const LABELS: Step4Labels = {
  title: 'Crediti aggiunti!',
  subtitle: '100 crediti aggiunti al tuo account. Buon gioco!',
  recapLabels: {
    previous: 'Crediti precedenti',
    purchased: 'Crediti acquistati',
    balance: 'Bilancio crediti',
    freeQuotaTitle: 'Quota free questo mese',
    resetIn: 'Si resetta il 1° giugno',
    rate: '1 paragrafo = 1 credito',
  },
  backToGameCta: '🎯 Torna al gioco →',
  receiptLink: 'Vedi ricevuta · email a sara@example.com',
};

describe('Step4Success', () => {
  it('renders title with aria-live polite', () => {
    render(
      <Step4Success
        previousCredits={0}
        purchasedCredits={100}
        freeQuotaUsed={50}
        freeQuotaTotal={50}
        labels={LABELS}
        onBackToGame={vi.fn()}
      />
    );
    const title = screen.getByRole('heading', { name: 'Crediti aggiunti!' });
    expect(title).toBeInTheDocument();
    expect(title).toHaveAttribute('aria-live', 'polite');
  });

  it('renders previous + purchased + balance recap', () => {
    render(
      <Step4Success
        previousCredits={0}
        purchasedCredits={100}
        freeQuotaUsed={50}
        freeQuotaTotal={50}
        labels={LABELS}
        onBackToGame={vi.fn()}
      />
    );
    expect(screen.getByText('Crediti precedenti')).toBeInTheDocument();
    expect(screen.getByText('Crediti acquistati')).toBeInTheDocument();
    expect(screen.getByText('Bilancio crediti')).toBeInTheDocument();
    expect(screen.getByTestId('credits-balance')).toHaveTextContent(/100/);
  });

  it('balance is previous + purchased', () => {
    render(
      <Step4Success
        previousCredits={50}
        purchasedCredits={100}
        freeQuotaUsed={50}
        freeQuotaTotal={50}
        labels={LABELS}
        onBackToGame={vi.fn()}
      />
    );
    expect(screen.getByTestId('credits-balance')).toHaveTextContent(/150/);
  });

  it('confetti container has aria-hidden', () => {
    render(
      <Step4Success
        previousCredits={0}
        purchasedCredits={100}
        freeQuotaUsed={50}
        freeQuotaTotal={50}
        labels={LABELS}
        onBackToGame={vi.fn()}
      />
    );
    expect(document.querySelector('[data-slot="confetti"]')?.getAttribute('aria-hidden')).toBe('true');
  });

  it('back-to-game button fires callback', async () => {
    const user = userEvent.setup();
    const onBack = vi.fn();
    render(
      <Step4Success
        previousCredits={0}
        purchasedCredits={100}
        freeQuotaUsed={50}
        freeQuotaTotal={50}
        labels={LABELS}
        onBackToGame={onBack}
      />
    );
    await user.click(screen.getByRole('button', { name: LABELS.backToGameCta }));
    expect(onBack).toHaveBeenCalledOnce();
  });
});
```

- [ ] **Step 2: Run test, verify it fails**

```bash
cd apps/web && pnpm vitest run src/components/features/gamebook/__tests__/checkout/Step4Success.test.tsx
```

Expected: FAIL with module not found.

- [ ] **Step 3: Implement**

```typescript
// apps/web/src/components/features/gamebook/checkout/Step4Success.tsx
'use client';

import type { ReactElement } from 'react';

import clsx from 'clsx';

export interface Step4Labels {
  readonly title: string;
  readonly subtitle: string;
  readonly recapLabels: {
    readonly previous: string;
    readonly purchased: string;
    readonly balance: string;
    readonly freeQuotaTitle: string;
    readonly resetIn: string;
    readonly rate: string;
  };
  readonly backToGameCta: string;
  readonly receiptLink: string;
}

export interface Step4SuccessProps {
  readonly previousCredits: number;
  readonly purchasedCredits: number;
  readonly freeQuotaUsed: number;
  readonly freeQuotaTotal: number;
  readonly labels: Step4Labels;
  readonly onBackToGame: () => void;
}

const CONFETTI_PIECES = 14;
const CONFETTI_COLORS = [
  'bg-entity-toolkit',
  'bg-entity-event',
  'bg-entity-agent',
  'bg-entity-chat',
  'bg-entity-game',
] as const;

export function Step4Success({
  previousCredits,
  purchasedCredits,
  freeQuotaUsed,
  freeQuotaTotal,
  labels,
  onBackToGame,
}: Step4SuccessProps): ReactElement {
  const balance = previousCredits + purchasedCredits;
  return (
    <div data-slot="checkout-step-4" className="relative flex flex-col items-stretch gap-4 p-5 text-center">
      <div data-slot="confetti" aria-hidden="true" className="pointer-events-none absolute inset-0 overflow-hidden">
        {Array.from({ length: CONFETTI_PIECES }).map((_, i) => (
          <span
            key={i}
            className={clsx(
              'absolute top-[-10px] block h-3 w-[7px] rounded-sm opacity-85 motion-reduce:opacity-0',
              'animate-[checkout-confetti_2s_ease-in_infinite] motion-reduce:animate-none',
              CONFETTI_COLORS[i % CONFETTI_COLORS.length]
            )}
            style={{
              left: `${(i * 7.3) % 100}%`,
              animationDelay: `${(i * 0.13) % 1.4}s`,
              animationDuration: `${1.6 + (i % 3) * 0.4}s`,
            }}
          />
        ))}
      </div>
      <div className="flex flex-col items-center gap-1.5">
        <div aria-hidden="true" className="text-6xl motion-safe:animate-bounce">🎉</div>
        <h2 aria-live="polite" className="text-3xl font-extrabold text-foreground">
          {labels.title}
        </h2>
        <p className="text-sm text-muted-foreground">{labels.subtitle}</p>
      </div>
      <section className="flex flex-col gap-1.5 rounded-lg border border-border bg-card px-4 py-3.5 text-left">
        <Row label={labels.recapLabels.previous} value={previousCredits} />
        <Row label={labels.recapLabels.purchased} value={`+${purchasedCredits}`} accent />
        <div className="border-t border-border pt-1.5">
          <Row label={labels.recapLabels.balance} value={balance} testId="credits-balance" accent bold />
        </div>
        <div className="border-t border-border pt-1.5 text-xs text-muted-foreground">
          <Row label={labels.recapLabels.freeQuotaTitle} value={`${freeQuotaUsed} / ${freeQuotaTotal}`} muted />
        </div>
        <p className="font-mono text-[10px] text-muted-foreground">
          {labels.recapLabels.resetIn} · {labels.recapLabels.rate}
        </p>
      </section>
      <div className="flex flex-col gap-2">
        <button
          type="button"
          onClick={onBackToGame}
          className={clsx(
            'w-full rounded-lg bg-entity-session px-5 py-3.5 text-sm font-bold text-white',
            'transition-[filter] duration-150 hover:brightness-110 active:scale-[0.98]',
            'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2'
          )}
        >
          {labels.backToGameCta}
        </button>
        <a href="#" className="self-center text-xs text-muted-foreground hover:text-foreground">
          {labels.receiptLink}
        </a>
      </div>
    </div>
  );
}

function Row({
  label,
  value,
  accent = false,
  bold = false,
  muted = false,
  testId,
}: {
  readonly label: string;
  readonly value: string | number;
  readonly accent?: boolean;
  readonly bold?: boolean;
  readonly muted?: boolean;
  readonly testId?: string;
}): ReactElement {
  return (
    <div className="flex items-center justify-between text-sm">
      <span className={clsx(muted ? 'text-muted-foreground' : 'text-foreground')}>{label}</span>
      <span
        data-testid={testId}
        className={clsx(
          'font-mono tabular-nums',
          bold && 'text-base font-extrabold',
          accent && 'text-entity-toolkit',
          muted && 'text-muted-foreground'
        )}
      >
        {value}
      </span>
    </div>
  );
}
```

- [ ] **Step 4: Add confetti keyframes to globals.css**

Append to `apps/web/src/app/globals.css`:

```css
@keyframes checkout-confetti {
  0%   { transform: translateY(-10px) rotate(0deg);   opacity: 0; }
  20%  { opacity: 0.95; }
  100% { transform: translateY(420px) rotate(540deg); opacity: 0; }
}
```

- [ ] **Step 5: Run test, verify it passes**

```bash
cd apps/web && pnpm vitest run src/components/features/gamebook/__tests__/checkout/Step4Success.test.tsx
```

Expected: 5 tests passing.

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/features/gamebook/checkout/Step4Success.tsx apps/web/src/components/features/gamebook/__tests__/checkout/Step4Success.test.tsx apps/web/src/app/globals.css
git commit -m "feat(gamebook): #953 Step4Success + confetti keyframes

Success step with CSS-only confetti (14 pieces, entity color palette).
prefers-reduced-motion guard. Recap rows previous/purchased/balance.
aria-live polite on title."
```

---

### Task 8: `CheckoutModal` orchestrator

**Files:**
- Create: `apps/web/src/components/features/gamebook/CheckoutModal.tsx`
- Test: `apps/web/src/components/features/gamebook/__tests__/CheckoutModal.test.tsx`

This is the longest task. It orchestrates Steps 1-4 inside a shadcn `Dialog`, owns the FSM, and simulates payment latency. The `__testPaymentResult` prop bypasses the simulated latency for deterministic test outcomes.

- [ ] **Step 1: Write the failing test**

```typescript
// apps/web/src/components/features/gamebook/__tests__/CheckoutModal.test.tsx
import { render, screen, act } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

import { CheckoutModal, type CheckoutLabels } from '../CheckoutModal';

const LABELS: CheckoutLabels = {
  modalTitle: (s) => `Checkout step ${s}`,
  stepIndicator: { step1: 'Quota', step2: 'Pacchetto', step3: 'Pagamento', step4: 'Fatto', ariaCurrent: (s) => `Passo ${s} di 4` },
  close: 'Chiudi',
  step1: {
    heading: 'Quota raggiunta',
    subheading: 'Hai tradotto 50 paragrafi.',
    quotaLabel: 'Quota mensile',
    resetIn: 'Reset tra 12 giorni',
    primaryCta: '💎 Acquista crediti',
    secondaryCta: '⏸️ Continua senza',
    explainLink: 'Cosa sono i crediti?',
  },
  step2: {
    heading: 'Scegli il tuo pacchetto',
    subheading: 'Più paragrafi = miglior prezzo.',
    disclaimer: 'I crediti non scadono.',
    totalLabel: 'Totale',
    continueCta: 'Continua →',
    packBadges: { popular: 'Più popolare', save: 'Risparmia 30%' },
    packNames: { starter: 'Starter', mid: 'Mid', pro: 'Pro' },
    packCreditsSuffix: 'crediti',
    perParagraphSuffix: '/ paragrafo',
  },
  step3: {
    heading: 'Pagamento',
    summary: (name, credits, eur) => `Pacchetto ${name} · ${credits} crediti · ${eur}`,
    fieldLabels: { card: 'Numero carta', expiry: 'Scadenza', cvc: 'CVC', name: 'Nome sulla carta', country: 'Paese' },
    trustChips: ['SSL', 'Stripe', '14gg'],
    payCta: (eur) => `Paga ${eur}`,
    loadingCta: 'Elaborazione…',
    backLink: '← Torna ai pacchetti',
    failedBanner: { title: 'Pagamento rifiutato', detail: 'Riprova.' },
    recapLabels: { credits: 'crediti', vat: 'IVA inclusa', total: 'Totale' },
  },
  step4: {
    title: 'Crediti aggiunti!',
    subtitle: (credits) => `${credits} crediti aggiunti.`,
    recapLabels: {
      previous: 'Crediti precedenti',
      purchased: 'Crediti acquistati',
      balance: 'Bilancio crediti',
      freeQuotaTitle: 'Quota free',
      resetIn: 'Reset 1° giugno',
      rate: '1 par = 1 credito',
    },
    backToGameCta: '🎯 Torna al gioco',
    receiptLink: (email) => `Ricevuta · ${email}`,
  },
};

const QUOTA = { used: 50, total: 50, resetDate: '1 giugno', previousCredits: 0 };

describe('CheckoutModal', () => {
  beforeEach(() => {
    vi.useFakeTimers({ shouldAdvanceTime: false });
  });
  afterEach(() => {
    vi.useRealTimers();
  });

  it('does not render when open=false', () => {
    render(
      <CheckoutModal
        open={false}
        initialStep={1}
        quota={QUOTA}
        userEmail="sara@example.com"
        labels={LABELS}
        onClose={vi.fn()}
        onPurchaseSuccess={vi.fn()}
      />
    );
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('opens at Step 1 when initialStep=1', () => {
    render(
      <CheckoutModal
        open
        initialStep={1}
        quota={QUOTA}
        userEmail="sara@example.com"
        labels={LABELS}
        onClose={vi.fn()}
        onPurchaseSuccess={vi.fn()}
      />
    );
    expect(screen.getByRole('heading', { name: 'Quota raggiunta' })).toBeInTheDocument();
  });

  it('opens at Step 2 when initialStep=2', () => {
    render(
      <CheckoutModal
        open
        initialStep={2}
        quota={QUOTA}
        userEmail="sara@example.com"
        labels={LABELS}
        onClose={vi.fn()}
        onPurchaseSuccess={vi.fn()}
      />
    );
    expect(screen.getByRole('heading', { name: 'Scegli il tuo pacchetto' })).toBeInTheDocument();
  });

  it('Step 1 → Step 2 via primary CTA', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    render(
      <CheckoutModal
        open
        initialStep={1}
        quota={QUOTA}
        userEmail="sara@example.com"
        labels={LABELS}
        onClose={vi.fn()}
        onPurchaseSuccess={vi.fn()}
      />
    );
    await user.click(screen.getByRole('button', { name: /Acquista crediti/i }));
    expect(screen.getByRole('heading', { name: 'Scegli il tuo pacchetto' })).toBeInTheDocument();
  });

  it('Step 2 → Step 3 via Continua', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    render(
      <CheckoutModal
        open
        initialStep={2}
        quota={QUOTA}
        userEmail="sara@example.com"
        labels={LABELS}
        onClose={vi.fn()}
        onPurchaseSuccess={vi.fn()}
      />
    );
    await user.click(screen.getByRole('button', { name: /Continua/i }));
    expect(screen.getByRole('heading', { name: 'Pagamento' })).toBeInTheDocument();
  });

  it('Step 3 → Step 4 via Paga with __testPaymentResult=success after 2s', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    const onSuccess = vi.fn();
    render(
      <CheckoutModal
        open
        initialStep={2}
        quota={QUOTA}
        userEmail="sara@example.com"
        labels={LABELS}
        onClose={vi.fn()}
        onPurchaseSuccess={onSuccess}
        __testPaymentResult="success"
      />
    );
    await user.click(screen.getByRole('button', { name: /Continua/i }));
    await user.click(screen.getByRole('button', { name: /Paga/i }));
    // loading state visible
    expect(screen.getByRole('button', { name: /Elaborazione/i })).toBeInTheDocument();
    // advance 2s
    await act(async () => {
      vi.advanceTimersByTime(2000);
    });
    expect(screen.getByRole('heading', { name: 'Crediti aggiunti!' })).toBeInTheDocument();
    expect(onSuccess).toHaveBeenCalledWith('starter', 100);
  });

  it('Step 3 with __testPaymentResult=failed shows red banner', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    render(
      <CheckoutModal
        open
        initialStep={2}
        quota={QUOTA}
        userEmail="sara@example.com"
        labels={LABELS}
        onClose={vi.fn()}
        onPurchaseSuccess={vi.fn()}
        __testPaymentResult="failed"
      />
    );
    await user.click(screen.getByRole('button', { name: /Continua/i }));
    await user.click(screen.getByRole('button', { name: /Paga/i }));
    await act(async () => {
      vi.advanceTimersByTime(2000);
    });
    expect(screen.getByRole('alert')).toHaveTextContent(/Pagamento rifiutato/);
    // form still visible
    expect(screen.getByRole('heading', { name: 'Pagamento' })).toBeInTheDocument();
  });

  it('Step 3 → Step 2 via back link', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    render(
      <CheckoutModal
        open
        initialStep={2}
        quota={QUOTA}
        userEmail="sara@example.com"
        labels={LABELS}
        onClose={vi.fn()}
        onPurchaseSuccess={vi.fn()}
      />
    );
    await user.click(screen.getByRole('button', { name: /Continua/i }));
    await user.click(screen.getByRole('button', { name: /Torna ai pacchetti/i }));
    expect(screen.getByRole('heading', { name: 'Scegli il tuo pacchetto' })).toBeInTheDocument();
  });

  it('Step 4 → close via Torna al gioco', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    const onClose = vi.fn();
    render(
      <CheckoutModal
        open
        initialStep={2}
        quota={QUOTA}
        userEmail="sara@example.com"
        labels={LABELS}
        onClose={onClose}
        onPurchaseSuccess={vi.fn()}
        __testPaymentResult="success"
      />
    );
    await user.click(screen.getByRole('button', { name: /Continua/i }));
    await user.click(screen.getByRole('button', { name: /Paga/i }));
    await act(async () => {
      vi.advanceTimersByTime(2000);
    });
    await user.click(screen.getByRole('button', { name: /Torna al gioco/i }));
    expect(onClose).toHaveBeenCalledOnce();
  });

  it('ESC closes modal', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    const onClose = vi.fn();
    render(
      <CheckoutModal
        open
        initialStep={1}
        quota={QUOTA}
        userEmail="sara@example.com"
        labels={LABELS}
        onClose={onClose}
        onPurchaseSuccess={vi.fn()}
      />
    );
    await user.keyboard('{Escape}');
    expect(onClose).toHaveBeenCalled();
  });
});
```

- [ ] **Step 2: Run test, verify it fails**

```bash
cd apps/web && pnpm vitest run src/components/features/gamebook/__tests__/CheckoutModal.test.tsx
```

Expected: FAIL with module not found.

- [ ] **Step 3: Implement**

```typescript
// apps/web/src/components/features/gamebook/CheckoutModal.tsx
'use client';

import { useCallback, useState, type ReactElement } from 'react';

import { Dialog, DialogContent } from '@/components/ui/overlays/dialog';
import { CHECKOUT_PACKS, formatEur, type CheckoutPackId } from '@/lib/gamebook/checkout-packs';

import { CheckoutStepIndicator } from './checkout/CheckoutStepIndicator';
import { Step1QuotaReached, type Step1Labels } from './checkout/Step1QuotaReached';
import { Step2PackPicker, type Step2Labels } from './checkout/Step2PackPicker';
import { Step3CheckoutForm, type Step3Labels, type Step3SubState } from './checkout/Step3CheckoutForm';
import { Step4Success, type Step4Labels } from './checkout/Step4Success';

type ModalStep = 1 | 2 | 3 | 4;

export interface CheckoutLabels {
  readonly modalTitle: (step: ModalStep) => string;
  readonly close: string;
  readonly stepIndicator: {
    readonly step1: string;
    readonly step2: string;
    readonly step3: string;
    readonly step4: string;
    /** Receives the active step number and returns sr-only text. */
    readonly ariaCurrent: (step: ModalStep) => string;
  };
  readonly step1: Step1Labels;
  readonly step2: Step2Labels;
  readonly step3: Omit<Step3Labels, 'summary' | 'payCta' | 'recapValues'> & {
    readonly summary: (packName: string, credits: number, eur: string) => string;
    readonly payCta: (eur: string) => string;
  };
  readonly step4: Omit<Step4Labels, 'subtitle' | 'receiptLink'> & {
    readonly subtitle: (credits: number) => string;
    readonly receiptLink: (email: string) => string;
  };
}

export interface CheckoutQuota {
  readonly used: number;
  readonly total: number;
  readonly resetDate: string;
  readonly previousCredits: number;
}

export interface CheckoutModalProps {
  readonly open: boolean;
  readonly initialStep: 1 | 2;
  readonly quota: CheckoutQuota;
  readonly userEmail: string;
  readonly labels: CheckoutLabels;
  readonly onClose: () => void;
  readonly onPurchaseSuccess: (packId: CheckoutPackId, creditsAdded: number) => void;
  /**
   * TEST-ONLY deterministic override. When set, the simulated payment latency
   * still runs (2s) but the outcome is forced. Production code never sets this.
   */
  readonly __testPaymentResult?: 'success' | 'failed';
}

const PAYMENT_LATENCY_MS = 2000;

export function CheckoutModal({
  open,
  initialStep,
  quota,
  userEmail,
  labels,
  onClose,
  onPurchaseSuccess,
  __testPaymentResult,
}: CheckoutModalProps): ReactElement | null {
  const [step, setStep] = useState<ModalStep>(initialStep);
  const [selectedPack, setSelectedPack] = useState<CheckoutPackId>('starter');
  const [paymentSubState, setPaymentSubState] = useState<Step3SubState>('filled');

  const pack = CHECKOUT_PACKS.find((p) => p.id === selectedPack)!;
  const priceEurString = formatEur(pack.priceEur);

  const handleOpenChange = useCallback(
    (next: boolean) => {
      if (!next) onClose();
    },
    [onClose]
  );

  const handlePay = useCallback(() => {
    setPaymentSubState('loading');
    const outcome: 'success' | 'failed' = __testPaymentResult ?? 'success';
    setTimeout(() => {
      if (outcome === 'success') {
        onPurchaseSuccess(pack.id, pack.credits);
        setStep(4);
        setPaymentSubState('filled');
      } else {
        setPaymentSubState('failed');
      }
    }, PAYMENT_LATENCY_MS);
  }, [__testPaymentResult, onPurchaseSuccess, pack.id, pack.credits]);

  if (!open) return null;

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent
        data-slot="checkout-modal"
        aria-labelledby="checkout-modal-title"
        className="max-w-[520px] gap-0 overflow-hidden p-0"
      >
        <header className="flex items-center gap-2 p-3.5 pb-0">
          <CheckoutStepIndicator
            currentStep={step}
            labels={{
              step1: labels.stepIndicator.step1,
              step2: labels.stepIndicator.step2,
              step3: labels.stepIndicator.step3,
              step4: labels.stepIndicator.step4,
              ariaCurrent: labels.stepIndicator.ariaCurrent(step),
            }}
          />
        </header>
        <h2 id="checkout-modal-title" className="sr-only">
          {labels.modalTitle(step)}
        </h2>
        <div data-slot="checkout-modal-body" className="max-h-[80vh] overflow-y-auto">
          {step === 1 && (
            <Step1QuotaReached
              used={quota.used}
              total={quota.total}
              labels={labels.step1}
              onPrimaryClick={() => setStep(2)}
              onSecondaryClick={onClose}
            />
          )}
          {step === 2 && (
            <Step2PackPicker
              selectedId={selectedPack}
              labels={labels.step2}
              onSelect={setSelectedPack}
              onContinue={() => setStep(3)}
            />
          )}
          {step === 3 && (
            <Step3CheckoutForm
              subState={paymentSubState}
              labels={{
                ...labels.step3,
                summary: labels.step3.summary(labels.step2.packNames[pack.id], pack.credits, priceEurString),
                payCta: labels.step3.payCta(priceEurString),
                recapValues: { credits: priceEurString, total: priceEurString },
              }}
              onPay={handlePay}
              onBack={() => setStep(2)}
            />
          )}
          {step === 4 && (
            <Step4Success
              previousCredits={quota.previousCredits}
              purchasedCredits={pack.credits}
              freeQuotaUsed={quota.used}
              freeQuotaTotal={quota.total}
              labels={{
                ...labels.step4,
                subtitle: labels.step4.subtitle(pack.credits),
                receiptLink: labels.step4.receiptLink(userEmail),
              }}
              onBackToGame={onClose}
            />
          )}
        </div>
      </DialogContent>
    </Dialog>
  );
}
```

- [ ] **Step 4: Run test, verify it passes**

```bash
cd apps/web && pnpm vitest run src/components/features/gamebook/__tests__/CheckoutModal.test.tsx
```

Expected: 10 tests passing. (Note: shadcn Dialog uses portals and Radix focus management; the tests verify behavior, not DOM portal placement.)

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/features/gamebook/CheckoutModal.tsx apps/web/src/components/features/gamebook/__tests__/CheckoutModal.test.tsx
git commit -m "feat(gamebook): #953 CheckoutModal orchestrator

4-step FSM modal using shadcn Dialog. Simulates 2s payment latency
(__testPaymentResult prop for deterministic tests). Pack selection
default=starter, mutable in Step 2. onPurchaseSuccess fires once on
Step 4 entry."
```

---

### Task 9: `SoftWarningCredits`

**Files:**
- Create: `apps/web/src/components/features/gamebook/SoftWarningCredits.tsx`
- Test: `apps/web/src/components/features/gamebook/__tests__/SoftWarningCredits.test.tsx`

- [ ] **Step 1: Write the failing test**

```typescript
// apps/web/src/components/features/gamebook/__tests__/SoftWarningCredits.test.tsx
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';

import { SoftWarningCredits, type SoftWarningCreditsLabels } from '../SoftWarningCredits';

const LABELS: SoftWarningCreditsLabels = {
  title: 'Quasi alla fine della quota',
  subtitle: (used, total, remaining) => `${used}/${total} — restano ${remaining} paragrafi gratis.`,
  upgradeCta: 'Acquista crediti ora',
  dismissCta: 'Ok, continua',
  close: 'Chiudi avviso',
};

describe('SoftWarningCredits', () => {
  it('toast-mobile variant renders role=status at bottom', () => {
    render(
      <SoftWarningCredits
        used={47}
        total={50}
        variant="toast-mobile"
        labels={LABELS}
        onUpgrade={vi.fn()}
        onDismiss={vi.fn()}
      />
    );
    expect(screen.getByRole('status')).toBeInTheDocument();
    expect(screen.getByText(/47\/50/)).toBeInTheDocument();
  });

  it('modal-desktop variant renders role=dialog with aria-modal', () => {
    render(
      <SoftWarningCredits
        used={47}
        total={50}
        variant="modal-desktop"
        labels={LABELS}
        onUpgrade={vi.fn()}
        onDismiss={vi.fn()}
      />
    );
    const dialog = screen.getByRole('dialog');
    expect(dialog).toHaveAttribute('aria-modal', 'true');
  });

  it('subtitle interpolates remaining count', () => {
    render(
      <SoftWarningCredits
        used={47}
        total={50}
        variant="toast-mobile"
        labels={LABELS}
        onUpgrade={vi.fn()}
        onDismiss={vi.fn()}
      />
    );
    expect(screen.getByText(/restano 3 paragrafi/)).toBeInTheDocument();
  });

  it('upgrade CTA fires onUpgrade', async () => {
    const user = userEvent.setup();
    const onUpgrade = vi.fn();
    render(
      <SoftWarningCredits
        used={47}
        total={50}
        variant="toast-mobile"
        labels={LABELS}
        onUpgrade={onUpgrade}
        onDismiss={vi.fn()}
      />
    );
    await user.click(screen.getByRole('button', { name: LABELS.upgradeCta }));
    expect(onUpgrade).toHaveBeenCalledOnce();
  });

  it('dismiss CTA fires onDismiss', async () => {
    const user = userEvent.setup();
    const onDismiss = vi.fn();
    render(
      <SoftWarningCredits
        used={47}
        total={50}
        variant="toast-mobile"
        labels={LABELS}
        onUpgrade={vi.fn()}
        onDismiss={onDismiss}
      />
    );
    await user.click(screen.getByRole('button', { name: LABELS.dismissCta }));
    expect(onDismiss).toHaveBeenCalledOnce();
  });

  it('desktop modal renders close X button', async () => {
    const user = userEvent.setup();
    const onDismiss = vi.fn();
    render(
      <SoftWarningCredits
        used={47}
        total={50}
        variant="modal-desktop"
        labels={LABELS}
        onUpgrade={vi.fn()}
        onDismiss={onDismiss}
      />
    );
    await user.click(screen.getByRole('button', { name: LABELS.close }));
    expect(onDismiss).toHaveBeenCalledOnce();
  });
});
```

- [ ] **Step 2: Run test, verify it fails**

```bash
cd apps/web && pnpm vitest run src/components/features/gamebook/__tests__/SoftWarningCredits.test.tsx
```

Expected: FAIL with module not found.

- [ ] **Step 3: Implement**

```typescript
// apps/web/src/components/features/gamebook/SoftWarningCredits.tsx
'use client';

import type { ReactElement } from 'react';

import clsx from 'clsx';

export interface SoftWarningCreditsLabels {
  readonly title: string;
  /** Receives (used, total, remaining) and returns the interpolated subtitle. */
  readonly subtitle: (used: number, total: number, remaining: number) => string;
  readonly upgradeCta: string;
  readonly dismissCta: string;
  /** aria-label for the close X button (desktop modal only). */
  readonly close: string;
}

export interface SoftWarningCreditsProps {
  readonly used: number;
  readonly total: number;
  readonly variant: 'toast-mobile' | 'modal-desktop';
  readonly labels: SoftWarningCreditsLabels;
  readonly onUpgrade: () => void;
  readonly onDismiss: () => void;
}

export function SoftWarningCredits({
  used,
  total,
  variant,
  labels,
  onUpgrade,
  onDismiss,
}: SoftWarningCreditsProps): ReactElement {
  const remaining = Math.max(0, total - used);
  if (variant === 'modal-desktop') {
    return (
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby="soft-warning-title"
        data-slot="soft-warning-modal"
        className="fixed inset-0 z-40 flex items-center justify-center bg-foreground/40 p-5 backdrop-blur-sm"
      >
        <div className="relative flex w-full max-w-md gap-3.5 rounded-xl border border-entity-agent/30 bg-card p-5 pb-4 shadow-lg">
          <div
            aria-hidden="true"
            className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-entity-agent/15 text-xl text-entity-agent"
          >
            🟡
          </div>
          <div className="flex flex-1 flex-col gap-1">
            <h3 id="soft-warning-title" className="text-base font-bold text-foreground">
              {labels.title}
            </h3>
            <p className="text-xs leading-relaxed text-muted-foreground">
              {labels.subtitle(used, total, remaining)}
            </p>
            <div className="mt-2 flex gap-2">
              <button
                type="button"
                onClick={onUpgrade}
                className={clsx(
                  'flex-1 rounded-md bg-entity-toolkit px-3.5 py-2.5 text-xs font-bold text-white',
                  'transition-[filter] hover:brightness-110 active:scale-[0.98]',
                  'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2'
                )}
              >
                {labels.upgradeCta}
              </button>
              <button
                type="button"
                onClick={onDismiss}
                className={clsx(
                  'flex-1 rounded-md border border-border-strong bg-card px-3.5 py-2.5 text-xs font-semibold text-foreground',
                  'transition-colors hover:bg-muted',
                  'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2'
                )}
              >
                {labels.dismissCta}
              </button>
            </div>
          </div>
          <button
            type="button"
            aria-label={labels.close}
            onClick={onDismiss}
            className="absolute right-2.5 top-2.5 flex h-8 w-8 items-center justify-center rounded-full border border-border bg-muted text-foreground hover:bg-card focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
          >
            ×
          </button>
        </div>
      </div>
    );
  }
  // toast-mobile
  return (
    <div
      role="status"
      aria-live="polite"
      data-slot="soft-warning-toast"
      className="fixed bottom-4 left-3 right-3 z-40 flex flex-col gap-2.5 rounded-lg border border-entity-agent/30 bg-card px-3.5 py-3 shadow-md"
    >
      <div className="flex items-start gap-2.5">
        <span
          aria-hidden="true"
          className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-entity-agent/15 text-entity-agent"
        >
          🟡
        </span>
        <div className="flex-1">
          <p className="text-xs font-bold text-foreground">{labels.title}</p>
          <p className="text-[11px] leading-relaxed text-muted-foreground">
            {labels.subtitle(used, total, remaining)}
          </p>
        </div>
      </div>
      <div className="flex gap-2">
        <button
          type="button"
          onClick={onUpgrade}
          className={clsx(
            'flex-1 rounded-md bg-entity-toolkit px-3 py-2 text-xs font-bold text-white',
            'transition-[filter] hover:brightness-110 active:scale-[0.98]',
            'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2'
          )}
        >
          {labels.upgradeCta}
        </button>
        <button
          type="button"
          onClick={onDismiss}
          className={clsx(
            'flex-1 rounded-md border border-border-strong bg-card px-3 py-2 text-xs font-semibold text-foreground',
            'transition-colors hover:bg-muted',
            'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2'
          )}
        >
          {labels.dismissCta}
        </button>
      </div>
    </div>
  );
}
```

- [ ] **Step 4: Run test, verify it passes**

```bash
cd apps/web && pnpm vitest run src/components/features/gamebook/__tests__/SoftWarningCredits.test.tsx
```

Expected: 6 tests passing.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/features/gamebook/SoftWarningCredits.tsx apps/web/src/components/features/gamebook/__tests__/SoftWarningCredits.test.tsx
git commit -m "feat(gamebook): #953 SoftWarningCredits component

47/50 warning. toast-mobile (bottom, role=status) or modal-desktop
(center, role=dialog, aria-modal). Close X on desktop. Entity-agent
icon, entity-toolkit upgrade CTA."
```

---

### Task 10: Re-export from feature index

**Files:**
- Modify: `apps/web/src/components/features/gamebook/index.ts`

- [ ] **Step 1: Add re-exports**

Append to `apps/web/src/components/features/gamebook/index.ts`:

```typescript
export { CheckoutModal, type CheckoutModalProps, type CheckoutLabels, type CheckoutQuota } from './CheckoutModal';
export { SoftWarningCredits, type SoftWarningCreditsProps, type SoftWarningCreditsLabels } from './SoftWarningCredits';
```

- [ ] **Step 2: Verify typecheck**

```bash
cd apps/web && pnpm typecheck
```

Expected: zero errors.

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/components/features/gamebook/index.ts
git commit -m "chore(gamebook): #953 re-export CheckoutModal + SoftWarningCredits"
```

---

### Task 11: Wire into `GamebookIndexView`

**Files:**
- Modify: `apps/web/src/app/(authenticated)/gamebook/_components/GamebookIndexView.tsx`

First, read the current file to understand the integration surface.

- [ ] **Step 1: Read existing wiring**

```bash
cd apps/web && head -80 src/app/\(authenticated\)/gamebook/_components/GamebookIndexView.tsx
```

Note: the QuotaWidget consumes `quota` from the orchestrator FSM (`deriveGamebookIndexState`). The widget already has an `onUpgradeClick` prop that's likely a no-op today. Identify:
- where `QuotaWidget` renders
- where `onUpgradeClick` is passed
- what variant logic computes (already returns `'default' | 'soft' | 'hard'`)

- [ ] **Step 2: Add state + handlers**

In `GamebookIndexView.tsx`, near the top of the component body (after existing hook calls):

```typescript
const [checkoutOpen, setCheckoutOpen] = useState(false);
const [checkoutInitialStep, setCheckoutInitialStep] = useState<1 | 2>(2);

const { shouldShow: showSoftWarning, dismiss: dismissSoftWarning } = useSoftWarningDismissal(
  quota.used,
  quota.total
);

const openCheckoutFromUpgrade = useCallback(() => {
  // hard variant: usage is 100%, open at Step 1 (announcement)
  // soft variant: usage is 90%-99%, skip announcement, open at pack picker
  const initial = quota.used >= quota.total ? 1 : 2;
  setCheckoutInitialStep(initial);
  setCheckoutOpen(true);
}, [quota.used, quota.total]);

const handlePurchaseSuccess = useCallback((packId: CheckoutPackId, creditsAdded: number) => {
  // MVP: no real persistence. Log for telemetry; future PR invalidates
  // quota query here when real backend lands.
  console.info('[gamebook] purchase success (visual-only)', { packId, creditsAdded });
}, []);
```

Add imports at the top:

```typescript
import { useCallback, useState } from 'react';
import { CheckoutModal } from '@/components/features/gamebook/CheckoutModal';
import { SoftWarningCredits } from '@/components/features/gamebook/SoftWarningCredits';
import { useSoftWarningDismissal } from '@/lib/gamebook/hooks/useSoftWarningDismissal';
import type { CheckoutPackId } from '@/lib/gamebook/checkout-packs';
```

(If `useState`/`useCallback` are already imported, do not duplicate.)

- [ ] **Step 3: Update `QuotaWidget` invocation**

Find the existing `<QuotaWidget ... />` render. Pass `onUpgradeClick`:

```tsx
<QuotaWidget
  quota={quota}
  variant={quotaVariant}
  labels={quotaLabels}
  onUpgradeClick={openCheckoutFromUpgrade}
/>
```

- [ ] **Step 4: Add `SoftWarningCredits` + `CheckoutModal` to JSX**

After the existing main render block (last `</div>` or fragment close), insert:

```tsx
{showSoftWarning && (
  <>
    {/* Mobile toast */}
    <div className="sm:hidden">
      <SoftWarningCredits
        used={quota.used}
        total={quota.total}
        variant="toast-mobile"
        labels={softWarningLabels}
        onUpgrade={() => {
          dismissSoftWarning();
          setCheckoutInitialStep(2);
          setCheckoutOpen(true);
        }}
        onDismiss={dismissSoftWarning}
      />
    </div>
    {/* Desktop modal */}
    <div className="hidden sm:block">
      <SoftWarningCredits
        used={quota.used}
        total={quota.total}
        variant="modal-desktop"
        labels={softWarningLabels}
        onUpgrade={() => {
          dismissSoftWarning();
          setCheckoutInitialStep(2);
          setCheckoutOpen(true);
        }}
        onDismiss={dismissSoftWarning}
      />
    </div>
  </>
)}

<CheckoutModal
  open={checkoutOpen}
  initialStep={checkoutInitialStep}
  quota={{
    used: quota.used,
    total: quota.total,
    resetDate: quota.resetDate ?? '1° del mese prossimo',
    previousCredits: 0,
  }}
  userEmail={userEmail}
  labels={checkoutLabels}
  onClose={() => setCheckoutOpen(false)}
  onPurchaseSuccess={handlePurchaseSuccess}
/>
```

Where `softWarningLabels`, `checkoutLabels`, and `userEmail` come from:
- `softWarningLabels` and `checkoutLabels`: add hardcoded Italian labels at the top of the file (placed inline; future i18n extraction is out-of-scope per spec).
- `userEmail`: read from the session via the existing auth context. Search the file for an existing `useSession` or `useUser` hook; if absent, pass `userEmail=""` as a placeholder (Step 4's receipt link will render with empty email — acceptable for MVP since this is visual-only).

- [ ] **Step 5: Add the labels constants at top of file**

```typescript
const CHECKOUT_LABELS: import('@/components/features/gamebook/CheckoutModal').CheckoutLabels = {
  modalTitle: (s) => `Checkout · passo ${s} di 4`,
  close: 'Chiudi checkout',
  stepIndicator: {
    step1: 'Quota',
    step2: 'Pacchetto',
    step3: 'Pagamento',
    step4: 'Fatto',
    ariaCurrent: (s) => `Passo ${s} di 4`,
  },
  step1: {
    heading: 'Quota raggiunta',
    subheading: 'Hai tradotto 50 paragrafi questo mese. La quota gratuita si resetta il 1° giugno.',
    quotaLabel: 'Quota mensile',
    resetIn: 'Reset tra 12 giorni',
    primaryCta: '💎 Acquista 100 crediti (€5)',
    secondaryCta: '⏸️ Continua senza traduzione',
    explainLink: 'Cosa sono i crediti? →',
  },
  step2: {
    heading: 'Scegli il tuo pacchetto',
    subheading: 'Più paragrafi = miglior prezzo. I crediti non scadono.',
    disclaimer: 'I crediti non scadono. La quota free di 50 paragrafi si resetta automaticamente ogni mese.',
    totalLabel: 'Totale',
    continueCta: 'Continua →',
    packBadges: { popular: 'Più popolare', save: 'Risparmia 30%' },
    packNames: { starter: 'Starter', mid: 'Mid', pro: 'Pro' },
    packCreditsSuffix: 'crediti',
    perParagraphSuffix: '/ paragrafo',
  },
  step3: {
    heading: 'Pagamento',
    summary: (name, credits, eur) => `Pacchetto ${name} · ${credits} crediti · ${eur}`,
    fieldLabels: { card: 'Numero carta', expiry: 'Scadenza', cvc: 'CVC', name: 'Nome sulla carta', country: 'Paese' },
    trustChips: ['SSL secure', 'Stripe powered', 'Politica rimborso 14gg'],
    payCta: (eur) => `Paga ${eur}`,
    loadingCta: 'Elaborazione…',
    backLink: '← Torna ai pacchetti',
    failedBanner: { title: 'Pagamento rifiutato', detail: 'Carta scaduta · prova un altro metodo o riscrivi i dati.' },
    recapLabels: { credits: '100 crediti', vat: 'IVA inclusa', total: 'Totale' },
  },
  step4: {
    title: 'Crediti aggiunti!',
    subtitle: (credits) => `${credits} crediti aggiunti al tuo account. Buon gioco!`,
    recapLabels: {
      previous: 'Crediti precedenti',
      purchased: 'Crediti acquistati',
      balance: 'Bilancio crediti',
      freeQuotaTitle: 'Quota free questo mese',
      resetIn: 'Si resetta il 1° giugno',
      rate: '1 paragrafo = 1 credito',
    },
    backToGameCta: '🎯 Torna al gioco →',
    receiptLink: (email) => `Vedi ricevuta · email a ${email}`,
  },
};

const SOFT_WARNING_LABELS: import('@/components/features/gamebook/SoftWarningCredits').SoftWarningCreditsLabels = {
  title: 'Quasi alla fine della quota',
  subtitle: (used, total, remaining) =>
    `Hai usato ${used}/${total} paragrafi gratis questo mese. Restano ${remaining}.`,
  upgradeCta: 'Acquista crediti ora',
  dismissCta: 'Ok, continua',
  close: 'Chiudi avviso',
};
```

- [ ] **Step 6: Typecheck + lint + existing tests**

```bash
cd apps/web && pnpm typecheck
cd apps/web && pnpm lint -- --max-warnings=0 src/app/\(authenticated\)/gamebook/
cd apps/web && pnpm vitest run src/app/\(authenticated\)/gamebook/
```

All three must pass with zero errors. If any fail, fix inline before commit.

- [ ] **Step 7: Commit**

```bash
git add apps/web/src/app/\(authenticated\)/gamebook/_components/GamebookIndexView.tsx
git commit -m "feat(gamebook): #953 wire CheckoutModal + SoftWarningCredits

QuotaWidget onUpgradeClick now opens CheckoutModal (Step 1 if hard,
Step 2 if soft). SoftWarningCredits auto-shows at 90%+ via
useSoftWarningDismissal hook. Italian labels inline (i18n extraction
out of scope)."
```

---

### Task 12: Playwright `@ci` E2E spec

**Files:**
- Create: `apps/web/e2e/gamebook-checkout-modal.spec.ts`

- [ ] **Step 1: Read existing E2E helper patterns**

```bash
cd apps/web && head -50 e2e/_helpers/*.ts | head -100
```

Identify the auth helper (typically `loginAsUser` or similar) and the fixture-injection mechanism used by other `@ci` specs.

- [ ] **Step 2: Write E2E spec**

```typescript
// apps/web/e2e/gamebook-checkout-modal.spec.ts
import { test, expect, type Page } from '@playwright/test';

import { loginAsTestUser } from './_helpers/auth';

/**
 * @ci E2E happy path for the gamebook checkout modal (#953).
 *
 * Quota state is seeded at 47/50 via the visual-test-fixture pattern
 * (no backend endpoint exists; this is the Gate B approach used by
 * other gamebook specs).
 */

test.describe('@ci gamebook checkout modal', () => {
  test.beforeEach(async ({ page }) => {
    await loginAsTestUser(page);
    // Inject quota fixture override via route interception
    await page.route('**/api/v1/users/me/quota*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ used: 47, total: 50, resetDate: '1 giugno' }),
      });
    });
    // Clear sessionStorage so soft warning shows
    await page.addInitScript(() => {
      sessionStorage.removeItem('gamebook.soft-warning.dismissed-at');
    });
  });

  test('soft warning → upgrade → step 2 → 3 → 4 → close', async ({ page }) => {
    await page.goto('/gamebook');

    // Soft warning visible (mobile or desktop)
    const softWarning = page
      .locator('[data-slot="soft-warning-toast"], [data-slot="soft-warning-modal"]')
      .first();
    await expect(softWarning).toBeVisible();

    // Click upgrade CTA from soft warning
    await softWarning.getByRole('button', { name: /Acquista crediti/i }).click();

    // Modal opens at Step 2 (pack picker)
    const modal = page.getByRole('dialog');
    await expect(modal).toBeVisible();
    await expect(modal.getByRole('heading', { name: 'Scegli il tuo pacchetto' })).toBeVisible();

    // Starter is pre-selected; click Continua
    await modal.getByRole('button', { name: /Continua/i }).click();
    await expect(modal.getByRole('heading', { name: 'Pagamento' })).toBeVisible();

    // Force payment success via URL parameter (read-only by CheckoutModal in non-prod)
    // This is the deterministic seam for E2E. Alternative: page.evaluate to inject prop.
    // For this spec we exercise the default success path (no failure injection).
    await modal.getByRole('button', { name: /Paga/i }).click();

    // Loading state visible (briefly)
    await expect(modal.getByRole('button', { name: /Elaborazione/i })).toBeVisible();

    // Step 4 success appears after ~2s
    await expect(modal.getByRole('heading', { name: 'Crediti aggiunti!' })).toBeVisible({ timeout: 4000 });

    // Click Torna al gioco
    await modal.getByRole('button', { name: /Torna al gioco/i }).click();

    // Modal closes
    await expect(modal).not.toBeVisible();
  });

  test('ESC closes the modal at any step', async ({ page }) => {
    await page.goto('/gamebook');
    const softWarning = page
      .locator('[data-slot="soft-warning-toast"], [data-slot="soft-warning-modal"]')
      .first();
    await softWarning.getByRole('button', { name: /Acquista crediti/i }).click();

    const modal = page.getByRole('dialog');
    await expect(modal).toBeVisible();
    await page.keyboard.press('Escape');
    await expect(modal).not.toBeVisible();
  });
});
```

- [ ] **Step 3: Run E2E spec**

```bash
cd apps/web && pnpm test:e2e -- gamebook-checkout-modal
```

Expected: 2 tests passing. If the auth helper or fixture interception is different from the template above, adapt to match the patterns used by neighboring `*.spec.ts` files. Do not invent new helpers.

- [ ] **Step 4: Commit**

```bash
git add apps/web/e2e/gamebook-checkout-modal.spec.ts
git commit -m "test(gamebook): #953 Playwright @ci checkout modal happy path

Soft warning → upgrade → Step 2 → 3 → 4 → close + ESC close.
Quota seeded at 47/50 via route interception (Gate B pattern)."
```

---

### Task 13: Chromatic stories

**Files:**
- Create: `apps/web/src/stories/features/gamebook/CheckoutModal.stories.tsx`

- [ ] **Step 1: Read existing story pattern**

```bash
ls apps/web/src/stories/features/ 2>/dev/null
cat apps/web/src/stories/features/gamebook/*.stories.tsx 2>/dev/null | head -80
```

If the `features/gamebook/` story directory doesn't exist yet, find any `.stories.tsx` under `apps/web/src/stories/` and use its CSF3 structure as a template.

- [ ] **Step 2: Write stories — 7 states × 2 themes**

```typescript
// apps/web/src/stories/features/gamebook/CheckoutModal.stories.tsx
import type { Meta, StoryObj } from '@storybook/react';
import { useState } from 'react';

import { CheckoutModal, type CheckoutLabels } from '@/components/features/gamebook/CheckoutModal';
import { SoftWarningCredits } from '@/components/features/gamebook/SoftWarningCredits';

const LABELS: CheckoutLabels = {
  modalTitle: (s) => `Checkout · passo ${s} di 4`,
  close: 'Chiudi checkout',
  stepIndicator: {
    step1: 'Quota', step2: 'Pacchetto', step3: 'Pagamento', step4: 'Fatto',
    ariaCurrent: (s) => `Passo ${s} di 4`,
  },
  step1: {
    heading: 'Quota raggiunta',
    subheading: 'Hai tradotto 50 paragrafi questo mese. La quota gratuita si resetta il 1° giugno.',
    quotaLabel: 'Quota mensile',
    resetIn: 'Reset tra 12 giorni',
    primaryCta: '💎 Acquista 100 crediti (€5)',
    secondaryCta: '⏸️ Continua senza traduzione',
    explainLink: 'Cosa sono i crediti? →',
  },
  step2: {
    heading: 'Scegli il tuo pacchetto',
    subheading: 'Più paragrafi = miglior prezzo. I crediti non scadono.',
    disclaimer: 'I crediti non scadono.',
    totalLabel: 'Totale',
    continueCta: 'Continua →',
    packBadges: { popular: 'Più popolare', save: 'Risparmia 30%' },
    packNames: { starter: 'Starter', mid: 'Mid', pro: 'Pro' },
    packCreditsSuffix: 'crediti',
    perParagraphSuffix: '/ paragrafo',
  },
  step3: {
    heading: 'Pagamento',
    summary: (name, credits, eur) => `Pacchetto ${name} · ${credits} crediti · ${eur}`,
    fieldLabels: { card: 'Numero carta', expiry: 'Scadenza', cvc: 'CVC', name: 'Nome sulla carta', country: 'Paese' },
    trustChips: ['SSL secure', 'Stripe powered', 'Politica rimborso 14gg'],
    payCta: (eur) => `Paga ${eur}`,
    loadingCta: 'Elaborazione…',
    backLink: '← Torna ai pacchetti',
    failedBanner: { title: 'Pagamento rifiutato', detail: 'Carta scaduta · prova un altro metodo.' },
    recapLabels: { credits: '100 crediti', vat: 'IVA inclusa', total: 'Totale' },
  },
  step4: {
    title: 'Crediti aggiunti!',
    subtitle: (credits) => `${credits} crediti aggiunti al tuo account. Buon gioco!`,
    recapLabels: {
      previous: 'Crediti precedenti',
      purchased: 'Crediti acquistati',
      balance: 'Bilancio crediti',
      freeQuotaTitle: 'Quota free questo mese',
      resetIn: 'Si resetta il 1° giugno',
      rate: '1 paragrafo = 1 credito',
    },
    backToGameCta: '🎯 Torna al gioco →',
    receiptLink: (email) => `Vedi ricevuta · email a ${email}`,
  },
};

const meta: Meta<typeof CheckoutModal> = {
  title: 'Features/Gamebook/CheckoutModal',
  component: CheckoutModal,
  parameters: { layout: 'fullscreen' },
};
export default meta;

type Story = StoryObj<typeof CheckoutModal>;

const baseQuota = { used: 50, total: 50, resetDate: '1 giugno', previousCredits: 0 };

export const Step1QuotaReached: Story = {
  args: { open: true, initialStep: 1, quota: baseQuota, userEmail: 'sara@example.com', labels: LABELS, onClose: () => {}, onPurchaseSuccess: () => {} },
};

export const Step2PackPicker: Story = {
  args: { ...Step1QuotaReached.args!, initialStep: 2 },
};

// For Step 3 and beyond we need an interactive variant since the FSM lives inside
// the modal. The simplest approach is to provide a "force step" prop, but the
// production component doesn't expose one. Instead, Step 3 and Step 4 stories
// rely on user interaction (Chromatic interaction snapshots not configured here).
// For visual regression of Step 3/4, render the underlying step components
// directly:

export const Step3CheckoutFilled: Story = {
  render: () => {
    // Compose the modal-shell-like wrapper around a directly-rendered Step3CheckoutForm
    // Import locally to keep meta default to CheckoutModal
    const { Step3CheckoutForm } = require('@/components/features/gamebook/checkout/Step3CheckoutForm');
    return (
      <div style={{ maxWidth: 520, margin: '40px auto', border: '1px solid var(--border)', borderRadius: 12 }}>
        <Step3CheckoutForm
          subState="filled"
          labels={{
            ...LABELS.step3,
            summary: 'Pacchetto Starter · 100 crediti · 5 €',
            payCta: 'Paga 5 €',
            recapValues: { credits: '5 €', total: '5 €' },
          }}
          onPay={() => {}}
          onBack={() => {}}
        />
      </div>
    );
  },
};

export const Step3CheckoutLoading: Story = {
  render: () => {
    const { Step3CheckoutForm } = require('@/components/features/gamebook/checkout/Step3CheckoutForm');
    return (
      <div style={{ maxWidth: 520, margin: '40px auto', border: '1px solid var(--border)', borderRadius: 12 }}>
        <Step3CheckoutForm
          subState="loading"
          labels={{
            ...LABELS.step3,
            summary: 'Pacchetto Starter · 100 crediti · 5 €',
            payCta: 'Paga 5 €',
            recapValues: { credits: '5 €', total: '5 €' },
          }}
          onPay={() => {}}
          onBack={() => {}}
        />
      </div>
    );
  },
};

export const Step3CheckoutFailed: Story = {
  render: () => {
    const { Step3CheckoutForm } = require('@/components/features/gamebook/checkout/Step3CheckoutForm');
    return (
      <div style={{ maxWidth: 520, margin: '40px auto', border: '1px solid var(--border)', borderRadius: 12 }}>
        <Step3CheckoutForm
          subState="failed"
          labels={{
            ...LABELS.step3,
            summary: 'Pacchetto Starter · 100 crediti · 5 €',
            payCta: 'Paga 5 €',
            recapValues: { credits: '5 €', total: '5 €' },
          }}
          onPay={() => {}}
          onBack={() => {}}
        />
      </div>
    );
  },
};

export const Step4Success: Story = {
  render: () => {
    const { Step4Success } = require('@/components/features/gamebook/checkout/Step4Success');
    return (
      <div style={{ maxWidth: 520, margin: '40px auto', border: '1px solid var(--border)', borderRadius: 12 }}>
        <Step4Success
          previousCredits={0}
          purchasedCredits={100}
          freeQuotaUsed={50}
          freeQuotaTotal={50}
          labels={{
            ...LABELS.step4,
            subtitle: '100 crediti aggiunti al tuo account. Buon gioco!',
            receiptLink: 'Vedi ricevuta · email a sara@example.com',
          }}
          onBackToGame={() => {}}
        />
      </div>
    );
  },
};

export const SoftWarningToast: Story = {
  render: () => (
    <div style={{ position: 'relative', minHeight: 200 }}>
      <SoftWarningCredits
        used={47}
        total={50}
        variant="toast-mobile"
        labels={{
          title: 'Quasi alla fine della quota',
          subtitle: (used, total, remaining) => `Hai usato ${used}/${total} paragrafi. Restano ${remaining}.`,
          upgradeCta: 'Acquista crediti',
          dismissCta: 'Ok',
          close: 'Chiudi avviso',
        }}
        onUpgrade={() => {}}
        onDismiss={() => {}}
      />
    </div>
  ),
};

export const SoftWarningModal: Story = {
  render: () => (
    <div style={{ position: 'relative', minHeight: 400 }}>
      <SoftWarningCredits
        used={47}
        total={50}
        variant="modal-desktop"
        labels={{
          title: 'Quasi alla fine della quota',
          subtitle: (used, total, remaining) => `Hai usato ${used}/${total} paragrafi gratis questo mese. Restano ${remaining}.`,
          upgradeCta: 'Acquista crediti ora',
          dismissCta: 'Ok, continua',
          close: 'Chiudi avviso',
        }}
        onUpgrade={() => {}}
        onDismiss={() => {}}
      />
    </div>
  ),
};
```

- [ ] **Step 3: Verify Storybook builds**

```bash
cd apps/web && pnpm storybook:build 2>&1 | tail -20
```

Expected: build completes successfully with the new stories registered.

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/stories/features/gamebook/CheckoutModal.stories.tsx
git commit -m "test(gamebook): #953 Chromatic stories for checkout modal + soft warning

7 stories: Step1/Step2 via CheckoutModal, Step3 (filled/loading/failed)
+ Step4 + SoftWarning toast/modal via direct sub-component rendering.
Auto-snapshots in light/dark via existing Chromatic config."
```

---

### Task 14: Update migration matrix + bundle measurement

**Files:**
- Modify: `docs/for-developers/frontend/v2-migration-matrix.md`

- [ ] **Step 1: Read matrix to find the SP6 quota-credits row**

```bash
grep -n "quota\|credits\|sp6-libro-game-quota" docs/for-developers/frontend/v2-migration-matrix.md | head -10
```

If a row exists with `Status = pending`, update it. If not, append a new row in the SP6 section (consult neighboring entries for format).

- [ ] **Step 2: Update / add row**

Example diff (adapt to the matrix's exact format):

```markdown
| sp6-libro-game-quota-credits | CheckoutModal + SoftWarningCredits | `apps/web/src/components/features/gamebook/{CheckoutModal,SoftWarningCredits}.tsx` | `/gamebook` (embedded) | 4-step modal + 47/50 soft warning, visual-only (no Stripe) | done | #<PR>  |
```

The PR number will be filled at PR-open time (Task 16). For now, leave a placeholder `#TBD-PR` that the PR creation step replaces.

- [ ] **Step 3: Bundle measurement**

```bash
cd apps/web && pnpm build 2>&1 | tee /tmp/build-after.log
```

Capture the `/gamebook` route size from the build output. Compare against a pre-task baseline if you have one (otherwise, simply record the delta in the PR description).

- [ ] **Step 4: Commit**

```bash
git add docs/for-developers/frontend/v2-migration-matrix.md
git commit -m "docs(matrix): #953 mark sp6-libro-game-quota-credits done"
```

---

### Task 15: Final verification gate

**Files:** none (verification only)

- [ ] **Step 1: Full FE suite**

```bash
cd apps/web && pnpm typecheck && pnpm lint -- --max-warnings=0 && pnpm test
```

All three commands must exit 0. If any fails, fix inline and re-run.

- [ ] **Step 2: a11y axe-core on the modal stories**

```bash
cd apps/web && pnpm test:e2e -- a11y/checkout-modal 2>&1 | tail -30
```

If no dedicated a11y spec exists for the new components, manually run the Playwright `@ci` spec from Task 12 and inspect the axe results in the Playwright report (`pnpm test:e2e:report`). Zero `serious` or `critical` violations required.

- [ ] **Step 3: Token discipline grep**

```bash
grep -rEn "bg-(white|slate-|gray-|zinc-|neutral-)[0-9]+|text-(slate-|gray-|zinc-|neutral-)[0-9]+" apps/web/src/components/features/gamebook/CheckoutModal.tsx apps/web/src/components/features/gamebook/SoftWarningCredits.tsx apps/web/src/components/features/gamebook/checkout/ || echo "TOKEN OK"
```

Expected: `TOKEN OK`. If any match, replace with semantic / entity tokens.

- [ ] **Step 4: Coverage report**

```bash
cd apps/web && pnpm test:coverage 2>&1 | grep -A 2 "checkout-packs\|useSoftWarningDismissal\|CheckoutModal\|SoftWarningCredits\|Step[1-4]" | head -30
```

Expected: each new file ≥ 85% statements + 80% branches. If below, add the missing tests before continuing.

---

### Task 16: Push branch + open PR + close issue

**Files:** none (git + GitHub)

- [ ] **Step 1: Push branch**

```bash
git push -u origin feature/issue-953-quota-credits-checkout-modal
```

- [ ] **Step 2: Open PR with full body**

```bash
gh pr create --base main-dev --title "feat(gamebook): #953 quota/credits checkout modal (visual-only)" --body "$(cat <<'EOF'
## Summary

4-step checkout modal + 47/50 soft warning for the libro-game quota/credits flow. 100% frontend, no backend, no real Stripe (mockup §709 explicitly visual-only).

- **Step 1** Quota reached announcement with two CTAs (acquista / continua senza)
- **Step 2** 3-pack radiogroup (Starter/Mid/Pro), single-select with totals recap
- **Step 3** Visual-only Stripe-style checkout form, 3 sub-states (filled/loading/failed) with deterministic test seam (`__testPaymentResult`)
- **Step 4** Success recap + CSS confetti (prefers-reduced-motion guard) + return-to-game CTA
- **SoftWarningCredits** auto-shows at 90%+ usage as toast (mobile) or modal (desktop), dismissable per session

## Scope clarification

Issue title said "widget" but the referenced mockup (`sp6-libro-game-quota-credits.jsx`) is the *checkout modal*, not a display widget. The display widget already shipped in #788 as `QuotaWidget`. This PR wires `QuotaWidget.onUpgradeClick` to open the new `CheckoutModal`. Path corrected from `components/v2/gamebook/` (deprecated post DS de-versioning) to `components/features/gamebook/`.

## Architecture

- shadcn `Dialog` for focus trap, ESC, ARIA wiring
- Pure-presentational step components owned by a `CheckoutModal` FSM
- `useSoftWarningDismissal` hook with `sessionStorage` per-tab persistence
- Hardcoded pack catalog (`CHECKOUT_PACKS`) — `GET /api/v1/budget/packs` deferred
- Token-discipline DS-15 compliant (no hardcoded neutral palette)

## Out of scope (future PRs)
- Real Stripe `<Elements>` + `PaymentIntent`
- Pack catalog endpoint
- Route `/gamebook/checkout/success`
- Cross-session dismissal persistence
- Promo codes / multi-currency / admin price override

## Test plan
- [x] Unit: `checkout-packs.test.ts` (8 tests), `useSoftWarningDismissal.test.ts` (8 tests)
- [x] Component: `CheckoutStepIndicator` (7), `Step1QuotaReached` (6), `Step2PackPicker` (8), `Step3CheckoutForm` (8), `Step4Success` (5), `CheckoutModal` (10), `SoftWarningCredits` (6)
- [x] E2E `@ci`: `gamebook-checkout-modal.spec.ts` (2 scenarios: happy path + ESC close)
- [x] Chromatic: 7 stories (Step 1, 2, 3×3, 4, SoftWarning×2)
- [x] a11y: zero serious/critical violations on all 7 mockup states
- [x] Token grep: TOKEN OK
- [x] Typecheck + lint zero warnings
- [x] Coverage: each new file ≥ 85% statements

## Bundle delta on /gamebook

<paste output of `pnpm build` route size diff here>

## Refs

- Closes #953
- Mockup: `admin-mockups/design_files/sp6-libro-game-quota-credits.jsx`
- Spec: `docs/superpowers/specs/2026-05-18-issue-953-gamebook-checkout-modal-design.md`
- Plan: `docs/superpowers/plans/2026-05-18-issue-953-gamebook-checkout-modal.md`

🤖 Generated with [Claude Code](https://claude.com/claude-code)

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

- [ ] **Step 3: Update matrix row PR placeholder**

After the PR is created, capture the PR number:

```bash
PR_NUM=$(gh pr view --json number -q .number)
echo "PR #$PR_NUM"
```

Replace `#TBD-PR` in the matrix with the real number:

```bash
sed -i "s|#TBD-PR|#$PR_NUM|g" docs/for-developers/frontend/v2-migration-matrix.md
git add docs/for-developers/frontend/v2-migration-matrix.md
git commit -m "docs(matrix): #953 backfill PR number"
git push
```

- [ ] **Step 4: Verify CI status**

```bash
gh pr checks --watch
```

Wait for all required checks to pass (frontend tests, lint, typecheck, axe-core, Chromatic). If anything fails, diagnose and fix.

- [ ] **Step 5: Merge (post-review)**

This step happens after human review (or self-merge if no review required). When merged:
- The branch will be auto-deleted (repo setting).
- The PR's `Closes #953` will auto-close the issue.

```bash
gh pr merge --squash --delete-branch
```

After merge, verify issue is closed:

```bash
gh issue view 953 --json state
# expect: {"state":"CLOSED"}
```

---

## Self-Review

**1. Spec coverage:**

| Spec AC | Plan task |
|---------|-----------|
| AC-1 (QuotaWidget wiring, variant routing) | Task 11 |
| AC-2 (4-step modal + StepIndicator + ESC + close) | Tasks 3, 8 |
| AC-3 (Step 3 sub-states + 2s latency + deterministic test) | Tasks 6, 8 |
| AC-4 (SoftWarningCredits two variants + localStorage) | Tasks 2, 9 |
| AC-5 (useSoftWarningDismissal hook) | Task 2 |
| AC-6 (Pack catalog + EUR formatter) | Task 1 |
| AC-7 (i18n label injection) | Tasks 4, 5, 6, 7, 8, 9, 11 (labels in every component) |
| AC-8 (a11y: focus trap, ARIA, prefers-reduced-motion) | Tasks 3-9, 15 (verification) |
| AC-9 (Playwright @ci happy path) | Task 12 |
| AC-10 (Chromatic stories 7 states) | Task 13 |
| AC-11 (matrix update with status=done + PR) | Tasks 14, 16 |
| AC-12 (bundle delta < 12 KB gzipped) | Tasks 14, 15 |

All 12 AC covered. ✅

**2. Placeholder scan:**
- No "TBD" or "TODO" in plan body (matrix placeholder `#TBD-PR` is an intentional fill-at-merge marker, not vague guidance).
- All code blocks are complete (full component bodies, full tests, full labels).
- No "similar to Task N" — every task lists its full code.
- No "add appropriate error handling" — error states are explicit (failed sub-state in Step 3, defensive `total <= 0` in hook, etc.).

**3. Type consistency:**
- `CheckoutPackId` defined in Task 1 (`'starter' | 'mid' | 'pro'`) → used in Tasks 5, 8, 11. ✅
- `Step3SubState` defined in Task 6 (`'filled' | 'loading' | 'failed'`) → used in Task 8. ✅
- `CheckoutLabels.step3.summary` defined as `(packName, credits, eur) => string` in Task 8 → CheckoutModal in Task 8 calls `labels.step3.summary(labels.step2.packNames[pack.id], pack.credits, priceEurString)`. ✅
- `Step3Labels` in Task 6 includes `summary: string` (plain string after CheckoutModal pre-resolves it) — note: Task 6 defines `summary: string`, Task 8's outer `CheckoutLabels.step3.summary` is the function form that CheckoutModal resolves before passing down. This is the Wave D.3 pattern (orchestrator pre-resolves ICU). ✅
- `Step4Labels.subtitle` similarly pre-resolved by CheckoutModal. ✅
- `SOFT_WARNING_STORAGE_KEY` exported from Task 2, used in test in Task 2. ✅
- `CHECKOUT_PACKS` exported from Task 1, imported in Tasks 5, 8. ✅

**4. Wiring sanity:**
- `Step3CheckoutForm` props: `subState`, `labels`, `onPay`, `onBack` (Task 6) ↔ called from `CheckoutModal` (Task 8) ✅
- `CheckoutModal` props: `open`, `initialStep`, `quota`, `userEmail`, `labels`, `onClose`, `onPurchaseSuccess`, `__testPaymentResult` (Task 8) ↔ used by `GamebookIndexView` (Task 11) ✅
- `useSoftWarningDismissal` returns `{ shouldShow, dismiss }` (Task 2) ↔ destructured in Task 11 ✅

All consistent. No gaps detected.

---

**Status**: ✅ Plan complete. Estimated effort 8h. 16 tasks, TDD throughout. Ready for execution.
