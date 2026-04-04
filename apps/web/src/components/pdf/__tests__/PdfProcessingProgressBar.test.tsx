/**
 * PdfProcessingProgressBar Tests (Issue #3369)
 *
 * Unit tests for PdfProcessingProgressBar component covering:
 * - Rendering all processing steps
 * - Progress bar visualization
 * - Time estimates display
 * - Error states with retry
 * - Cancel functionality
 * - Accessibility features
 */

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import type { ProcessingProgress } from '@/lib/api';

import { PdfProcessingProgressBar } from '../PdfProcessingProgressBar';

// Mock the usePdfProcessingProgress hook
const mockRefetch = vi.fn();
vi.mock('@/hooks/usePdfProcessingProgress', () => ({
  usePdfProcessingProgress: vi.fn(),
}));

// Mock the api.pdf.cancelProcessing
vi.mock('@/lib/api', () => ({
  api: {
    pdf: {
      cancelProcessing: vi.fn(),
    },
  },
}));

// Import mocked modules for direct access
import { usePdfProcessingProgress } from '@/hooks/usePdfProcessingProgress';
import { api } from '@/lib/api';

const mockUsePdfProcessingProgress = vi.mocked(usePdfProcessingProgress);
const mockCancelProcessing = vi.mocked(api.pdf.cancelProcessing);

// Helper to create mock progress data
function createMockProgress(overrides: Partial<ProcessingProgress> = {}): ProcessingProgress {
  return {
    pdfId: '123e4567-e89b-12d3-a456-426614174000',
    currentStep: 'Uploading',
    percentComplete: 0,
    startedAt: '2024-01-15T10:00:00Z',
    elapsedTime: '00:01:30.0000000',
    estimatedTimeRemaining: '00:03:00.0000000',
    errorMessage: null,
    ...overrides,
  };
}

describe('PdfProcessingProgressBar', () => {
  const defaultProps = {
    pdfId: '123e4567-e89b-12d3-a456-426614174000',
  };

  beforeEach(() => {
    vi.clearAllMocks();
    mockUsePdfProcessingProgress.mockReturnValue({
      progress: createMockProgress(),
      isLoading: false,
      error: null,
      refetch: mockRefetch,
    });
  });

  describe('Basic Rendering', () => {
    it('should render the component with default props', () => {
      render(<PdfProcessingProgressBar {...defaultProps} />);

      expect(screen.getByTestId('pdf-processing-progress-bar')).toBeInTheDocument();
      expect(screen.getByText('Elaborazione PDF')).toBeInTheDocument();
    });

    it('should render all 6 step indicators', () => {
      render(<PdfProcessingProgressBar {...defaultProps} />);

      expect(screen.getByTestId('step-indicator-uploading')).toBeInTheDocument();
      expect(screen.getByTestId('step-indicator-extracting')).toBeInTheDocument();
      expect(screen.getByTestId('step-indicator-chunking')).toBeInTheDocument();
      expect(screen.getByTestId('step-indicator-embedding')).toBeInTheDocument();
      expect(screen.getByTestId('step-indicator-indexing')).toBeInTheDocument();
      expect(screen.getByTestId('step-indicator-completed')).toBeInTheDocument();
    });

    it('should render progress bar', () => {
      render(<PdfProcessingProgressBar {...defaultProps} />);

      expect(screen.getByRole('progressbar')).toBeInTheDocument();
    });

    it('should apply custom className', () => {
      render(<PdfProcessingProgressBar {...defaultProps} className="custom-class" />);

      expect(screen.getByTestId('pdf-processing-progress-bar')).toHaveClass('custom-class');
    });
  });

  describe('Loading State', () => {
    it('should render loading state when isLoading and no progress', () => {
      mockUsePdfProcessingProgress.mockReturnValue({
        progress: null,
        isLoading: true,
        error: null,
        refetch: mockRefetch,
      });

      render(<PdfProcessingProgressBar {...defaultProps} />);

      expect(screen.getByTestId('pdf-progress-loading')).toBeInTheDocument();
      expect(screen.getByText('Caricamento stato elaborazione...')).toBeInTheDocument();
    });
  });

  describe('Processing Steps', () => {
    it.each([
      ['Uploading', 'Caricamento del file PDF...', 10],
      ['Extracting', 'Estrazione del testo dal documento...', 30],
      ['Chunking', 'Suddivisione del testo in chunks...', 50],
      ['Embedding', 'Generazione degli embeddings vettoriali...', 70],
      ['Indexing', 'Indicizzazione nel database vettoriale...', 90],
    ] as const)(
      'should display correct description for %s step',
      (step, expectedDescription, percent) => {
        mockUsePdfProcessingProgress.mockReturnValue({
          progress: createMockProgress({ currentStep: step, percentComplete: percent }),
          isLoading: false,
          error: null,
          refetch: mockRefetch,
        });

        render(<PdfProcessingProgressBar {...defaultProps} />);

        expect(screen.getByText(expectedDescription)).toBeInTheDocument();
        expect(screen.getByText(`${percent}%`)).toBeInTheDocument();
      }
    );

    it('should highlight active step', () => {
      mockUsePdfProcessingProgress.mockReturnValue({
        progress: createMockProgress({ currentStep: 'Chunking', percentComplete: 50 }),
        isLoading: false,
        error: null,
        refetch: mockRefetch,
      });

      render(<PdfProcessingProgressBar {...defaultProps} />);

      const chunkingStep = screen.getByTestId('step-indicator-chunking');
      expect(chunkingStep).toBeInTheDocument();
    });
  });

  describe('Progress Display', () => {
    it('should display percentage complete', () => {
      mockUsePdfProcessingProgress.mockReturnValue({
        progress: createMockProgress({ percentComplete: 45 }),
        isLoading: false,
        error: null,
        refetch: mockRefetch,
      });

      render(<PdfProcessingProgressBar {...defaultProps} />);

      expect(screen.getByText('45%')).toBeInTheDocument();
    });

    it('should display 0% at start', () => {
      mockUsePdfProcessingProgress.mockReturnValue({
        progress: createMockProgress({ percentComplete: 0 }),
        isLoading: false,
        error: null,
        refetch: mockRefetch,
      });

      render(<PdfProcessingProgressBar {...defaultProps} />);

      expect(screen.getByText('0%')).toBeInTheDocument();
    });

    it('should display 100% when complete', () => {
      mockUsePdfProcessingProgress.mockReturnValue({
        progress: createMockProgress({ currentStep: 'Completed', percentComplete: 100 }),
        isLoading: false,
        error: null,
        refetch: mockRefetch,
      });

      render(<PdfProcessingProgressBar {...defaultProps} />);

      expect(screen.getByText('100%')).toBeInTheDocument();
    });
  });

  describe('Time Estimates', () => {
    it('should display elapsed time', () => {
      mockUsePdfProcessingProgress.mockReturnValue({
        progress: createMockProgress({
          elapsedTime: '00:02:30.0000000',
          currentStep: 'Chunking',
        }),
        isLoading: false,
        error: null,
        refetch: mockRefetch,
      });

      render(<PdfProcessingProgressBar {...defaultProps} />);

      expect(screen.getByText(/Tempo trascorso:/)).toBeInTheDocument();
      // formatTimeSpan returns "2m 30s" for "00:02:30.0000000"
      expect(screen.getByText(/2m 30s/)).toBeInTheDocument();
    });

    it('should display estimated time remaining', () => {
      mockUsePdfProcessingProgress.mockReturnValue({
        progress: createMockProgress({
          estimatedTimeRemaining: '00:05:00.0000000',
          currentStep: 'Chunking',
        }),
        isLoading: false,
        error: null,
        refetch: mockRefetch,
      });

      render(<PdfProcessingProgressBar {...defaultProps} />);

      expect(screen.getByText(/Tempo stimato:/)).toBeInTheDocument();
      // formatTimeSpan returns "5m 0s" for "00:05:00.0000000"
      expect(screen.getByText(/5m 0s/)).toBeInTheDocument();
    });

    it('should display hours when elapsed time is long', () => {
      mockUsePdfProcessingProgress.mockReturnValue({
        progress: createMockProgress({
          elapsedTime: '01:30:45.0000000',
          currentStep: 'Embedding',
        }),
        isLoading: false,
        error: null,
        refetch: mockRefetch,
      });

      render(<PdfProcessingProgressBar {...defaultProps} />);

      // formatTimeSpan returns "1h 30m 45s" for hours > 0
      expect(screen.getByText(/1h 30m 45s/)).toBeInTheDocument();
    });

    it('should display seconds only for short times', () => {
      mockUsePdfProcessingProgress.mockReturnValue({
        progress: createMockProgress({
          elapsedTime: '00:00:15.0000000',
          currentStep: 'Uploading',
        }),
        isLoading: false,
        error: null,
        refetch: mockRefetch,
      });

      render(<PdfProcessingProgressBar {...defaultProps} />);

      // formatTimeSpan returns "15s" for seconds only (no hours or minutes)
      expect(screen.getByText(/15s/)).toBeInTheDocument();
    });

    it('should display "--:--" for null time values', () => {
      mockUsePdfProcessingProgress.mockReturnValue({
        progress: {
          pdfId: '123e4567-e89b-12d3-a456-426614174000',
          currentStep: 'Uploading' as const,
          percentComplete: 10,
          startedAt: '2024-01-15T10:00:00Z',
          elapsedTime: null,
          estimatedTimeRemaining: null,
          errorMessage: null,
        },
        isLoading: false,
        error: null,
        refetch: mockRefetch,
      });

      render(<PdfProcessingProgressBar {...defaultProps} />);

      // formatTimeSpan returns "--:--" for null values
      // Both elapsed time and estimated time should show "--:--"
      expect(screen.getByText(/Tempo trascorso:/)).toBeInTheDocument();
      expect(screen.getByText(/Tempo stimato:/)).toBeInTheDocument();
      // Check the time sections contain "--:--"
      const timeInfoContainer = screen.getByText(/Tempo trascorso:/).closest('div');
      expect(timeInfoContainer).toHaveTextContent('--:--');
    });

    it('should not display time info when completed', () => {
      mockUsePdfProcessingProgress.mockReturnValue({
        progress: createMockProgress({
          currentStep: 'Completed',
          percentComplete: 100,
        }),
        isLoading: false,
        error: null,
        refetch: mockRefetch,
      });

      render(<PdfProcessingProgressBar {...defaultProps} />);

      expect(screen.queryByText(/Tempo trascorso:/)).not.toBeInTheDocument();
      expect(screen.queryByText(/Tempo stimato:/)).not.toBeInTheDocument();
    });
  });

  describe('Completed State', () => {
    it('should display success message when completed', () => {
      mockUsePdfProcessingProgress.mockReturnValue({
        progress: createMockProgress({ currentStep: 'Completed', percentComplete: 100 }),
        isLoading: false,
        error: null,
        refetch: mockRefetch,
      });

      render(<PdfProcessingProgressBar {...defaultProps} />);

      expect(screen.getByRole('status')).toBeInTheDocument();
      expect(
        screen.getByText('Elaborazione completata! Il PDF è pronto per le domande.')
      ).toBeInTheDocument();
    });

    it('should hide cancel button when completed', () => {
      mockUsePdfProcessingProgress.mockReturnValue({
        progress: createMockProgress({ currentStep: 'Completed', percentComplete: 100 }),
        isLoading: false,
        error: null,
        refetch: mockRefetch,
      });

      render(<PdfProcessingProgressBar {...defaultProps} />);

      expect(screen.queryByTestId('cancel-button')).not.toBeInTheDocument();
    });

    it('should call onComplete callback', () => {
      const onComplete = vi.fn();
      mockUsePdfProcessingProgress.mockImplementation((pdfId, options) => {
        // Simulate the hook calling onComplete
        options?.onComplete?.();
        return {
          progress: createMockProgress({ currentStep: 'Completed', percentComplete: 100 }),
          isLoading: false,
          error: null,
          refetch: mockRefetch,
        };
      });

      render(<PdfProcessingProgressBar {...defaultProps} onComplete={onComplete} />);

      expect(onComplete).toHaveBeenCalled();
    });
  });

  describe('Error State', () => {
    it('should display error message when processing fails', () => {
      mockUsePdfProcessingProgress.mockReturnValue({
        progress: createMockProgress({
          currentStep: 'Failed',
          errorMessage: 'Document extraction failed: corrupted PDF',
        }),
        isLoading: false,
        error: null,
        refetch: mockRefetch,
      });

      render(<PdfProcessingProgressBar {...defaultProps} />);

      expect(screen.getByRole('alert')).toBeInTheDocument();
      expect(screen.getByText('Elaborazione fallita')).toBeInTheDocument();
      expect(screen.getByText('Document extraction failed: corrupted PDF')).toBeInTheDocument();
    });

    it('should display retry button on failure', () => {
      mockUsePdfProcessingProgress.mockReturnValue({
        progress: createMockProgress({ currentStep: 'Failed' }),
        isLoading: false,
        error: null,
        refetch: mockRefetch,
      });

      render(<PdfProcessingProgressBar {...defaultProps} />);

      expect(screen.getByRole('button', { name: /riprova/i })).toBeInTheDocument();
    });

    it('should call refetch when retry button is clicked', async () => {
      const user = userEvent.setup();
      mockUsePdfProcessingProgress.mockReturnValue({
        progress: createMockProgress({ currentStep: 'Failed' }),
        isLoading: false,
        error: null,
        refetch: mockRefetch,
      });

      render(<PdfProcessingProgressBar {...defaultProps} />);

      await user.click(screen.getByRole('button', { name: /riprova/i }));

      expect(mockRefetch).toHaveBeenCalled();
    });

    it('should call onError callback', () => {
      const onError = vi.fn();
      mockUsePdfProcessingProgress.mockImplementation((pdfId, options) => {
        // Simulate the hook calling onError
        options?.onError?.('Processing failed');
        return {
          progress: createMockProgress({ currentStep: 'Failed' }),
          isLoading: false,
          error: null,
          refetch: mockRefetch,
        };
      });

      render(<PdfProcessingProgressBar {...defaultProps} onError={onError} />);

      expect(onError).toHaveBeenCalledWith('Processing failed');
    });
  });

  describe('Network Error State', () => {
    it('should display connection error when hook returns error', () => {
      mockUsePdfProcessingProgress.mockReturnValue({
        progress: null,
        isLoading: false,
        error: new Error('Network error'),
        refetch: mockRefetch,
      });

      render(<PdfProcessingProgressBar {...defaultProps} />);

      expect(screen.getByTestId('pdf-progress-error')).toBeInTheDocument();
      expect(screen.getByText('Errore di connessione')).toBeInTheDocument();
      expect(screen.getByText('Network error')).toBeInTheDocument();
    });

    it('should display retry button on connection error', () => {
      mockUsePdfProcessingProgress.mockReturnValue({
        progress: null,
        isLoading: false,
        error: new Error('Network error'),
        refetch: mockRefetch,
      });

      render(<PdfProcessingProgressBar {...defaultProps} />);

      expect(screen.getByRole('button', { name: /riprova/i })).toBeInTheDocument();
    });

    it('should call refetch when retry button is clicked on connection error', async () => {
      const user = userEvent.setup();
      mockUsePdfProcessingProgress.mockReturnValue({
        progress: null,
        isLoading: false,
        error: new Error('Network error'),
        refetch: mockRefetch,
      });

      render(<PdfProcessingProgressBar {...defaultProps} />);

      await user.click(screen.getByRole('button', { name: /riprova/i }));

      expect(mockRefetch).toHaveBeenCalled();
    });
  });

  describe('Cancel Functionality', () => {
    it('should display cancel button during processing', () => {
      mockUsePdfProcessingProgress.mockReturnValue({
        progress: createMockProgress({ currentStep: 'Chunking', percentComplete: 50 }),
        isLoading: false,
        error: null,
        refetch: mockRefetch,
      });

      render(<PdfProcessingProgressBar {...defaultProps} />);

      expect(screen.getByTestId('cancel-button')).toBeInTheDocument();
      expect(screen.getByText('Annulla')).toBeInTheDocument();
    });

    it('should open confirmation dialog when cancel is clicked', async () => {
      const user = userEvent.setup();
      mockUsePdfProcessingProgress.mockReturnValue({
        progress: createMockProgress({ currentStep: 'Chunking', percentComplete: 50 }),
        isLoading: false,
        error: null,
        refetch: mockRefetch,
      });

      render(<PdfProcessingProgressBar {...defaultProps} />);

      await user.click(screen.getByTestId('cancel-button'));

      expect(screen.getByText("Vuoi cancellare l'elaborazione?")).toBeInTheDocument();
    });

    it('should call api.pdf.cancelProcessing when confirmed', async () => {
      const user = userEvent.setup();
      mockCancelProcessing.mockResolvedValue(undefined);

      mockUsePdfProcessingProgress.mockReturnValue({
        progress: createMockProgress({ currentStep: 'Chunking', percentComplete: 50 }),
        isLoading: false,
        error: null,
        refetch: mockRefetch,
      });

      render(<PdfProcessingProgressBar {...defaultProps} />);

      // Open dialog
      await user.click(screen.getByTestId('cancel-button'));

      // Confirm cancel
      await user.click(screen.getByRole('button', { name: /sì, cancella/i }));

      await waitFor(() => {
        expect(mockCancelProcessing).toHaveBeenCalledWith(defaultProps.pdfId);
      });
    });

    it('should call onCancel callback when cancellation is confirmed', async () => {
      const user = userEvent.setup();
      const onCancel = vi.fn();
      mockCancelProcessing.mockResolvedValue(undefined);

      mockUsePdfProcessingProgress.mockReturnValue({
        progress: createMockProgress({ currentStep: 'Chunking', percentComplete: 50 }),
        isLoading: false,
        error: null,
        refetch: mockRefetch,
      });

      render(<PdfProcessingProgressBar {...defaultProps} onCancel={onCancel} />);

      // Open dialog
      await user.click(screen.getByTestId('cancel-button'));

      // Confirm cancel
      await user.click(screen.getByRole('button', { name: /sì, cancella/i }));

      await waitFor(() => {
        expect(onCancel).toHaveBeenCalled();
      });
    });

    it('should close dialog when cancel is declined', async () => {
      const user = userEvent.setup();
      mockUsePdfProcessingProgress.mockReturnValue({
        progress: createMockProgress({ currentStep: 'Chunking', percentComplete: 50 }),
        isLoading: false,
        error: null,
        refetch: mockRefetch,
      });

      render(<PdfProcessingProgressBar {...defaultProps} />);

      // Open dialog
      await user.click(screen.getByTestId('cancel-button'));

      // Decline cancel
      await user.click(screen.getByRole('button', { name: /no, continua/i }));

      // Dialog should close
      await waitFor(() => {
        expect(screen.queryByText("Vuoi cancellare l'elaborazione?")).not.toBeInTheDocument();
      });
    });

    it('should not display cancel button when failed', () => {
      mockUsePdfProcessingProgress.mockReturnValue({
        progress: createMockProgress({ currentStep: 'Failed' }),
        isLoading: false,
        error: null,
        refetch: mockRefetch,
      });

      render(<PdfProcessingProgressBar {...defaultProps} />);

      expect(screen.queryByTestId('cancel-button')).not.toBeInTheDocument();
    });
  });

  describe('Accessibility', () => {
    it('should have accessible region role', () => {
      render(<PdfProcessingProgressBar {...defaultProps} />);

      expect(
        screen.getByRole('region', { name: /progresso elaborazione pdf/i })
      ).toBeInTheDocument();
    });

    it('should have aria-live region for progress updates', () => {
      mockUsePdfProcessingProgress.mockReturnValue({
        progress: createMockProgress({ currentStep: 'Chunking', percentComplete: 50 }),
        isLoading: false,
        error: null,
        refetch: mockRefetch,
      });

      render(<PdfProcessingProgressBar {...defaultProps} />);

      const liveRegion = screen.getByRole('region', { name: /progresso elaborazione pdf/i });
      // The time info section should have aria-live
      expect(liveRegion.querySelector('[aria-live="polite"]')).toBeInTheDocument();
    });

    it('should have accessible progress bar label', () => {
      mockUsePdfProcessingProgress.mockReturnValue({
        progress: createMockProgress({ percentComplete: 45 }),
        isLoading: false,
        error: null,
        refetch: mockRefetch,
      });

      render(<PdfProcessingProgressBar {...defaultProps} />);

      expect(screen.getByRole('progressbar')).toHaveAttribute(
        'aria-label',
        'Elaborazione 45% completata'
      );
    });

    it('should have aria-live assertive for error state', () => {
      mockUsePdfProcessingProgress.mockReturnValue({
        progress: createMockProgress({ currentStep: 'Failed' }),
        isLoading: false,
        error: null,
        refetch: mockRefetch,
      });

      render(<PdfProcessingProgressBar {...defaultProps} />);

      const errorAlert = screen.getByRole('alert');
      expect(errorAlert).toHaveAttribute('aria-live', 'assertive');
    });

    it('should have accessible step labels', () => {
      render(<PdfProcessingProgressBar {...defaultProps} />);

      expect(screen.getByText('Caricamento')).toBeInTheDocument();
      expect(screen.getByText('Estrazione')).toBeInTheDocument();
      expect(screen.getByText('Suddivisione')).toBeInTheDocument();
      expect(screen.getByText('Embedding')).toBeInTheDocument();
      expect(screen.getByText('Indicizzazione')).toBeInTheDocument();
      expect(screen.getByText('Completato')).toBeInTheDocument();
    });
  });

  describe('Hook Configuration', () => {
    it('should pass correct pdfId to hook', () => {
      render(<PdfProcessingProgressBar pdfId="test-pdf-id-123" />);

      expect(mockUsePdfProcessingProgress).toHaveBeenCalledWith(
        'test-pdf-id-123',
        expect.objectContaining({
          pollingInterval: 2000,
        })
      );
    });

    it('should pass callbacks to hook', () => {
      const onComplete = vi.fn();
      const onError = vi.fn();

      render(
        <PdfProcessingProgressBar {...defaultProps} onComplete={onComplete} onError={onError} />
      );

      expect(mockUsePdfProcessingProgress).toHaveBeenCalledWith(
        defaultProps.pdfId,
        expect.objectContaining({
          onComplete,
          onError,
        })
      );
    });
  });

  describe('Edge Cases', () => {
    it('should handle very long error messages', () => {
      const longErrorMessage = 'A'.repeat(500);
      mockUsePdfProcessingProgress.mockReturnValue({
        progress: createMockProgress({
          currentStep: 'Failed',
          errorMessage: longErrorMessage,
        }),
        isLoading: false,
        error: null,
        refetch: mockRefetch,
      });

      render(<PdfProcessingProgressBar {...defaultProps} />);

      expect(screen.getByText(longErrorMessage)).toBeInTheDocument();
    });

    it('should handle null error message in failed state', () => {
      mockUsePdfProcessingProgress.mockReturnValue({
        progress: createMockProgress({
          currentStep: 'Failed',
          errorMessage: null,
        }),
        isLoading: false,
        error: null,
        refetch: mockRefetch,
      });

      render(<PdfProcessingProgressBar {...defaultProps} />);

      expect(screen.getByText('Elaborazione fallita')).toBeInTheDocument();
      // Should not display error message text
      expect(screen.queryByText('null')).not.toBeInTheDocument();
    });

    it('should handle rapid progress updates', () => {
      const { rerender } = render(<PdfProcessingProgressBar {...defaultProps} />);

      // Simulate rapid progress updates
      for (let i = 0; i <= 100; i += 10) {
        mockUsePdfProcessingProgress.mockReturnValue({
          progress: createMockProgress({ percentComplete: i }),
          isLoading: false,
          error: null,
          refetch: mockRefetch,
        });
        rerender(<PdfProcessingProgressBar {...defaultProps} />);
      }

      expect(screen.getByText('100%')).toBeInTheDocument();
    });
  });
});
