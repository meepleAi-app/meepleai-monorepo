/**
 * GameProcessingQueue Tests
 *
 * Validates the mini-widget displays game-specific and global queue counts,
 * priority badges, progress indicators, deep-link, and hides when empty.
 */

import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { GameProcessingQueue } from '../GameProcessingQueue';

// ============================================================================
// Mocks
// ============================================================================

const mockGetQueueJobs = vi.fn();
const mockGetQueueStatus = vi.fn();

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getQueueJobs: (...args: unknown[]) => mockGetQueueJobs(...args),
      getQueueStatus: (...args: unknown[]) => mockGetQueueStatus(...args),
    },
  },
}));

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

function renderWidget(gameId = 'game-123') {
  const qc = createTestQueryClient();
  return render(
    <QueryClientProvider client={qc}>
      <GameProcessingQueue gameId={gameId} />
    </QueryClientProvider>
  );
}

// ============================================================================
// Tests
// ============================================================================

describe('GameProcessingQueue', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders game-specific and global queue counts', async () => {
    mockGetQueueJobs.mockResolvedValue({
      jobs: [
        {
          id: 'job-1',
          pdfDocumentId: 'pdf-1',
          pdfFileName: 'rules.pdf',
          userId: 'user-1',
          status: 'Queued',
          priority: 1,
          currentStep: null,
          createdAt: '2026-03-16T10:00:00Z',
          startedAt: null,
          completedAt: null,
          errorMessage: null,
          retryCount: 0,
          maxRetries: 3,
          canRetry: true,
        },
      ],
      total: 2,
      page: 1,
      pageSize: 5,
      totalPages: 1,
    });
    mockGetQueueStatus.mockResolvedValue({
      queueDepth: 7,
      backpressureThreshold: 100,
      isUnderPressure: false,
      isPaused: false,
      maxConcurrentWorkers: 4,
      estimatedWaitMinutes: 5,
    });

    renderWidget();

    expect(await screen.findByText('Coda elaborazione')).toBeInTheDocument();
    // "2 di questo gioco · 7 totali"
    expect(await screen.findByText(/2 di questo gioco/)).toBeInTheDocument();
    expect(screen.getByText(/7 totali/)).toBeInTheDocument();
  });

  it('renders job rows with priority badges', async () => {
    mockGetQueueJobs.mockResolvedValue({
      jobs: [
        {
          id: 'job-urgent',
          pdfDocumentId: 'pdf-1',
          pdfFileName: 'urgent-rules.pdf',
          userId: 'user-1',
          status: 'Processing',
          priority: 3,
          currentStep: 'Chunking',
          createdAt: '2026-03-16T10:00:00Z',
          startedAt: '2026-03-16T10:01:00Z',
          completedAt: null,
          errorMessage: null,
          retryCount: 0,
          maxRetries: 3,
          canRetry: true,
        },
        {
          id: 'job-normal',
          pdfDocumentId: 'pdf-2',
          pdfFileName: 'normal-rules.pdf',
          userId: 'user-1',
          status: 'Queued',
          priority: 1,
          currentStep: null,
          createdAt: '2026-03-16T10:02:00Z',
          startedAt: null,
          completedAt: null,
          errorMessage: null,
          retryCount: 0,
          maxRetries: 3,
          canRetry: true,
        },
      ],
      total: 2,
      page: 1,
      pageSize: 5,
      totalPages: 1,
    });
    mockGetQueueStatus.mockResolvedValue({
      queueDepth: 2,
      backpressureThreshold: 100,
      isUnderPressure: false,
      isPaused: false,
      maxConcurrentWorkers: 4,
      estimatedWaitMinutes: 1,
    });

    renderWidget();

    // Priority badges
    expect(await screen.findByText('Urgente')).toBeInTheDocument();
    expect(screen.getByText('Normale')).toBeInTheDocument();

    // File names
    expect(screen.getByText('urgent-rules.pdf')).toBeInTheDocument();
    expect(screen.getByText('normal-rules.pdf')).toBeInTheDocument();

    // "In coda" for queued job
    expect(screen.getByText('In coda')).toBeInTheDocument();
  });

  it('renders deep-link with correct gameId param', async () => {
    mockGetQueueJobs.mockResolvedValue({
      jobs: [],
      total: 1,
      page: 1,
      pageSize: 5,
      totalPages: 1,
    });
    mockGetQueueStatus.mockResolvedValue({
      queueDepth: 3,
      backpressureThreshold: 100,
      isUnderPressure: false,
      isPaused: false,
      maxConcurrentWorkers: 4,
      estimatedWaitMinutes: 2,
    });

    renderWidget('my-game-456');

    const link = await screen.findByText(/Apri coda completa/);
    const anchor = link.closest('a');
    expect(anchor).toHaveAttribute('href', '/admin/knowledge-base/queue?gameId=my-game-456');
  });

  it('returns null when there are no jobs', async () => {
    mockGetQueueJobs.mockResolvedValue({
      jobs: [],
      total: 0,
      page: 1,
      pageSize: 5,
      totalPages: 0,
    });
    mockGetQueueStatus.mockResolvedValue({
      queueDepth: 0,
      backpressureThreshold: 100,
      isUnderPressure: false,
      isPaused: false,
      maxConcurrentWorkers: 4,
      estimatedWaitMinutes: 0,
    });

    const { container } = renderWidget();

    // Wait for queries to resolve
    await vi.waitFor(() => {
      expect(mockGetQueueJobs).toHaveBeenCalled();
    });

    // Widget should not render anything
    expect(container.innerHTML).toBe('');
  });
});
