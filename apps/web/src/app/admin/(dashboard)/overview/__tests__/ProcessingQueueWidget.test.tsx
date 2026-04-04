import { screen, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

const mockGetProcessingQueue = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getProcessingQueue: mockGetProcessingQueue,
    },
  },
}));

vi.mock('next/link', () => ({
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  default: ({ children, href, ...rest }: any) => (
    <a href={href} {...rest}>
      {children}
    </a>
  ),
}));

import { renderWithQuery } from '@/__tests__/utils/query-test-utils';
import { ProcessingQueueWidget } from '../ProcessingQueueWidget';

function makeQueueResponse(total: number) {
  return { jobs: [], total, page: 1, pageSize: 1, totalPages: total };
}

describe('ProcessingQueueWidget', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders nothing when all counts are zero', async () => {
    mockGetProcessingQueue.mockResolvedValue(makeQueueResponse(0));

    const { container } = renderWithQuery(<ProcessingQueueWidget />);

    await waitFor(() => {
      expect(container).toBeEmptyDOMElement();
    });
  });

  it('shows queued count when there are jobs in attesa', async () => {
    mockGetProcessingQueue.mockImplementation(({ status }: { status: string }) => {
      if (status === 'Queued') return Promise.resolve(makeQueueResponse(3));
      return Promise.resolve(makeQueueResponse(0));
    });

    renderWithQuery(<ProcessingQueueWidget />);

    await waitFor(() => {
      expect(screen.getByText('3 in attesa')).toBeInTheDocument();
    });
  });

  it('shows processing count when there are jobs in elaborazione', async () => {
    mockGetProcessingQueue.mockImplementation(({ status }: { status: string }) => {
      if (status === 'Processing') return Promise.resolve(makeQueueResponse(2));
      return Promise.resolve(makeQueueResponse(0));
    });

    renderWithQuery(<ProcessingQueueWidget />);

    await waitFor(() => {
      expect(screen.getByText('2 in elaborazione')).toBeInTheDocument();
    });
  });

  it('shows failed count when there are jobs falliti', async () => {
    mockGetProcessingQueue.mockImplementation(({ status }: { status: string }) => {
      if (status === 'Failed') return Promise.resolve(makeQueueResponse(1));
      return Promise.resolve(makeQueueResponse(0));
    });

    renderWithQuery(<ProcessingQueueWidget />);

    await waitFor(() => {
      expect(screen.getByText('1 falliti')).toBeInTheDocument();
    });
  });

  it('calls getProcessingQueue with correct status parameters for each queue type', async () => {
    mockGetProcessingQueue.mockResolvedValue(makeQueueResponse(0));

    renderWithQuery(<ProcessingQueueWidget />);

    await waitFor(() => {
      expect(mockGetProcessingQueue).toHaveBeenCalledTimes(3);
    });

    expect(mockGetProcessingQueue).toHaveBeenCalledWith({ status: 'Queued', pageSize: 1 });
    expect(mockGetProcessingQueue).toHaveBeenCalledWith({
      status: 'Processing',
      pageSize: 1,
    });
    expect(mockGetProcessingQueue).toHaveBeenCalledWith({ status: 'Failed', pageSize: 1 });
  });

  it('links to the processing queue page', async () => {
    mockGetProcessingQueue.mockImplementation(({ status }: { status: string }) => {
      if (status === 'Queued') return Promise.resolve(makeQueueResponse(1));
      return Promise.resolve(makeQueueResponse(0));
    });

    renderWithQuery(<ProcessingQueueWidget />);

    await waitFor(() => {
      const link = screen.getByTestId('processing-queue-widget');
      expect(link.closest('a')).toHaveAttribute('href', '/admin/knowledge-base/queue');
    });
  });

  it('renders null when API calls fail', async () => {
    mockGetProcessingQueue.mockRejectedValue(new Error('Network error'));

    const { container } = renderWithQuery(<ProcessingQueueWidget />);

    // All counts default to 0 on error → no activity → renders null
    await waitFor(() => {
      expect(container).toBeEmptyDOMElement();
    });
  });
});
