/**
 * RulebookSection Component Tests
 *
 * Tests for the RulebookSection component states:
 * - Empty state: upload button shown
 * - Processing state: spinner and "elaborazione" text
 * - Ready state: file name and chat button
 * - Failed state: error text and retry/remove actions
 * - Multiple rulebooks rendered
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

// Mock the upload hook before importing the component
vi.mock('@/lib/hooks/use-rulebook-upload', () => ({
  useRulebookUpload: () => ({
    mutateAsync: vi.fn(),
    isPending: false,
  }),
}));

// Mock sonner toast
vi.mock('sonner', () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
    info: vi.fn(),
  },
}));

import { RulebookSection } from '@/components/game/rulebook-section';

function renderWithQuery(ui: React.ReactElement) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(<QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>);
}

const BASE_PROPS = {
  gameId: 'game-123',
  rulebooks: [],
};

describe('RulebookSection', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Empty state (no rulebooks)', () => {
    it('renders upload button when no rulebooks', () => {
      renderWithQuery(<RulebookSection {...BASE_PROPS} />);
      expect(screen.getByRole('button', { name: /carica regolamento/i })).toBeInTheDocument();
    });

    it('shows singular title "Regolamento" with no rulebooks', () => {
      renderWithQuery(<RulebookSection {...BASE_PROPS} />);
      expect(screen.getByText('Regolamento')).toBeInTheDocument();
    });

    it('does not show the "Pronto" header badge when no rulebooks', () => {
      renderWithQuery(<RulebookSection {...BASE_PROPS} />);
      // The header "Pronto" badge should not appear
      expect(screen.queryByText('Pronto')).not.toBeInTheDocument();
    });
  });

  describe('Processing state', () => {
    const processingRulebook = {
      pdfDocumentId: 'pdf-1',
      fileName: 'rulebook.pdf',
      kbStatus: 'processing' as const,
      indexedAt: null,
    };

    it('renders processing text with "elaborazione"', () => {
      renderWithQuery(<RulebookSection {...BASE_PROPS} rulebooks={[processingRulebook]} />);
      expect(screen.getByText(/in elaborazione/i)).toBeInTheDocument();
    });

    it('renders the file name', () => {
      renderWithQuery(<RulebookSection {...BASE_PROPS} rulebooks={[processingRulebook]} />);
      expect(screen.getByText('rulebook.pdf')).toBeInTheDocument();
    });

    it('does not show chat button when only processing', () => {
      const onChatClick = vi.fn();
      renderWithQuery(
        <RulebookSection
          {...BASE_PROPS}
          rulebooks={[processingRulebook]}
          onChatClick={onChatClick}
        />
      );
      expect(screen.queryByRole('button', { name: /chatta/i })).not.toBeInTheDocument();
    });
  });

  describe('Ready state', () => {
    const readyRulebook = {
      pdfDocumentId: 'pdf-2',
      fileName: 'catan-rulebook.pdf',
      kbStatus: 'ready' as const,
      indexedAt: '2026-03-18T00:00:00Z',
    };

    it('renders the file name', () => {
      renderWithQuery(<RulebookSection {...BASE_PROPS} rulebooks={[readyRulebook]} />);
      expect(screen.getByText('catan-rulebook.pdf')).toBeInTheDocument();
    });

    it('renders "Pronto" status text for ready rulebook', () => {
      renderWithQuery(<RulebookSection {...BASE_PROPS} rulebooks={[readyRulebook]} />);
      // At least one "Pronto" text visible (could be header badge or row)
      expect(screen.getAllByText('Pronto').length).toBeGreaterThanOrEqual(1);
    });

    it('renders chat button when ready and onChatClick provided', () => {
      const onChatClick = vi.fn();
      renderWithQuery(
        <RulebookSection {...BASE_PROPS} rulebooks={[readyRulebook]} onChatClick={onChatClick} />
      );
      expect(screen.getByRole('button', { name: /chatta/i })).toBeInTheDocument();
    });

    it('does not render chat button when onChatClick not provided', () => {
      renderWithQuery(<RulebookSection {...BASE_PROPS} rulebooks={[readyRulebook]} />);
      expect(screen.queryByRole('button', { name: /chatta/i })).not.toBeInTheDocument();
    });

    it('shows "Carica altro" upload button when rulebooks exist', () => {
      renderWithQuery(<RulebookSection {...BASE_PROPS} rulebooks={[readyRulebook]} />);
      expect(screen.getByRole('button', { name: /carica altro/i })).toBeInTheDocument();
    });
  });

  describe('Failed state', () => {
    const failedRulebook = {
      pdfDocumentId: 'pdf-3',
      fileName: 'failed-rulebook.pdf',
      kbStatus: 'failed' as const,
      indexedAt: null,
    };

    it('renders "Elaborazione fallita" text', () => {
      renderWithQuery(<RulebookSection {...BASE_PROPS} rulebooks={[failedRulebook]} />);
      expect(screen.getByText(/elaborazione fallita/i)).toBeInTheDocument();
    });

    it('renders retry button when onRetry provided', () => {
      const onRetry = vi.fn();
      renderWithQuery(
        <RulebookSection {...BASE_PROPS} rulebooks={[failedRulebook]} onRetry={onRetry} />
      );
      // RefreshCw icon button should be present (aria-label from lucide icon)
      const buttons = screen.getAllByRole('button');
      // At minimum 2: retry icon + "Carica altro"
      expect(buttons.length).toBeGreaterThanOrEqual(2);
    });

    it('does not render retry button when onRetry not provided', () => {
      renderWithQuery(<RulebookSection {...BASE_PROPS} rulebooks={[failedRulebook]} />);
      // Only "Carica altro" button
      const buttons = screen.getAllByRole('button');
      expect(buttons).toHaveLength(1);
    });

    it('renders remove button when onRemove provided', () => {
      const onRemove = vi.fn();
      renderWithQuery(
        <RulebookSection {...BASE_PROPS} rulebooks={[failedRulebook]} onRemove={onRemove} />
      );
      const buttons = screen.getAllByRole('button');
      // At minimum: remove icon + "Carica altro"
      expect(buttons.length).toBeGreaterThanOrEqual(2);
    });

    it('does not show chat button for failed state', () => {
      const onChatClick = vi.fn();
      renderWithQuery(
        <RulebookSection {...BASE_PROPS} rulebooks={[failedRulebook]} onChatClick={onChatClick} />
      );
      expect(screen.queryByRole('button', { name: /chatta/i })).not.toBeInTheDocument();
    });
  });

  describe('Multiple rulebooks', () => {
    const multipleRulebooks = [
      {
        pdfDocumentId: 'pdf-a',
        fileName: 'base-rulebook.pdf',
        kbStatus: 'ready' as const,
        indexedAt: '2026-03-01T00:00:00Z',
      },
      {
        pdfDocumentId: 'pdf-b',
        fileName: 'expansion-rulebook.pdf',
        kbStatus: 'processing' as const,
        indexedAt: null,
      },
      {
        pdfDocumentId: 'pdf-c',
        fileName: 'promo-rulebook.pdf',
        kbStatus: 'failed' as const,
        indexedAt: null,
      },
    ];

    it('renders all file names', () => {
      renderWithQuery(<RulebookSection {...BASE_PROPS} rulebooks={multipleRulebooks} />);
      expect(screen.getByText('base-rulebook.pdf')).toBeInTheDocument();
      expect(screen.getByText('expansion-rulebook.pdf')).toBeInTheDocument();
      expect(screen.getByText('promo-rulebook.pdf')).toBeInTheDocument();
    });

    it('shows plural title "Regolamenti" for multiple rulebooks', () => {
      renderWithQuery(<RulebookSection {...BASE_PROPS} rulebooks={multipleRulebooks} />);
      expect(screen.getByText('Regolamenti')).toBeInTheDocument();
    });

    it('shows chat button when at least one is ready', () => {
      const onChatClick = vi.fn();
      renderWithQuery(
        <RulebookSection {...BASE_PROPS} rulebooks={multipleRulebooks} onChatClick={onChatClick} />
      );
      expect(screen.getByRole('button', { name: /chatta/i })).toBeInTheDocument();
    });

    it('shows processing text for the processing rulebook', () => {
      renderWithQuery(<RulebookSection {...BASE_PROPS} rulebooks={multipleRulebooks} />);
      expect(screen.getByText(/in elaborazione/i)).toBeInTheDocument();
    });

    it('shows failed text for the failed rulebook', () => {
      renderWithQuery(<RulebookSection {...BASE_PROPS} rulebooks={multipleRulebooks} />);
      expect(screen.getByText(/elaborazione fallita/i)).toBeInTheDocument();
    });
  });
});
