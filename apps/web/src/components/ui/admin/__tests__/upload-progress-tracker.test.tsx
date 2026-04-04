/**
 * UploadProgressTracker Tests
 *
 * Tests covering:
 * 1. Returns null when documentId is null
 * 2. Returns null when status is null (loading)
 * 3. Shows fileName when provided
 * 4. Shows correct step states for 'extracting' (Extracting=current, rest pending)
 * 5. Shows correct step states for 'chunking' (Extracting=done, Chunking=current)
 * 6. Shows progress percentage
 * 7. Shows error message on 'failed' state
 * 8. Shows ETA when available
 */

import React from 'react';
import { render, screen, act } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { UploadProgressTracker } from '../upload-progress-tracker';

// ============================================================================
// Mocks
// ============================================================================

vi.mock('@/hooks/usePdfProgress');

import { usePdfProgress } from '@/hooks/usePdfProgress';

const mockUsePdfProgress = vi.mocked(usePdfProgress);

// ============================================================================
// Helpers
// ============================================================================

function makeStatus(
  overrides: Partial<{
    state: string;
    progress: number;
    eta: string | null;
    timestamp: string;
    errorMessage: string | null;
  }> = {}
) {
  return {
    state: 'extracting',
    progress: 30,
    eta: null,
    timestamp: new Date().toISOString(),
    errorMessage: null,
    ...overrides,
  };
}

function makeHookReturn(overrides: Partial<ReturnType<typeof usePdfProgress>> = {}) {
  return {
    status: null,
    connectionState: 'connected' as const,
    isConnected: true,
    isPolling: false,
    isLoading: false,
    error: null,
    connectionMetrics: {
      connectionUptime: 0,
      reconnectionCount: 0,
      fallbackTriggers: 0,
      lastConnectedAt: null,
    },
    reconnect: vi.fn(),
    metrics: null,
    metricsLoading: false,
    metricsError: null,
    refreshMetrics: vi.fn(),
    ...overrides,
  };
}

// ============================================================================
// Tests
// ============================================================================

describe('UploadProgressTracker', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  describe('null rendering conditions', () => {
    it('returns null when documentId is null', () => {
      mockUsePdfProgress.mockReturnValue(makeHookReturn({ status: null }));

      const { container } = render(<UploadProgressTracker documentId={null} />);

      expect(container.firstChild).toBeNull();
    });

    it('returns null when status is null (loading)', () => {
      mockUsePdfProgress.mockReturnValue(makeHookReturn({ status: null }));

      const { container } = render(<UploadProgressTracker documentId="doc-123" />);

      expect(container.firstChild).toBeNull();
    });
  });

  describe('display', () => {
    it('shows tracker when documentId and status are provided', () => {
      mockUsePdfProgress.mockReturnValue(makeHookReturn({ status: makeStatus() as any }));

      render(<UploadProgressTracker documentId="doc-123" />);

      expect(screen.getByTestId('upload-progress-tracker')).toBeInTheDocument();
    });

    it('shows fileName when provided', () => {
      mockUsePdfProgress.mockReturnValue(makeHookReturn({ status: makeStatus() as any }));

      render(<UploadProgressTracker documentId="doc-123" fileName="rulebook.pdf" />);

      expect(screen.getByText('rulebook.pdf')).toBeInTheDocument();
    });

    it('shows generic label when fileName is not provided', () => {
      mockUsePdfProgress.mockReturnValue(makeHookReturn({ status: makeStatus() as any }));

      render(<UploadProgressTracker documentId="doc-123" />);

      expect(screen.getByText('Elaborazione PDF in corso...')).toBeInTheDocument();
    });

    it('shows progress percentage', () => {
      mockUsePdfProgress.mockReturnValue(
        makeHookReturn({ status: makeStatus({ progress: 45 }) as any })
      );

      render(<UploadProgressTracker documentId="doc-123" />);

      expect(screen.getByText('45%')).toBeInTheDocument();
    });

    it('shows ETA when available', () => {
      mockUsePdfProgress.mockReturnValue(
        makeHookReturn({ status: makeStatus({ eta: '2 min' }) as any })
      );

      render(<UploadProgressTracker documentId="doc-123" />);

      expect(screen.getByText(/2 min/)).toBeInTheDocument();
    });
  });

  describe('pipeline step states for extracting', () => {
    beforeEach(() => {
      mockUsePdfProgress.mockReturnValue(
        makeHookReturn({ status: makeStatus({ state: 'extracting', progress: 20 }) as any })
      );
    });

    it('shows Extracting step as current', () => {
      render(<UploadProgressTracker documentId="doc-123" />);

      expect(screen.getByTestId('progress-step-extracting')).toHaveAttribute(
        'data-status',
        'current'
      );
    });

    it('shows Chunking step as pending', () => {
      render(<UploadProgressTracker documentId="doc-123" />);

      expect(screen.getByTestId('progress-step-chunking')).toHaveAttribute(
        'data-status',
        'pending'
      );
    });

    it('shows Embedding step as pending', () => {
      render(<UploadProgressTracker documentId="doc-123" />);

      expect(screen.getByTestId('progress-step-embedding')).toHaveAttribute(
        'data-status',
        'pending'
      );
    });

    it('shows Indexing step as pending', () => {
      render(<UploadProgressTracker documentId="doc-123" />);

      expect(screen.getByTestId('progress-step-indexing')).toHaveAttribute(
        'data-status',
        'pending'
      );
    });
  });

  describe('pipeline step states for chunking', () => {
    beforeEach(() => {
      mockUsePdfProgress.mockReturnValue(
        makeHookReturn({ status: makeStatus({ state: 'chunking', progress: 50 }) as any })
      );
    });

    it('shows Extracting step as done', () => {
      render(<UploadProgressTracker documentId="doc-123" />);

      expect(screen.getByTestId('progress-step-extracting')).toHaveAttribute('data-status', 'done');
    });

    it('shows Chunking step as current', () => {
      render(<UploadProgressTracker documentId="doc-123" />);

      expect(screen.getByTestId('progress-step-chunking')).toHaveAttribute(
        'data-status',
        'current'
      );
    });

    it('shows Embedding step as pending', () => {
      render(<UploadProgressTracker documentId="doc-123" />);

      expect(screen.getByTestId('progress-step-embedding')).toHaveAttribute(
        'data-status',
        'pending'
      );
    });

    it('shows Indexing step as pending', () => {
      render(<UploadProgressTracker documentId="doc-123" />);

      expect(screen.getByTestId('progress-step-indexing')).toHaveAttribute(
        'data-status',
        'pending'
      );
    });
  });

  describe('pipeline step states for embedding', () => {
    beforeEach(() => {
      mockUsePdfProgress.mockReturnValue(
        makeHookReturn({ status: makeStatus({ state: 'embedding', progress: 70 }) as any })
      );
    });

    it('shows Extracting and Chunking as done', () => {
      render(<UploadProgressTracker documentId="doc-123" />);

      expect(screen.getByTestId('progress-step-extracting')).toHaveAttribute('data-status', 'done');
      expect(screen.getByTestId('progress-step-chunking')).toHaveAttribute('data-status', 'done');
    });

    it('shows Embedding as current', () => {
      render(<UploadProgressTracker documentId="doc-123" />);

      expect(screen.getByTestId('progress-step-embedding')).toHaveAttribute(
        'data-status',
        'current'
      );
    });

    it('shows Indexing as pending', () => {
      render(<UploadProgressTracker documentId="doc-123" />);

      expect(screen.getByTestId('progress-step-indexing')).toHaveAttribute(
        'data-status',
        'pending'
      );
    });
  });

  describe('pipeline step states for indexing', () => {
    beforeEach(() => {
      mockUsePdfProgress.mockReturnValue(
        makeHookReturn({ status: makeStatus({ state: 'indexing', progress: 85 }) as any })
      );
    });

    it('shows Extracting, Chunking and Embedding as done', () => {
      render(<UploadProgressTracker documentId="doc-123" />);

      expect(screen.getByTestId('progress-step-extracting')).toHaveAttribute('data-status', 'done');
      expect(screen.getByTestId('progress-step-chunking')).toHaveAttribute('data-status', 'done');
      expect(screen.getByTestId('progress-step-embedding')).toHaveAttribute('data-status', 'done');
    });

    it('shows Indexing as current', () => {
      render(<UploadProgressTracker documentId="doc-123" />);

      expect(screen.getByTestId('progress-step-indexing')).toHaveAttribute(
        'data-status',
        'current'
      );
    });
  });

  describe('pipeline step states for ready', () => {
    it('shows all steps as done when state is ready', () => {
      mockUsePdfProgress.mockReturnValue(
        makeHookReturn({ status: makeStatus({ state: 'ready', progress: 100 }) as any })
      );

      render(<UploadProgressTracker documentId="doc-123" />);

      expect(screen.getByTestId('progress-step-extracting')).toHaveAttribute('data-status', 'done');
      expect(screen.getByTestId('progress-step-chunking')).toHaveAttribute('data-status', 'done');
      expect(screen.getByTestId('progress-step-embedding')).toHaveAttribute('data-status', 'done');
      expect(screen.getByTestId('progress-step-indexing')).toHaveAttribute('data-status', 'done');
    });
  });

  describe('error state', () => {
    it('shows error message when state is failed and errorMessage is provided', () => {
      mockUsePdfProgress.mockReturnValue(
        makeHookReturn({
          status: makeStatus({
            state: 'failed',
            errorMessage: "Errore durante l'estrazione",
          }) as any,
        })
      );

      render(<UploadProgressTracker documentId="doc-123" />);

      expect(screen.getByTestId('progress-error-message')).toBeInTheDocument();
      expect(screen.getByText("Errore durante l'estrazione")).toBeInTheDocument();
    });

    it('does not show error element when state is failed but no errorMessage', () => {
      mockUsePdfProgress.mockReturnValue(
        makeHookReturn({
          status: makeStatus({ state: 'failed', errorMessage: null }) as any,
        })
      );

      render(<UploadProgressTracker documentId="doc-123" />);

      expect(screen.queryByTestId('progress-error-message')).not.toBeInTheDocument();
    });
  });

  describe('connection warning', () => {
    it('shows polling warning when not connected and not in terminal state', () => {
      mockUsePdfProgress.mockReturnValue(
        makeHookReturn({
          status: makeStatus({ state: 'extracting' }) as any,
          isConnected: false,
        })
      );

      render(<UploadProgressTracker documentId="doc-123" />);

      expect(screen.getByText('Connessione SSE persa, polling attivo...')).toBeInTheDocument();
    });

    it('does not show polling warning when connected', () => {
      mockUsePdfProgress.mockReturnValue(
        makeHookReturn({
          status: makeStatus({ state: 'extracting' }) as any,
          isConnected: true,
        })
      );

      render(<UploadProgressTracker documentId="doc-123" />);

      expect(
        screen.queryByText('Connessione SSE persa, polling attivo...')
      ).not.toBeInTheDocument();
    });

    it('does not show polling warning when state is ready even if not connected', () => {
      mockUsePdfProgress.mockReturnValue(
        makeHookReturn({
          status: makeStatus({ state: 'ready', progress: 100 }) as any,
          isConnected: false,
        })
      );

      render(<UploadProgressTracker documentId="doc-123" />);

      expect(
        screen.queryByText('Connessione SSE persa, polling attivo...')
      ).not.toBeInTheDocument();
    });
  });

  describe('auto-hide behavior', () => {
    it('hides after 5 seconds when state is ready', () => {
      mockUsePdfProgress.mockReturnValue(
        makeHookReturn({ status: makeStatus({ state: 'ready', progress: 100 }) as any })
      );

      render(<UploadProgressTracker documentId="doc-123" />);

      expect(screen.getByTestId('upload-progress-tracker')).toBeInTheDocument();

      act(() => {
        vi.advanceTimersByTime(5000);
      });

      expect(screen.queryByTestId('upload-progress-tracker')).not.toBeInTheDocument();
    });

    it('hides after 5 seconds when state is failed', () => {
      mockUsePdfProgress.mockReturnValue(
        makeHookReturn({
          status: makeStatus({ state: 'failed', errorMessage: 'Error' }) as any,
        })
      );

      render(<UploadProgressTracker documentId="doc-123" />);

      expect(screen.getByTestId('upload-progress-tracker')).toBeInTheDocument();

      act(() => {
        vi.advanceTimersByTime(5000);
      });

      expect(screen.queryByTestId('upload-progress-tracker')).not.toBeInTheDocument();
    });

    it('does not hide before 5 seconds', () => {
      mockUsePdfProgress.mockReturnValue(
        makeHookReturn({ status: makeStatus({ state: 'ready', progress: 100 }) as any })
      );

      render(<UploadProgressTracker documentId="doc-123" />);

      act(() => {
        vi.advanceTimersByTime(4999);
      });

      expect(screen.getByTestId('upload-progress-tracker')).toBeInTheDocument();
    });
  });
});
