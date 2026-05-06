/**
 * StepIndicator unit tests — SP6 Phase C.1.B Task B (Issue #789).
 *
 * Coverage:
 *   - data-slot identity + data-current-step
 *   - 3 step circles + connecting lines
 *   - active/done/pending visual states
 *   - aria-current="step" on active step
 *   - role="navigation" + aria-label
 *   - ICU plural label resolution (orchestrator pre-resolves)
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { StepIndicator } from '../StepIndicator';
import type { StepIndicatorProps } from '../StepIndicator';

const LABELS: StepIndicatorProps['labels'] = {
  step1: 'Selezione gioco',
  step2: 'Cattura foto',
  step3: 'Indicizzazione',
  ariaCurrent: 'Passo 2 di 3',
};

describe('StepIndicator', () => {
  it('renders data-slot="wizard-step-indicator"', () => {
    render(<StepIndicator currentStep={1} labels={LABELS} />);
    expect(document.querySelector('[data-slot="wizard-step-indicator"]')).not.toBeNull();
  });

  it('renders data-current-step attribute matching prop', () => {
    render(<StepIndicator currentStep={2} labels={LABELS} />);
    const root = document.querySelector('[data-slot="wizard-step-indicator"]');
    expect(root?.getAttribute('data-current-step')).toBe('2');
  });

  it('renders 3 step circles', () => {
    render(<StepIndicator currentStep={1} labels={LABELS} />);
    const circles = document.querySelectorAll('[data-slot="wizard-step-indicator-circle"]');
    expect(circles).toHaveLength(3);
  });

  it('renders all 3 step labels from labels prop', () => {
    render(<StepIndicator currentStep={1} labels={LABELS} />);
    expect(screen.getByText('Selezione gioco')).toBeTruthy();
    expect(screen.getByText('Cattura foto')).toBeTruthy();
    expect(screen.getByText('Indicizzazione')).toBeTruthy();
  });

  it('renders 2 connecting lines between 3 steps', () => {
    render(<StepIndicator currentStep={1} labels={LABELS} />);
    const lines = document.querySelectorAll('[data-slot="wizard-step-indicator-line"]');
    expect(lines).toHaveLength(2);
  });

  it('marks active step with aria-current="step"', () => {
    render(<StepIndicator currentStep={2} labels={LABELS} />);
    const activeStep = document.querySelector('[aria-current="step"]');
    expect(activeStep).not.toBeNull();
    expect(activeStep?.getAttribute('data-step-number')).toBe('2');
  });

  it('only one step has aria-current="step"', () => {
    render(<StepIndicator currentStep={3} labels={LABELS} />);
    const currentSteps = document.querySelectorAll('[aria-current="step"]');
    expect(currentSteps).toHaveLength(1);
  });

  it('passes ariaCurrent label to active step (visually-hidden text)', () => {
    render(<StepIndicator currentStep={2} labels={LABELS} />);
    expect(screen.getByText('Passo 2 di 3')).toBeTruthy();
  });

  it('marks completed steps with data-state="done"', () => {
    render(<StepIndicator currentStep={3} labels={LABELS} />);
    const doneSteps = document.querySelectorAll('[data-state="done"]');
    // currentStep=3 → steps 1+2 done = 2 done steps
    expect(doneSteps).toHaveLength(2);
  });

  it('marks active step with data-state="active"', () => {
    render(<StepIndicator currentStep={2} labels={LABELS} />);
    const activeSteps = document.querySelectorAll('[data-state="active"]');
    expect(activeSteps).toHaveLength(1);
  });

  it('marks pending steps with data-state="pending"', () => {
    render(<StepIndicator currentStep={1} labels={LABELS} />);
    const pendingSteps = document.querySelectorAll('[data-state="pending"]');
    // currentStep=1 → steps 2+3 pending = 2 pending steps
    expect(pendingSteps).toHaveLength(2);
  });

  it('shows checkmark glyph on done steps', () => {
    render(<StepIndicator currentStep={3} labels={LABELS} />);
    const checkmarks = document.querySelectorAll('[data-state="done"]');
    checkmarks.forEach(el => {
      expect(el.textContent).toContain('✓');
    });
  });

  it('shows step number on active step', () => {
    render(<StepIndicator currentStep={2} labels={LABELS} />);
    const active = document.querySelector('[data-state="active"]');
    expect(active?.textContent).toContain('2');
  });

  it('shows step number on pending steps', () => {
    render(<StepIndicator currentStep={1} labels={LABELS} />);
    const pending = document.querySelectorAll('[data-state="pending"]');
    expect(pending[0].textContent).toContain('2');
    expect(pending[1].textContent).toContain('3');
  });

  it('exposes role="navigation" with aria-label', () => {
    render(<StepIndicator currentStep={1} labels={LABELS} />);
    const nav = screen.getByRole('navigation');
    expect(nav.getAttribute('aria-label')).toBeTruthy();
  });

  it('applies custom className to root', () => {
    render(<StepIndicator currentStep={1} labels={LABELS} className="extra-class" />);
    const root = document.querySelector('[data-slot="wizard-step-indicator"]');
    expect(root?.classList.contains('extra-class')).toBe(true);
  });

  it('handles all 3 valid currentStep values', () => {
    for (const step of [1, 2, 3] as const) {
      const { unmount } = render(<StepIndicator currentStep={step} labels={LABELS} />);
      const root = document.querySelector('[data-slot="wizard-step-indicator"]');
      expect(root?.getAttribute('data-current-step')).toBe(String(step));
      unmount();
    }
  });
});
