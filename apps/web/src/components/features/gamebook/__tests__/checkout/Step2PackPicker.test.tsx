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
    render(
      <Step2PackPicker selectedId="starter" labels={LABELS} onSelect={vi.fn()} onContinue={vi.fn()} />
    );
    expect(document.querySelectorAll('[data-slot="pack-card"]')).toHaveLength(3);
  });

  it('starter pack has popular badge', () => {
    render(
      <Step2PackPicker selectedId="starter" labels={LABELS} onSelect={vi.fn()} onContinue={vi.fn()} />
    );
    expect(screen.getByText('Più popolare')).toBeInTheDocument();
  });

  it('pro pack has save badge', () => {
    render(
      <Step2PackPicker selectedId="starter" labels={LABELS} onSelect={vi.fn()} onContinue={vi.fn()} />
    );
    expect(screen.getByText('Risparmia 30%')).toBeInTheDocument();
  });

  it('default selection is starter; renders €5 in total', () => {
    render(
      <Step2PackPicker selectedId="starter" labels={LABELS} onSelect={vi.fn()} onContinue={vi.fn()} />
    );
    expect(screen.getByTestId('checkout-total')).toHaveTextContent(/5/);
  });

  it('mid selection updates total to €20', () => {
    render(
      <Step2PackPicker selectedId="mid" labels={LABELS} onSelect={vi.fn()} onContinue={vi.fn()} />
    );
    expect(screen.getByTestId('checkout-total')).toHaveTextContent(/20/);
  });

  it('fires onSelect with packId on radio change', async () => {
    const user = userEvent.setup();
    const onSelect = vi.fn();
    render(
      <Step2PackPicker selectedId="starter" labels={LABELS} onSelect={onSelect} onContinue={vi.fn()} />
    );
    await user.click(screen.getByLabelText(/Pacchetto Pro/i));
    expect(onSelect).toHaveBeenCalledWith('pro');
  });

  it('continue button fires onContinue', async () => {
    const user = userEvent.setup();
    const onContinue = vi.fn();
    render(
      <Step2PackPicker selectedId="starter" labels={LABELS} onSelect={vi.fn()} onContinue={onContinue} />
    );
    await user.click(screen.getByRole('button', { name: LABELS.continueCta }));
    expect(onContinue).toHaveBeenCalledOnce();
  });

  it('radiogroup role for accessibility', () => {
    render(
      <Step2PackPicker selectedId="starter" labels={LABELS} onSelect={vi.fn()} onContinue={vi.fn()} />
    );
    expect(screen.getByRole('radiogroup', { name: /pacchetto/i })).toBeInTheDocument();
  });
});
