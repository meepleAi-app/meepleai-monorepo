/**
 * KnowledgeBaseTab Component Tests (Issue #4229)
 *
 * Issue #4138: these tests were written around the per-agent fetch — `useGameAgents` plus one
 * `api.agents.getDocuments(agent.id)` call per agent, and fixtures of `AgentDocumentsDto`. The tab
 * now reads the per-game listing (`useDocumentsByGame`), because `/agents/{id}/documents` is not
 * mounted on the backend and documents belong to a game, not to an agent.
 *
 * The test this file was missing is the one added below: **a failed fetch must not look like an
 * empty KB**. That confusion is precisely how the unmounted route stayed invisible — the error
 * surfaced as "No Documents Indexed Yet", which is also the legitimate empty state.
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { KnowledgeBaseTab } from '../KnowledgeBaseTab';
import * as useDocumentsByGameModule from '@/hooks/queries/useDocumentsByGame';
import * as useEmbeddingStatusModule from '@/hooks/useEmbeddingStatus';
import type { PdfDocumentDto } from '@/lib/api/schemas/pdf.schemas';

// ============================================================================
// Test Setup
// ============================================================================

const GAME_ID = '99999999-9999-9999-9999-999999999999';

const createTestQueryClient = () =>
  new QueryClient({
    defaultOptions: {
      queries: { retry: false },
      mutations: { retry: false },
    },
  });

const renderWithQueryClient = (ui: React.ReactElement) => {
  const queryClient = createTestQueryClient();
  return render(<QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>);
};

function doc(overrides: Partial<PdfDocumentDto> & Pick<PdfDocumentDto, 'id' | 'fileName'>) {
  return {
    gameId: GAME_ID,
    filePath: `/pdfs/${overrides.fileName}`,
    fileSizeBytes: 1024,
    processingStatus: 'completed',
    uploadedAt: '2024-01-01T00:00:00Z',
    processedAt: '2024-01-01T00:05:00Z',
    pageCount: 12,
    documentType: 'base' as const,
    isPublic: false,
    processingState: 'Completed',
    progressPercentage: 100,
    retryCount: 0,
    maxRetries: 3,
    ...overrides,
  } as PdfDocumentDto;
}

/** Shapes the hook's return so only what the component reads needs to be supplied. */
function mockDocuments(
  state: Partial<{ data: PdfDocumentDto[]; isLoading: boolean; error: Error | null }>
) {
  vi.spyOn(useDocumentsByGameModule, 'useDocumentsByGame').mockReturnValue({
    data: state.data,
    isLoading: state.isLoading ?? false,
    error: state.error ?? null,
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
  } as any);
}

// ============================================================================
// Tests
// ============================================================================

describe('KnowledgeBaseTab', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.spyOn(useEmbeddingStatusModule, 'useEmbeddingStatus').mockReturnValue({
      data: { status: 'Completed', progress: 100, totalChunks: 10, processedChunks: 10 },
      isLoading: false,
      isPolling: false,
      isReady: true,
      isFailed: false,
      stageLabel: 'Completed',
      chunkProgress: { processed: 10, total: 10 },
      error: null,
      refetch: vi.fn(),
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
    } as any);
  });

  describe('Loading States', () => {
    it('renders loading state while documents are being fetched', () => {
      mockDocuments({ isLoading: true });

      renderWithQueryClient(<KnowledgeBaseTab gameId={GAME_ID} />);

      expect(screen.getByText(/Loading Knowledge Base/i)).toBeInTheDocument();
    });
  });

  describe('Error States', () => {
    // 🔴 The assertion the old suite lacked. A failed fetch rendering as "no documents" is how
    // the unmounted /agents/{id}/documents route stayed invisible: the two states are
    // indistinguishable to a reader, so the bug looked like an empty knowledge base.
    it('renders an error, NOT the empty state, when the fetch fails', () => {
      mockDocuments({ error: new Error('Network error') });

      renderWithQueryClient(<KnowledgeBaseTab gameId={GAME_ID} />);

      expect(screen.getByText(/Failed to load documents/i)).toBeInTheDocument();
      expect(screen.queryByText(/No Documents Indexed Yet/i)).not.toBeInTheDocument();
    });
  });

  describe('Empty States', () => {
    it('renders the empty state when the game has no documents', () => {
      mockDocuments({ data: [] });

      renderWithQueryClient(<KnowledgeBaseTab gameId={GAME_ID} />);

      expect(screen.getByText(/No Documents Indexed Yet/i)).toBeInTheDocument();
      expect(screen.queryByText(/Failed to load documents/i)).not.toBeInTheDocument();
    });
  });

  describe('Document Display', () => {
    it('lists the documents of the game', async () => {
      mockDocuments({
        data: [
          doc({ id: '33333333-3333-3333-3333-333333333333', fileName: 'rulebook.pdf' }),
          doc({
            id: '44444444-4444-4444-4444-444444444444',
            fileName: 'errata-v1.1.pdf',
            documentType: 'errata',
          }),
        ],
      });

      renderWithQueryClient(<KnowledgeBaseTab gameId={GAME_ID} />);

      await waitFor(() => {
        expect(screen.getByText('rulebook.pdf')).toBeInTheDocument();
      });
      expect(screen.getByText('errata-v1.1.pdf')).toBeInTheDocument();
      expect(screen.getByText(/2 documents indexed/i)).toBeInTheDocument();
    });

    // The retired card showed `document.gameName` as the title, so every row displayed the same
    // text. Two documents of one game is the fixture that catches it.
    it('shows each document FILE name, not the game name', async () => {
      mockDocuments({
        data: [
          doc({ id: '33333333-3333-3333-3333-333333333333', fileName: 'rulebook.pdf' }),
          doc({ id: '44444444-4444-4444-4444-444444444444', fileName: 'homerules.pdf' }),
        ],
      });

      renderWithQueryClient(<KnowledgeBaseTab gameId={GAME_ID} />);

      await waitFor(() => {
        expect(screen.getByText('rulebook.pdf')).toBeInTheDocument();
      });
      expect(screen.getByText('homerules.pdf')).toBeInTheDocument();
    });

    it('maps the string documentType to a label', async () => {
      mockDocuments({
        data: [
          doc({
            id: '33333333-3333-3333-3333-333333333333',
            fileName: 'a.pdf',
            documentType: 'base',
          }),
          doc({
            id: '44444444-4444-4444-4444-444444444444',
            fileName: 'b.pdf',
            documentType: 'expansion',
          }),
          doc({
            id: '55555555-5555-5555-5555-555555555555',
            fileName: 'c.pdf',
            documentType: 'homerule',
          }),
        ],
      });

      renderWithQueryClient(<KnowledgeBaseTab gameId={GAME_ID} />);

      await waitFor(() => {
        expect(screen.getByText('Rulebook')).toBeInTheDocument();
      });
      expect(screen.getByText('Expansion')).toBeInTheDocument();
      expect(screen.getByText('Homerule')).toBeInTheDocument();
    });

    it('singularises the count for one document', async () => {
      mockDocuments({
        data: [doc({ id: '33333333-3333-3333-3333-333333333333', fileName: 'only.pdf' })],
      });

      renderWithQueryClient(<KnowledgeBaseTab gameId={GAME_ID} />);

      await waitFor(() => {
        expect(screen.getByText(/1 document indexed/i)).toBeInTheDocument();
      });
    });
  });

  describe('Accessibility', () => {
    it('gives the truncated file name a title attribute', async () => {
      mockDocuments({
        data: [
          doc({ id: '33333333-3333-3333-3333-333333333333', fileName: 'a-very-long-name.pdf' }),
        ],
      });

      renderWithQueryClient(<KnowledgeBaseTab gameId={GAME_ID} />);

      await waitFor(() => {
        expect(screen.getByTitle('a-very-long-name.pdf')).toBeInTheDocument();
      });
    });
  });
});
