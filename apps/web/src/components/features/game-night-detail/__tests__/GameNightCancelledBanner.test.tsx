/**
 * Tests for GameNightCancelledBanner (Issue #951 commit 2b).
 */

import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import {
  GameNightCancelledBanner,
  type GameNightCancelledBannerLabels,
} from '../GameNightCancelledBanner';

const LABELS: GameNightCancelledBannerLabels = {
  title: 'Serata cancellata',
  meta: 'Cancellata da Marco · 2 giorni fa',
  reasonLabel: 'Motivo:',
  ctaLabel: 'Crea nuova serata',
};

describe('GameNightCancelledBanner', () => {
  it('renders as an alert landmark with the title and meta', () => {
    render(<GameNightCancelledBanner labels={LABELS} />);
    const alert = screen.getByRole('alert');
    expect(within(alert).getByText('Serata cancellata')).toBeInTheDocument();
    expect(within(alert).getByText('Cancellata da Marco · 2 giorni fa')).toBeInTheDocument();
  });

  it('renders the reason block when reason is provided', () => {
    render(
      <GameNightCancelledBanner labels={LABELS} reason="Mia figlia ha la febbre, riprogrammiamo." />
    );
    expect(screen.getByText('Motivo:')).toBeInTheDocument();
    expect(screen.getByText(/Mia figlia ha la febbre/)).toBeInTheDocument();
  });

  it('omits the reason block when reason is null', () => {
    render(<GameNightCancelledBanner labels={LABELS} reason={null} />);
    expect(screen.queryByText('Motivo:')).not.toBeInTheDocument();
  });

  it('omits the reason block when reason is empty/whitespace', () => {
    render(<GameNightCancelledBanner labels={LABELS} reason="   " />);
    expect(screen.queryByText('Motivo:')).not.toBeInTheDocument();
  });

  it('renders the CTA when onCreateNew is provided', async () => {
    const user = userEvent.setup();
    const onCreateNew = vi.fn();
    render(<GameNightCancelledBanner labels={LABELS} onCreateNew={onCreateNew} />);

    const btn = screen.getByRole('button', { name: /Crea nuova serata/ });
    await user.click(btn);
    expect(onCreateNew).toHaveBeenCalledOnce();
  });

  it('omits the CTA when onCreateNew is not provided', () => {
    render(<GameNightCancelledBanner labels={LABELS} />);
    expect(screen.queryByRole('button', { name: /Crea nuova serata/ })).not.toBeInTheDocument();
  });

  it('forwards className for layout overrides', () => {
    render(<GameNightCancelledBanner labels={LABELS} className="custom-mt" />);
    const alert = screen.getByTestId('game-night-cancelled-banner');
    expect(alert.className).toContain('custom-mt');
  });

  it('uses destructive entity tokens (no hardcoded color)', () => {
    render(<GameNightCancelledBanner labels={LABELS} />);
    const alert = screen.getByTestId('game-night-cancelled-banner');
    expect(alert.className).toContain('border-destructive');
    expect(alert.className).toContain('bg-destructive');
  });
});
