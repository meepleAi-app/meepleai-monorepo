import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import {
  CheckoutStepIndicator,
  type CheckoutStepIndicatorLabels,
} from '../../checkout/CheckoutStepIndicator';

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
    const step1 = document.querySelector(
      '[data-step-number="1"] [data-slot="checkout-step-circle"]'
    );
    const step2 = document.querySelector(
      '[data-step-number="2"] [data-slot="checkout-step-circle"]'
    );
    expect(step1?.textContent).toBe('✓');
    expect(step2?.textContent).toBe('✓');
  });

  it('pending steps render the number', () => {
    render(<CheckoutStepIndicator currentStep={2} labels={LABELS} />);
    const step4 = document.querySelector(
      '[data-step-number="4"] [data-slot="checkout-step-circle"]'
    );
    expect(step4?.textContent).toBe('4');
  });

  it('data-current-step matches prop', () => {
    render(<CheckoutStepIndicator currentStep={3} labels={LABELS} />);
    expect(
      document.querySelector('[data-slot="checkout-step-indicator"]')?.getAttribute('data-current-step')
    ).toBe('3');
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
