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
