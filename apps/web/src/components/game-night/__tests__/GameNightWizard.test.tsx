import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi, describe, it, expect, beforeEach } from 'vitest';

const mockSearch = vi.fn();
const mockBggSearch = vi.fn();
const mockCreateSession = vi.fn();
const mockAddPlayer = vi.fn();
const mockStartSession = vi.fn();
const mockPush = vi.fn();

vi.mock('@/lib/api', () => ({
  api: {
    sharedGames: {
      search: (...args: unknown[]) => mockSearch(...args),
    },
    bgg: {
      search: (...args: unknown[]) => mockBggSearch(...args),
    },
    liveSessions: {
      createSession: (...args: unknown[]) => mockCreateSession(...args),
      addPlayer: (...args: unknown[]) => mockAddPlayer(...args),
      startSession: (...args: unknown[]) => mockStartSession(...args),
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

describe('GameNightWizard', () => {
  const onComplete = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders step 1: find game', () => {
    render(<GameNightWizard onComplete={onComplete} />);

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

    render(<GameNightWizard onComplete={onComplete} />);

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

    render(<GameNightWizard onComplete={onComplete} />);

    // Search and select
    await user.type(screen.getByTestId('game-search-input'), 'Catan{Enter}');
    await waitFor(() => expect(screen.getByText('Catan')).toBeInTheDocument());
    await user.click(screen.getByText('Catan'));

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

    render(<GameNightWizard onComplete={onComplete} />);

    // Step 1: search + select
    await user.type(screen.getByTestId('game-search-input'), 'Ticket{Enter}');
    await waitFor(() => expect(screen.getByText('Ticket to Ride')).toBeInTheDocument());
    await user.click(screen.getByText('Ticket to Ride'));

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

    render(<GameNightWizard onComplete={onComplete} />);

    // Navigate to step 3
    await user.type(screen.getByTestId('game-search-input'), 'Splendor{Enter}');
    await waitFor(() => expect(screen.getByText('Splendor')).toBeInTheDocument());
    await user.click(screen.getByText('Splendor'));
    await user.click(screen.getByTestId('skip-rules-button'));

    // Should start with 2 player inputs
    expect(screen.getByTestId('player-input-0')).toBeInTheDocument();
    expect(screen.getByTestId('player-input-1')).toBeInTheDocument();

    // Add a player
    await user.click(screen.getByTestId('add-player-button'));
    expect(screen.getByTestId('player-input-2')).toBeInTheDocument();
  });

  it('creates session without gameId for BGG-only games', async () => {
    const user = userEvent.setup();
    // No catalog results → triggers BGG fallback
    mockSearch.mockResolvedValue({ items: [], totalCount: 0 });
    mockBggSearch.mockResolvedValue({
      results: [{ bggId: 42, name: 'Everdell', thumbnailUrl: null, yearPublished: 2018 }],
    });
    mockCreateSession.mockResolvedValue('sess-bgg');
    mockAddPlayer.mockResolvedValue(undefined);
    mockStartSession.mockResolvedValue(undefined);

    render(<GameNightWizard onComplete={onComplete} />);

    // Search triggers BGG fallback
    await user.type(screen.getByTestId('game-search-input'), 'Everdell{Enter}');
    await waitFor(() => expect(screen.getByText('Everdell')).toBeInTheDocument());
    await user.click(screen.getByText('Everdell'));

    // Skip PDF upload
    await user.click(screen.getByTestId('skip-rules-button'));

    // Add players and create session
    await user.type(screen.getByTestId('player-input-0'), 'Anna');
    await user.type(screen.getByTestId('player-input-1'), 'Paolo');
    await user.click(screen.getByTestId('create-session-button'));

    await waitFor(() => {
      // Should create with gameName only, no gameId
      expect(mockCreateSession).toHaveBeenCalledWith(
        expect.objectContaining({ gameName: 'Everdell' })
      );
      const callArg = mockCreateSession.mock.calls[0][0];
      expect(callArg).not.toHaveProperty('gameId');
    });

    expect(onComplete).toHaveBeenCalledWith('sess-bgg');
  });
});
