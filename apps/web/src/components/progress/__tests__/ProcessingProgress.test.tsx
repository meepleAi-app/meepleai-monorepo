/**
 * ProcessingProgress Component Tests (PDF-08)
 *
 * Test Coverage:
 * - Loading state display
 * - Progress bar and step indicators
 * - Polling and API integration
 * - Cancel functionality
 * - Completion and error callbacks
 * - Network error handling
 *
 * Updated for Issue #3371: Schema alignment with backend ProcessingProgress model.
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, waitFor, fireEvent } from '@testing-library/react';

import { ProcessingProgress } from '../ProcessingProgress';
import { api } from '@/lib/api';
import { ProcessingStep } from '@/types/pdf';

// Mock the API module
vi.mock('@/lib/api', () => ({
  api: {
    pdf: {
      getProcessingProgress: vi.fn(),
      cancelProcessing: vi.fn(),
    },
  },
}));

// Mock SkeletonLoader
vi.mock('../../loading', () => ({
  SkeletonLoader: ({ ariaLabel }: { ariaLabel: string }) => (
    <div data-testid="skeleton-loader" aria-label={ariaLabel}>
      Loading...
    </div>
  ),
}));

/**
 * Helper to create mock API response matching backend ProcessingProgress model.
 * Issue #3371: Schema alignment with backend.
 */
function createMockProgress(overrides: {
  currentStep?: string;
  percentComplete?: number;
  errorMessage?: string | null;
  pagesProcessed?: number;
  totalPages?: number;
} = {}) {
  return {
    currentStep: overrides.currentStep ?? 'Extracting',
    percentComplete: overrides.percentComplete ?? 50,
    elapsedTime: '00:01:23.0000000',
    estimatedTimeRemaining: '00:02:00.0000000',
    pagesProcessed: overrides.pagesProcessed ?? 5,
    totalPages: overrides.totalPages ?? 10,
    startedAt: '2026-02-01T10:30:00.000Z',
    completedAt: null,
    errorMessage: overrides.errorMessage ?? null,
  };
}

describe('ProcessingProgress', () => {
  const mockOnComplete = vi.fn();
  const mockOnError = vi.fn();
  const defaultPdfId = 'test-pdf-123';

  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(() => {
    vi.clearAllTimers();
  });

  describe('Loading State', () => {
    it('shows skeleton loader while initial fetch is in progress', () => {
      // Never resolve the API call
      vi.mocked(api.pdf.getProcessingProgress).mockImplementation(
        () => new Promise(() => {})
      );

      render(<ProcessingProgress pdfId={defaultPdfId} />);

      expect(screen.getByTestId('skeleton-loader')).toBeInTheDocument();
    });

    it('removes skeleton loader after data is fetched', async () => {
      vi.mocked(api.pdf.getProcessingProgress).mockResolvedValue(
        createMockProgress({ currentStep: 'Extracting', percentComplete: 50 })
      );

      render(<ProcessingProgress pdfId={defaultPdfId} />);

      await waitFor(() => {
        expect(screen.queryByTestId('skeleton-loader')).not.toBeInTheDocument();
      });
    });
  });

  describe('Progress Display', () => {
    it('displays progress bar with correct percentage', async () => {
      vi.mocked(api.pdf.getProcessingProgress).mockResolvedValue(
        createMockProgress({ currentStep: 'Chunking', percentComplete: 75 })
      );

      render(<ProcessingProgress pdfId={defaultPdfId} />);

      await waitFor(() => {
        const progressBar = screen.getByRole('progressbar');
        expect(progressBar).toHaveAttribute('aria-valuenow', '75');
      });
    });

    it('displays all step indicators', async () => {
      vi.mocked(api.pdf.getProcessingProgress).mockResolvedValue(
        createMockProgress({ currentStep: 'Extracting', percentComplete: 30 })
      );

      render(<ProcessingProgress pdfId={defaultPdfId} />);

      await waitFor(() => {
        expect(screen.getByText(ProcessingStep.Uploading)).toBeInTheDocument();
        expect(screen.getByText(ProcessingStep.Extracting)).toBeInTheDocument();
        expect(screen.getByText(ProcessingStep.Chunking)).toBeInTheDocument();
        expect(screen.getByText(ProcessingStep.Embedding)).toBeInTheDocument();
        expect(screen.getByText(ProcessingStep.Indexing)).toBeInTheDocument();
      });
    });

    it('shows processing status text', async () => {
      vi.mocked(api.pdf.getProcessingProgress).mockResolvedValue(
        createMockProgress({ currentStep: 'Chunking', percentComplete: 50 })
      );

      render(<ProcessingProgress pdfId={defaultPdfId} />);

      await waitFor(() => {
        expect(screen.getByText(/processing status/i)).toBeInTheDocument();
        // Check that progress percentage is displayed (text is split across elements)
        expect(screen.getByText('50', { exact: false })).toBeInTheDocument();
      });
    });

    it('displays correct progress percentage', async () => {
      vi.mocked(api.pdf.getProcessingProgress).mockResolvedValue(
        createMockProgress({ currentStep: 'Embedding', percentComplete: 42 })
      );

      render(<ProcessingProgress pdfId={defaultPdfId} />);

      await waitFor(() => {
        expect(screen.getByText(/42%/)).toBeInTheDocument();
      });
    });
  });

  describe('Completion Callback', () => {
    it('calls onComplete when processing is completed', async () => {
      vi.mocked(api.pdf.getProcessingProgress).mockResolvedValue({
        ...createMockProgress({ currentStep: 'Completed', percentComplete: 100 }),
        completedAt: '2026-02-01T10:35:00.000Z',
      });

      render(
        <ProcessingProgress
          pdfId={defaultPdfId}
          onComplete={mockOnComplete}
        />
      );

      await waitFor(() => {
        expect(mockOnComplete).toHaveBeenCalledTimes(1);
      });
    });

    it('only calls onComplete once on initial render', async () => {
      vi.mocked(api.pdf.getProcessingProgress).mockResolvedValue({
        ...createMockProgress({ currentStep: 'Completed', percentComplete: 100 }),
        completedAt: '2026-02-01T10:35:00.000Z',
      });

      render(
        <ProcessingProgress
          pdfId={defaultPdfId}
          onComplete={mockOnComplete}
        />
      );

      await waitFor(() => {
        expect(mockOnComplete).toHaveBeenCalled();
      });

      // Verify it was called exactly once
      expect(mockOnComplete).toHaveBeenCalledTimes(1);
    });
  });

  describe('Error Callback', () => {
    it('calls onError when processing fails with error message', async () => {
      const errorMessage = 'PDF extraction failed';
      vi.mocked(api.pdf.getProcessingProgress).mockResolvedValue(
        createMockProgress({ currentStep: 'Failed', percentComplete: 0, errorMessage })
      );

      render(
        <ProcessingProgress
          pdfId={defaultPdfId}
          onError={mockOnError}
        />
      );

      await waitFor(() => {
        expect(mockOnError).toHaveBeenCalledWith(errorMessage);
      });
    });

    it('displays error message when processing fails', async () => {
      vi.mocked(api.pdf.getProcessingProgress).mockResolvedValue(
        createMockProgress({ currentStep: 'Failed', percentComplete: 0, errorMessage: 'OCR failed for this document' })
      );

      render(<ProcessingProgress pdfId={defaultPdfId} />);

      await waitFor(() => {
        expect(screen.getByText(/OCR failed for this document/)).toBeInTheDocument();
      });
    });
  });

  describe('Network Error Handling', () => {
    it('displays network error when API call fails', async () => {
      vi.mocked(api.pdf.getProcessingProgress).mockRejectedValue(
        new Error('Network connection failed')
      );

      render(<ProcessingProgress pdfId={defaultPdfId} />);

      await waitFor(() => {
        expect(screen.getByText(/network error/i)).toBeInTheDocument();
        expect(screen.getByText(/Network connection failed/)).toBeInTheDocument();
      });
    });

    it('displays generic error for unknown errors', async () => {
      vi.mocked(api.pdf.getProcessingProgress).mockRejectedValue('unknown error');

      render(<ProcessingProgress pdfId={defaultPdfId} />);

      await waitFor(() => {
        expect(screen.getByText(/Unknown error/)).toBeInTheDocument();
      });
    });

    it('displays network error when API returns null', async () => {
      vi.mocked(api.pdf.getProcessingProgress).mockResolvedValue(null as any);

      render(<ProcessingProgress pdfId={defaultPdfId} />);

      await waitFor(() => {
        expect(screen.getByText(/Failed to fetch processing progress/)).toBeInTheDocument();
      });
    });
  });

  describe('Cancel Functionality', () => {
    it('shows cancel button during processing', async () => {
      vi.mocked(api.pdf.getProcessingProgress).mockResolvedValue(
        createMockProgress({ currentStep: 'Extracting', percentComplete: 50 })
      );

      render(<ProcessingProgress pdfId={defaultPdfId} />);

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /cancel pdf processing/i })).toBeInTheDocument();
      });
    });

    it('hides cancel button when processing is complete', async () => {
      vi.mocked(api.pdf.getProcessingProgress).mockResolvedValue({
        ...createMockProgress({ currentStep: 'Completed', percentComplete: 100 }),
        completedAt: '2026-02-01T10:35:00.000Z',
      });

      render(<ProcessingProgress pdfId={defaultPdfId} />);

      await waitFor(() => {
        expect(screen.queryByRole('button', { name: /cancel/i })).not.toBeInTheDocument();
      });
    });

    it('opens cancel dialog when cancel button is clicked', async () => {
      vi.mocked(api.pdf.getProcessingProgress).mockResolvedValue(
        createMockProgress({ currentStep: 'Chunking', percentComplete: 50 })
      );

      render(<ProcessingProgress pdfId={defaultPdfId} />);

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /cancel pdf processing/i })).toBeInTheDocument();
      });

      fireEvent.click(screen.getByRole('button', { name: /cancel pdf processing/i }));

      expect(screen.getByRole('dialog')).toBeInTheDocument();
      expect(screen.getByText(/cancel pdf processing\?/i)).toBeInTheDocument();
    });

    it('closes cancel dialog when "No, Continue" is clicked', async () => {
      vi.mocked(api.pdf.getProcessingProgress).mockResolvedValue(
        createMockProgress({ currentStep: 'Embedding', percentComplete: 50 })
      );

      render(<ProcessingProgress pdfId={defaultPdfId} />);

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /cancel pdf processing/i })).toBeInTheDocument();
      });

      fireEvent.click(screen.getByRole('button', { name: /cancel pdf processing/i }));
      fireEvent.click(screen.getByText(/no, continue processing/i));

      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    });

    it('closes cancel dialog when overlay is clicked', async () => {
      vi.mocked(api.pdf.getProcessingProgress).mockResolvedValue(
        createMockProgress({ currentStep: 'Indexing', percentComplete: 50 })
      );

      render(<ProcessingProgress pdfId={defaultPdfId} />);

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /cancel pdf processing/i })).toBeInTheDocument();
      });

      fireEvent.click(screen.getByRole('button', { name: /cancel pdf processing/i }));

      // Click the overlay (dialog backdrop)
      const dialog = screen.getByRole('dialog');
      fireEvent.click(dialog);

      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    });

    it('calls cancel API when "Yes, Cancel" is clicked', async () => {
      vi.mocked(api.pdf.getProcessingProgress).mockResolvedValue(
        createMockProgress({ currentStep: 'Extracting', percentComplete: 50 })
      );
      vi.mocked(api.pdf.cancelProcessing).mockResolvedValue(undefined);

      render(<ProcessingProgress pdfId={defaultPdfId} />);

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /cancel pdf processing/i })).toBeInTheDocument();
      });

      fireEvent.click(screen.getByRole('button', { name: /cancel pdf processing/i }));
      fireEvent.click(screen.getByText(/yes, cancel/i));

      await waitFor(() => {
        expect(api.pdf.cancelProcessing).toHaveBeenCalledWith(defaultPdfId);
      });
    });

    it('shows "Canceling..." while cancel is in progress', async () => {
      vi.mocked(api.pdf.getProcessingProgress).mockResolvedValue(
        createMockProgress({ currentStep: 'Chunking', percentComplete: 50 })
      );
      // Never resolve cancel API
      vi.mocked(api.pdf.cancelProcessing).mockImplementation(
        () => new Promise(() => {})
      );

      render(<ProcessingProgress pdfId={defaultPdfId} />);

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /cancel pdf processing/i })).toBeInTheDocument();
      });

      fireEvent.click(screen.getByRole('button', { name: /cancel pdf processing/i }));
      fireEvent.click(screen.getByText(/yes, cancel/i));

      await waitFor(() => {
        expect(screen.getByText(/canceling\.\.\./i)).toBeInTheDocument();
      });
    });

    it('displays error when cancel fails', async () => {
      vi.mocked(api.pdf.getProcessingProgress).mockResolvedValue(
        createMockProgress({ currentStep: 'Embedding', percentComplete: 50 })
      );
      vi.mocked(api.pdf.cancelProcessing).mockRejectedValue(
        new Error('Cancel request failed')
      );

      render(<ProcessingProgress pdfId={defaultPdfId} />);

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /cancel pdf processing/i })).toBeInTheDocument();
      });

      fireEvent.click(screen.getByRole('button', { name: /cancel pdf processing/i }));
      fireEvent.click(screen.getByText(/yes, cancel/i));

      await waitFor(() => {
        expect(screen.getByText(/Cancel request failed/)).toBeInTheDocument();
      });
    });
  });

  describe('Polling Behavior', () => {
    it('calls API on initial render', async () => {
      vi.mocked(api.pdf.getProcessingProgress).mockResolvedValue(
        createMockProgress({ currentStep: 'Uploading', percentComplete: 25 })
      );

      render(<ProcessingProgress pdfId={defaultPdfId} />);

      await waitFor(() => {
        expect(api.pdf.getProcessingProgress).toHaveBeenCalledWith(defaultPdfId);
      });
    });

    it('resets state when pdfId changes', async () => {
      vi.mocked(api.pdf.getProcessingProgress).mockResolvedValue(
        createMockProgress({ currentStep: 'Extracting', percentComplete: 50 })
      );

      const { rerender } = render(<ProcessingProgress pdfId="pdf-1" />);

      await waitFor(() => {
        expect(api.pdf.getProcessingProgress).toHaveBeenCalledWith('pdf-1');
      });

      rerender(<ProcessingProgress pdfId="pdf-2" />);

      await waitFor(() => {
        expect(api.pdf.getProcessingProgress).toHaveBeenCalledWith('pdf-2');
      });
    });
  });

  describe('Component Header', () => {
    it('displays the component title', async () => {
      vi.mocked(api.pdf.getProcessingProgress).mockResolvedValue(
        createMockProgress({ currentStep: 'Chunking', percentComplete: 50 })
      );

      render(<ProcessingProgress pdfId={defaultPdfId} />);

      await waitFor(() => {
        expect(screen.getByText('PDF Processing Progress')).toBeInTheDocument();
      });
    });
  });

  describe('Accessibility', () => {
    it('has accessible progress bar', async () => {
      vi.mocked(api.pdf.getProcessingProgress).mockResolvedValue(
        createMockProgress({ currentStep: 'Embedding', percentComplete: 60 })
      );

      render(<ProcessingProgress pdfId={defaultPdfId} />);

      await waitFor(() => {
        const progressBar = screen.getByRole('progressbar');
        expect(progressBar).toHaveAttribute('aria-label', 'PDF processing progress');
        expect(progressBar).toHaveAttribute('aria-valuemin', '0');
        expect(progressBar).toHaveAttribute('aria-valuemax', '100');
        expect(progressBar).toHaveAttribute('aria-valuenow', '60');
        expect(progressBar).toHaveAttribute('aria-live', 'polite');
      });
    });

    it('error messages have role="alert"', async () => {
      vi.mocked(api.pdf.getProcessingProgress).mockResolvedValue(
        createMockProgress({ currentStep: 'Failed', percentComplete: 0, errorMessage: 'Processing failed' })
      );

      render(<ProcessingProgress pdfId={defaultPdfId} />);

      await waitFor(() => {
        const alerts = screen.getAllByRole('alert');
        expect(alerts.length).toBeGreaterThan(0);
      });
    });

    it('cancel dialog is properly labeled', async () => {
      vi.mocked(api.pdf.getProcessingProgress).mockResolvedValue(
        createMockProgress({ currentStep: 'Indexing', percentComplete: 50 })
      );

      render(<ProcessingProgress pdfId={defaultPdfId} />);

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /cancel pdf processing/i })).toBeInTheDocument();
      });

      fireEvent.click(screen.getByRole('button', { name: /cancel pdf processing/i }));

      const dialog = screen.getByRole('dialog');
      expect(dialog).toHaveAttribute('aria-modal', 'true');
      expect(dialog).toHaveAttribute('aria-labelledby', 'cancel-dialog-title');
    });
  });
});
