/**
 * Admin KB Processing Queue Page Tests (Issue #4892)
 */

import { screen, waitFor, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { renderWithQuery } from '@/__tests__/utils/query-test-utils';

import { ProcessingQueueClient } from '../client';

// ─── Mocks ────────────────────────────────────────────────────────────────────

vi.mock('@/components/admin/AdminAuthGuard', () => ({
  AdminAuthGuard: ({ children }: { children: React.ReactNode }) => <>{children}</>,
}));

vi.mock('@/components/auth/AuthProvider', () => ({
  useAuthUser: vi.fn(() => ({ user: { id: '1', email: 'admin@test.com', role: 'Admin' }, loading: false })),
}));

const mockGetProcessingQueueAdmin = vi.fn();
const mockReindexPdf = vi.fn();

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getProcessingQueueAdmin: (...args: unknown[]) => mockGetProcessingQueueAdmin(...args),
      reindexPdf: (...args: unknown[]) => mockReindexPdf(...args),
    },
  },
}));

// ─── Test Data ────────────────────────────────────────────────────────────────

const makeJob = (overrides = {}) => ({
  id: 'job-1',
  pdfDocumentId: 'pdf-abc123',
  pdfFileName: 'rulebook.pdf',
  userId: 'user-1',
  status: 'Pending',
  priority: 5,
  currentStep: null,
  createdAt: '2026-01-15T10:00:00Z',
  startedAt: null,
  completedAt: null,
  errorMessage: null,
  retryCount: 0,
  maxRetries: 3,
  canRetry: false,
  ...overrides,
});

const mockQueueResponse = {
  jobs: [
    makeJob({ id: 'job-1', pdfFileName: 'rules.pdf', status: 'Completed', canRetry: false }),
    makeJob({ id: 'job-2', pdfFileName: 'faq.pdf', status: 'Failed', canRetry: true, errorMessage: 'Parse error', retryCount: 1 }),
    makeJob({ id: 'job-3', pdfFileName: 'strategy.pdf', status: 'Processing', currentStep: 'Embedding', canRetry: false }),
  ],
  total: 3,
  page: 1,
  pageSize: 20,
  totalPages: 1,
};

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('ProcessingQueueClient', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGetProcessingQueueAdmin.mockResolvedValue(mockQueueResponse);
    mockReindexPdf.mockResolvedValue({ success: true, message: 'Queued' });
  });

  it('renders the page heading', () => {
    renderWithQuery(<ProcessingQueueClient />);
    expect(screen.getByText('Processing Queue')).toBeInTheDocument();
  });

  it('shows back navigation link', async () => {
    renderWithQuery(<ProcessingQueueClient />);
    await waitFor(() => {
      expect(screen.getByText('Knowledge Base')).toBeInTheDocument();
    });
  });

  it('shows loading skeletons initially', () => {
    mockGetProcessingQueueAdmin.mockReturnValue(new Promise(() => {}));
    renderWithQuery(<ProcessingQueueClient />);
    // Skeleton uses animate-pulse class (shadcn/ui Skeleton component)
    const skeletons = document.querySelectorAll('[class*="animate-pulse"]');
    expect(skeletons.length).toBeGreaterThan(0);
  });

  it('renders job rows after loading', async () => {
    renderWithQuery(<ProcessingQueueClient />);
    await waitFor(() => {
      expect(screen.getByText('rules.pdf')).toBeInTheDocument();
      expect(screen.getByText('faq.pdf')).toBeInTheDocument();
      expect(screen.getByText('strategy.pdf')).toBeInTheDocument();
    });
  });

  it('shows status badges', async () => {
    renderWithQuery(<ProcessingQueueClient />);
    await waitFor(() => {
      expect(screen.getByText('Completed')).toBeInTheDocument();
      expect(screen.getByText('Failed')).toBeInTheDocument();
      expect(screen.getByText('Processing')).toBeInTheDocument();
    });
  });

  it('shows retry button only for canRetry jobs', async () => {
    renderWithQuery(<ProcessingQueueClient />);
    await waitFor(() => {
      // faq.pdf has canRetry: true, so should have a retry button
      const retryButtons = screen.queryAllByTitle('Reindex this document');
      expect(retryButtons).toHaveLength(1);
    });
  });

  it('shows error message', async () => {
    renderWithQuery(<ProcessingQueueClient />);
    await waitFor(() => {
      expect(screen.getByText('Parse error')).toBeInTheDocument();
    });
  });

  it('shows empty state when no jobs match', async () => {
    mockGetProcessingQueueAdmin.mockResolvedValue({
      jobs: [], total: 0, page: 1, pageSize: 20, totalPages: 0,
    });
    renderWithQuery(<ProcessingQueueClient />);
    await waitFor(() => {
      expect(screen.getByText(/No jobs found/i)).toBeInTheDocument();
    });
  });

  it('shows total count in header', async () => {
    renderWithQuery(<ProcessingQueueClient />);
    await waitFor(() => {
      expect(screen.getByText('3 total jobs')).toBeInTheDocument();
    });
  });

  it('shows API error alert', async () => {
    mockGetProcessingQueueAdmin.mockRejectedValue(new Error('Queue unavailable'));
    renderWithQuery(<ProcessingQueueClient />);
    await waitFor(() => {
      // Error appears in both alert and toast - use getAllByText
      expect(screen.getAllByText('Queue unavailable').length).toBeGreaterThan(0);
    });
  });

  it('calls API with correct params on mount', async () => {
    renderWithQuery(<ProcessingQueueClient />);
    await waitFor(() => {
      expect(mockGetProcessingQueueAdmin).toHaveBeenCalledWith({
        statusFilter: undefined,
        searchText: undefined,
        page: 1,
        pageSize: 20,
      });
    });
  });

  it('calls reindexPdf when retry button is clicked', async () => {
    renderWithQuery(<ProcessingQueueClient />);
    await waitFor(() => {
      expect(screen.getByText('faq.pdf')).toBeInTheDocument();
    });
    const retryButton = screen.getByTitle('Reindex this document');
    fireEvent.click(retryButton);
    await waitFor(() => {
      expect(mockReindexPdf).toHaveBeenCalledWith('pdf-abc123');
    });
  });

  it('shows pagination when totalPages > 1', async () => {
    mockGetProcessingQueueAdmin.mockResolvedValue({
      jobs: [makeJob({ id: 'job-1', pdfFileName: 'file.pdf' })],
      total: 25,
      page: 1,
      pageSize: 20,
      totalPages: 2,
    });
    renderWithQuery(<ProcessingQueueClient />);
    await waitFor(() => {
      expect(screen.getByText('Page 1 of 2')).toBeInTheDocument();
    });
  });
});
