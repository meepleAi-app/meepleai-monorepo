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
