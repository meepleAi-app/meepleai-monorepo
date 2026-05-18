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
