/**
 * ProgressBadge Component Tests (Issue #4210)
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';
import { ProgressBadge } from '../progress-badge';

vi.mock('@/hooks/usePdfProgress', () => ({
  usePdfProgress: vi.fn(() => ({
    status: null,
    metrics: null,
    isConnected: false,
    isPolling: false,
    isLoading: false,
    error: null,
    metricsError: null,
    metricsLoading: false,
    reconnect: vi.fn(),
    refreshMetrics: vi.fn(),
  })),
}));

describe('ProgressBadge', () => {
  it('should render with static state', () => {
    render(<ProgressBadge state="uploading" progress={25} />);

    const badge = screen.getByRole('status');
    expect(badge).toHaveAttribute('aria-label', 'PDF status: Uploading');
  });

  it('should render ready state with green color', () => {
    render(<ProgressBadge state="ready" progress={100} />);

    const badge = screen.getByRole('status');
    expect(badge).toHaveAttribute('aria-label', 'PDF status: Ready');
  });

  it('should render failed state', () => {
    render(<ProgressBadge state="failed" progress={40} />);

    const badge = screen.getByRole('status');
    expect(badge).toHaveAttribute('aria-label', 'PDF status: Failed');
  });

  it('should have pulse animation for processing states', () => {
    const { container } = render(<ProgressBadge state="embedding" progress={75} />);

    const badge = container.querySelector('.animate-pulse');
    expect(badge).toBeInTheDocument();
  });

  /**
   * #4360: `meeple-info-card` passa `doc.processingState`, cioè lo stato in PascalCase del
   * backend (`Failed`, `Extracting`, …). Colore ed etichetta funzionavano per caso, ma icona,
   * pulsazione e testo del tooltip confrontavano chiavi minuscole: un PDF `Failed` mostrava la
   * rotella e «Progress: 0%» finché l'SSE non mandava lo stato minuscolo.
   */
  describe('stato in PascalCase, come lo manda il backend (#4360)', () => {
    const STATES = [
      'Pending',
      'Uploading',
      'Extracting',
      'Chunking',
      'Embedding',
      'Indexing',
      'Ready',
      'Failed',
    ] as const;

    it.each(STATES)('%s si vede come la sua forma minuscola', state => {
      const pascal = render(<ProgressBadge state={state} progress={40} />);
      const pascalHtml = pascal.container.innerHTML;
      pascal.unmount();

      const lower = render(<ProgressBadge state={state.toLowerCase()} progress={40} />);

      expect(pascalHtml).toBe(lower.container.innerHTML);
    });

    it('un PDF Failed non mostra la rotella', () => {
      const { container } = render(<ProgressBadge state="Failed" progress={0} />);

      expect(container.querySelector('.animate-spin')).not.toBeInTheDocument();
    });

    it('un PDF Ready non mostra la rotella', () => {
      const { container } = render(<ProgressBadge state="Ready" progress={100} />);

      expect(container.querySelector('.animate-spin')).not.toBeInTheDocument();
    });

    it('il tooltip di un PDF Failed dice che è fallito, non quanto manca', async () => {
      const user = userEvent.setup();
      render(<ProgressBadge state="Failed" progress={0} />);

      await user.hover(screen.getByRole('status'));

      expect((await screen.findAllByText('Processing failed')).length).toBeGreaterThan(0);
      expect(screen.queryByText(/Progress:/)).not.toBeInTheDocument();
    });
  });
});
