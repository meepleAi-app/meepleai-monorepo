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
          'whitespace-nowrap text-[11px] font-semibold',
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
        'mt-3 h-0.5 min-w-2 flex-1 self-start rounded-full transition-colors motion-reduce:transition-none',
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
      <StepLine highlighted={currentStep > 3} />
      <StepCircle
        stepNumber={4}
        state={deriveState(4, currentStep)}
        label={stepLabels[4]}
        ariaCurrent={labels.ariaCurrent}
      />
    </nav>
  );
}
