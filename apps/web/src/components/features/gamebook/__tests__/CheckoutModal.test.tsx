import { render, screen, act } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

import { CheckoutModal, type CheckoutLabels } from '../CheckoutModal';

const LABELS: CheckoutLabels = {
  modalTitle: (s) => `Checkout step ${s}`,
  stepIndicator: {
    step1: 'Quota',
    step2: 'Pacchetto',
    step3: 'Pagamento',
    step4: 'Fatto',
    ariaCurrent: (s) => `Passo ${s} di 4`,
  },
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
  afterEach(() => {
    vi.useRealTimers();
  });

  it('does not render dialog when open=false', () => {
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
    const user = userEvent.setup();
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
    const user = userEvent.setup();
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
    vi.useFakeTimers({ shouldAdvanceTime: true });
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
    expect(screen.getByRole('button', { name: /Elaborazione/i })).toBeInTheDocument();
    await act(async () => {
      vi.advanceTimersByTime(2100);
    });
    expect(screen.getByRole('heading', { name: 'Crediti aggiunti!' })).toBeInTheDocument();
    expect(onSuccess).toHaveBeenCalledWith('starter', 100);
  });

  it('Step 3 with __testPaymentResult=failed shows red banner', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
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
      vi.advanceTimersByTime(2100);
    });
    expect(screen.getByRole('alert')).toHaveTextContent(/Pagamento rifiutato/);
    expect(screen.getByRole('heading', { name: 'Pagamento' })).toBeInTheDocument();
  });

  it('Step 3 → Step 2 via back link', async () => {
    const user = userEvent.setup();
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
    vi.useFakeTimers({ shouldAdvanceTime: true });
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
      vi.advanceTimersByTime(2100);
    });
    await user.click(screen.getByRole('button', { name: /Torna al gioco/i }));
    expect(onClose).toHaveBeenCalledOnce();
  });

  it('ESC closes modal', async () => {
    const user = userEvent.setup();
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
