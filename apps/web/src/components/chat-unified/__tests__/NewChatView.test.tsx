/**
 * NewChatView Tests - Issue #4363
 *
 * Tests for the new chat page with game + agent selection:
 * 1. Renders game selection section
 * 2. Renders agent selection with 4 options
 * 3. Selects a game card
 * 4. Selects an agent card
 * 5. Pre-selects via query params
 * 6. Search filters games
 * 7. Quick start suggestions change with game
 * 8. Start chat creates thread and redirects
 * 9. Shows error on API failure
 * 10. Shows loading skeleton while fetching games
 * 11. Skip game option (generic chat)
 *
 * Pattern: Vitest + React Testing Library
 */

import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach, type Mock } from 'vitest';
import React from 'react';

import { NewChatView } from '../NewChatView';

// Mock next/navigation
const mockPush = vi.fn();
const mockSearchParams = new URLSearchParams();

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: mockPush }),
  useSearchParams: () => mockSearchParams,
}));

// Mock API
vi.mock('@/lib/api', () => ({
  api: {
    games: {
      getAll: vi.fn(),
    },
    agents: {
      getAvailable: vi.fn(),
      getUserAgentsForGame: vi.fn(),
    },
    chat: {
      createThread: vi.fn(),
    },
  },
}));

// Mock MeepleCard
vi.mock('@/components/ui/data-display/meeple-card', () => ({
  MeepleCard: ({ title, entity, subtitle, badge, className }: any) => (
    <div data-testid="meeple-card" data-entity={entity} className={className}>
      <span>{title}</span>
      {subtitle && <span>{subtitle}</span>}
      {badge && <span>{badge}</span>}
    </div>
  ),
}));

// ============================================================================
// Test Data
// ============================================================================

const mockGames = [
  { id: 'game-1', title: 'Catan', createdAt: '2024-01-01' },
  { id: 'game-2', title: 'Ticket to Ride', createdAt: '2024-01-02' },
  { id: 'game-3', title: 'Wingspan', createdAt: '2024-01-03' },
];

const mockAgents = [
  { id: 'agent-1', name: 'QA Agent', type: 'qa', strategyName: 'qa', strategyParameters: {}, isActive: true, createdAt: '2024-01-01T00:00:00Z', lastInvokedAt: null, invocationCount: 0, isRecentlyUsed: false, isIdle: true },
  { id: 'agent-2', name: 'Rules Agent', type: 'rules', strategyName: 'rules', strategyParameters: {}, isActive: true, createdAt: '2024-01-01T00:00:00Z', lastInvokedAt: null, invocationCount: 0, isRecentlyUsed: false, isIdle: true },
];

// ============================================================================
// Helpers
// ============================================================================

let apiMock: any;

async function renderView() {
  const result = render(<NewChatView />);
  // Wait for games to load
  await waitFor(() => {
    expect(screen.queryByTestId('game-selection-section')).toBeInTheDocument();
  });
  return result;
}

// ============================================================================
// Tests
// ============================================================================

describe('NewChatView', () => {
  beforeEach(async () => {
    vi.clearAllMocks();

    // Import api mock
    const { api } = await import('@/lib/api');
    apiMock = api;

    (apiMock.games.getAll as Mock).mockResolvedValue({ games: mockGames });
    (apiMock.agents.getAvailable as Mock).mockResolvedValue(mockAgents);
    (apiMock.agents.getUserAgentsForGame as Mock).mockResolvedValue([]);
    (apiMock.chat.createThread as Mock).mockResolvedValue({ id: 'thread-new-1' });
  });

  // --------------------------------------------------------------------------
  // Section Rendering
  // --------------------------------------------------------------------------

  it('renders game selection section', async () => {
    await renderView();
    expect(screen.getByTestId('game-selection-section')).toBeInTheDocument();
    expect(screen.getByText('Seleziona un gioco')).toBeInTheDocument();
  });

  it('renders agent selection with 4 options', async () => {
    await renderView();
    expect(screen.getByTestId('agent-selection-section')).toBeInTheDocument();
    expect(screen.getByTestId('agent-card-auto')).toBeInTheDocument();
    expect(screen.getByTestId('agent-card-qa')).toBeInTheDocument();
    expect(screen.getByTestId('agent-card-rules')).toBeInTheDocument();
    expect(screen.getByTestId('agent-card-strategy')).toBeInTheDocument();
  });

  it('renders quick start section', async () => {
    await renderView();
    expect(screen.getByTestId('quick-start-section')).toBeInTheDocument();
  });

  it('renders start chat button', async () => {
    await renderView();
    expect(screen.getByTestId('start-chat-btn')).toBeInTheDocument();
    expect(screen.getByText('Inizia Chat')).toBeInTheDocument();
  });

  // --------------------------------------------------------------------------
  // Game Selection
  // --------------------------------------------------------------------------

  it('renders game cards after loading', async () => {
    await renderView();

    await waitFor(() => {
      expect(screen.getByText('Catan')).toBeInTheDocument();
      expect(screen.getByText('Ticket to Ride')).toBeInTheDocument();
      expect(screen.getByText('Wingspan')).toBeInTheDocument();
    });
  });

  it('selects a game card on click', async () => {
    const user = userEvent.setup();
    await renderView();

    await waitFor(() => {
      expect(screen.getByTestId('game-card-game-1')).toBeInTheDocument();
    });

    await user.click(screen.getByTestId('game-card-game-1'));

    expect(screen.getByTestId('game-card-game-1')).toHaveAttribute('aria-pressed', 'true');
  });

  it('filters games with search input', async () => {
    const user = userEvent.setup();
    await renderView();

    await waitFor(() => {
      expect(screen.getByText('Catan')).toBeInTheDocument();
    });

    const searchInput = screen.getByTestId('game-search-input');
    await user.type(searchInput, 'Catan');

    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.queryByText('Ticket to Ride')).not.toBeInTheDocument();
  });

  it('allows skipping game selection', async () => {
    const user = userEvent.setup();
    await renderView();

    await waitFor(() => {
      expect(screen.getByTestId('skip-game-btn')).toBeInTheDocument();
    });

    await user.click(screen.getByTestId('skip-game-btn'));
    expect(screen.getByText('Continua senza gioco (chat generica)')).toBeInTheDocument();
  });

  // --------------------------------------------------------------------------
  // Agent Selection
  // --------------------------------------------------------------------------

  it('selects an agent card on click', async () => {
    const user = userEvent.setup();
    await renderView();

    await user.click(screen.getByTestId('agent-card-rules'));
    expect(screen.getByTestId('agent-card-rules')).toHaveAttribute('aria-pressed', 'true');
  });

  it('has auto agent pre-selected by default', async () => {
    await renderView();
    expect(screen.getByTestId('agent-card-auto')).toHaveAttribute('aria-pressed', 'true');
  });

  // --------------------------------------------------------------------------
  // Quick Start
  // --------------------------------------------------------------------------

  it('shows contextual suggestions when game selected', async () => {
    const user = userEvent.setup();
    await renderView();

    await waitFor(() => {
      expect(screen.getByTestId('game-card-game-1')).toBeInTheDocument();
    });

    // Before selecting game - generic suggestions
    expect(screen.getByText('Consiglia un gioco')).toBeInTheDocument();

    // Select game
    await user.click(screen.getByTestId('game-card-game-1'));

    // After selecting - contextual suggestions
    await waitFor(() => {
      expect(screen.getByText('Come si gioca a Catan')).toBeInTheDocument();
    });
  });

  // --------------------------------------------------------------------------
  // Thread Creation
  // --------------------------------------------------------------------------

  it('creates thread and redirects on start', async () => {
    const user = userEvent.setup();
    await renderView();

    await waitFor(() => {
      expect(screen.getByTestId('game-card-game-1')).toBeInTheDocument();
    });

    // Select game
    await user.click(screen.getByTestId('game-card-game-1'));

    // Click start
    await user.click(screen.getByTestId('start-chat-btn'));

    await waitFor(() => {
      expect(apiMock.chat.createThread).toHaveBeenCalledWith(
        expect.objectContaining({
          gameId: 'game-1',
          title: 'Chat: Catan',
          initialMessage: null,
        })
      );
      expect(mockPush).toHaveBeenCalledWith('/chat?threadId=thread-new-1');
    });
  });

  it('creates generic thread when no game selected', async () => {
    const user = userEvent.setup();
    await renderView();

    // Click start without game
    await user.click(screen.getByTestId('start-chat-btn'));

    await waitFor(() => {
      expect(apiMock.chat.createThread).toHaveBeenCalledWith(
        expect.objectContaining({
          gameId: null,
          title: 'Nuova conversazione',
          initialMessage: null,
        })
      );
    });
  });

  // --------------------------------------------------------------------------
  // Error Handling
  // --------------------------------------------------------------------------

  it('shows error when API fails', async () => {
    (apiMock.games.getAll as Mock).mockRejectedValue(new Error('Network'));
    render(<NewChatView />);

    await waitFor(() => {
      expect(screen.getByTestId('new-chat-error')).toBeInTheDocument();
      expect(screen.getByText('Errore nel caricamento dei dati')).toBeInTheDocument();
    });
  });

  it('shows error when thread creation fails', async () => {
    const user = userEvent.setup();
    (apiMock.chat.createThread as Mock).mockRejectedValue(new Error('Failed'));
    await renderView();

    await user.click(screen.getByTestId('start-chat-btn'));

    await waitFor(() => {
      expect(screen.getByText('Errore nella creazione della conversazione')).toBeInTheDocument();
    });
  });
});
