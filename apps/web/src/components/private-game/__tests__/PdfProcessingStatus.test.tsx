/**
 * PdfProcessingStatus Component Tests
 *
 * Issue #3664: Private game PDF support — KB readiness polling.
 *
 * Test Coverage:
 * - Loading state: spinner displayed
 * - Error state: error message shown
 * - Waiting state (no data yet)
 * - Pending/Extracting/Chunking/Embedding: stage indicators and progress bar
 * - Completed: success card with chunk count
 * - Failed: error card with error message
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { PdfProcessingStatus } from '../PdfProcessingStatus';

// ============================================================================
// Mocks
// ============================================================================

vi.mock('@/hooks/queries/usePrivateGameKbStatus', () => ({
  usePrivateGameKbStatus: vi.fn(),
}));

// eslint-disable-next-line @typescript-eslint/no-explicit-any
const { usePrivateGameKbStatus } = (await import('@/hooks/queries/usePrivateGameKbStatus')) as any;

// ============================================================================
// Helpers
// ============================================================================

function mockQuery(overrides: Record<string, unknown>) {
  usePrivateGameKbStatus.mockReturnValue({
    data: undefined,
    isLoading: false,
    isError: false,
    ...overrides,
  });
}

type KbStatus = 'Pending' | 'Extracting' | 'Chunking' | 'Embedding' | 'Completed' | 'Failed';

function makeStatus(status: KbStatus, extra?: Record<string, unknown>) {
  return {
    status,
    progress: 50,
    totalChunks: 100,
    processedChunks: 50,
    errorMessage: null,
    gameName: 'Test Game',
    ...extra,
  };
}

// ============================================================================
// Tests
// ============================================================================

describe('PdfProcessingStatus', () => {
  const TEST_GAME_ID = 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee';

  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Loading state', () => {
    it('shows loading spinner while initial fetch is in progress', () => {
      mockQuery({ isLoading: true });

      render(<PdfProcessingStatus privateGameId={TEST_GAME_ID} />);

      expect(screen.getByTestId('pdf-processing-status-loading')).toBeInTheDocument();
      expect(screen.getByText(/Verifica stato indicizzazione/i)).toBeInTheDocument();
    });

    it('does not show the main panel while loading with no data', () => {
      mockQuery({ isLoading: true });

      render(<PdfProcessingStatus privateGameId={TEST_GAME_ID} />);

      expect(screen.queryByTestId('pdf-processing-status')).not.toBeInTheDocument();
    });
  });

  describe('Error state', () => {
    it('shows error message when fetch fails', () => {
      mockQuery({ isError: true });

      render(<PdfProcessingStatus privateGameId={TEST_GAME_ID} />);

      expect(screen.getByTestId('pdf-processing-status-error')).toBeInTheDocument();
      expect(screen.getByText(/Impossibile recuperare lo stato/i)).toBeInTheDocument();
    });

    it('uses role=alert for the error element', () => {
      mockQuery({ isError: true });

      render(<PdfProcessingStatus privateGameId={TEST_GAME_ID} />);

      expect(screen.getByRole('alert')).toBeInTheDocument();
    });
  });

  describe('Waiting state (no data)', () => {
    it('shows waiting message when there is no data and not loading', () => {
      mockQuery({ data: undefined, isLoading: false });

      render(<PdfProcessingStatus privateGameId={TEST_GAME_ID} />);

      expect(screen.getByTestId('pdf-processing-status-waiting')).toBeInTheDocument();
      expect(screen.getByText(/In attesa dell/i)).toBeInTheDocument();
    });
  });

  describe('Pending status', () => {
    it('shows "Indicizzazione in corso" header', () => {
      mockQuery({ data: makeStatus('Pending', { progress: 0 }) });

      render(<PdfProcessingStatus privateGameId={TEST_GAME_ID} />);

      expect(screen.getByTestId('pdf-processing-status')).toBeInTheDocument();
      expect(screen.getByText(/Indicizzazione in corso/i)).toBeInTheDocument();
    });

    it('shows progress bar for Pending status', () => {
      mockQuery({ data: makeStatus('Pending', { progress: 5 }) });

      render(<PdfProcessingStatus privateGameId={TEST_GAME_ID} />);

      expect(screen.getByRole('progressbar')).toBeInTheDocument();
    });

    it('shows background note for non-terminal status', () => {
      mockQuery({ data: makeStatus('Pending') });

      render(<PdfProcessingStatus privateGameId={TEST_GAME_ID} />);

      expect(screen.getByText(/continua in background/i)).toBeInTheDocument();
    });
  });

  describe('Processing statuses', () => {
    it.each(['Extracting', 'Chunking', 'Embedding'] as KbStatus[])(
      'shows progress bar when status is %s',
      status => {
        mockQuery({ data: makeStatus(status, { progress: 50 }) });

        render(<PdfProcessingStatus privateGameId={TEST_GAME_ID} />);

        expect(screen.getByRole('progressbar')).toBeInTheDocument();
        expect(screen.getByRole('progressbar')).toHaveAttribute('aria-valuenow', '50');
      }
    );

    it('shows progress percentage in header when processing', () => {
      mockQuery({ data: makeStatus('Extracting', { progress: 42 }) });

      render(<PdfProcessingStatus privateGameId={TEST_GAME_ID} />);

      expect(screen.getByText('42%')).toBeInTheDocument();
    });
  });

  describe('Completed status', () => {
    it('shows "Indicizzazione completata" header', () => {
      mockQuery({ data: makeStatus('Completed', { progress: 100, processedChunks: 128 }) });

      render(<PdfProcessingStatus privateGameId={TEST_GAME_ID} />);

      expect(screen.getByText(/Indicizzazione completata/i)).toBeInTheDocument();
    });

    it('does not show progress bar when completed', () => {
      mockQuery({ data: makeStatus('Completed', { progress: 100 }) });

      render(<PdfProcessingStatus privateGameId={TEST_GAME_ID} />);

      expect(screen.queryByRole('progressbar')).not.toBeInTheDocument();
    });

    it('shows chunk count when processedChunks > 0', () => {
      mockQuery({ data: makeStatus('Completed', { progress: 100, processedChunks: 256 }) });

      render(<PdfProcessingStatus privateGameId={TEST_GAME_ID} />);

      expect(screen.getByText(/256 chunk indicizzati/i)).toBeInTheDocument();
    });

    it('does not show background note when completed', () => {
      mockQuery({ data: makeStatus('Completed', { progress: 100 }) });

      render(<PdfProcessingStatus privateGameId={TEST_GAME_ID} />);

      expect(screen.queryByText(/continua in background/i)).not.toBeInTheDocument();
    });
  });

  describe('Failed status', () => {
    it('shows "Indicizzazione fallita" header', () => {
      mockQuery({
        data: makeStatus('Failed', { progress: 0, errorMessage: 'Parse error on page 5' }),
      });

      render(<PdfProcessingStatus privateGameId={TEST_GAME_ID} />);

      expect(screen.getByText(/Indicizzazione fallita/i)).toBeInTheDocument();
    });

    it('shows error message when provided', () => {
      mockQuery({
        data: makeStatus('Failed', { progress: 0, errorMessage: 'Parse error on page 5' }),
      });

      render(<PdfProcessingStatus privateGameId={TEST_GAME_ID} />);

      expect(screen.getByRole('alert')).toHaveTextContent('Parse error on page 5');
    });

    it('does not show error alert when errorMessage is null', () => {
      mockQuery({
        data: makeStatus('Failed', { progress: 0, errorMessage: null }),
      });

      render(<PdfProcessingStatus privateGameId={TEST_GAME_ID} />);

      // The component should not crash and no error alert for null message
      expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    });

    it('does not show progress bar when failed', () => {
      mockQuery({
        data: makeStatus('Failed', { progress: 0, errorMessage: 'Error' }),
      });

      render(<PdfProcessingStatus privateGameId={TEST_GAME_ID} />);

      expect(screen.queryByRole('progressbar')).not.toBeInTheDocument();
    });

    it('does not show background note when failed', () => {
      mockQuery({
        data: makeStatus('Failed', { errorMessage: 'Error' }),
      });

      render(<PdfProcessingStatus privateGameId={TEST_GAME_ID} />);

      expect(screen.queryByText(/continua in background/i)).not.toBeInTheDocument();
    });
  });

  describe('Hook integration', () => {
    it('passes privateGameId to usePrivateGameKbStatus', () => {
      mockQuery({ isLoading: true });

      render(<PdfProcessingStatus privateGameId="my-game-uuid" />);

      expect(usePrivateGameKbStatus).toHaveBeenCalledWith('my-game-uuid');
    });

    it('renders main panel container with data-testid', () => {
      mockQuery({ data: makeStatus('Pending') });

      render(<PdfProcessingStatus privateGameId={TEST_GAME_ID} />);

      expect(screen.getByTestId('pdf-processing-status')).toBeInTheDocument();
    });
  });
});
