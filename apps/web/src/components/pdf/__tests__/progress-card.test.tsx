/**
 * ProgressCard Component Tests (Issue #4210)
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';
import { ProgressCard } from '../progress-card';

vi.mock('@/hooks/usePdfProgress', () => ({
  usePdfProgress: vi.fn(() => ({
    status: { state: 'ready', progress: 100, timestamp: new Date().toISOString() },
    metrics: {
      documentId: 'doc-1',
      currentState: 'Completed',
      progressPercentage: 100,
      pageCount: 28,
      estimatedTimeRemaining: null,
      totalDuration: '00:09:00',
      stateDurations: {},
      retryCount: 0,
    },
    isPolling: false,
    isConnected: true,
    isLoading: false,
    error: null,
    metricsError: null,
    metricsLoading: false,
    reconnect: vi.fn(),
    refreshMetrics: vi.fn(),
  })),
}));

import { usePdfProgress } from '@/hooks/usePdfProgress';

const mockedUsePdfProgress = vi.mocked(usePdfProgress);

describe('ProgressCard', () => {
  it('should render title and progress ring', () => {
    render(<ProgressCard documentId="doc-123" title="Game Rules.pdf" />);

    expect(screen.getByText('Game Rules.pdf')).toBeInTheDocument();
    expect(screen.getByText('100%')).toBeInTheDocument();
  });

  it('should show status badge with correct label', () => {
    render(<ProgressCard documentId="doc-123" title="Test.pdf" />);

    expect(screen.getByText('Ready')).toBeInTheDocument();
  });

  it('should expand and collapse details', async () => {
    const user = userEvent.setup();
    render(<ProgressCard documentId="doc-123" title="Test.pdf" />);

    const expandBtn = screen.getByText('Show Details');
    await user.click(expandBtn);

    expect(screen.getByText('Pages')).toBeInTheDocument();
    expect(screen.getByText('28')).toBeInTheDocument();

    const collapseBtn = screen.getByText('Collapse');
    await user.click(collapseBtn);

    expect(screen.queryByText('Pages')).not.toBeInTheDocument();
  });

  it('should render expanded by default when defaultExpanded=true', () => {
    render(<ProgressCard documentId="doc-123" title="Test.pdf" defaultExpanded />);

    expect(screen.getByText('Pages')).toBeInTheDocument();
    expect(screen.getByText('Duration')).toBeInTheDocument(); // Shows "Duration" when state is 'ready'
  });

  it('should call onViewDetails when clicked', async () => {
    const user = userEvent.setup();
    const onViewDetails = vi.fn();

    render(<ProgressCard documentId="doc-123" title="Test.pdf" defaultExpanded onViewDetails={onViewDetails} />);

    const viewBtn = screen.getByText('View Details');
    await user.click(viewBtn);

    expect(onViewDetails).toHaveBeenCalled();
  });
});