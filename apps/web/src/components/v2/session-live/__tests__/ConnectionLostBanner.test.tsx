/**
 * ConnectionLostBanner unit tests — Wave D.2 Interactions sub-PR (Issue #750)
 *
 * Coverage:
 * - Render shape (data-slot, data-kind)
 * - role="status" for reconnecting + degraded-polling
 * - role="alert" for failed
 * - aria-live="polite" for reconnecting + degraded-polling
 * - Message text per kind
 * - Retry count shown for reconnecting
 * - Manual retry button for degraded-polling and failed
 * - onManualRetry fires on button click
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import type {
  ConnectionLostBannerLabels,
  ConnectionLostBannerProps,
} from '../ConnectionLostBanner';
import { ConnectionLostBanner } from '../ConnectionLostBanner';

// ─── Fixtures ─────────────────────────────────────────────────────────────────

const LABELS: ConnectionLostBannerLabels = {
  retryCountResolved: 'Tentativo 2/5',
  reconnecting: 'Riconnessione in corso...',
  degradedPolling: 'Aggiornamenti ogni 5s',
  failed: 'Connessione persa',
  manualRetryLabel: 'Riprova',
};

function renderBanner(overrides: Partial<ConnectionLostBannerProps> = {}) {
  const onManualRetry = vi.fn();
  const props: ConnectionLostBannerProps = {
    kind: 'reconnecting',
    labels: LABELS,
    onManualRetry,
    ...overrides,
  };
  const result = render(<ConnectionLostBanner {...props} />);
  return { ...result, onManualRetry };
}

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('ConnectionLostBanner — render shape', () => {
  it('renders data-slot="connection-lost-banner"', () => {
    renderBanner();
    expect(document.querySelector('[data-slot="connection-lost-banner"]')).toBeInTheDocument();
  });

  it('renders data-kind attribute', () => {
    renderBanner({ kind: 'failed' });
    expect(document.querySelector('[data-kind="failed"]')).toBeInTheDocument();
  });
});

describe('ConnectionLostBanner — role and aria-live', () => {
  it('reconnecting: role="status" aria-live="polite"', () => {
    renderBanner({ kind: 'reconnecting' });
    const banner = screen.getByRole('status');
    expect(banner).toHaveAttribute('aria-live', 'polite');
  });

  it('degraded-polling: role="status" aria-live="polite"', () => {
    renderBanner({ kind: 'degraded-polling' });
    const banner = screen.getByRole('status');
    expect(banner).toHaveAttribute('aria-live', 'polite');
  });

  it('failed: role="alert" (assertive)', () => {
    renderBanner({ kind: 'failed' });
    expect(screen.getByRole('alert')).toBeInTheDocument();
  });
});

describe('ConnectionLostBanner — message text per kind', () => {
  it('shows reconnecting message', () => {
    renderBanner({ kind: 'reconnecting' });
    expect(screen.getByText('Riconnessione in corso...')).toBeInTheDocument();
  });

  it('shows degraded-polling message', () => {
    renderBanner({ kind: 'degraded-polling' });
    expect(screen.getByText('Aggiornamenti ogni 5s')).toBeInTheDocument();
  });

  it('shows failed message', () => {
    renderBanner({ kind: 'failed' });
    expect(screen.getByText('Connessione persa')).toBeInTheDocument();
  });
});

describe('ConnectionLostBanner — retry count', () => {
  it('shows retryCountResolved for reconnecting with retryCount', () => {
    renderBanner({ kind: 'reconnecting', retryCount: 2 });
    expect(screen.getByText('Tentativo 2/5')).toBeInTheDocument();
  });

  it('does not show retry count for degraded-polling', () => {
    renderBanner({ kind: 'degraded-polling', retryCount: 2 });
    expect(screen.queryByText('Tentativo 2/5')).not.toBeInTheDocument();
  });
});

describe('ConnectionLostBanner — manual retry button', () => {
  it('shows retry button for degraded-polling when onManualRetry provided', () => {
    renderBanner({ kind: 'degraded-polling' });
    expect(screen.getByRole('button', { name: 'Riprova' })).toBeInTheDocument();
  });

  it('shows retry button for failed when onManualRetry provided', () => {
    renderBanner({ kind: 'failed' });
    expect(screen.getByRole('button', { name: 'Riprova' })).toBeInTheDocument();
  });

  it('does not show retry button for reconnecting', () => {
    renderBanner({ kind: 'reconnecting' });
    expect(screen.queryByRole('button', { name: 'Riprova' })).not.toBeInTheDocument();
  });

  it('calls onManualRetry on button click (degraded-polling)', async () => {
    const user = userEvent.setup();
    const { onManualRetry } = renderBanner({ kind: 'degraded-polling' });

    await user.click(screen.getByRole('button', { name: 'Riprova' }));
    expect(onManualRetry).toHaveBeenCalledOnce();
  });

  it('calls onManualRetry on button click (failed)', async () => {
    const user = userEvent.setup();
    const { onManualRetry } = renderBanner({ kind: 'failed' });

    await user.click(screen.getByRole('button', { name: 'Riprova' }));
    expect(onManualRetry).toHaveBeenCalledOnce();
  });
});
