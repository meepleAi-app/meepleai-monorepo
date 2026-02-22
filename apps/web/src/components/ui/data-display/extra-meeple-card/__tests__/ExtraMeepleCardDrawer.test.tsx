/**
 * Tests for ExtraMeepleCardDrawer
 * Issue #5024 — ExtraMeepleCard Drawer System (Epic #5023)
 */

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import {
  DrawerErrorState,
  DrawerLoadingSkeleton,
  ExtraMeepleCardDrawer,
} from '../ExtraMeepleCardDrawer';
import { DRAWER_TEST_IDS } from '../drawer-test-ids';

// ============================================================================
// Mocks
// ============================================================================

// Mock fetch globally
const mockFetch = vi.fn();
vi.stubGlobal('fetch', mockFetch);

// Mock matchMedia (required by jsdom for responsive components)
vi.stubGlobal('matchMedia', (query: string) => ({
  matches: false,
  media: query,
  onchange: null,
  addListener: vi.fn(),
  removeListener: vi.fn(),
  addEventListener: vi.fn(),
  removeEventListener: vi.fn(),
  dispatchEvent: vi.fn(),
}));

const mockGameApiResponse = {
  id: 'game-1',
  title: 'Catan',
  publisher: 'Kosmos',
  yearPublished: 1995,
  minPlayers: 3,
  maxPlayers: 4,
  playTimeMinutes: 90,
  description: 'Trade, build, settle.',
  averageRating: 7.8,
  totalPlays: 150,
  faqCount: 12,
  rulesDocumentCount: 3,
};

function mockSuccessfulFetch(data = mockGameApiResponse) {
  mockFetch.mockResolvedValue({
    ok: true,
    json: () => Promise.resolve(data),
  } as Response);
}

function mockFailedFetch(status = 404) {
  mockFetch.mockResolvedValue({
    ok: false,
    status,
    json: () => Promise.resolve({ message: 'Not found' }),
  } as Response);
}

// ============================================================================
// Test Helpers
// ============================================================================

function renderDrawer(props: Partial<React.ComponentProps<typeof ExtraMeepleCardDrawer>> = {}) {
  const defaultProps = {
    entityType: 'game' as const,
    entityId: 'game-1',
    open: true,
    onClose: vi.fn(),
  };
  return render(<ExtraMeepleCardDrawer {...defaultProps} {...props} />);
}

// ============================================================================
// ExtraMeepleCardDrawer Tests
// ============================================================================

describe('ExtraMeepleCardDrawer', () => {
  beforeEach(() => {
    mockFetch.mockReset();
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  // --------------------------------------------------------------------------
  // Open / Close
  // --------------------------------------------------------------------------

  describe('open and close behavior', () => {
    it('renders the panel header when open', async () => {
      mockSuccessfulFetch();
      renderDrawer({ entityType: 'game', open: true });

      expect(screen.getByTestId(DRAWER_TEST_IDS.ENTITY_LABEL)).toBeInTheDocument();
    });

    it('does not render content when closed', () => {
      renderDrawer({ open: false });

      expect(screen.queryByText('Dettaglio Gioco')).not.toBeInTheDocument();
    });

    it('calls onClose when close button is clicked', async () => {
      mockSuccessfulFetch();
      const onClose = vi.fn();
      renderDrawer({ onClose });

      const closeButton = screen.getByRole('button', { name: /chiudi pannello/i });
      await userEvent.click(closeButton);

      expect(onClose).toHaveBeenCalledTimes(1);
    });

    it('calls onClose when Escape is pressed', async () => {
      mockSuccessfulFetch();
      const onClose = vi.fn();
      renderDrawer({ onClose });

      await userEvent.keyboard('{Escape}');

      expect(onClose).toHaveBeenCalledTimes(1);
    });
  });

  // --------------------------------------------------------------------------
  // Entity Header
  // --------------------------------------------------------------------------

  describe('entity header', () => {
    it('shows correct label for game entity', async () => {
      mockSuccessfulFetch();
      renderDrawer({ entityType: 'game' });

      expect(screen.getByTestId(DRAWER_TEST_IDS.ENTITY_LABEL)).toHaveTextContent('Dettaglio Gioco');
    });

    it('shows correct label for agent entity', () => {
      renderDrawer({ entityType: 'agent', entityId: 'agent-1' });

      expect(screen.getByTestId(DRAWER_TEST_IDS.ENTITY_LABEL)).toHaveTextContent('Dettaglio Agente');
    });

    it('shows correct label for chat entity', () => {
      renderDrawer({ entityType: 'chat', entityId: 'thread-1' });

      expect(screen.getByTestId(DRAWER_TEST_IDS.ENTITY_LABEL)).toHaveTextContent('Dettaglio Chat');
    });

    it('shows correct label for kb entity', () => {
      renderDrawer({ entityType: 'kb', entityId: 'doc-1' });

      expect(screen.getByTestId(DRAWER_TEST_IDS.ENTITY_LABEL)).toHaveTextContent('Documento KB');
    });

    it('has an accessible title for screen readers', async () => {
      mockSuccessfulFetch();
      renderDrawer({ entityType: 'game' });

      // SheetTitle renders as an sr-only heading for screen reader accessibility
      const srTitle = screen
        .getAllByRole('heading')
        .find((el) => el.classList.contains('sr-only'));
      expect(srTitle).toBeInTheDocument();
      expect(srTitle).toHaveTextContent('Dettaglio Gioco');
    });
  });

  // --------------------------------------------------------------------------
  // Entity Routing
  // --------------------------------------------------------------------------

  describe('entity routing', () => {
    it('shows skeleton initially while fetching game data', () => {
      // fetch will never resolve during this check
      mockFetch.mockReturnValue(new Promise(() => {}));
      renderDrawer({ entityType: 'game', entityId: 'game-1' });

      expect(screen.getByTestId(DRAWER_TEST_IDS.LOADING_SKELETON)).toBeInTheDocument();
    });

    it('renders game content after successful fetch', async () => {
      mockSuccessfulFetch();
      renderDrawer({ entityType: 'game', entityId: 'game-1' });

      await waitFor(() => {
        expect(screen.getByText('Catan')).toBeInTheDocument();
      });
    });

    it('shows skeleton initially while fetching agent data', () => {
      // fetch never resolves during this check
      mockFetch.mockReturnValue(new Promise(() => {}));
      renderDrawer({ entityType: 'agent', entityId: 'agent-1' });

      expect(screen.getByTestId(DRAWER_TEST_IDS.LOADING_SKELETON)).toBeInTheDocument();
    });

    it('renders agent content after successful fetch', async () => {
      const agentApiResponse = {
        id: 'agent-1',
        name: 'Catan Expert',
        type: 'qa',
        strategyName: 'hybrid-rag',
        strategyParameters: {},
        isActive: true,
        isIdle: false,
        invocationCount: 5,
        lastInvokedAt: null,
        createdAt: '2026-01-01T00:00:00Z',
      };
      mockFetch.mockImplementation((url: string) => {
        if ((url as string).includes('/api/v1/agents/')) {
          return Promise.resolve({ ok: true, json: () => Promise.resolve(agentApiResponse) } as Response);
        }
        // threads / docs secondary calls
        return Promise.resolve({ ok: true, json: () => Promise.resolve([]) } as Response);
      });
      renderDrawer({ entityType: 'agent', entityId: 'agent-1' });

      await waitFor(() => {
        expect(screen.getByText('Catan Expert')).toBeInTheDocument();
      });
    });

    it('shows skeleton initially while fetching chat data', () => {
      mockFetch.mockReturnValue(new Promise(() => {}));
      renderDrawer({ entityType: 'chat', entityId: 'thread-1' });

      expect(screen.getByTestId(DRAWER_TEST_IDS.LOADING_SKELETON)).toBeInTheDocument();
    });

    it('renders chat content after successful fetch', async () => {
      const threadApiResponse = {
        id: 'thread-1',
        status: 'active',
        agentId: 'agent-1',
        agentName: 'Catan Expert',
        agentModel: 'gpt-4o',
        gameId: 'game-1',
        gameName: 'Catan',
        startedAt: '2026-01-15T10:00:00Z',
        messageCount: 2,
      };
      mockFetch.mockImplementation((url: string) => {
        if ((url as string).includes('/messages')) {
          return Promise.resolve({ ok: true, json: () => Promise.resolve([]) } as Response);
        }
        return Promise.resolve({ ok: true, json: () => Promise.resolve(threadApiResponse) } as Response);
      });
      renderDrawer({ entityType: 'chat', entityId: 'thread-1' });

      await waitFor(() => {
        expect(screen.getByText('Catan Expert')).toBeInTheDocument();
      });
    });

    it('shows skeleton initially while fetching kb data', () => {
      mockFetch.mockReturnValue(new Promise(() => {}));
      renderDrawer({ entityType: 'kb', entityId: 'doc-1' });

      expect(screen.getByTestId(DRAWER_TEST_IDS.LOADING_SKELETON)).toBeInTheDocument();
    });

    it('renders kb content after successful fetch', async () => {
      const kbApiResponse = {
        id: 'doc-1',
        fileName: 'catan-rules.pdf',
        processingStatus: 'indexed',
        fileSize: 204800,
        pageCount: 10,
        characterCount: 5000,
        uploadedAt: '2026-01-10T09:00:00Z',
        processedAt: '2026-01-10T09:05:00Z',
        extractedText: 'Regole del gioco...',
      };
      mockFetch.mockResolvedValue({
        ok: true,
        json: () => Promise.resolve(kbApiResponse),
      } as Response);
      renderDrawer({ entityType: 'kb', entityId: 'doc-1' });

      await waitFor(() => {
        expect(screen.getAllByText('catan-rules.pdf').length).toBeGreaterThan(0);
      });
    });
  });

  // --------------------------------------------------------------------------
  // Loading State
  // --------------------------------------------------------------------------

  describe('loading state', () => {
    it('shows loading skeleton while fetching game data', () => {
      mockFetch.mockReturnValue(new Promise(() => {}));
      renderDrawer({ entityType: 'game' });

      const skeleton = screen.getByTestId(DRAWER_TEST_IDS.LOADING_SKELETON);
      expect(skeleton).toBeInTheDocument();
      expect(skeleton).toHaveAttribute('aria-busy', 'true');
    });

    it('hides skeleton after data loads', async () => {
      mockSuccessfulFetch();
      renderDrawer({ entityType: 'game' });

      await waitFor(() => {
        expect(screen.queryByTestId(DRAWER_TEST_IDS.LOADING_SKELETON)).not.toBeInTheDocument();
      });
    });
  });

  // --------------------------------------------------------------------------
  // Error State
  // --------------------------------------------------------------------------

  describe('error state', () => {
    it('shows error state when fetch fails', async () => {
      mockFailedFetch(404);
      renderDrawer({ entityType: 'game', entityId: 'missing-id' });

      await waitFor(() => {
        expect(screen.getByTestId(DRAWER_TEST_IDS.ERROR_STATE)).toBeInTheDocument();
      });
    });

    it('shows error message on fetch failure', async () => {
      mockFailedFetch(500);
      renderDrawer({ entityType: 'game' });

      await waitFor(() => {
        expect(screen.getByText('Si è verificato un errore')).toBeInTheDocument();
      });
    });

    it('shows retry button on error', async () => {
      mockFailedFetch(500);
      renderDrawer({ entityType: 'game' });

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /riprova/i })).toBeInTheDocument();
      });
    });

    it('retries fetch when retry button is clicked', async () => {
      mockFailedFetch(500);
      renderDrawer({ entityType: 'game' });

      await waitFor(() => screen.getByRole('button', { name: /riprova/i }));
      expect(mockFetch).toHaveBeenCalledTimes(1);

      // Setup successful response for retry
      mockSuccessfulFetch();
      await userEvent.click(screen.getByRole('button', { name: /riprova/i }));

      expect(mockFetch).toHaveBeenCalledTimes(2);
      await waitFor(() => {
        expect(screen.getByText('Catan')).toBeInTheDocument();
      });
    });

    it('shows error state when fetch throws network error', async () => {
      mockFetch.mockRejectedValue(new Error('Network error'));
      renderDrawer({ entityType: 'game' });

      await waitFor(() => {
        expect(screen.getByText('Network error')).toBeInTheDocument();
      });
    });
  });

  // --------------------------------------------------------------------------
  // Props
  // --------------------------------------------------------------------------

  describe('props', () => {
    it('passes data-testid to the drawer panel', async () => {
      mockSuccessfulFetch();
      renderDrawer({ 'data-testid': 'my-drawer' });

      // The SheetPrimitive.Content gets the testId
      await waitFor(() => {
        expect(document.querySelector('[data-testid="my-drawer"]')).toBeTruthy();
      });
    });

    it('fetches with the provided entityId', async () => {
      mockSuccessfulFetch();
      renderDrawer({ entityType: 'game', entityId: 'specific-game-id' });

      await waitFor(() => {
        expect(mockFetch).toHaveBeenCalledWith('/api/v1/library/games/specific-game-id', expect.objectContaining({ signal: expect.any(AbortSignal) }));
      });
    });
  });
});

// ============================================================================
// DrawerLoadingSkeleton Tests
// ============================================================================

describe('DrawerLoadingSkeleton', () => {
  it('renders with default testid', () => {
    render(<DrawerLoadingSkeleton />);

    expect(screen.getByTestId(DRAWER_TEST_IDS.LOADING_SKELETON)).toBeInTheDocument();
  });

  it('renders with custom testid', () => {
    render(<DrawerLoadingSkeleton data-testid="custom-skeleton" />);

    expect(screen.getByTestId('custom-skeleton')).toBeInTheDocument();
  });

  it('has aria-busy attribute', () => {
    render(<DrawerLoadingSkeleton />);

    expect(screen.getByTestId(DRAWER_TEST_IDS.LOADING_SKELETON)).toHaveAttribute('aria-busy', 'true');
  });

  it('renders multiple animated placeholder elements', () => {
    render(<DrawerLoadingSkeleton />);

    const animated = document.querySelectorAll('.animate-pulse');
    expect(animated.length).toBeGreaterThanOrEqual(3);
  });
});

// ============================================================================
// DrawerErrorState Tests
// ============================================================================

describe('DrawerErrorState', () => {
  it('renders with default testid', () => {
    render(<DrawerErrorState error="Something went wrong" />);

    expect(screen.getByTestId(DRAWER_TEST_IDS.ERROR_STATE)).toBeInTheDocument();
  });

  it('renders error message', () => {
    render(<DrawerErrorState error="Gioco non trovato" />);

    expect(screen.getByText('Gioco non trovato')).toBeInTheDocument();
  });

  it('renders fixed header text', () => {
    render(<DrawerErrorState error="Any error" />);

    expect(screen.getByText('Si è verificato un errore')).toBeInTheDocument();
  });

  it('renders retry button when onRetry is provided', () => {
    const onRetry = vi.fn();
    render(<DrawerErrorState error="Error" onRetry={onRetry} />);

    expect(screen.getByRole('button', { name: /riprova/i })).toBeInTheDocument();
  });

  it('does not render retry button when onRetry is not provided', () => {
    render(<DrawerErrorState error="Error" />);

    expect(screen.queryByRole('button', { name: /riprova/i })).not.toBeInTheDocument();
  });

  it('calls onRetry when retry button is clicked', async () => {
    const onRetry = vi.fn();
    render(<DrawerErrorState error="Error" onRetry={onRetry} />);

    await userEvent.click(screen.getByRole('button', { name: /riprova/i }));

    expect(onRetry).toHaveBeenCalledTimes(1);
  });

  it('has role="alert" for accessibility', () => {
    render(<DrawerErrorState error="Error" />);

    expect(screen.getByRole('alert')).toBeInTheDocument();
  });

  it('renders with custom testid', () => {
    render(<DrawerErrorState error="Error" data-testid="my-error" />);

    expect(screen.getByTestId('my-error')).toBeInTheDocument();
  });
});
