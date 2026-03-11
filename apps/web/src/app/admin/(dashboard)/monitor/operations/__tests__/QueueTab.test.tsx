import { vi, describe, it, expect, beforeEach, afterEach } from 'vitest';

const mockGetProcessingQueue = vi.hoisted(() => vi.fn());
const mockGetQueueStatus = vi.hoisted(() => vi.fn());
const mockCancelJob = vi.hoisted(() => vi.fn());
const mockRetryJob = vi.hoisted(() => vi.fn());
const mockRemoveJob = vi.hoisted(() => vi.fn());
const mockEnqueueJob = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getProcessingQueue: mockGetProcessingQueue,
      getQueueStatus: mockGetQueueStatus,
      cancelJob: mockCancelJob,
      retryJob: mockRetryJob,
      removeJob: mockRemoveJob,
      enqueueJob: mockEnqueueJob,
    },
  },
}));

const mockToast = vi.hoisted(() => vi.fn());
vi.mock('@/hooks/use-toast', () => ({
  useToast: () => ({ toast: mockToast }),
}));

import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { QueueTab } from '../QueueTab';

// ---------- Mock Data ----------

const MOCK_QUEUE = {
  jobs: [
    {
      id: 'j1',
      pdfDocumentId: 'd1',
      pdfFileName: 'catan.pdf',
      userId: 'u1',
      status: 'Processing',
      priority: 1,
      createdAt: '2026-03-01T10:00:00Z',
      startedAt: '2026-03-01T10:01:00Z',
      completedAt: null,
      errorMessage: null,
      retryCount: 0,
      maxRetries: 3,
      currentStep: 'Chunking',
      canRetry: false,
    },
    {
      id: 'j2',
      pdfDocumentId: 'd2',
      pdfFileName: 'monopoly.pdf',
      userId: 'u2',
      status: 'Failed',
      priority: 5,
      createdAt: '2026-03-01T09:00:00Z',
      startedAt: '2026-03-01T09:01:00Z',
      completedAt: null,
      errorMessage: 'Timeout',
      retryCount: 1,
      maxRetries: 3,
      currentStep: null,
      canRetry: true,
    },
    {
      id: 'j3',
      pdfDocumentId: 'd3',
      pdfFileName: 'risk.pdf',
      userId: 'u3',
      status: 'Completed',
      priority: 3,
      createdAt: '2026-03-01T08:00:00Z',
      startedAt: '2026-03-01T08:01:00Z',
      completedAt: '2026-03-01T08:05:00Z',
      errorMessage: null,
      retryCount: 0,
      maxRetries: 3,
      currentStep: null,
      canRetry: false,
    },
    {
      id: 'j4',
      pdfDocumentId: 'd4',
      pdfFileName: 'chess.pdf',
      userId: 'u4',
      status: 'Pending',
      priority: 7,
      createdAt: '2026-03-01T07:00:00Z',
      startedAt: null,
      completedAt: null,
      errorMessage: null,
      retryCount: 0,
      maxRetries: 3,
      currentStep: null,
      canRetry: false,
    },
  ],
  total: 4,
  page: 1,
  pageSize: 20,
  totalPages: 1,
};

const MOCK_STATUS_HEALTHY = {
  queueDepth: 5,
  backpressureThreshold: 50,
  maxConcurrentWorkers: 3,
  estimatedWaitMinutes: 12,
  isPaused: false,
  isUnderPressure: false,
};

const MOCK_STATUS_PAUSED = {
  ...MOCK_STATUS_HEALTHY,
  isPaused: true,
};

const MOCK_STATUS_PRESSURE = {
  ...MOCK_STATUS_HEALTHY,
  isUnderPressure: true,
};

function setupMocks(queueData = MOCK_QUEUE, statusData = MOCK_STATUS_HEALTHY) {
  mockGetProcessingQueue.mockResolvedValue(queueData);
  mockGetQueueStatus.mockResolvedValue(statusData);
  mockCancelJob.mockResolvedValue(undefined);
  mockRetryJob.mockResolvedValue(undefined);
  mockRemoveJob.mockResolvedValue(undefined);
  mockEnqueueJob.mockResolvedValue(undefined);
}

describe('QueueTab', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.useFakeTimers({ shouldAdvanceTime: true });
    setupMocks();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('renders heading "Processing Queue"', async () => {
    render(<QueueTab />);

    await waitFor(() => {
      expect(screen.getByText('Processing Queue')).toBeInTheDocument();
    });
  });

  it('shows healthy queue status banner', async () => {
    render(<QueueTab />);

    await waitFor(() => {
      expect(screen.getByText('Queue Healthy')).toBeInTheDocument();
    });

    expect(screen.getByTestId('queue-status-banner')).toBeInTheDocument();
    expect(screen.getByText(/Depth: 5/)).toBeInTheDocument();
  });

  it('shows paused queue status banner', async () => {
    setupMocks(MOCK_QUEUE, MOCK_STATUS_PAUSED);
    render(<QueueTab />);

    await waitFor(() => {
      expect(screen.getByText('Queue Paused')).toBeInTheDocument();
    });
  });

  it('shows under-pressure queue status banner', async () => {
    setupMocks(MOCK_QUEUE, MOCK_STATUS_PRESSURE);
    render(<QueueTab />);

    await waitFor(() => {
      expect(screen.getByText('Queue Under Pressure')).toBeInTheDocument();
    });
  });

  it('renders job table with file names', async () => {
    render(<QueueTab />);

    await waitFor(() => {
      expect(screen.getByText('catan.pdf')).toBeInTheDocument();
    });

    expect(screen.getByText('monopoly.pdf')).toBeInTheDocument();
    expect(screen.getByText('risk.pdf')).toBeInTheDocument();
    expect(screen.getByText('chess.pdf')).toBeInTheDocument();
  });

  it('renders status badges for jobs', async () => {
    render(<QueueTab />);

    await waitFor(() => {
      expect(screen.getByText('catan.pdf')).toBeInTheDocument();
    });

    // Status text also appears in the filter <option> elements, so use getAllByText
    const processingElements = screen.getAllByText('Processing');
    expect(processingElements.length).toBeGreaterThanOrEqual(2); // badge + option

    const failedElements = screen.getAllByText('Failed');
    expect(failedElements.length).toBeGreaterThanOrEqual(2);

    const completedElements = screen.getAllByText('Completed');
    expect(completedElements.length).toBeGreaterThanOrEqual(2);

    const pendingElements = screen.getAllByText('Pending');
    expect(pendingElements.length).toBeGreaterThanOrEqual(2);
  });

  it('shows cancel button for Processing and Pending jobs', async () => {
    render(<QueueTab />);

    await waitFor(() => {
      expect(screen.getByText('catan.pdf')).toBeInTheDocument();
    });

    // Processing (catan.pdf) and Pending (chess.pdf) should have cancel buttons
    const cancelButtons = screen.getAllByRole('button', { name: /cancel job/i });
    expect(cancelButtons).toHaveLength(2);
  });

  it('shows retry button for Failed+canRetry job', async () => {
    render(<QueueTab />);

    await waitFor(() => {
      expect(screen.getByText('monopoly.pdf')).toBeInTheDocument();
    });

    const retryButtons = screen.getAllByRole('button', { name: /retry job/i });
    expect(retryButtons).toHaveLength(1);
  });

  it('shows remove button for Completed and Cancelled jobs', async () => {
    render(<QueueTab />);

    await waitFor(() => {
      expect(screen.getByText('risk.pdf')).toBeInTheDocument();
    });

    // Only Completed (risk.pdf) in our mock data
    const removeButtons = screen.getAllByRole('button', { name: /remove job/i });
    expect(removeButtons).toHaveLength(1);
  });

  it('cancel button opens confirmation dialog', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    render(<QueueTab />);

    await waitFor(() => {
      expect(screen.getByText('catan.pdf')).toBeInTheDocument();
    });

    const cancelButtons = screen.getAllByRole('button', { name: /cancel job/i });
    await user.click(cancelButtons[0]);

    await waitFor(() => {
      expect(screen.getByText('Cancel Job')).toBeInTheDocument();
      expect(screen.getByText(/cancel "catan.pdf"/)).toBeInTheDocument();
    });
  });

  it('retry action calls retryJob and shows toast', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    render(<QueueTab />);

    await waitFor(() => {
      expect(screen.getByText('monopoly.pdf')).toBeInTheDocument();
    });

    const retryButton = screen.getByRole('button', { name: /retry job/i });
    await user.click(retryButton);

    // Confirmation dialog appears
    await waitFor(() => {
      expect(screen.getByText('Retry Job')).toBeInTheDocument();
    });

    // Confirm (Level 1)
    await user.click(screen.getByRole('button', { name: /conferma/i }));

    await waitFor(() => {
      expect(mockRetryJob).toHaveBeenCalledWith('j2');
    });

    expect(mockToast).toHaveBeenCalledWith(
      expect.objectContaining({ title: 'Job retried: monopoly.pdf' })
    );
  });

  it('Enqueue Job button opens dialog', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    render(<QueueTab />);

    await waitFor(() => {
      expect(screen.getByTestId('enqueue-job-button')).toBeInTheDocument();
    });

    await user.click(screen.getByTestId('enqueue-job-button'));

    await waitFor(() => {
      expect(screen.getByText('Enqueue New Job')).toBeInTheDocument();
      expect(screen.getByTestId('enqueue-job-type')).toBeInTheDocument();
      expect(screen.getByTestId('enqueue-job-priority')).toBeInTheDocument();
    });
  });

  it('enqueue dialog submits job and shows toast', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    render(<QueueTab />);

    await waitFor(() => {
      expect(screen.getByTestId('enqueue-job-button')).toBeInTheDocument();
    });

    await user.click(screen.getByTestId('enqueue-job-button'));

    await waitFor(() => {
      expect(screen.getByText('Enqueue New Job')).toBeInTheDocument();
    });

    // Click the Enqueue submit button inside the dialog
    const dialog = screen.getByRole('dialog');
    const enqueueButton = within(dialog).getByRole('button', { name: /^enqueue$/i });
    await user.click(enqueueButton);

    await waitFor(() => {
      expect(mockEnqueueJob).toHaveBeenCalledWith({
        jobType: 'pdf-processing',
        priority: 5,
      });
    });

    expect(mockToast).toHaveBeenCalledWith(
      expect.objectContaining({ title: 'Job enqueued successfully' })
    );
  });

  it('shows empty state when no jobs', async () => {
    setupMocks({ jobs: [], total: 0, page: 1, pageSize: 20, totalPages: 0 });
    render(<QueueTab />);

    await waitFor(() => {
      expect(screen.getByText('No jobs found.')).toBeInTheDocument();
    });
  });

  it('shows error toast when API fails', async () => {
    mockGetProcessingQueue.mockRejectedValue(new Error('Network error'));
    mockGetQueueStatus.mockRejectedValue(new Error('Network error'));
    render(<QueueTab />);

    await waitFor(() => {
      expect(mockToast).toHaveBeenCalledWith(
        expect.objectContaining({
          title: 'Failed to load queue data',
          variant: 'destructive',
        })
      );
    });
  });

  it('renders search input and status filter', async () => {
    render(<QueueTab />);

    await waitFor(() => {
      expect(screen.getByTestId('queue-search-input')).toBeInTheDocument();
    });

    expect(screen.getByTestId('queue-status-filter')).toBeInTheDocument();
  });

  it('shows pagination when totalPages > 1', async () => {
    setupMocks({
      ...MOCK_QUEUE,
      total: 40,
      totalPages: 2,
    });
    render(<QueueTab />);

    await waitFor(() => {
      expect(screen.getByText(/Page 1 of 2/)).toBeInTheDocument();
    });

    expect(screen.getByRole('button', { name: /previous/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /next/i })).toBeInTheDocument();
  });

  it('does not show pagination when only 1 page', async () => {
    render(<QueueTab />);

    await waitFor(() => {
      expect(screen.getByText('catan.pdf')).toBeInTheDocument();
    });

    expect(screen.queryByText(/Page 1 of 1/)).not.toBeInTheDocument();
  });

  it('shows current step for processing jobs', async () => {
    render(<QueueTab />);

    await waitFor(() => {
      expect(screen.getByText('Chunking')).toBeInTheDocument();
    });
  });
});
