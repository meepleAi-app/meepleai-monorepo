/**
 * PdfProcessingNotifier Tests
 *
 * Validates SSE-driven toast notifications for PDF processing events.
 *
 * Tests:
 * - Shows success toast on JobCompleted event
 * - Shows error toast with "Riprova" on JobFailed event
 * - Handles events without game info gracefully (no "per null")
 * - Cleans up EventSource on unmount
 */

import { render, act } from '@testing-library/react';
import { vi, describe, it, expect, beforeEach, afterEach } from 'vitest';

// ─── Hoisted mocks ──────────────────────────────────────────────────────────

const { mockPush, mockToastSuccess, mockToastError, mockRetryJob } = vi.hoisted(() => ({
  mockPush: vi.fn(),
  mockToastSuccess: vi.fn(),
  mockToastError: vi.fn(),
  mockRetryJob: vi.fn().mockResolvedValue(undefined),
}));

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: mockPush }),
}));

vi.mock('sonner', () => ({
  toast: {
    success: mockToastSuccess,
    error: mockToastError,
  },
}));

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      retryJob: mockRetryJob,
    },
  },
}));

// ─── EventSource mock ───────────────────────────────────────────────────────

type EventHandler = (event: MessageEvent) => void;

class MockEventSource {
  static instances: MockEventSource[] = [];

  url: string;
  onmessage: ((event: MessageEvent) => void) | null = null;
  onopen: (() => void) | null = null;
  onerror: (() => void) | null = null;
  closed = false;

  private namedListeners = new Map<string, EventHandler[]>();

  constructor(url: string) {
    this.url = url;
    MockEventSource.instances.push(this);
  }

  addEventListener(type: string, handler: EventHandler) {
    const existing = this.namedListeners.get(type) ?? [];
    existing.push(handler);
    this.namedListeners.set(type, existing);
  }

  removeEventListener(type: string, handler: EventHandler) {
    const existing = this.namedListeners.get(type) ?? [];
    this.namedListeners.set(
      type,
      existing.filter(h => h !== handler)
    );
  }

  close() {
    this.closed = true;
  }

  // Test helpers

  /** Simulate a named SSE event (event: JobCompleted / event: JobFailed) */
  emitNamed(eventType: string, data: object) {
    const handlers = this.namedListeners.get(eventType) ?? [];
    const messageEvent = new MessageEvent(eventType, {
      data: JSON.stringify(data),
    });
    for (const handler of handlers) {
      handler(messageEvent);
    }
  }

  /** Simulate an unnamed SSE event (generic onmessage) */
  emitMessage(data: object) {
    if (this.onmessage) {
      const messageEvent = new MessageEvent('message', {
        data: JSON.stringify(data),
      });
      this.onmessage(messageEvent);
    }
  }
}

// ─── Test setup ─────────────────────────────────────────────────────────────

// Dynamic import to ensure mocks are in place before module loads
let PdfProcessingNotifier: typeof import('../PdfProcessingNotifier').PdfProcessingNotifier;

beforeEach(async () => {
  MockEventSource.instances = [];
  vi.stubGlobal('EventSource', MockEventSource);
  vi.useFakeTimers();

  // Fresh import each test to avoid stale closure over mocks
  const mod = await import('../PdfProcessingNotifier');
  PdfProcessingNotifier = mod.PdfProcessingNotifier;
});

afterEach(() => {
  vi.restoreAllMocks();
  vi.useRealTimers();
  vi.resetModules();
  mockPush.mockReset();
  mockToastSuccess.mockReset();
  mockToastError.mockReset();
  mockRetryJob.mockReset().mockResolvedValue(undefined);
});

// ─── Tests ──────────────────────────────────────────────────────────────────

describe('PdfProcessingNotifier', () => {
  it('renders nothing (returns null)', () => {
    const { container } = render(<PdfProcessingNotifier />);
    expect(container.innerHTML).toBe('');
  });

  it('connects to SSE endpoint on mount', () => {
    render(<PdfProcessingNotifier />);
    expect(MockEventSource.instances).toHaveLength(1);
    expect(MockEventSource.instances[0].url).toBe('/api/v1/admin/queue/stream');
  });

  it('shows success toast on named JobCompleted event', () => {
    render(<PdfProcessingNotifier />);
    const es = MockEventSource.instances[0];

    act(() => {
      es.emitNamed('JobCompleted', {
        jobId: 'job-1',
        fileName: 'rules.pdf',
        sharedGameId: 'game-123',
        gameName: 'Catan',
      });
    });

    expect(mockToastSuccess).toHaveBeenCalledWith('rules.pdf per Catan', {
      description: 'Indicizzazione completata',
      duration: 8000,
      action: {
        label: 'Vai al gioco',
        onClick: expect.any(Function),
      },
    });
  });

  it('navigates to shared game on success toast action click', () => {
    render(<PdfProcessingNotifier />);
    const es = MockEventSource.instances[0];

    act(() => {
      es.emitNamed('JobCompleted', {
        jobId: 'job-1',
        fileName: 'rules.pdf',
        sharedGameId: 'game-123',
        gameName: 'Catan',
      });
    });

    const call = mockToastSuccess.mock.calls[0];
    const action = call[1].action;
    action.onClick();

    expect(mockPush).toHaveBeenCalledWith('/admin/shared-games/game-123');
  });

  it('shows error toast on named JobFailed event', () => {
    render(<PdfProcessingNotifier />);
    const es = MockEventSource.instances[0];

    act(() => {
      es.emitNamed('JobFailed', {
        jobId: 'job-2',
        fileName: 'manual.pdf',
        sharedGameId: 'game-456',
        gameName: 'Agricola',
        error: 'OCR timeout',
      });
    });

    expect(mockToastError).toHaveBeenCalledWith('manual.pdf per Agricola', {
      description: 'Elaborazione fallita: OCR timeout',
      duration: 8000,
      action: {
        label: 'Riprova',
        onClick: expect.any(Function),
      },
    });
  });

  it('calls retryJob on error toast action click', () => {
    render(<PdfProcessingNotifier />);
    const es = MockEventSource.instances[0];

    act(() => {
      es.emitNamed('JobFailed', {
        jobId: 'job-2',
        fileName: 'manual.pdf',
        gameName: 'Agricola',
        error: 'OCR timeout',
      });
    });

    const call = mockToastError.mock.calls[0];
    const action = call[1].action;
    action.onClick();

    expect(mockRetryJob).toHaveBeenCalledWith('job-2');
  });

  it('handles events without game info gracefully', () => {
    render(<PdfProcessingNotifier />);
    const es = MockEventSource.instances[0];

    act(() => {
      es.emitNamed('JobCompleted', {
        jobId: 'job-3',
        fileName: 'rules.pdf',
      });
    });

    // Title should be just the filename, no "per null" or "per undefined"
    expect(mockToastSuccess).toHaveBeenCalledWith('rules.pdf', {
      description: 'Indicizzazione completata',
      duration: 8000,
      action: undefined,
    });
  });

  it('handles events without fileName gracefully', () => {
    render(<PdfProcessingNotifier />);
    const es = MockEventSource.instances[0];

    act(() => {
      es.emitNamed('JobCompleted', {
        jobId: 'job-4',
        gameName: 'Catan',
      });
    });

    // Falls back to "File" when fileName is missing
    expect(mockToastSuccess).toHaveBeenCalledWith('File per Catan', {
      description: 'Indicizzazione completata',
      duration: 8000,
      action: undefined,
    });
  });

  it('handles unnamed events via onmessage with type field', () => {
    render(<PdfProcessingNotifier />);
    const es = MockEventSource.instances[0];

    act(() => {
      es.emitMessage({
        type: 'JobCompleted',
        jobId: 'job-5',
        fileName: 'setup.pdf',
        sharedGameId: 'game-789',
        gameName: 'Wingspan',
      });
    });

    expect(mockToastSuccess).toHaveBeenCalledWith('setup.pdf per Wingspan', {
      description: 'Indicizzazione completata',
      duration: 8000,
      action: {
        label: 'Vai al gioco',
        onClick: expect.any(Function),
      },
    });
  });

  it('handles unnamed JobFailed events via onmessage', () => {
    render(<PdfProcessingNotifier />);
    const es = MockEventSource.instances[0];

    act(() => {
      es.emitMessage({
        type: 'JobFailed',
        jobId: 'job-6',
        fileName: 'broken.pdf',
        gameName: 'Gloomhaven',
        error: 'Parse error',
      });
    });

    expect(mockToastError).toHaveBeenCalledWith('broken.pdf per Gloomhaven', {
      description: 'Elaborazione fallita: Parse error',
      duration: 8000,
      action: {
        label: 'Riprova',
        onClick: expect.any(Function),
      },
    });
  });

  it('cleans up EventSource on unmount', () => {
    const { unmount } = render(<PdfProcessingNotifier />);
    const es = MockEventSource.instances[0];

    expect(es.closed).toBe(false);
    unmount();
    expect(es.closed).toBe(true);
  });

  it('reconnects with exponential backoff on error', () => {
    render(<PdfProcessingNotifier />);
    const es = MockEventSource.instances[0];

    // Trigger error
    act(() => {
      if (es.onerror) es.onerror();
    });

    expect(es.closed).toBe(true);
    expect(MockEventSource.instances).toHaveLength(1);

    // Advance by 1s (first backoff)
    act(() => {
      vi.advanceTimersByTime(1000);
    });

    expect(MockEventSource.instances).toHaveLength(2);
    const es2 = MockEventSource.instances[1];

    // Trigger another error
    act(() => {
      if (es2.onerror) es2.onerror();
    });

    // Should NOT reconnect after 1s (backoff is now 2s)
    act(() => {
      vi.advanceTimersByTime(1000);
    });
    expect(MockEventSource.instances).toHaveLength(2);

    // Advance another 1s (total 2s)
    act(() => {
      vi.advanceTimersByTime(1000);
    });
    expect(MockEventSource.instances).toHaveLength(3);
  });

  it('clears reconnect timeout on unmount', () => {
    const { unmount } = render(<PdfProcessingNotifier />);
    const es = MockEventSource.instances[0];

    // Trigger error to start reconnect timer
    act(() => {
      if (es.onerror) es.onerror();
    });

    unmount();

    // Advance time — should NOT create a new EventSource
    act(() => {
      vi.advanceTimersByTime(5000);
    });

    expect(MockEventSource.instances).toHaveLength(1);
  });

  it('ignores unknown event types in onmessage', () => {
    render(<PdfProcessingNotifier />);
    const es = MockEventSource.instances[0];

    act(() => {
      es.emitMessage({
        type: 'Heartbeat',
        jobId: 'hb-1',
      });
    });

    expect(mockToastSuccess).not.toHaveBeenCalled();
    expect(mockToastError).not.toHaveBeenCalled();
  });

  it('ignores malformed JSON in events', () => {
    render(<PdfProcessingNotifier />);
    const es = MockEventSource.instances[0];

    act(() => {
      if (es.onmessage) {
        es.onmessage(new MessageEvent('message', { data: 'not-json' }));
      }
    });

    expect(mockToastSuccess).not.toHaveBeenCalled();
    expect(mockToastError).not.toHaveBeenCalled();
  });
});
