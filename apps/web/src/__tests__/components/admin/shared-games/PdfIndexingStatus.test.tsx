/**
 * PdfIndexingStatus Component Unit Tests - Issue #3642
 *
 * Tests for the PDF indexing status component with:
 * - Stage timeline display
 * - Progress bar updates
 * - Compact and full display modes
 * - Completion and error states
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { PdfIndexingStatus, type PdfIndexingStatusData } from '@/components/admin/shared-games/PdfIndexingStatus';

// ============================================================================
// Mock Data
// ============================================================================

const mockStatusUploaded: PdfIndexingStatusData = {
  pdfId: 'pdf-123',
  status: 'uploaded',
  progress: 0,
  currentStep: 'File uploaded, waiting for processing',
};

const mockStatusProcessing: PdfIndexingStatusData = {
  pdfId: 'pdf-123',
  status: 'processing',
  progress: 25,
  currentStep: 'Analyzing document structure',
};

const mockStatusChunking: PdfIndexingStatusData = {
  pdfId: 'pdf-123',
  status: 'chunked',
  progress: 60,
  currentStep: 'Splitting into chunks',
  chunksProcessed: 15,
  totalChunks: 30,
};

const mockStatusEmbedding: PdfIndexingStatusData = {
  pdfId: 'pdf-123',
  status: 'embedding',
  progress: 80,
  currentStep: 'Generating embeddings',
  chunksProcessed: 25,
  totalChunks: 30,
};

const mockStatusIndexed: PdfIndexingStatusData = {
  pdfId: 'pdf-123',
  status: 'indexed',
  progress: 100,
  currentStep: 'Complete',
  completedAt: '2024-01-20T12:00:00Z',
};

const mockStatusFailed: PdfIndexingStatusData = {
  pdfId: 'pdf-123',
  status: 'failed',
  progress: 0,
  error: 'Failed to extract text from PDF',
};

// ============================================================================
// Mock Fetch
// ============================================================================

let mockFetchResponse: PdfIndexingStatusData = mockStatusUploaded;

beforeEach(() => {
  vi.clearAllMocks();
  mockFetchResponse = mockStatusUploaded;

  global.fetch = vi.fn(() =>
    Promise.resolve({
      ok: true,
      json: () => Promise.resolve(mockFetchResponse),
    } as Response)
  );
});

afterEach(() => {
  vi.restoreAllMocks();
});

// ============================================================================
// Test Utilities
// ============================================================================

function createTestQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
        gcTime: 0,
      },
    },
  });
}

function renderWithProviders(component: React.ReactElement) {
  const queryClient = createTestQueryClient();
  return render(
    <QueryClientProvider client={queryClient}>
      {component}
    </QueryClientProvider>
  );
}

// ============================================================================
// Tests
// ============================================================================

describe('PdfIndexingStatus', () => {
  const defaultProps = {
    pdfId: 'pdf-123',
  };

  describe('Loading State', () => {
    it('should show loading state initially', () => {
      renderWithProviders(<PdfIndexingStatus {...defaultProps} />);

      expect(screen.getByText(/Caricamento stato/i)).toBeInTheDocument();
    });
  });

  describe('Full Mode Rendering', () => {
    it('should render stage timeline in full mode', async () => {
      mockFetchResponse = mockStatusProcessing;
      renderWithProviders(<PdfIndexingStatus {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByText('Stato Indicizzazione')).toBeInTheDocument();
      });

      // Check stage labels
      expect(screen.getByText('Caricato')).toBeInTheDocument();
      expect(screen.getByText('Elaborazione')).toBeInTheDocument();
      expect(screen.getByText('Estratto')).toBeInTheDocument();
      expect(screen.getByText('Suddiviso')).toBeInTheDocument();
      expect(screen.getByText('Embedding')).toBeInTheDocument();
      expect(screen.getByText('Indicizzato')).toBeInTheDocument();
    });

    it('should display progress bar with percentage', async () => {
      mockFetchResponse = mockStatusProcessing;
      renderWithProviders(<PdfIndexingStatus {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByText('Progresso')).toBeInTheDocument();
      });
    });

    it('should show filename when provided', async () => {
      mockFetchResponse = mockStatusProcessing;
      renderWithProviders(<PdfIndexingStatus {...defaultProps} fileName="rulebook.pdf" />);

      await waitFor(() => {
        expect(screen.getByText('rulebook.pdf')).toBeInTheDocument();
      });
    });
  });

  describe('Compact Mode Rendering', () => {
    it('should render compact badge when processing', async () => {
      mockFetchResponse = mockStatusProcessing;
      renderWithProviders(<PdfIndexingStatus {...defaultProps} compact />);

      await waitFor(() => {
        expect(screen.getByText('Elaborazione')).toBeInTheDocument();
      });
    });

    it('should render RAG Ready badge when indexed', async () => {
      mockFetchResponse = mockStatusIndexed;
      renderWithProviders(<PdfIndexingStatus {...defaultProps} compact />);

      await waitFor(() => {
        expect(screen.getByText('RAG Ready')).toBeInTheDocument();
      });
    });

    it('should render error badge when failed', async () => {
      mockFetchResponse = mockStatusFailed;
      renderWithProviders(<PdfIndexingStatus {...defaultProps} compact />);

      await waitFor(() => {
        expect(screen.getByText('Errore')).toBeInTheDocument();
      });
    });
  });

  describe('Stage Progression', () => {
    it('should show uploaded stage correctly', async () => {
      mockFetchResponse = mockStatusUploaded;
      renderWithProviders(<PdfIndexingStatus {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByText('Caricato')).toBeInTheDocument();
      });
    });

    it('should show chunking progress with counts', async () => {
      mockFetchResponse = mockStatusChunking;
      renderWithProviders(<PdfIndexingStatus {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByText('15/30')).toBeInTheDocument();
      });
    });

    it('should show embedding progress with counts', async () => {
      mockFetchResponse = mockStatusEmbedding;
      renderWithProviders(<PdfIndexingStatus {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByText('25/30')).toBeInTheDocument();
      });
    });
  });

  describe('Completion State', () => {
    it('should show success message when indexed', async () => {
      mockFetchResponse = mockStatusIndexed;
      renderWithProviders(<PdfIndexingStatus {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByText(/indicizzato con successo/i)).toBeInTheDocument();
      });
    });

    it('should show RAG Ready badge in full mode when indexed', async () => {
      mockFetchResponse = mockStatusIndexed;
      renderWithProviders(<PdfIndexingStatus {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByText('RAG Ready')).toBeInTheDocument();
      });
    });

    it('should call onComplete callback when indexed', async () => {
      const onComplete = vi.fn();
      mockFetchResponse = mockStatusIndexed;

      renderWithProviders(<PdfIndexingStatus {...defaultProps} onComplete={onComplete} />);

      await waitFor(() => {
        expect(onComplete).toHaveBeenCalled();
      });
    });
  });

  describe('Error State', () => {
    it('should show error message when failed', async () => {
      mockFetchResponse = mockStatusFailed;
      renderWithProviders(<PdfIndexingStatus {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByText(/Failed to extract text from PDF/i)).toBeInTheDocument();
      });
    });

    it('should show Fallito badge when failed', async () => {
      mockFetchResponse = mockStatusFailed;
      renderWithProviders(<PdfIndexingStatus {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByText('Fallito')).toBeInTheDocument();
      });
    });

    it('should call onError callback when failed', async () => {
      const onError = vi.fn();
      mockFetchResponse = mockStatusFailed;

      renderWithProviders(<PdfIndexingStatus {...defaultProps} onError={onError} />);

      await waitFor(() => {
        expect(onError).toHaveBeenCalledWith('Failed to extract text from PDF');
      });
    });
  });

  describe('Fetch Error Handling', () => {
    it('should show error state when fetch fails', async () => {
      global.fetch = vi.fn(() =>
        Promise.resolve({
          ok: false,
          status: 500,
        } as Response)
      );

      renderWithProviders(<PdfIndexingStatus {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByText(/Errore nel recupero dello stato/i)).toBeInTheDocument();
      });
    });

    it('should show retry button on fetch error', async () => {
      global.fetch = vi.fn(() =>
        Promise.resolve({
          ok: false,
          status: 500,
        } as Response)
      );

      renderWithProviders(<PdfIndexingStatus {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByText('Riprova')).toBeInTheDocument();
      });
    });
  });

  describe('Disabled State', () => {
    it('should not fetch when pdfId is empty', () => {
      renderWithProviders(<PdfIndexingStatus pdfId="" />);

      expect(global.fetch).not.toHaveBeenCalled();
    });
  });
});
