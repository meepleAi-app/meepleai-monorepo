/**
 * Tests for Game Detail Page (Issue #855)
 *
 * Tests all 4 tabs: Overview, Rules, Sessions, Notes
 */

import React from 'react';
import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import { useRouter } from 'next/router';
import GameDetailPage from '../[id]';
import { api } from '@/lib/api';

// Mock next/router
jest.mock('next/router', () => ({
  useRouter: jest.fn(),
}));

// Mock API
jest.mock('@/lib/api', () => ({
  api: {
    games: {
      getById: jest.fn(),
    },
    bgg: {
      getGameDetails: jest.fn(),
    },
    sessions: {
      getHistory: jest.fn(),
    },
  },
}));

// Mock localStorage
const localStorageMock = (() => {
  let store: Record<string, string> = {};
  return {
    getItem: (key: string) => store[key] || null,
    setItem: (key: string, value: string) => {
      store[key] = value;
    },
    removeItem: (key: string) => {
      delete store[key];
    },
    clear: () => {
      store = {};
    },
  };
})();
Object.defineProperty(window, 'localStorage', { value: localStorageMock });

describe('GameDetailPage', () => {
  const mockGame = {
    id: 'game-1',
    title: 'Catan',
    publisher: 'Kosmos',
    yearPublished: 1995,
    minPlayers: 3,
    maxPlayers: 4,
    minPlayTimeMinutes: 60,
    maxPlayTimeMinutes: 120,
    bggId: 13,
    createdAt: '2024-01-01T00:00:00Z',
  };

  const mockBggDetails = {
    id: 13,
    name: 'Catan',
    yearPublished: 1995,
    minPlayers: 3,
    maxPlayers: 4,
    playingTime: 90,
    minPlayTime: 60,
    maxPlayTime: 120,
    minAge: 10,
    description: '<p>Settle the island of Catan</p>',
    imageUrl: 'https://example.com/catan.jpg',
    thumbnailUrl: 'https://example.com/catan-thumb.jpg',
    categories: ['Strategy', 'Family'],
    mechanics: ['Dice Rolling', 'Trading'],
    designers: ['Klaus Teuber'],
    publishers: ['Kosmos'],
    averageRating: 7.5,
    usersRated: 100000,
    averageWeight: 2.3,
    rank: 250,
  };

  const mockSessions = [
    {
      id: 'session-1',
      gameId: 'game-1',
      status: 'Completed',
      startedAt: '2024-01-15T10:00:00Z',
      completedAt: '2024-01-15T12:00:00Z',
      playerCount: 4,
      players: [
        { playerName: 'Alice', playerOrder: 1, color: 'red' },
        { playerName: 'Bob', playerOrder: 2, color: 'blue' },
        { playerName: 'Charlie', playerOrder: 3, color: 'green' },
        { playerName: 'Diana', playerOrder: 4, color: 'yellow' },
      ],
      winnerName: 'Alice',
      notes: 'Great game!',
      durationMinutes: 120,
    },
  ];

  beforeEach(() => {
    jest.clearAllMocks();
    localStorageMock.clear();
    (useRouter as jest.Mock).mockReturnValue({
      query: { id: 'game-1' },
      push: jest.fn(),
    });
    (api.games.getById as jest.Mock).mockResolvedValue(mockGame);
    (api.bgg.getGameDetails as jest.Mock).mockResolvedValue(mockBggDetails);
    (api.sessions.getHistory as jest.Mock).mockResolvedValue({
      sessions: mockSessions,
      total: 1,
      page: 1,
      pageSize: 50,
    });
  });

  describe('Page Loading', () => {
    it('should render loading skeleton initially', () => {
      (api.games.getById as jest.Mock).mockImplementation(
        () => new Promise(() => {}) // Never resolves
      );

      render(<GameDetailPage />);

      // Check for skeleton elements instead of role="status"
      const skeletons = document.querySelectorAll('[class*="skeleton"]');
      expect(skeletons.length).toBeGreaterThan(0);
    });

    it('should load and display game data', async () => {
      render(<GameDetailPage />);

      await waitFor(() => {
        expect(screen.getByText('Catan')).toBeInTheDocument();
      });

      // Use getAllByText for duplicated text
      const kosmosElements = screen.getAllByText('Kosmos');
      expect(kosmosElements.length).toBeGreaterThan(0);
      expect(screen.getByText('3-4 players')).toBeInTheDocument();
      expect(screen.getByText('60-120 min')).toBeInTheDocument();
      expect(screen.getByText('1995')).toBeInTheDocument();
    });

    it('should handle game not found error', async () => {
      (api.games.getById as jest.Mock).mockRejectedValue(new Error('Not found'));

      render(<GameDetailPage />);

      await waitFor(() => {
        expect(screen.getByText(/Failed to load game/i)).toBeInTheDocument();
      });

      expect(screen.getByRole('link', { name: /Back to Games/i })).toBeInTheDocument();
    });
  });

  describe('Overview Tab', () => {
    it('should display overview tab by default', async () => {
      render(<GameDetailPage />);

      await waitFor(() => {
        expect(screen.getByText('Catan')).toBeInTheDocument();
      });

      // Should show BGG details
      await waitFor(() => {
        expect(screen.getByText(/Rating: 7.50/i)).toBeInTheDocument();
      });

      expect(screen.getByText(/Complexity: 2.30\/5/i)).toBeInTheDocument();
      // Rating count uses toLocaleString() which adds commas
      expect(screen.getByText(/100,000 ratings/i)).toBeInTheDocument();
    });

    it('should display BGG categories and mechanics', async () => {
      render(<GameDetailPage />);

      await waitFor(() => {
        expect(screen.getByText('Strategy')).toBeInTheDocument();
      });

      expect(screen.getByText('Family')).toBeInTheDocument();
      expect(screen.getByText('Dice Rolling')).toBeInTheDocument();
      expect(screen.getByText('Trading')).toBeInTheDocument();
    });

    it('should display designers and publishers', async () => {
      render(<GameDetailPage />);

      await waitFor(() => {
        expect(screen.getByText('Klaus Teuber')).toBeInTheDocument();
      });

      // Kosmos appears multiple times
      const kosmosElements = screen.getAllByText('Kosmos');
      expect(kosmosElements.length).toBeGreaterThan(0);
    });

    it('should show BGG link', async () => {
      render(<GameDetailPage />);

      await waitFor(() => {
        const bggLink = screen.getByRole('link', { name: /View on BoardGameGeek/i });
        expect(bggLink).toHaveAttribute('href', 'https://boardgamegeek.com/boardgame/13');
      });
    });

    it('should handle game without BGG ID', async () => {
      const gameWithoutBgg = { ...mockGame, bggId: null };
      (api.games.getById as jest.Mock).mockResolvedValue(gameWithoutBgg);

      render(<GameDetailPage />);

      await waitFor(() => {
        expect(screen.getByText(/No BoardGameGeek data available/i)).toBeInTheDocument();
      });
    });
  });

  describe('Rules Tab', () => {
    it('should display rules placeholder', async () => {
      render(<GameDetailPage />);

      await waitFor(() => {
        expect(screen.getByText('Catan')).toBeInTheDocument();
      });

      // Click Rules tab
      const rulesTab = screen.getByRole('tab', { name: /Rules/i });
      fireEvent.click(rulesTab);

      await waitFor(() => {
        expect(screen.getByText(/Rules integration coming soon/i)).toBeInTheDocument();
      });

      expect(screen.getByText(/GetRuleSpecsQuery/i)).toBeInTheDocument();
    });
  });

  describe('Sessions Tab', () => {
    it('should load and display sessions when tab is activated', async () => {
      render(<GameDetailPage />);

      await waitFor(() => {
        expect(screen.getByText('Catan')).toBeInTheDocument();
      });

      // Click Sessions tab
      const sessionsTab = screen.getByRole('tab', { name: /Sessions/i });
      fireEvent.click(sessionsTab);

      await waitFor(() => {
        expect(api.sessions.getHistory).toHaveBeenCalledWith({
          gameId: 'game-1',
          limit: 50,
        });
      });

      await waitFor(() => {
        expect(screen.getByText('4 players: Alice, Bob, Charlie, Diana')).toBeInTheDocument();
      });

      expect(screen.getByText(/Winner: Alice/i)).toBeInTheDocument();
      expect(screen.getByText(/Great game!/i)).toBeInTheDocument();
    });

    it('should handle empty sessions', async () => {
      (api.sessions.getHistory as jest.Mock).mockResolvedValue({
        sessions: [],
        total: 0,
        page: 1,
        pageSize: 50,
      });

      render(<GameDetailPage />);

      await waitFor(() => {
        expect(screen.getByText('Catan')).toBeInTheDocument();
      });

      // Click Sessions tab
      const sessionsTab = screen.getByRole('tab', { name: /Sessions/i });
      fireEvent.click(sessionsTab);

      await waitFor(() => {
        expect(screen.getByText(/No play sessions recorded yet/i)).toBeInTheDocument();
      });
    });

    it('should handle session loading error gracefully', async () => {
      (api.sessions.getHistory as jest.Mock).mockRejectedValue(new Error('Failed'));

      render(<GameDetailPage />);

      await waitFor(() => {
        expect(screen.getByText('Catan')).toBeInTheDocument();
      });

      // Click Sessions tab
      const sessionsTab = screen.getByRole('tab', { name: /Sessions/i });
      fireEvent.click(sessionsTab);

      await waitFor(() => {
        expect(screen.getByText(/No play sessions recorded yet/i)).toBeInTheDocument();
      });
    });
  });

  describe('Notes Tab', () => {
    it('should load notes from localStorage', async () => {
      localStorageMock.setItem(
        'meepleai_game_notes',
        JSON.stringify({ 'game-1': 'My test notes' })
      );

      render(<GameDetailPage />);

      await waitFor(() => {
        expect(screen.getByText('Catan')).toBeInTheDocument();
      });

      // Click Notes tab
      const notesTab = screen.getByRole('tab', { name: /Notes/i });
      fireEvent.click(notesTab);

      const textarea = screen.getByPlaceholderText(/Write your notes about strategies/i);
      expect(textarea).toHaveValue('My test notes');
    });

    it('should save notes to localStorage', async () => {
      render(<GameDetailPage />);

      await waitFor(() => {
        expect(screen.getByText('Catan')).toBeInTheDocument();
      });

      // Click Notes tab
      const notesTab = screen.getByRole('tab', { name: /Notes/i });
      fireEvent.click(notesTab);

      const textarea = screen.getByPlaceholderText(/Write your notes about strategies/i);
      fireEvent.change(textarea, { target: { value: 'New notes' } });

      const saveButton = screen.getByRole('button', { name: /Save Notes/i });

      // Mock window.alert
      global.alert = jest.fn();

      fireEvent.click(saveButton);

      expect(localStorageMock.getItem('meepleai_game_notes')).toBe(
        JSON.stringify({ 'game-1': 'New notes' })
      );
      expect(global.alert).toHaveBeenCalledWith('Notes saved successfully!');
    });

    it('should handle empty notes gracefully', async () => {
      render(<GameDetailPage />);

      await waitFor(() => {
        expect(screen.getByText('Catan')).toBeInTheDocument();
      });

      // Click Notes tab
      const notesTab = screen.getByRole('tab', { name: /Notes/i });
      fireEvent.click(notesTab);

      const textarea = screen.getByPlaceholderText(/Write your notes about strategies/i);
      expect(textarea).toHaveValue('');
    });
  });

  describe('Navigation', () => {
    it('should have back to games link', async () => {
      render(<GameDetailPage />);

      await waitFor(() => {
        expect(screen.getByText('Catan')).toBeInTheDocument();
      });

      const backLink = screen.getAllByRole('link', { name: /Back to Games/i })[0];
      expect(backLink).toHaveAttribute('href', '/games');
    });

    it('should switch between tabs', async () => {
      render(<GameDetailPage />);

      await waitFor(() => {
        expect(screen.getByText('Catan')).toBeInTheDocument();
      });

      // Start on Overview
      expect(screen.getByRole('tab', { name: /Overview/i })).toHaveAttribute(
        'data-state',
        'active'
      );

      // Switch to Rules
      fireEvent.click(screen.getByRole('tab', { name: /Rules/i }));
      await waitFor(() => {
        expect(screen.getByText(/Rules integration coming soon/i)).toBeInTheDocument();
      });

      // Switch to Sessions
      fireEvent.click(screen.getByRole('tab', { name: /Sessions/i }));
      await waitFor(() => {
        expect(api.sessions.getHistory).toHaveBeenCalled();
      });

      // Switch to Notes
      fireEvent.click(screen.getByRole('tab', { name: /Notes/i }));
      expect(screen.getByPlaceholderText(/Write your notes about strategies/i)).toBeInTheDocument();
    });
  });
});