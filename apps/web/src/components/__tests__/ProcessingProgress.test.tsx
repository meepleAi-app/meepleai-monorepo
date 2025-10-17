/**
 * ProcessingProgress Component Tests (PDF-08)
 * Comprehensive unit tests for PDF processing progress tracking component
 */

import { render, screen, waitFor, act } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ProcessingProgress } from '../ProcessingProgress';
import { ProcessingStep } from '../../types/pdf';
import { api } from '../../lib/api';

// Mock the API
jest.mock('../../lib/api', () => ({
  api: {
    pdf: {
      getProcessingProgress: jest.fn(),
      cancelProcessing: jest.fn()
    }
  }
}));

const mockGetProgress = api.pdf.getProcessingProgress as jest.MockedFunction<
  typeof api.pdf.getProcessingProgress
>;
const mockCancelProcessing = api.pdf.cancelProcessing as jest.MockedFunction<
  typeof api.pdf.cancelProcessing
>;

describe('ProcessingProgress', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    jest.useFakeTimers();
  });

  afterEach(() => {
    jest.runOnlyPendingTimers();
    jest.useRealTimers();
  });

  describe('Rendering', () => {
    it('should render loading state initially', async () => {
      mockGetProgress.mockResolvedValue(null);

      render(<ProcessingProgress pdfId="test-pdf-id" />);

      expect(screen.getByText(/processing pdf/i)).toBeInTheDocument();
      expect(screen.getByText(/loading progress information/i)).toBeInTheDocument();
    });

    it('should render progress bar with correct percentage', async () => {
      mockGetProgress.mockResolvedValue({
        currentStep: ProcessingStep.Extracting,
        percentComplete: 45,
        estimatedTimeRemaining: 120,
        updatedAt: new Date().toISOString()
      });

      render(<ProcessingProgress pdfId="test-pdf-id" />);

      await waitFor(() => {
        const progressBar = screen.getByRole('progressbar');
        expect(progressBar).toHaveAttribute('aria-valuenow', '45');
      });
    });

    it('should render all processing steps', async () => {
      mockGetProgress.mockResolvedValue({
        currentStep: ProcessingStep.Chunking,
        percentComplete: 50,
        updatedAt: new Date().toISOString()
      });

      render(<ProcessingProgress pdfId="test-pdf-id" />);

      await waitFor(() => {
        expect(screen.getByText('Uploading')).toBeInTheDocument();
        expect(screen.getByText('Extracting')).toBeInTheDocument();
        expect(screen.getByText('Chunking')).toBeInTheDocument();
        expect(screen.getByText('Embedding')).toBeInTheDocument();
        expect(screen.getByText('Indexing')).toBeInTheDocument();
      });
    });

    it('should render current status text', async () => {
      mockGetProgress.mockResolvedValue({
        currentStep: ProcessingStep.Embedding,
        percentComplete: 75,
        updatedAt: new Date().toISOString()
      });

      render(<ProcessingProgress pdfId="test-pdf-id" />);

      await waitFor(() => {
        expect(screen.getByText(/status:/i)).toBeInTheDocument();
        expect(screen.getByText(/generating embeddings/i)).toBeInTheDocument();
      });
    });
  });

  describe('Time Formatting', () => {
    it('should format time remaining correctly for seconds only', async () => {
      mockGetProgress.mockResolvedValue({
        currentStep: ProcessingStep.Extracting,
        percentComplete: 30,
        estimatedTimeRemaining: 45,
        updatedAt: new Date().toISOString()
      });

      render(<ProcessingProgress pdfId="test-pdf-id" />);

      await waitFor(() => {
        expect(screen.getByText(/45 sec/i)).toBeInTheDocument();
      });
    });

    it('should format time remaining correctly for minutes only', async () => {
      mockGetProgress.mockResolvedValue({
        currentStep: ProcessingStep.Embedding,
        percentComplete: 60,
        estimatedTimeRemaining: 120,
        updatedAt: new Date().toISOString()
      });

      render(<ProcessingProgress pdfId="test-pdf-id" />);

      await waitFor(() => {
        expect(screen.getByText(/2 min/i)).toBeInTheDocument();
      });
    });

    it('should format time remaining correctly for minutes and seconds', async () => {
      mockGetProgress.mockResolvedValue({
        currentStep: ProcessingStep.Indexing,
        percentComplete: 85,
        estimatedTimeRemaining: 150,
        updatedAt: new Date().toISOString()
      });

      render(<ProcessingProgress pdfId="test-pdf-id" />);

      await waitFor(() => {
        expect(screen.getByText(/2 min 30 sec/i)).toBeInTheDocument();
      });
    });

    it('should show "Less than a minute" for zero or negative time', async () => {
      mockGetProgress.mockResolvedValue({
        currentStep: ProcessingStep.Indexing,
        percentComplete: 95,
        estimatedTimeRemaining: 0,
        updatedAt: new Date().toISOString()
      });

      render(<ProcessingProgress pdfId="test-pdf-id" />);

      await waitFor(() => {
        expect(screen.getByText(/less than a minute/i)).toBeInTheDocument();
      });
    });

    it('should not show time remaining when undefined', async () => {
      mockGetProgress.mockResolvedValue({
        currentStep: ProcessingStep.Uploading,
        percentComplete: 10,
        updatedAt: new Date().toISOString()
      });

      render(<ProcessingProgress pdfId="test-pdf-id" />);

      await waitFor(() => {
        expect(screen.queryByText(/estimated time remaining/i)).not.toBeInTheDocument();
      });
    });
  });

  describe('Polling Behavior', () => {
    it('should poll for progress every 2 seconds', async () => {
      mockGetProgress.mockResolvedValue({
        currentStep: ProcessingStep.Extracting,
        percentComplete: 30,
        updatedAt: new Date().toISOString()
      });

      render(<ProcessingProgress pdfId="test-pdf-id" />);

      await waitFor(() => {
        expect(mockGetProgress).toHaveBeenCalledTimes(1);
      });

      // Advance timer by 2 seconds
      act(() => {
        jest.advanceTimersByTime(2000);
      });

      await waitFor(() => {
        expect(mockGetProgress).toHaveBeenCalledTimes(2);
      });

      // Advance another 2 seconds
      act(() => {
        jest.advanceTimersByTime(2000);
      });

      await waitFor(() => {
        expect(mockGetProgress).toHaveBeenCalledTimes(3);
      });
    });

    it('should stop polling when completed', async () => {
      mockGetProgress
        .mockResolvedValueOnce({
          currentStep: ProcessingStep.Indexing,
          percentComplete: 90,
          updatedAt: new Date().toISOString()
        })
        .mockResolvedValueOnce({
          currentStep: ProcessingStep.Completed,
          percentComplete: 100,
          updatedAt: new Date().toISOString()
        });

      const onComplete = jest.fn();
      render(<ProcessingProgress pdfId="test-pdf-id" onComplete={onComplete} />);

      await waitFor(() => {
        expect(mockGetProgress).toHaveBeenCalledTimes(1);
      });

      await act(async () => {
        jest.advanceTimersByTime(2000);
        await Promise.resolve();
      });

      await waitFor(() => {
        expect(mockGetProgress).toHaveBeenCalledTimes(2);
        expect(onComplete).toHaveBeenCalledTimes(1);
      });

      // Should not poll again after completion
      await act(async () => {
        jest.advanceTimersByTime(2000);
        await Promise.resolve();
      });

      // Still should be 2 calls
      expect(mockGetProgress).toHaveBeenCalledTimes(2);
    });

    it('should stop polling when failed', async () => {
      mockGetProgress.mockResolvedValue({
        currentStep: ProcessingStep.Failed,
        percentComplete: 50,
        errorMessage: 'Processing failed due to invalid PDF',
        updatedAt: new Date().toISOString()
      });

      const onError = jest.fn();
      render(<ProcessingProgress pdfId="test-pdf-id" onError={onError} />);

      await waitFor(() => {
        expect(mockGetProgress).toHaveBeenCalledTimes(1);
        expect(onError).toHaveBeenCalledWith('Processing failed due to invalid PDF');
      });

      // Should not poll again after failure
      await act(async () => {
        jest.advanceTimersByTime(2000);
        await Promise.resolve();
      });

      // Still should be 1 call
      expect(mockGetProgress).toHaveBeenCalledTimes(1);
    });

    it('should cleanup polling on unmount', async () => {
      mockGetProgress.mockResolvedValue({
        currentStep: ProcessingStep.Extracting,
        percentComplete: 30,
        updatedAt: new Date().toISOString()
      });

      const { unmount } = render(<ProcessingProgress pdfId="test-pdf-id" />);

      await waitFor(() => {
        expect(mockGetProgress).toHaveBeenCalledTimes(1);
      });

      unmount();

      // Advance timer after unmount
      act(() => {
        jest.advanceTimersByTime(2000);
      });

      // Should not make additional calls after unmount
      expect(mockGetProgress).toHaveBeenCalledTimes(1);
    });
  });

  describe('Error Handling', () => {
    it('should display error message when failed', async () => {
      mockGetProgress.mockResolvedValue({
        currentStep: ProcessingStep.Failed,
        percentComplete: 40,
        errorMessage: 'PDF extraction failed',
        updatedAt: new Date().toISOString()
      });

      render(<ProcessingProgress pdfId="test-pdf-id" />);

      await waitFor(() => {
        expect(screen.getByRole('alert')).toHaveTextContent(/pdf extraction failed/i);
      });
    });

    it('should display network error when API call fails', async () => {
      mockGetProgress.mockRejectedValue(new Error('Network timeout'));

      render(<ProcessingProgress pdfId="test-pdf-id" />);

      await waitFor(() => {
        const alerts = screen.getAllByRole('alert');
        const hasNetworkError = alerts.some(alert => alert.textContent?.match(/network timeout/i));
        expect(hasNetworkError).toBe(true);
      });
    });

    it('should call onError callback when processing fails', async () => {
      const onError = jest.fn();
      mockGetProgress.mockResolvedValue({
        currentStep: ProcessingStep.Failed,
        percentComplete: 50,
        errorMessage: 'Invalid file format',
        updatedAt: new Date().toISOString()
      });

      render(<ProcessingProgress pdfId="test-pdf-id" onError={onError} />);

      await waitFor(() => {
        expect(onError).toHaveBeenCalledWith('Invalid file format');
      });
    });
  });

  describe('Cancel Functionality', () => {
    it('should show cancel button when not in terminal state', async () => {
      mockGetProgress.mockResolvedValue({
        currentStep: ProcessingStep.Extracting,
        percentComplete: 40,
        updatedAt: new Date().toISOString()
      });

      render(<ProcessingProgress pdfId="test-pdf-id" />);

      await waitFor(() => {
        expect(screen.getByText(/cancel processing/i)).toBeInTheDocument();
      });
    });

    it('should not show cancel button when completed', async () => {
      mockGetProgress.mockResolvedValue({
        currentStep: ProcessingStep.Completed,
        percentComplete: 100,
        updatedAt: new Date().toISOString()
      });

      render(<ProcessingProgress pdfId="test-pdf-id" />);

      await waitFor(() => {
        expect(screen.queryByText(/cancel processing/i)).not.toBeInTheDocument();
      });
    });

    it('should not show cancel button when failed', async () => {
      mockGetProgress.mockResolvedValue({
        currentStep: ProcessingStep.Failed,
        percentComplete: 50,
        errorMessage: 'Failed',
        updatedAt: new Date().toISOString()
      });

      render(<ProcessingProgress pdfId="test-pdf-id" />);

      await waitFor(() => {
        expect(screen.queryByText(/cancel processing/i)).not.toBeInTheDocument();
      });
    });

    it('should show confirmation dialog when cancel is clicked', async () => {
      const user = userEvent.setup({ delay: null });
      mockGetProgress.mockResolvedValue({
        currentStep: ProcessingStep.Embedding,
        percentComplete: 60,
        updatedAt: new Date().toISOString()
      });

      render(<ProcessingProgress pdfId="test-pdf-id" />);

      await waitFor(() => {
        expect(screen.getByText(/cancel processing/i)).toBeInTheDocument();
      });

      await user.click(screen.getByText(/cancel processing/i));

      expect(screen.getByRole('dialog')).toBeInTheDocument();
      expect(screen.getByText(/cancel pdf processing/i)).toBeInTheDocument();
      expect(screen.getByText(/yes, cancel/i)).toBeInTheDocument();
      expect(screen.getByText(/no, continue processing/i)).toBeInTheDocument();
    });

    it('should close dialog when "No, Continue Processing" is clicked', async () => {
      const user = userEvent.setup({ delay: null });
      mockGetProgress.mockResolvedValue({
        currentStep: ProcessingStep.Chunking,
        percentComplete: 50,
        updatedAt: new Date().toISOString()
      });

      render(<ProcessingProgress pdfId="test-pdf-id" />);

      await waitFor(() => {
        expect(screen.getByText(/cancel processing/i)).toBeInTheDocument();
      });

      await user.click(screen.getByText(/cancel processing/i));
      expect(screen.getByRole('dialog')).toBeInTheDocument();

      await user.click(screen.getByText(/no, continue processing/i));

      await waitFor(() => {
        expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
      });
    });

    it('should call cancel API when "Yes, Cancel" is clicked', async () => {
      const user = userEvent.setup({ delay: null });
      mockGetProgress.mockResolvedValue({
        currentStep: ProcessingStep.Indexing,
        percentComplete: 80,
        updatedAt: new Date().toISOString()
      });
      mockCancelProcessing.mockResolvedValue();

      render(<ProcessingProgress pdfId="test-pdf-id" />);

      await waitFor(() => {
        expect(screen.getByText(/cancel processing/i)).toBeInTheDocument();
      });

      await user.click(screen.getByText(/cancel processing/i));
      await user.click(screen.getByText(/yes, cancel/i));

      await waitFor(() => {
        expect(mockCancelProcessing).toHaveBeenCalledWith('test-pdf-id');
        expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
      });
    });

    it('should fetch updated progress after cancel', async () => {
      const user = userEvent.setup({ delay: null });
      mockGetProgress
        .mockResolvedValueOnce({
          currentStep: ProcessingStep.Extracting,
          percentComplete: 30,
          updatedAt: new Date().toISOString()
        })
        .mockResolvedValueOnce({
          currentStep: ProcessingStep.Failed,
          percentComplete: 30,
          errorMessage: 'Cancelled by user',
          updatedAt: new Date().toISOString()
        });
      mockCancelProcessing.mockResolvedValue();

      render(<ProcessingProgress pdfId="test-pdf-id" />);

      await waitFor(() => {
        expect(screen.getByText(/cancel processing/i)).toBeInTheDocument();
      });

      await user.click(screen.getByText(/cancel processing/i));

      await act(async () => {
        await user.click(screen.getByText(/yes, cancel/i));
      });

      await waitFor(() => {
        expect(mockCancelProcessing).toHaveBeenCalled();
        expect(mockGetProgress).toHaveBeenCalledTimes(2);
      }, { timeout: 5000 });
    });

    it('should handle cancel API failure gracefully', async () => {
      const user = userEvent.setup({ delay: null });
      mockGetProgress.mockResolvedValue({
        currentStep: ProcessingStep.Chunking,
        percentComplete: 55,
        updatedAt: new Date().toISOString()
      });
      mockCancelProcessing.mockRejectedValue(new Error('Cancel failed'));

      render(<ProcessingProgress pdfId="test-pdf-id" />);

      await waitFor(() => {
        expect(screen.getByText(/cancel processing/i)).toBeInTheDocument();
      });

      await user.click(screen.getByText(/cancel processing/i));
      await user.click(screen.getByText(/yes, cancel/i));

      await waitFor(() => {
        expect(screen.getByRole('alert')).toHaveTextContent(/cancel failed/i);
      });
    });
  });

  describe('Callbacks', () => {
    it('should call onComplete when processing completes', async () => {
      const onComplete = jest.fn();
      mockGetProgress.mockResolvedValue({
        currentStep: ProcessingStep.Completed,
        percentComplete: 100,
        updatedAt: new Date().toISOString()
      });

      render(<ProcessingProgress pdfId="test-pdf-id" onComplete={onComplete} />);

      await waitFor(() => {
        expect(mockGetProgress).toHaveBeenCalled();
        expect(onComplete).toHaveBeenCalledTimes(1);
      }, { timeout: 5000 });
    });

    it('should not call onComplete when processing is incomplete', async () => {
      const onComplete = jest.fn();
      mockGetProgress.mockResolvedValue({
        currentStep: ProcessingStep.Embedding,
        percentComplete: 70,
        updatedAt: new Date().toISOString()
      });

      render(<ProcessingProgress pdfId="test-pdf-id" onComplete={onComplete} />);

      await waitFor(() => {
        expect(mockGetProgress).toHaveBeenCalled();
      });

      expect(onComplete).not.toHaveBeenCalled();
    });

    it('should not call onError when processing is successful', async () => {
      const onError = jest.fn();
      mockGetProgress.mockResolvedValue({
        currentStep: ProcessingStep.Completed,
        percentComplete: 100,
        updatedAt: new Date().toISOString()
      });

      render(<ProcessingProgress pdfId="test-pdf-id" onError={onError} />);

      await waitFor(() => {
        expect(mockGetProgress).toHaveBeenCalled();
      });

      expect(onError).not.toHaveBeenCalled();
    });
  });

  describe('Accessibility', () => {
    it('should have proper ARIA attributes on progress bar', async () => {
      mockGetProgress.mockResolvedValue({
        currentStep: ProcessingStep.Extracting,
        percentComplete: 35,
        updatedAt: new Date().toISOString()
      });

      render(<ProcessingProgress pdfId="test-pdf-id" />);

      await waitFor(() => {
        const progressBar = screen.getByRole('progressbar');
        expect(progressBar).toHaveAttribute('aria-label', 'PDF processing progress');
        expect(progressBar).toHaveAttribute('aria-valuenow', '35');
        expect(progressBar).toHaveAttribute('aria-valuemin', '0');
        expect(progressBar).toHaveAttribute('aria-valuemax', '100');
        expect(progressBar).toHaveAttribute('aria-live', 'polite');
      });
    });

    it('should have proper ARIA attributes on dialog', async () => {
      const user = userEvent.setup({ delay: null });
      mockGetProgress.mockResolvedValue({
        currentStep: ProcessingStep.Embedding,
        percentComplete: 60,
        updatedAt: new Date().toISOString()
      });

      render(<ProcessingProgress pdfId="test-pdf-id" />);

      await waitFor(() => {
        expect(screen.getByText(/cancel processing/i)).toBeInTheDocument();
      });

      await user.click(screen.getByText(/cancel processing/i));

      const dialog = screen.getByRole('dialog');
      expect(dialog).toHaveAttribute('aria-modal', 'true');
      expect(dialog).toHaveAttribute('aria-labelledby', 'cancel-dialog-title');
    });

    it('should have role="alert" on error messages', async () => {
      mockGetProgress.mockResolvedValue({
        currentStep: ProcessingStep.Failed,
        percentComplete: 40,
        errorMessage: 'Processing error',
        updatedAt: new Date().toISOString()
      });

      render(<ProcessingProgress pdfId="test-pdf-id" />);

      await waitFor(() => {
        const alert = screen.getByRole('alert');
        expect(alert).toHaveTextContent(/processing error/i);
      });
    });
  });
});
