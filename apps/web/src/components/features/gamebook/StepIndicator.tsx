/**
 * StepIndicator — SP6 Phase C.1.B v2 component (Issue #789).
 *
 * 3-step horizontal sticky bar for the /gamebook/upload wizard. Pure
 * component (Wave D.3 pattern): all i18n labels are resolved by orchestrator
 * and injected via `labels` prop. ICU plural / interpolation (e.g. `Passo {n}
 * di 3`) is pre-resolved upstream.
 *
 * Mapped from `admin-mockups/design_files/sp6-libro-game-photo-upload.jsx`
 * (StepIndicator). Visual identity: game entity orange (HSL 25, 95%, 39%) for
 * active+done circles, slate-400 for pending. WCAG AA SC 1.4.3 ≥ 4.5:1.
 *
 * a11y:
 *   - `<nav role="navigation" aria-label="Wizard progress">`
 *   - Active step has `aria-current="step"` + visually-hidden text from
 *     `labels.ariaCurrent` (orchestrator pre-resolves "Passo {n} di 3").
 *   - Done steps render ✓ glyph (decorative, color is supplemental).
 *   - Each step exposes `data-state={done|active|pending}` for E2E.
 *   - Connecting lines have `aria-hidden="true"` (decorative).
 *
 * data-slot="wizard-step-indicator" + `data-current-step={currentStep}` for
 * E2E selectors and a11y exclusion scoping.
 */

'use client';

import type { ReactElement } from 'react';

import clsx from 'clsx';

export interface StepIndicatorLabels {
  /** Step 1 label (e.g. "Selezione gioco"). */
  readonly step1: string;
  /** Step 2 label (e.g. "Cattura foto"). */
  readonly step2: string;
  /** Step 3 label (e.g. "Indicizzazione"). */
  readonly step3: string;
  /** Pre-resolved screenreader text for active step (e.g. "Passo 2 di 3"). */
  readonly ariaCurrent: string;
}

export interface StepIndicatorProps {
  /** Active step (1, 2, or 3). */
  readonly currentStep: 1 | 2 | 3;
  readonly labels: StepIndicatorLabels;
  readonly className?: string;
}

// game entity colours replaced with Tailwind entity-token classes (P2 #807 Task 6+7+8)

type StepState = 'done' | 'active' | 'pending';

function deriveState(stepNumber: 1 | 2 | 3, currentStep: 1 | 2 | 3): StepState {
  if (stepNumber < currentStep) return 'done';
  if (stepNumber === currentStep) return 'active';
  return 'pending';
}

interface StepCircleProps {
  readonly stepNumber: 1 | 2 | 3;
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
        data-slot="wizard-step-indicator-circle"
        className={clsx(
          'flex h-8 w-8 items-center justify-center rounded-full border-2 text-sm font-bold tabular-nums',
          'transition-colors motion-reduce:transition-none',
          isDone || isActive
            ? 'border-entity-game bg-entity-game text-white'
            : 'border-border bg-transparent text-muted-foreground'
        )}
      >
        {isDone ? '✓' : stepNumber}
      </span>
      <span
        className={clsx('text-xs font-semibold', isActive ? 'text-foreground' : 'text-muted-foreground')}
      >
        {label}
      </span>
      {isActive && <span className="sr-only">{ariaCurrent}</span>}
    </div>
  );
}

interface StepLineProps {
  readonly highlighted: boolean;
}

function StepLine({ highlighted }: StepLineProps): ReactElement {
  return (
    <span
      data-slot="wizard-step-indicator-line"
      aria-hidden="true"
      className={clsx(
        'h-0.5 flex-1 self-start mt-4 transition-colors motion-reduce:transition-none',
        highlighted ? 'bg-entity-game/80' : 'bg-slate-200'
      )}
    />
  );
}

export function StepIndicator({
  currentStep,
  labels,
  className,
}: StepIndicatorProps): ReactElement {
  const stepLabels: Record<1 | 2 | 3, string> = {
    1: labels.step1,
    2: labels.step2,
    3: labels.step3,
  };

  return (
    <nav
      data-slot="wizard-step-indicator"
      data-current-step={currentStep}
      aria-label="Wizard progress"
      role="navigation"
      className={clsx(
        'sticky top-0 z-10 flex items-start gap-2 border-b border-border bg-background px-4 py-3 sm:px-6',
        className
      )}
    >
      <div className="mx-auto flex w-full max-w-2xl items-start gap-2 sm:gap-4">
        <StepCircle
          stepNumber={1}
          state={deriveState(1, currentStep)}
          label={stepLabels[1]}
          ariaCurrent={labels.ariaCurrent}
        />
        <StepLine highlighted={currentStep > 1} />
        <StepCircle
          stepNumber={2}
          state={deriveState(2, currentStep)}
          label={stepLabels[2]}
          ariaCurrent={labels.ariaCurrent}
        />
        <StepLine highlighted={currentStep > 2} />
        <StepCircle
          stepNumber={3}
          state={deriveState(3, currentStep)}
          label={stepLabels[3]}
          ariaCurrent={labels.ariaCurrent}
        />
      </div>
    </nav>
  );
}
