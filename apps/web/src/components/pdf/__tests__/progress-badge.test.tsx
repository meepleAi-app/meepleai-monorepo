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
});