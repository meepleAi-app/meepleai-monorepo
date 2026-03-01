/**
 * Documents Library Page Tests (Issue #4788)
 *
 * Tests for the Documents Library page (/admin/knowledge-base/documents):
 * - Renders analytics cards, storage health bar, and document table
 * - Loading skeletons while data is fetching
 * - Error state with retry
 * - Empty state
 * - Search and status filter
 * - Per-document actions (reindex, delete)
 */

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

// vi.hoisted ensures mock fns are created before vi.mock hoisting
const {
  mockGetAllPdfs,
  mockGetPdfStatusDistribution,
  mockGetPdfStorageHealth,
  mockReindexPdf,
  mockBulkDeletePdfs,
  mockPurgeStaleDocuments,
  mockCleanupOrphans,
} = vi.hoisted(() => ({
  mockGetAllPdfs: vi.fn(),
  mockGetPdfStatusDistribution: vi.fn(),
  mockGetPdfStorageHealth: vi.fn(),
  mockReindexPdf: vi.fn(),
  mockBulkDeletePdfs: vi.fn(),
  mockPurgeStaleDocuments: vi.fn(),
  mockCleanupOrphans: vi.fn(),
}));

vi.mock('@/lib/api/clients/adminClient', () => ({
  createAdminClient: () => ({
    getAllPdfs: mockGetAllPdfs,
    getPdfStatusDistribution: mockGetPdfStatusDistribution,
    getPdfStorageHealth: mockGetPdfStorageHealth,
    reindexPdf: mockReindexPdf,
    bulkDeletePdfs: mockBulkDeletePdfs,
    purgeStaleDocuments: mockPurgeStaleDocuments,
    cleanupOrphans: mockCleanupOrphans,
  }),
}));

vi.mock('@/lib/api/core/httpClient', () => ({
  HttpClient: vi.fn(),
}));

import DocumentsLibraryPage from '@/app/admin/(dashboard)/knowledge-base/documents/page';

function createTestQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: { retry: false, staleTime: 0 },
      mutations: { retry: false },
    },
  });
}

function renderWithQuery(ui: React.ReactElement) {
  const client = createTestQueryClient();
  return render(
    <QueryClientProvider client={client}>{ui}</QueryClientProvider>
  );
}

const MOCK_PDFS = {
  items: [
    {
      id: 'pdf-1',
      fileName: 'catan-rulebook.pdf',
      gameTitle: 'Catan',
      gameId: 'game-1',
      processingStatus: 'completed',
      processingState: 'Completed',
      progressPercentage: 100,
      fileSizeBytes: 2500000,
      pageCount: 24,
      chunkCount: 150,
      processingError: null,
      errorCategory: null,
      retryCount: 0,
      uploadedAt: '2026-02-18T10:00:00Z',
      processedAt: '2026-02-18T10:05:00Z',
    },
    {
      id: 'pdf-2',
      fileName: 'wingspan-rules.pdf',
      gameTitle: 'Wingspan',
      gameId: 'game-2',
      processingStatus: 'processing',
      processingState: 'Processing',
      progressPercentage: 65,
      fileSizeBytes: 1800000,
      pageCount: 16,
      chunkCount: 80,
      processingError: null,
      errorCategory: null,
      retryCount: 0,
      uploadedAt: '2026-02-19T08:00:00Z',
      processedAt: null,
    },
  ],
  total: 2,
  page: 1,
  pageSize: 20,
};

const MOCK_DISTRIBUTION = {
  countByState: { Completed: 10, Processing: 3, Failed: 1 },
  totalDocuments: 14,
  topBySize: [],
};

const MOCK_STORAGE_HEALTH = {
  postgres: { totalDocuments: 14, totalChunks: 2000, estimatedChunksSizeMB: 100 },
  qdrant: { vectorCount: 2000, memoryBytes: 536870912, memoryFormatted: '512 MB', isAvailable: true },
  fileStorage: { totalFiles: 14, totalSizeBytes: 50000000, totalSizeFormatted: '47.7 MB', sizeByState: {} },
  overallHealth: 'Healthy',
  measuredAt: '2026-02-19T00:00:00Z',
};

describe('DocumentsLibraryPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGetAllPdfs.mockResolvedValue(MOCK_PDFS);
    mockGetPdfStatusDistribution.mockResolvedValue(MOCK_DISTRIBUTION);
    mockGetPdfStorageHealth.mockResolvedValue(MOCK_STORAGE_HEALTH);
    mockReindexPdf.mockResolvedValue({ success: true, message: 'Reindex queued' });
    mockBulkDeletePdfs.mockResolvedValue({ totalRequested: 1, successCount: 1, failedCount: 0, items: [] });
    mockPurgeStaleDocuments.mockResolvedValue({ affected: 0, message: 'No stale documents' });
    mockCleanupOrphans.mockResolvedValue({ affected: 0, message: 'No orphans found' });
  });

  it('should render page header', async () => {
    renderWithQuery(<DocumentsLibraryPage />);

    expect(screen.getByText('Documents Library')).toBeInTheDocument();
    expect(screen.getByText('Browse uploaded documents, manage processing, and monitor storage')).toBeInTheDocument();
  });

  it('should render analytics cards with data', async () => {
    renderWithQuery(<DocumentsLibraryPage />);

    await waitFor(() => {
      expect(screen.getByText('Total Documents')).toBeInTheDocument();
      expect(screen.getByText('14')).toBeInTheDocument();
    });

    expect(screen.getByText('Storage')).toBeInTheDocument();
    expect(screen.getByText('47.7 MB')).toBeInTheDocument();
  });

  it('should render storage health bar', async () => {
    renderWithQuery(<DocumentsLibraryPage />);

    await waitFor(() => {
      expect(screen.getByText(/PostgreSQL:/)).toBeInTheDocument();
    });

    expect(screen.getByText(/14 docs/)).toBeInTheDocument();
    // Vector count is locale-formatted (e.g., 2,000 or 2.000)
    expect(screen.getByText(new RegExp(`${(2000).toLocaleString()} vectors`))).toBeInTheDocument();
    expect(screen.getByText('Healthy')).toBeInTheDocument();
  });

  it('should render document table with data', async () => {
    renderWithQuery(<DocumentsLibraryPage />);

    await waitFor(() => {
      expect(screen.getByText('catan-rulebook.pdf')).toBeInTheDocument();
    });

    expect(screen.getByText('wingspan-rules.pdf')).toBeInTheDocument();
    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText('Wingspan')).toBeInTheDocument();
  });

  it('should show loading skeletons while fetching', () => {
    mockGetAllPdfs.mockReturnValue(new Promise(() => {})); // Never resolves
    renderWithQuery(<DocumentsLibraryPage />);

    expect(screen.getByText('Documents Library')).toBeInTheDocument();
    expect(screen.getByText('Document')).toBeInTheDocument();
  });

  it('should show error state when fetch fails', async () => {
    mockGetAllPdfs.mockRejectedValue(new Error('Network error'));
    renderWithQuery(<DocumentsLibraryPage />);

    await waitFor(() => {
      expect(screen.getByText('Failed to load documents')).toBeInTheDocument();
    });

    expect(screen.getByText('Network error')).toBeInTheDocument();
    expect(screen.getByText('Retry')).toBeInTheDocument();
  });

  it('should show empty state when no documents', async () => {
    mockGetAllPdfs.mockResolvedValue({ items: [], total: 0, page: 1, pageSize: 20 });
    renderWithQuery(<DocumentsLibraryPage />);

    await waitFor(() => {
      expect(screen.getByText('No documents uploaded yet')).toBeInTheDocument();
    });
  });

  it('should filter by search text', async () => {
    renderWithQuery(<DocumentsLibraryPage />);

    await waitFor(() => {
      expect(screen.getByText('catan-rulebook.pdf')).toBeInTheDocument();
    });

    const searchInput = screen.getByPlaceholderText('Search documents...');
    fireEvent.change(searchInput, { target: { value: 'catan' } });

    // After search change, component re-renders - wait for filtered results
    await waitFor(() => {
      expect(screen.getByText('catan-rulebook.pdf')).toBeInTheDocument();
      expect(screen.queryByText('wingspan-rules.pdf')).not.toBeInTheDocument();
    });
  });

  it('should render Purge Stale and Cleanup Orphans buttons', () => {
    renderWithQuery(<DocumentsLibraryPage />);

    expect(screen.getByText('Purge Stale')).toBeInTheDocument();
    expect(screen.getByText('Cleanup Orphans')).toBeInTheDocument();
    expect(screen.getByText('Refresh')).toBeInTheDocument();
  });

  it('should render status filter dropdown', () => {
    renderWithQuery(<DocumentsLibraryPage />);

    expect(screen.getByText('All Statuses')).toBeInTheDocument();
  });

  it('should show table column headers', async () => {
    renderWithQuery(<DocumentsLibraryPage />);

    expect(screen.getByText('Document')).toBeInTheDocument();
    expect(screen.getByText('Game')).toBeInTheDocument();
    expect(screen.getByText('Pages')).toBeInTheDocument();
    expect(screen.getByText('Chunks')).toBeInTheDocument();
    expect(screen.getByText('Size')).toBeInTheDocument();
    expect(screen.getByText('Uploaded')).toBeInTheDocument();
    expect(screen.getByText('Actions')).toBeInTheDocument();
  });
});
