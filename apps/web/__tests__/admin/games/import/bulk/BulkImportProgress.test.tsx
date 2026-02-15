/**
 * BulkImportProgress Component & useBulkImportProgress Hook Tests - Issue #4174
 *
 * Test coverage:
 * - useBulkImportProgress hook (SSE event handling)
 * - BulkImportProgress component rendering
 * - Status display (connecting, in_progress, complete, error, cancelled)
 * - Progress bar percentage calculation
 * - Stats grid display
 * - Current game indicator
 * - Recent successes display
 * - Cancel button
 * - Error alert display
 *
 * Target: >= 85% coverage
 */

import { render, screen, act, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

import {
  BulkImportProgress,
  useBulkImportProgress,
  type BulkImportProgressEvent,
} from '@/app/(authenticated)/admin/games/import/bulk/BulkImportProgress';

// ─── EventSource Mock ───────────────────────────────────────────

type EventSourceHandler = ((event: MessageEvent | Event) => void) | null;

class MockEventSource {
  static instances: MockEventSource[] = [];
  static CONNECTING = 0;
  static OPEN = 1;
  static CLOSED = 2;

  url: string;
  withCredentials: boolean;
  readyState: number;
  onopen: EventSourceHandler = null;
  onmessage: EventSourceHandler = null;
  onerror: EventSourceHandler = null;
  close = vi.fn(() => {
    this.readyState = MockEventSource.CLOSED;
  });

  constructor(url: string, init?: { withCredentials?: boolean }) {
    this.url = url;
    this.withCredentials = init?.withCredentials ?? false;
    this.readyState = MockEventSource.CONNECTING;
    MockEventSource.instances.push(this);
  }

  simulateOpen() {
    this.readyState = MockEventSource.OPEN;
    if (this.onopen) {
      this.onopen(new Event('open'));
    }
  }

  simulateMessage(data: BulkImportProgressEvent) {
    if (this.onmessage) {
      this.onmessage(new MessageEvent('message', { data: JSON.stringify(data) }));
    }
  }

  simulateError() {
    this.readyState = MockEventSource.CLOSED;
    if (this.onerror) {
      this.onerror(new Event('error'));
    }
  }
}

// ─── Setup ──────────────────────────────────────────────────────

beforeEach(() => {
  MockEventSource.instances = [];
  vi.stubGlobal('EventSource', MockEventSource);
  vi.stubGlobal('fetch', vi.fn(() => Promise.resolve({ ok: true })));
});

afterEach(() => {
  vi.restoreAllMocks();
});

function getLatestEventSource(): MockEventSource {
  return MockEventSource.instances[MockEventSource.instances.length - 1];
}

// ─── Hook Test Helper ───────────────────────────────────────────

function HookWrapper({ jobId }: { jobId: string }) {
  const state = useBulkImportProgress(jobId);
  return (
    <div>
      <span data-testid="hook-status">{state.status}</span>
      <span data-testid="hook-total">{state.total}</span>
      <span data-testid="hook-completed">{state.completed}</span>
      <span data-testid="hook-failed">{state.failed}</span>
      <span data-testid="hook-current-game">{state.currentGame}</span>
      <span data-testid="hook-message">{state.message}</span>
      <span data-testid="hook-recent">{state.recentSuccesses.join(',')}</span>
      <button data-testid="hook-cancel" onClick={state.cancel}>
        Cancel
      </button>
    </div>
  );
}

// ─── useBulkImportProgress Hook Tests ───────────────────────────

describe('useBulkImportProgress', () => {
  it('starts in connecting state', () => {
    render(<HookWrapper jobId="job-123" />);

    expect(screen.getByTestId('hook-status')).toHaveTextContent('connecting');
    expect(screen.getByTestId('hook-message')).toHaveTextContent('Connecting...');
  });

  it('creates EventSource with correct URL', () => {
    render(<HookWrapper jobId="job-123" />);

    const es = getLatestEventSource();
    expect(es.url).toBe('/api/v1/admin/games/bulk-import/job-123/progress');
    expect(es.withCredentials).toBe(true);
  });

  it('transitions to in_progress on open', () => {
    render(<HookWrapper jobId="job-123" />);

    act(() => {
      getLatestEventSource().simulateOpen();
    });

    expect(screen.getByTestId('hook-status')).toHaveTextContent('in_progress');
    expect(screen.getByTestId('hook-message')).toHaveTextContent('Import in progress...');
  });

  it('handles progress events', () => {
    render(<HookWrapper jobId="job-123" />);
    const es = getLatestEventSource();

    act(() => {
      es.simulateOpen();
      es.simulateMessage({
        type: 'progress',
        data: { total: 10, completed: 3, failed: 1, currentGame: 'Catan' },
      });
    });

    expect(screen.getByTestId('hook-total')).toHaveTextContent('10');
    expect(screen.getByTestId('hook-completed')).toHaveTextContent('3');
    expect(screen.getByTestId('hook-failed')).toHaveTextContent('1');
    expect(screen.getByTestId('hook-current-game')).toHaveTextContent('Catan');
    expect(screen.getByTestId('hook-message')).toHaveTextContent('Processing 3/10...');
  });

  it('tracks recent successes (max 5)', () => {
    render(<HookWrapper jobId="job-123" />);
    const es = getLatestEventSource();

    act(() => {
      es.simulateOpen();
      for (let i = 1; i <= 7; i++) {
        es.simulateMessage({
          type: 'progress',
          data: { total: 10, completed: i, failed: 0, currentGame: `Game ${i}` },
        });
      }
    });

    const recent = screen.getByTestId('hook-recent').textContent!;
    const games = recent.split(',');
    expect(games).toHaveLength(5);
    expect(games[0]).toBe('Game 7');
    expect(games[4]).toBe('Game 3');
  });

  it('handles complete event', () => {
    render(<HookWrapper jobId="job-123" />);
    const es = getLatestEventSource();

    act(() => {
      es.simulateOpen();
      es.simulateMessage({
        type: 'complete',
        data: { total: 5, completed: 4, failed: 1 },
      });
    });

    expect(screen.getByTestId('hook-status')).toHaveTextContent('complete');
    expect(screen.getByTestId('hook-message')).toHaveTextContent('Import complete!');
    expect(es.close).toHaveBeenCalled();
  });

  it('handles error event', () => {
    render(<HookWrapper jobId="job-123" />);
    const es = getLatestEventSource();

    act(() => {
      es.simulateOpen();
      es.simulateMessage({
        type: 'error',
        data: { total: 5, completed: 2, failed: 3, message: 'Server error occurred' },
      });
    });

    expect(screen.getByTestId('hook-status')).toHaveTextContent('error');
    expect(screen.getByTestId('hook-message')).toHaveTextContent('Server error occurred');
    expect(es.close).toHaveBeenCalled();
  });

  it('handles cancelled event', () => {
    render(<HookWrapper jobId="job-123" />);
    const es = getLatestEventSource();

    act(() => {
      es.simulateOpen();
      es.simulateMessage({
        type: 'cancelled',
        data: { total: 5, completed: 2, failed: 0 },
      });
    });

    expect(screen.getByTestId('hook-status')).toHaveTextContent('cancelled');
    expect(screen.getByTestId('hook-message')).toHaveTextContent('Import cancelled');
    expect(es.close).toHaveBeenCalled();
  });

  it('handles connection lost (EventSource error with CLOSED state)', () => {
    render(<HookWrapper jobId="job-123" />);
    const es = getLatestEventSource();

    act(() => {
      es.simulateOpen();
      es.simulateError();
    });

    expect(screen.getByTestId('hook-status')).toHaveTextContent('error');
    expect(screen.getByTestId('hook-message')).toHaveTextContent('Connection lost');
  });

  it('does not override complete/cancelled status on connection error', () => {
    render(<HookWrapper jobId="job-123" />);
    const es = getLatestEventSource();

    act(() => {
      es.simulateOpen();
      es.simulateMessage({
        type: 'complete',
        data: { total: 5, completed: 5, failed: 0 },
      });
    });

    // Simulate a late error after completion
    act(() => {
      es.readyState = MockEventSource.CLOSED;
      if (es.onerror) {
        es.onerror(new Event('error'));
      }
    });

    // Status should still be complete
    expect(screen.getByTestId('hook-status')).toHaveTextContent('complete');
  });

  it('cancel calls API and closes EventSource', async () => {
    const user = userEvent.setup();
    render(<HookWrapper jobId="job-123" />);
    const es = getLatestEventSource();

    act(() => {
      es.simulateOpen();
    });

    await user.click(screen.getByTestId('hook-cancel'));

    expect(es.close).toHaveBeenCalled();
    expect(fetch).toHaveBeenCalledWith(
      '/api/v1/admin/games/bulk-import/job-123/cancel',
      { method: 'POST' }
    );
    expect(screen.getByTestId('hook-status')).toHaveTextContent('cancelled');
  });

  it('handles malformed SSE data gracefully', () => {
    render(<HookWrapper jobId="job-123" />);
    const es = getLatestEventSource();

    act(() => {
      es.simulateOpen();
      // Send malformed JSON
      if (es.onmessage) {
        es.onmessage(new MessageEvent('message', { data: 'not-json' }));
      }
    });

    // Should still be in_progress (malformed message ignored)
    expect(screen.getByTestId('hook-status')).toHaveTextContent('in_progress');
  });

  it('does not create EventSource when jobId is empty', () => {
    render(<HookWrapper jobId="" />);

    expect(MockEventSource.instances).toHaveLength(0);
  });

  it('cleans up EventSource on unmount', () => {
    const { unmount } = render(<HookWrapper jobId="job-123" />);
    const es = getLatestEventSource();

    unmount();
    expect(es.close).toHaveBeenCalled();
  });
});

// ─── BulkImportProgress Component Tests ─────────────────────────

describe('BulkImportProgress', () => {
  const mockOnComplete = vi.fn();
  const mockOnCancel = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders the progress card', () => {
    render(<BulkImportProgress jobId="job-123" />);

    expect(screen.getByTestId('bulk-import-progress')).toBeInTheDocument();
  });

  it('shows "Connecting..." title initially', () => {
    render(<BulkImportProgress jobId="job-123" />);

    // "Connecting..." appears in both CardTitle and message span
    const matches = screen.getAllByText('Connecting...');
    expect(matches.length).toBeGreaterThanOrEqual(1);
  });

  it('applies custom className', () => {
    render(<BulkImportProgress jobId="job-123" className="my-class" />);

    const card = screen.getByTestId('bulk-import-progress');
    expect(card.className).toContain('my-class');
  });

  it('shows "Import In Progress" when connected', () => {
    render(<BulkImportProgress jobId="job-123" />);

    act(() => {
      getLatestEventSource().simulateOpen();
    });

    expect(screen.getByText('Import In Progress')).toBeInTheDocument();
  });

  it('shows cancel button during import', () => {
    render(<BulkImportProgress jobId="job-123" onCancel={mockOnCancel} />);

    act(() => {
      getLatestEventSource().simulateOpen();
    });

    expect(screen.getByTestId('cancel-import-button')).toBeInTheDocument();
  });

  it('does not show cancel button when not in_progress', () => {
    render(<BulkImportProgress jobId="job-123" />);

    // Still connecting
    expect(screen.queryByTestId('cancel-import-button')).not.toBeInTheDocument();
  });

  it('displays progress percentage', () => {
    render(<BulkImportProgress jobId="job-123" />);
    const es = getLatestEventSource();

    act(() => {
      es.simulateOpen();
      es.simulateMessage({
        type: 'progress',
        data: { total: 10, completed: 5, failed: 0 },
      });
    });

    expect(screen.getByTestId('progress-percentage')).toHaveTextContent('50%');
  });

  it('displays 0% when total is 0', () => {
    render(<BulkImportProgress jobId="job-123" />);

    expect(screen.getByTestId('progress-percentage')).toHaveTextContent('0%');
  });

  it('shows progress bar with aria-label', () => {
    render(<BulkImportProgress jobId="job-123" />);

    const progressBar = screen.getByTestId('progress-bar');
    expect(progressBar).toHaveAttribute('aria-label', 'Import progress: 0%');
  });

  it('displays stats grid with total, completed, failed', () => {
    render(<BulkImportProgress jobId="job-123" />);
    const es = getLatestEventSource();

    act(() => {
      es.simulateOpen();
      es.simulateMessage({
        type: 'progress',
        data: { total: 20, completed: 15, failed: 3 },
      });
    });

    const statsGrid = screen.getByTestId('stats-grid');
    expect(within(statsGrid).getByTestId('stat-total')).toHaveTextContent('20');
    expect(within(statsGrid).getByTestId('stat-completed')).toHaveTextContent('15');
    expect(within(statsGrid).getByTestId('stat-failed')).toHaveTextContent('3');
  });

  it('shows current game indicator during progress', () => {
    render(<BulkImportProgress jobId="job-123" />);
    const es = getLatestEventSource();

    act(() => {
      es.simulateOpen();
      es.simulateMessage({
        type: 'progress',
        data: { total: 10, completed: 3, failed: 0, currentGame: 'Wingspan' },
      });
    });

    const currentGame = screen.getByTestId('current-game');
    expect(currentGame).toHaveTextContent('Wingspan');
  });

  it('shows recent successes badges', () => {
    render(<BulkImportProgress jobId="job-123" />);
    const es = getLatestEventSource();

    act(() => {
      es.simulateOpen();
      es.simulateMessage({
        type: 'progress',
        data: { total: 10, completed: 1, failed: 0, currentGame: 'Catan' },
      });
      es.simulateMessage({
        type: 'progress',
        data: { total: 10, completed: 2, failed: 0, currentGame: 'Wingspan' },
      });
    });

    const recentSection = screen.getByTestId('recent-successes');
    expect(recentSection).toHaveTextContent('Wingspan');
    expect(recentSection).toHaveTextContent('Catan');
  });

  it('shows "Import Complete" on complete', () => {
    render(<BulkImportProgress jobId="job-123" />);
    const es = getLatestEventSource();

    act(() => {
      es.simulateOpen();
      es.simulateMessage({
        type: 'complete',
        data: { total: 5, completed: 5, failed: 0 },
      });
    });

    expect(screen.getByText('Import Complete')).toBeInTheDocument();
    expect(screen.getByTestId('status-complete')).toBeInTheDocument();
  });

  it('shows "Import Failed" on error with alert', () => {
    render(<BulkImportProgress jobId="job-123" />);
    const es = getLatestEventSource();

    act(() => {
      es.simulateOpen();
      es.simulateMessage({
        type: 'error',
        data: { total: 5, completed: 2, failed: 3, message: 'Fatal error' },
      });
    });

    expect(screen.getByText('Import Failed')).toBeInTheDocument();
    expect(screen.getByTestId('status-error')).toBeInTheDocument();
    expect(screen.getByTestId('error-alert')).toBeInTheDocument();
  });

  it('shows "Import Cancelled" on cancel', () => {
    render(<BulkImportProgress jobId="job-123" />);
    const es = getLatestEventSource();

    act(() => {
      es.simulateOpen();
      es.simulateMessage({
        type: 'cancelled',
        data: { total: 5, completed: 2, failed: 0 },
      });
    });

    expect(screen.getByText('Import Cancelled')).toBeInTheDocument();
    expect(screen.getByTestId('status-cancelled')).toBeInTheDocument();
  });

  it('calls onComplete when import completes', async () => {
    render(
      <BulkImportProgress jobId="job-123" onComplete={mockOnComplete} />
    );
    const es = getLatestEventSource();

    act(() => {
      es.simulateOpen();
      es.simulateMessage({
        type: 'complete',
        data: { total: 10, completed: 8, failed: 2 },
      });
    });

    await waitFor(() => {
      expect(mockOnComplete).toHaveBeenCalledWith({
        total: 10,
        completed: 8,
        failed: 2,
      });
    });
  });

  it('calls onCancel when cancel button is clicked', async () => {
    const user = userEvent.setup();
    render(
      <BulkImportProgress jobId="job-123" onCancel={mockOnCancel} />
    );
    const es = getLatestEventSource();

    act(() => {
      es.simulateOpen();
    });

    await user.click(screen.getByTestId('cancel-import-button'));

    await waitFor(() => {
      expect(mockOnCancel).toHaveBeenCalledTimes(1);
    });
  });

  it('shows connecting status icon', () => {
    render(<BulkImportProgress jobId="job-123" />);

    expect(screen.getByTestId('status-connecting')).toBeInTheDocument();
  });

  it('shows in-progress status icon', () => {
    render(<BulkImportProgress jobId="job-123" />);

    act(() => {
      getLatestEventSource().simulateOpen();
    });

    expect(screen.getByTestId('status-in-progress')).toBeInTheDocument();
  });
});
