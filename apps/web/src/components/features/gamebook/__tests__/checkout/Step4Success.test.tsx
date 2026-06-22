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
    expect(document.querySelector('[data-slot="confetti"]')?.getAttribute('aria-hidden')).toBe(
      'true'
    );
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
