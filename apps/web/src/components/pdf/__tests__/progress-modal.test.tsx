/**
 * ProgressModal Component Tests (Issue #4210)
 */

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { ProgressModal } from '../progress-modal';

// Mock usePdfProgress
vi.mock('@/hooks/usePdfProgress', () => ({
  usePdfProgress: vi.fn(() => ({
    status: { state: 'uploading', progress: 25, eta: '5m 0s', timestamp: new Date().toISOString() },
    metrics: {
      documentId: 'doc-123',
      currentState: 'Uploading',
      progressPercentage: 25,
      totalDuration: '00:02:00',
      estimatedTimeRemaining: '00:05:00',
      stateDurations: {},
      retryCount: 0,
      pageCount: null,
    },
    isConnected: true,
    isPolling: false,
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

describe('ProgressModal', () => {
  const defaultProps = {
    documentId: 'doc-123',
    isOpen: true,
    onClose: vi.fn(),
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should render modal when open', () => {
    render(<ProgressModal {...defaultProps} />);

    expect(screen.getByText('Processing PDF')).toBeInTheDocument();
    expect(screen.getByText('Uploading PDF')).toBeInTheDocument();
  });

  it('should display progress bar with correct value', () => {
    render(<ProgressModal {...defaultProps} />);

    const progressBar = screen.getByRole('progressbar');
    expect(progressBar).toHaveAttribute('aria-label', 'Processing progress: 25%');
  });

  it('should show step indicators with current step highlighted', () => {
    render(<ProgressModal {...defaultProps} />);

    const currentStep = screen.getByLabelText(/Step 1: Uploading PDF/i);
    expect(currentStep).toBeInTheDocument();
  });

  it('should display metrics correctly', () => {
    mockedUsePdfProgress.mockReturnValue({
      status: { state: 'extracting', progress: 30, eta: '4m 0s', timestamp: new Date().toISOString() },
      metrics: {
        documentId: 'doc-123',
        currentState: 'Extracting',
        progressPercentage: 30,
        totalDuration: '00:02:30',
        estimatedTimeRemaining: '00:04:00',
        stateDurations: {},
        retryCount: 0,
        pageCount: 42,
      },
      isConnected: true,
      isPolling: false,
      isLoading: false,
      error: null,
      metricsError: null,
      metricsLoading: false,
      reconnect: vi.fn(),
      refreshMetrics: vi.fn(),
    });

    render(<ProgressModal {...defaultProps} />);

    expect(screen.getByText('42')).toBeInTheDocument(); // pageCount
    expect(screen.getByText(/4m/i)).toBeInTheDocument(); // ETA (formatTimeSpan omits 0s)
  });

  it('should show cancel button and confirmation dialog', async () => {
    const user = userEvent.setup();
    const onCancel = vi.fn().mockResolvedValue(undefined);

    render(<ProgressModal {...defaultProps} onCancel={onCancel} />);

    const cancelBtn = screen.getByText('Cancel Processing');
    await user.click(cancelBtn);

    expect(screen.getByText('Cancel Processing?')).toBeInTheDocument();
    expect(screen.getByText(/progress will be lost/i)).toBeInTheDocument();

    const confirmBtn = screen.getByText('Yes, Cancel');
    await user.click(confirmBtn);

    await waitFor(() => expect(onCancel).toHaveBeenCalled());
  });

  it('should show completion state', () => {
    mockedUsePdfProgress.mockReturnValue({
      status: { state: 'ready', progress: 100, eta: null, timestamp: new Date().toISOString() },
      metrics: {
        documentId: 'doc-123',
        currentState: 'Completed',
        progressPercentage: 100,
        totalDuration: '00:09:00',
        estimatedTimeRemaining: null,
        stateDurations: {},
        retryCount: 0,
        pageCount: 42,
      },
      isConnected: true,
      isPolling: false,
      isLoading: false,
      error: null,
      metricsError: null,
      metricsLoading: false,
      reconnect: vi.fn(),
      refreshMetrics: vi.fn(),
    });

    render(<ProgressModal {...defaultProps} />);

    expect(screen.getByText('Processing Complete')).toBeInTheDocument();
    expect(screen.getByText('Your PDF has been processed successfully.')).toBeInTheDocument();
    // Multiple "Close" buttons exist (X button + footer button), just check one exists
    const closeButtons = screen.getAllByText('Close');
    expect(closeButtons.length).toBeGreaterThan(0);
  });

  it('should show error state', () => {
    mockedUsePdfProgress.mockReturnValue({
      status: {
        state: 'failed',
        progress: 40,
        eta: null,
        timestamp: new Date().toISOString(),
        errorMessage: 'PDF extraction failed',
      },
      metrics: {
        documentId: 'doc-123',
        currentState: 'Failed',
        progressPercentage: 40,
        totalDuration: '00:03:00',
        estimatedTimeRemaining: null,
        stateDurations: {},
        retryCount: 2,
        pageCount: null,
      },
      isConnected: true,
      isPolling: false,
      isLoading: false,
      error: null,
      metricsError: null,
      metricsLoading: false,
      reconnect: vi.fn(),
      refreshMetrics: vi.fn(),
    });

    render(<ProgressModal {...defaultProps} />);

    expect(screen.getByText('Processing Failed')).toBeInTheDocument();
    expect(screen.getByText('PDF extraction failed')).toBeInTheDocument();
  });

  it('should show polling fallback indicator', () => {
    mockedUsePdfProgress.mockReturnValue({
      status: { state: 'chunking', progress: 50, eta: '3m 0s', timestamp: new Date().toISOString() },
      metrics: null,
      isConnected: false,
      isPolling: true,
      isLoading: false,
      error: null,
      metricsError: null,
      metricsLoading: false,
      reconnect: vi.fn(),
      refreshMetrics: vi.fn(),
    });

    render(<ProgressModal {...defaultProps} />);

    expect(screen.getByText('⚠️ Checking status...')).toBeInTheDocument();
  });

  it('should call onClose when user closes modal', async () => {
    const user = userEvent.setup();
    const onClose = vi.fn();

    mockedUsePdfProgress.mockReturnValue({
      status: { state: 'ready', progress: 100, eta: null, timestamp: new Date().toISOString() },
      metrics: {
        documentId: 'doc-123',
        currentState: 'Completed',
        progressPercentage: 100,
        totalDuration: '00:09:00',
        estimatedTimeRemaining: null,
        stateDurations: {},
        retryCount: 0,
        pageCount: 42,
      },
      isConnected: true,
      isPolling: false,
      isLoading: false,
      error: null,
      metricsError: null,
      metricsLoading: false,
      reconnect: vi.fn(),
      refreshMetrics: vi.fn(),
    });

    render(<ProgressModal {...defaultProps} onClose={onClose} />);

    const closeButtons = screen.getAllByRole('button', { name: /close/i });
    const footerCloseBtn = closeButtons[closeButtons.length - 1]; // Last one is footer button
    await user.click(footerCloseBtn);

    expect(onClose).toHaveBeenCalled();
  });
});