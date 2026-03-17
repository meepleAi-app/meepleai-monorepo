/**
 * RecentlyProcessedWidget Tests
 *
 * Validates the widget displays recently processed PDFs with correct status
 * badges, retry actions, collapse persistence, and empty state.
 */

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { RecentlyProcessedWidget } from '../RecentlyProcessedWidget';
import { type RecentlyProcessedDocument } from '@/lib/api/clients/sharedGamesClient';

// ============================================================================
// Mocks
// ============================================================================

const mockGetRecentlyProcessed = vi.fn();
const mockRetryJob = vi.fn();

vi.mock('@/lib/api', () => ({
  api: {
    sharedGames: {
      getRecentlyProcessed: (...args: unknown[]) => mockGetRecentlyProcessed(...args),
    },
    admin: {
      retryJob: (...args: unknown[]) => mockRetryJob(...args),
    },
  },
}));

vi.mock('sonner', () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
  },
}));

// ============================================================================
// Fixtures
// ============================================================================

function makeDoc(overrides: Partial<RecentlyProcessedDocument> = {}): RecentlyProcessedDocument {
  return {
    pdfDocumentId: 'pdf-1',
    jobId: 'job-1',
    fileName: 'rules.pdf',
    processingState: 'Ready',
    timestamp: '2026-03-16T10:00:00Z',
    errorCategory: null,
    canRetry: false,
    sharedGameId: 'game-1',
    gameName: 'Catan',
    thumbnailUrl: null,
    ...overrides,
  };
}

const readyDoc = makeDoc();
const failedDoc = makeDoc({
  pdfDocumentId: 'pdf-2',
  jobId: 'job-2',
  fileName: 'broken.pdf',
  processingState: 'Failed',
  errorCategory: 'ParseError',
  canRetry: true,
  sharedGameId: 'game-2',
  gameName: 'Azul',
});
const processingDoc = makeDoc({
  pdfDocumentId: 'pdf-3',
  jobId: 'job-3',
  fileName: 'uploading.pdf',
  processingState: 'Processing',
  sharedGameId: 'game-3',
  gameName: 'Wingspan',
});

// ============================================================================
// Helpers
// ============================================================================

function createTestQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: { retry: false, gcTime: 0 },
    },
  });
}

function renderWidget() {
  const qc = createTestQueryClient();
  return render(
    <QueryClientProvider client={qc}>
      <RecentlyProcessedWidget />
    </QueryClientProvider>
  );
}

// ============================================================================
// Tests
// ============================================================================

describe('RecentlyProcessedWidget', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
  });

  it('renders table with PDF rows', async () => {
    mockGetRecentlyProcessed.mockResolvedValue([readyDoc, failedDoc, processingDoc]);

    renderWidget();

    await waitFor(() => {
      expect(screen.getByText('rules.pdf')).toBeInTheDocument();
    });

    expect(screen.getByText('broken.pdf')).toBeInTheDocument();
    expect(screen.getByText('uploading.pdf')).toBeInTheDocument();
    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText('Azul')).toBeInTheDocument();
    expect(screen.getByText('Wingspan')).toBeInTheDocument();
  });

  it('shows correct status badges for Ready/Failed/Processing', async () => {
    mockGetRecentlyProcessed.mockResolvedValue([readyDoc, failedDoc, processingDoc]);

    renderWidget();

    await waitFor(() => {
      expect(screen.getByText('Indicizzato')).toBeInTheDocument();
    });

    expect(screen.getByText('Fallito')).toBeInTheDocument();
    expect(screen.getByText('Elaborazione')).toBeInTheDocument();
  });

  it('shows retry button for Failed + canRetry documents', async () => {
    mockGetRecentlyProcessed.mockResolvedValue([failedDoc]);
    mockRetryJob.mockResolvedValue(undefined);

    const user = userEvent.setup();
    renderWidget();

    const retryBtn = await screen.findByRole('button', { name: /riprova/i });
    expect(retryBtn).toBeInTheDocument();

    await user.click(retryBtn);

    await waitFor(() => {
      expect(mockRetryJob).toHaveBeenCalledWith('job-2');
    });
  });

  it('shows "Vai al gioco" for Ready documents', async () => {
    mockGetRecentlyProcessed.mockResolvedValue([readyDoc]);

    renderWidget();

    const link = await screen.findByRole('link', { name: /vai al gioco/i });
    expect(link).toHaveAttribute('href', '/admin/shared-games/game-1');
  });

  it('shows "Vai alla coda" for Processing documents', async () => {
    mockGetRecentlyProcessed.mockResolvedValue([processingDoc]);

    renderWidget();

    const link = await screen.findByRole('link', { name: /vai alla coda/i });
    expect(link).toHaveAttribute('href', '/admin/knowledge-base/documents');
  });

  it('collapse toggle persists to localStorage', async () => {
    mockGetRecentlyProcessed.mockResolvedValue([readyDoc]);

    const user = userEvent.setup();
    renderWidget();

    // Wait for content to render
    await screen.findByText('rules.pdf');

    // Click collapse button
    const collapseBtn = screen.getByRole('button', { name: /comprimi/i });
    await user.click(collapseBtn);

    expect(localStorage.getItem('admin:recentPdfs:collapsed')).toBe('true');

    // Table content should be hidden
    expect(screen.queryByText('rules.pdf')).not.toBeInTheDocument();

    // Expand again
    const expandBtn = screen.getByRole('button', { name: /espandi/i });
    await user.click(expandBtn);

    expect(localStorage.getItem('admin:recentPdfs:collapsed')).toBe('false');
    await waitFor(() => {
      expect(screen.getByText('rules.pdf')).toBeInTheDocument();
    });
  });

  it('returns null when empty and not loading', async () => {
    mockGetRecentlyProcessed.mockResolvedValue([]);

    const { container } = renderWidget();

    await waitFor(() => {
      expect(container.innerHTML).toBe('');
    });
  });

  it('reads collapse state from localStorage on mount', async () => {
    localStorage.setItem('admin:recentPdfs:collapsed', 'true');
    mockGetRecentlyProcessed.mockResolvedValue([readyDoc]);

    renderWidget();

    // Should show expand button since it starts collapsed
    await waitFor(() => {
      expect(screen.getByRole('button', { name: /espandi/i })).toBeInTheDocument();
    });

    // Table content should not be visible
    expect(screen.queryByText('rules.pdf')).not.toBeInTheDocument();
  });
});
