/**
 * ProcessingProgress Component Tests (PDF-08) - Fixed for act() warnings
 * Key fixes:
 * 1. Use waitFor for ALL assertions that depend on async state
 * 2. Flush promises after advancing timers: await act(async () => { jest.advanceTimersByTime(ms); await Promise.resolve(); })
 * 3. Don't wrap render() in act() - RTL handles this automatically
 * 4. Ensure all async operations complete before test ends
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

  afterEach(async () => {
    // Ensure all async operations complete
    await act(async () => {
      jest.runOnlyPendingTimers();
      await Promise.resolve();
    });
    jest.useRealTimers();
  });

  describe('Rendering', () => {
    it('should render loading state initially', async () => {
      mockGetProgress.mockResolvedValue(null);

      render(<ProcessingProgress pdfId="test-pdf-id" />);

      // Wait for initial fetch to complete
      await waitFor(() => {
        expect(mockGetProgress).toHaveBeenCalledTimes(1);
      });

      // All assertions in waitFor to handle async state updates
      await waitFor(() => {
        const skeleton = screen.getByLabelText(/Loading processing progress/i);
        expect(skeleton).toBeInTheDocument();
      });
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

      // Advance timer and flush promises
      await act(async () => {
        jest.advanceTimersByTime(2000);
        await Promise.resolve();
      });

      await waitFor(() => {
        expect(mockGetProgress).toHaveBeenCalledTimes(2);
      });

      // Advance another 2 seconds
      await act(async () => {
        jest.advanceTimersByTime(2000);
        await Promise.resolve();
      });

      await waitFor(() => {
        expect(mockGetProgress).toHaveBeenCalledTimes(3);
      });
    });
  });

  describe('Error Handling', () => {
    it('should display network error when API call fails', async () => {
      mockGetProgress.mockRejectedValue(new Error('Network timeout'));

      render(<ProcessingProgress pdfId="test-pdf-id" />);

      // Wait for error to be displayed
      await waitFor(
        () => {
          const alerts = screen.getAllByRole('alert');
          const networkAlert = alerts.find(alert =>
            alert.textContent?.toLowerCase().includes('network timeout')
          );
          expect(networkAlert).toBeInTheDocument();
        },
        { timeout: 5000 }
      );
    });
  });
});
