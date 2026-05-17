/**
 * OfflineBanner unit tests — SP6 Phase C.2.B Task B (Issue #789).
 *
 * Coverage:
 *   - data-slot + data-attempt
 *   - role="alert" + aria-live="polite"
 *   - Title + countdown text rendered from labels (orchestrator pre-resolves)
 *   - role="progressbar" + aria-valuenow + aria-valuemax + aria-valuetext
 *   - Progress bar width calculation (totalElapsed / budgetTotal)
 *   - Progress clamps to budget total (overflow safety)
 *   - Retry button calls onRetryNow
 *   - Cancel button calls onCancel
 *   - Reduced motion: progress bar transition-none fallback
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { OfflineBanner, type OfflineBannerLabels, type OfflineBannerProps } from '../OfflineBanner';

const LABELS: OfflineBannerLabels = {
  title: 'Connessione persa',
  retryIn: 'Tentativo 2/5 in 4s...',
  retryNow: 'Riprova ora',
  cancel: 'Annulla',
  progressAria: '7s di 31s',
  retryNowAria: 'Riprova ora il caricamento',
  cancelAria: 'Annulla il caricamento',
};

function renderBanner(overrides: Partial<OfflineBannerProps> = {}) {
  const onRetryNow = vi.fn();
  const onCancel = vi.fn();
  const props: OfflineBannerProps = {
    attempt: 2,
    nextRetryInSeconds: 4,
    totalElapsedSeconds: 7,
    budgetTotalSeconds: 31,
    onRetryNow,
    onCancel,
    labels: LABELS,
    ...overrides,
  };
  const result = render(<OfflineBanner {...props} />);
  return { ...result, onRetryNow, onCancel };
}

describe('OfflineBanner — render shape', () => {
  it('renders data-slot="offline-banner"', () => {
    renderBanner();
    expect(document.querySelector('[data-slot="offline-banner"]')).not.toBeNull();
  });

  it('renders data-attempt attribute matching prop', () => {
    renderBanner({ attempt: 3 });
    const root = document.querySelector('[data-slot="offline-banner"]');
    expect(root?.getAttribute('data-attempt')).toBe('3');
  });

  it('renders title from labels.title', () => {
    renderBanner();
    expect(screen.getByText('Connessione persa')).toBeInTheDocument();
  });

  it('renders countdown text from labels.retryIn', () => {
    renderBanner();
    expect(screen.getByText('Tentativo 2/5 in 4s...')).toBeInTheDocument();
  });

  it('exposes role="alert" + aria-live="polite"', () => {
    renderBanner();
    const banner = screen.getByRole('alert');
    expect(banner.getAttribute('aria-live')).toBe('polite');
  });
});

describe('OfflineBanner — progress bar', () => {
  it('renders progressbar role with valuenow + valuemax', () => {
    renderBanner();
    const bar = screen.getByRole('progressbar');
    expect(bar.getAttribute('aria-valuenow')).toBe('7');
    expect(bar.getAttribute('aria-valuemax')).toBe('31');
  });

  it('exposes aria-valuetext from labels.progressAria', () => {
    renderBanner();
    const bar = screen.getByRole('progressbar');
    expect(bar.getAttribute('aria-valuetext')).toBe('7s di 31s');
  });

  it('progress bar fill width matches elapsed / budget %', () => {
    renderBanner({ totalElapsedSeconds: 15, budgetTotalSeconds: 31 });
    const bar = document.querySelector('[data-slot="offline-banner-progress"]');
    const fill = bar?.querySelector('div') as HTMLElement;
    // 15/31 ≈ 48%
    expect(fill.style.width).toBe('48%');
  });

  it('clamps elapsed > budget to budget total (no overflow)', () => {
    renderBanner({ totalElapsedSeconds: 50, budgetTotalSeconds: 31 });
    const bar = screen.getByRole('progressbar');
    expect(bar.getAttribute('aria-valuenow')).toBe('31');
  });

  it('renders 0% width when elapsed=0', () => {
    renderBanner({ totalElapsedSeconds: 0 });
    const bar = document.querySelector('[data-slot="offline-banner-progress"]');
    const fill = bar?.querySelector('div') as HTMLElement;
    expect(fill.style.width).toBe('0%');
  });

  it('renders 100% width when elapsed equals budget', () => {
    renderBanner({ totalElapsedSeconds: 31 });
    const bar = document.querySelector('[data-slot="offline-banner-progress"]');
    const fill = bar?.querySelector('div') as HTMLElement;
    expect(fill.style.width).toBe('100%');
  });

  it('progress fill has motion-reduce:transition-none for reduced motion', () => {
    renderBanner();
    const bar = document.querySelector('[data-slot="offline-banner-progress"]');
    const fill = bar?.querySelector('div');
    expect(fill?.className).toContain('motion-reduce:transition-none');
  });
});

describe('OfflineBanner — interactive buttons', () => {
  it('renders retry button with aria-label from labels.retryNowAria', () => {
    renderBanner();
    expect(screen.getByRole('button', { name: 'Riprova ora il caricamento' })).toBeInTheDocument();
  });

  it('renders cancel button with aria-label from labels.cancelAria', () => {
    renderBanner();
    expect(screen.getByRole('button', { name: 'Annulla il caricamento' })).toBeInTheDocument();
  });

  it('calls onRetryNow when retry button clicked', async () => {
    const user = userEvent.setup();
    const { onRetryNow } = renderBanner();

    await user.click(screen.getByRole('button', { name: 'Riprova ora il caricamento' }));
    expect(onRetryNow).toHaveBeenCalledOnce();
  });

  it('calls onCancel when cancel button clicked', async () => {
    const user = userEvent.setup();
    const { onCancel } = renderBanner();

    await user.click(screen.getByRole('button', { name: 'Annulla il caricamento' }));
    expect(onCancel).toHaveBeenCalledOnce();
  });

  it('retry and cancel buttons are independent', async () => {
    const user = userEvent.setup();
    const { onRetryNow, onCancel } = renderBanner();

    await user.click(screen.getByRole('button', { name: 'Riprova ora il caricamento' }));
    expect(onRetryNow).toHaveBeenCalledOnce();
    expect(onCancel).not.toHaveBeenCalled();

    await user.click(screen.getByRole('button', { name: 'Annulla il caricamento' }));
    expect(onCancel).toHaveBeenCalledOnce();
  });
});

describe('OfflineBanner — defensive', () => {
  it('handles budgetTotalSeconds=0 without divide-by-zero', () => {
    renderBanner({ totalElapsedSeconds: 5, budgetTotalSeconds: 0 });
    const bar = document.querySelector('[data-slot="offline-banner-progress"]');
    const fill = bar?.querySelector('div') as HTMLElement;
    // budgetTotal coerced to 1 → elapsed clamped → 100%
    expect(fill.style.width).toBe('100%');
  });

  it('renders all 5 attempt values', () => {
    for (const attempt of [1, 2, 3, 4, 5]) {
      const { unmount } = renderBanner({ attempt });
      const root = document.querySelector('[data-slot="offline-banner"]');
      expect(root?.getAttribute('data-attempt')).toBe(String(attempt));
      unmount();
    }
  });
});
