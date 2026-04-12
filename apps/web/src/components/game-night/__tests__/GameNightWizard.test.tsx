import { createElement, type ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi, describe, it, expect, beforeEach } from 'vitest';

const mockSearch = vi.fn();
const mockCreateSession = vi.fn();
const mockAddPlayer = vi.fn();
const mockStartSession = vi.fn();
const mockGetUserGameKbStatus = vi.fn();
const mockPush = vi.fn();

vi.mock('@/lib/api', () => ({
  api: {
    sharedGames: {
      search: (...args: unknown[]) => mockSearch(...args),
    },
    liveSessions: {
      createSession: (...args: unknown[]) => mockCreateSession(...args),
      addPlayer: (...args: unknown[]) => mockAddPlayer(...args),
      startSession: (...args: unknown[]) => mockStartSession(...args),
    },
    knowledgeBase: {
      getUserGameKbStatus: (...args: unknown[]) => mockGetUserGameKbStatus(...args),
    },
  },
}));

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: mockPush }),
}));

vi.mock('next/image', () => ({
  default: (props: Record<string, unknown>) => <img {...props} />,
}));

vi.mock('@/components/library/PdfProcessingStatus', () => ({
  PdfProcessingStatus: () => <div data-testid="pdf-processing-status">Processing...</div>,
}));

vi.mock('@/components/pdf/CopyrightDisclaimerModal', () => ({
  CopyrightDisclaimerModal: ({ open, onAccept }: { open: boolean; onAccept: () => void }) =>
    open ? (
      <div data-testid="copyright-modal">
        <button data-testid="accept-disclaimer" onClick={onAccept}>
          Confermo
        </button>
      </div>
    ) : null,
}));

import { GameNightWizard } from '../GameNightWizard';

function renderWizard(onComplete: (sessionId: string) => void): ReactNode {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false, staleTime: 0 } },
  });
  return render(
    createElement(
      QueryClientProvider,
      { client: queryClient },
      createElement(GameNightWizard, { onComplete })
    )
  );
}

describe('GameNightWizard', () => {
  const onComplete = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
    // Default KB status: indexed=false so warning appears but tests still proceed
    mockGetUserGameKbStatus.mockResolvedValue({
      gameId: 'g1',
      isIndexed: false,
      documentCount: 0,
      coverageScore: 0,
      coverageLevel: 'None',
      suggestedQuestions: [],
    });
  });

  it('renders step 1: find game', () => {
    renderWizard(onComplete);

    expect(screen.getByTestId('game-night-wizard')).toBeInTheDocument();
    expect(screen.getByTestId('search-game-step')).toBeInTheDocument();
    expect(screen.getByTestId('game-search-input')).toBeInTheDocument();
  });

  it('searches catalog via Enter key and shows results', async () => {
    const user = userEvent.setup();
    mockSearch.mockResolvedValue({
      items: [{ id: 'g1', title: 'Agricola', thumbnailUrl: '/thumb.jpg', yearPublished: 2007 }],
      totalCount: 1,
    });

    renderWizard(onComplete);

    await user.type(screen.getByTestId('game-search-input'), 'Agricola{Enter}');

    await waitFor(() => {
      expect(mockSearch).toHaveBeenCalledTimes(1);
      expect(mockSearch).toHaveBeenCalledWith(expect.objectContaining({ searchTerm: 'Agricola' }));
    });

    const results = await screen.findByTestId('game-search-results');
    expect(results).toBeInTheDocument();
    expect(screen.getByText('Agricola')).toBeInTheDocument();
  });

  it('advances to step 2 on game selection, then to step 3 on skip', async () => {
    const user = userEvent.setup();
    mockSearch.mockResolvedValue({
      items: [{ id: 'g1', title: 'Catan', thumbnailUrl: null, yearPublished: 1995 }],
      totalCount: 1,
    });

    renderWizard(onComplete);

    // Search and select
    await user.type(screen.getByTestId('game-search-input'), 'Catan{Enter}');
    await waitFor(() => expect(screen.getByText('Catan')).toBeInTheDocument());
    await user.click(screen.getByText('Catan'));
    // Confirm selection (MVP hardening F1: user sees KB status before advancing)
    await user.click(screen.getByTestId('confirm-game-button'));

    // Should be on step 2
    expect(screen.getByTestId('upload-rules-step')).toBeInTheDocument();

    // Skip upload
    await user.click(screen.getByTestId('skip-rules-button'));

    // Should be on step 3
    expect(screen.getByTestId('create-session-step')).toBeInTheDocument();
  });

  it('creates session with players on step 3', async () => {
    const user = userEvent.setup();
    mockSearch.mockResolvedValue({
      items: [{ id: 'g1', title: 'Ticket to Ride', thumbnailUrl: null }],
      totalCount: 1,
    });
    mockCreateSession.mockResolvedValue('sess-abc');
    mockAddPlayer.mockResolvedValue(undefined);
    mockStartSession.mockResolvedValue(undefined);

    renderWizard(onComplete);

    // Step 1: search + select + confirm
    await user.type(screen.getByTestId('game-search-input'), 'Ticket{Enter}');
    await waitFor(() => expect(screen.getByText('Ticket to Ride')).toBeInTheDocument());
    await user.click(screen.getByText('Ticket to Ride'));
    await user.click(screen.getByTestId('confirm-game-button'));

    // Step 2: skip
    await user.click(screen.getByTestId('skip-rules-button'));

    // Step 3: add players
    await user.type(screen.getByTestId('player-input-0'), 'Marco');
    await user.type(screen.getByTestId('player-input-1'), 'Luca');

    // Create session
    await user.click(screen.getByTestId('create-session-button'));

    await waitFor(() => {
      expect(mockCreateSession).toHaveBeenCalledWith(
        expect.objectContaining({ gameId: 'g1', gameName: 'Ticket to Ride' })
      );
    });

    expect(mockAddPlayer).toHaveBeenCalledTimes(2);
    expect(mockStartSession).toHaveBeenCalledWith('sess-abc');
    expect(onComplete).toHaveBeenCalledWith('sess-abc');
  });

  it('allows adding and removing players', async () => {
    const user = userEvent.setup();
    mockSearch.mockResolvedValue({
      items: [{ id: 'g1', title: 'Splendor' }],
      totalCount: 1,
    });

    renderWizard(onComplete);

    // Navigate to step 3
    await user.type(screen.getByTestId('game-search-input'), 'Splendor{Enter}');
    await waitFor(() => expect(screen.getByText('Splendor')).toBeInTheDocument());
    await user.click(screen.getByText('Splendor'));
    await user.click(screen.getByTestId('confirm-game-button'));
    await user.click(screen.getByTestId('skip-rules-button'));

    // Should start with 2 player inputs
    expect(screen.getByTestId('player-input-0')).toBeInTheDocument();
    expect(screen.getByTestId('player-input-1')).toBeInTheDocument();

    // Add a player
    await user.click(screen.getByTestId('add-player-button'));
    expect(screen.getByTestId('player-input-2')).toBeInTheDocument();
  });

  describe('PDF-aware soft filter (MVP hardening F1)', () => {
    it('shows a KB badge next to each game result', async () => {
      const user = userEvent.setup();
      mockSearch.mockResolvedValue({
        items: [
          { id: 'g1', title: 'Agricola', thumbnailUrl: null, yearPublished: 2007 },
          { id: 'g2', title: 'Brass', thumbnailUrl: null, yearPublished: 2007 },
        ],
        totalCount: 2,
      });
      mockGetUserGameKbStatus.mockResolvedValue({
        gameId: 'g1',
        isIndexed: true,
        documentCount: 2,
        coverageScore: 80,
        coverageLevel: 'Standard',
        suggestedQuestions: [],
      });

      renderWizard(onComplete);

      await user.type(screen.getByTestId('game-search-input'), 'Ag{Enter}');
      await waitFor(() => expect(screen.getByText('Agricola')).toBeInTheDocument());

      // Badge should appear for all game options (after hook resolves)
      await waitFor(() => {
        expect(screen.getAllByTestId('game-kb-badge').length).toBeGreaterThanOrEqual(1);
      });
    });

    it('shows the KB warning when a non-indexed game is selected', async () => {
      const user = userEvent.setup();
      mockSearch.mockResolvedValue({
        items: [{ id: 'g1', title: 'Catan', thumbnailUrl: null, yearPublished: 1995 }],
        totalCount: 1,
      });
      mockGetUserGameKbStatus.mockResolvedValue({
        gameId: 'g1',
        isIndexed: false,
        documentCount: 0,
        coverageScore: 0,
        coverageLevel: 'None',
        suggestedQuestions: [],
      });

      renderWizard(onComplete);

      await user.type(screen.getByTestId('game-search-input'), 'Catan{Enter}');
      await waitFor(() => expect(screen.getByText('Catan')).toBeInTheDocument());

      // Warning not visible before selection
      expect(screen.queryByTestId('kb-warning')).not.toBeInTheDocument();

      // Select the non-indexed game
      await user.click(screen.getByText('Catan'));

      // Warning should appear with the exact spec text
      await waitFor(() => {
        expect(screen.getByTestId('kb-warning')).toBeInTheDocument();
      });
      expect(screen.getByText(/Agente AI non disponibile/)).toBeInTheDocument();

      // User can still proceed (warning is informative, not blocking)
      await user.click(screen.getByTestId('confirm-game-button'));
      expect(screen.getByTestId('upload-rules-step')).toBeInTheDocument();
    });

    it('does not show the warning when an indexed game is selected', async () => {
      const user = userEvent.setup();
      mockSearch.mockResolvedValue({
        items: [{ id: 'g1', title: 'Wingspan', thumbnailUrl: null, yearPublished: 2019 }],
        totalCount: 1,
      });
      mockGetUserGameKbStatus.mockResolvedValue({
        gameId: 'g1',
        isIndexed: true,
        documentCount: 4,
        coverageScore: 90,
        coverageLevel: 'Full',
        suggestedQuestions: [],
      });

      renderWizard(onComplete);

      await user.type(screen.getByTestId('game-search-input'), 'Wingspan{Enter}');
      await waitFor(() => expect(screen.getByText('Wingspan')).toBeInTheDocument());
      await user.click(screen.getByText('Wingspan'));

      // Warning should NOT appear for indexed games
      await waitFor(() => {
        expect(screen.getByTestId('confirm-game-button')).toBeInTheDocument();
      });
      expect(screen.queryByTestId('kb-warning')).not.toBeInTheDocument();
    });
  });
});
