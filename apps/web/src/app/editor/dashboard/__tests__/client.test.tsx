/**
 * EditorDashboardClient Tests - Issue #2894
 *
 * Tests for enhanced editor dashboard:
 * - Stats card rendering and filtering
 * - Priority calculation and sorting
 * - Tab filtering (All, Pending, In Review)
 * - Real-time polling behavior
 * - Bulk submission functionality
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { EditorDashboardClient } from '../client';
import { api } from '@/lib/api';
import type { SharedGame } from '@/lib/api';

// Mock dependencies
vi.mock('@/components/auth/AuthProvider', () => ({
  useAuthUser: () => ({
    user: { id: 'test-user', email: 'test@example.com', role: 'Admin' },
    loading: false,
  }),
}));

vi.mock('next/navigation', () => ({
  useRouter: () => ({
    push: vi.fn(),
  }),
}));

vi.mock('@/lib/api', () => ({
  api: {
    sharedGames: {
      getAll: vi.fn(),
      submitForApproval: vi.fn(),
    },
  },
}));

// Test data
const mockGames: SharedGame[] = [
  {
    id: '1',
    title: 'Test Game 1',
    yearPublished: 2020,
    status: 'Draft',
    minPlayers: 2,
    maxPlayers: 4,
    playingTimeMinutes: 60,
    complexityRating: 2.5,
    createdAt: new Date(Date.now() - 8 * 24 * 60 * 60 * 1000).toISOString(), // 8 days ago
    updatedAt: new Date().toISOString(),
    bggId: 123,
    thumbnailUrl: null,
    imageUrl: null,
    description: 'Test description',
    categories: [],
    mechanics: [],
    designer: 'Test Designer',
    publisher: 'Test Publisher',
    isExpansion: false,
  },
  {
    id: '2',
    title: 'Test Game 2',
    yearPublished: 2021,
    status: 'PendingApproval',
    minPlayers: 1,
    maxPlayers: 2,
    playingTimeMinutes: 30,
    complexityRating: 1.5,
    createdAt: new Date(Date.now() - 4 * 24 * 60 * 60 * 1000).toISOString(), // 4 days ago
    updatedAt: new Date().toISOString(),
    bggId: 124,
    thumbnailUrl: null,
    imageUrl: null,
    description: 'Test description 2',
    categories: [],
    mechanics: [],
    designer: 'Test Designer',
    publisher: 'Test Publisher',
    isExpansion: false,
  },
  {
    id: '3',
    title: 'Test Game 3',
    yearPublished: 2022,
    status: 'Published',
    minPlayers: 2,
    maxPlayers: 6,
    playingTimeMinutes: 90,
    complexityRating: 3.0,
    createdAt: new Date(Date.now() - 1 * 24 * 60 * 60 * 1000).toISOString(), // 1 day ago
    updatedAt: new Date().toISOString(),
    bggId: 125,
    thumbnailUrl: null,
    imageUrl: null,
    description: 'Test description 3',
    categories: [],
    mechanics: [],
    designer: 'Test Designer',
    publisher: 'Test Publisher',
    isExpansion: false,
  },
  {
    id: '4',
    title: 'Test Game 4',
    yearPublished: 2023,
    status: 'Archived',
    minPlayers: 3,
    maxPlayers: 5,
    playingTimeMinutes: 120,
    complexityRating: 4.0,
    createdAt: new Date().toISOString(), // Today
    updatedAt: new Date().toISOString(),
    bggId: 126,
    thumbnailUrl: null,
    imageUrl: null,
    description: 'Test description 4',
    categories: [],
    mechanics: [],
    designer: 'Test Designer',
    publisher: 'Test Publisher',
    isExpansion: false,
  },
];

describe('EditorDashboardClient', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(api.sharedGames.getAll).mockResolvedValue({
      items: mockGames,
      totalCount: mockGames.length,
      page: 1,
      pageSize: 20,
      totalPages: 1,
    });
  });

  afterEach(() => {
    vi.clearAllTimers();
  });

  describe('Stats Cards', () => {
    it('renders all 4 stats cards with correct counts', async () => {
      render(<EditorDashboardClient />);

      await waitFor(() => {
        expect(screen.getByTestId('stats-card-pending')).toBeInTheDocument();
      });

      // Check counts
      const pendingCard = screen.getByTestId('stats-card-pending');
      expect(within(pendingCard).getByText('1')).toBeInTheDocument(); // 1 PendingApproval

      const approvedCard = screen.getByTestId('stats-card-approved');
      expect(within(approvedCard).getByText('1')).toBeInTheDocument(); // 1 Published

      const rejectedCard = screen.getByTestId('stats-card-rejected');
      expect(within(rejectedCard).getByText('1')).toBeInTheDocument(); // 1 Archived

      const activityCard = screen.getByTestId('stats-card-activity');
      expect(within(activityCard).getByText('1')).toBeInTheDocument(); // 1 created today
    });

    it('clicking stats card filters the games', async () => {
      const user = userEvent.setup();
      render(<EditorDashboardClient />);

      await waitFor(() => {
        expect(screen.getByTestId('stats-card-pending')).toBeInTheDocument();
      });

      // Click pending card
      await user.click(screen.getByTestId('stats-card-pending'));

      // Should show "In Review" tab active
      await waitFor(() => {
        const inReviewTab = screen.getByRole('tab', { name: /In Review/i });
        expect(inReviewTab).toHaveAttribute('data-state', 'active');
      });
    });

    it('pending card has orange highlight and pulse animation', async () => {
      render(<EditorDashboardClient />);

      await waitFor(() => {
        expect(screen.getByTestId('stats-card-pending')).toBeInTheDocument();
      });

      const pendingCard = screen.getByTestId('stats-card-pending');
      const accentLine = pendingCard.querySelector('.absolute.top-0');
      expect(accentLine).toHaveClass('from-orange-500', 'to-orange-600', 'animate-pulse');
    });
  });

  describe('Filter Tabs', () => {
    it('renders all three tabs (All, Pending, In Review)', async () => {
      render(<EditorDashboardClient />);

      await waitFor(() => {
        expect(screen.getByRole('tab', { name: /All Games/i })).toBeInTheDocument();
      });

      expect(screen.getByRole('tab', { name: /Pending/i })).toBeInTheDocument();
      expect(screen.getByRole('tab', { name: /In Review/i })).toBeInTheDocument();
    });

    it('filters games when tab is changed', async () => {
      const user = userEvent.setup();
      render(<EditorDashboardClient />);

      await waitFor(() => {
        expect(screen.getByTestId('games-table')).toBeInTheDocument();
      });

      // Initially shows all 4 games
      expect(screen.getAllByTestId(/^game-row-/)).toHaveLength(4);

      // Click "Pending" tab
      await user.click(screen.getByRole('tab', { name: /Pending/i }));

      // Should show only Draft games (1 game)
      await waitFor(() => {
        expect(screen.getAllByTestId(/^game-row-/)).toHaveLength(1);
        expect(screen.getByTestId('game-row-1')).toBeInTheDocument();
      });

      // Click "In Review" tab
      await user.click(screen.getByRole('tab', { name: /In Review/i }));

      // Should show only PendingApproval games (1 game)
      await waitFor(() => {
        expect(screen.getAllByTestId(/^game-row-/)).toHaveLength(1);
        expect(screen.getByTestId('game-row-2')).toBeInTheDocument();
      });
    });

    it('shows badge counts on tabs', async () => {
      render(<EditorDashboardClient />);

      await waitFor(() => {
        expect(screen.getByRole('tab', { name: /Pending/i })).toBeInTheDocument();
      });

      const pendingTab = screen.getByRole('tab', { name: /Pending/i });
      expect(within(pendingTab).getByText('1')).toBeInTheDocument(); // 1 Draft game

      const inReviewTab = screen.getByRole('tab', { name: /In Review/i });
      expect(within(inReviewTab).getByText('1')).toBeInTheDocument(); // 1 PendingApproval game
    });
  });

  describe('Priority Indicators', () => {
    it('calculates priority correctly based on age and status', async () => {
      render(<EditorDashboardClient />);

      await waitFor(() => {
        expect(screen.getByTestId('games-table')).toBeInTheDocument();
      });

      // Game 1: Draft, 8 days old → High priority
      const row1 = screen.getByTestId('game-row-1');
      expect(within(row1).getByText('High')).toBeInTheDocument();

      // Game 2: PendingApproval, 4 days old → High priority
      const row2 = screen.getByTestId('game-row-2');
      expect(within(row2).getByText('High')).toBeInTheDocument();

      // Game 3: Published, 1 day old → Low priority
      const row3 = screen.getByTestId('game-row-3');
      expect(within(row3).getByText('Low')).toBeInTheDocument();

      // Game 4: Archived, today → Low priority
      const row4 = screen.getByTestId('game-row-4');
      expect(within(row4).getByText('Low')).toBeInTheDocument();
    });

    it('sorts games by priority (high first)', async () => {
      render(<EditorDashboardClient />);

      await waitFor(() => {
        expect(screen.getByTestId('games-table')).toBeInTheDocument();
      });

      const rows = screen.getAllByTestId(/^game-row-/);

      // First two rows should have "High" priority
      expect(within(rows[0]).getByText('High')).toBeInTheDocument();
      expect(within(rows[1]).getByText('High')).toBeInTheDocument();

      // Last two rows should have "Low" priority
      expect(within(rows[2]).getByText('Low')).toBeInTheDocument();
      expect(within(rows[3]).getByText('Low')).toBeInTheDocument();
    });
  });

  describe('Real-time Polling', () => {
    it.skip('polls every 30 seconds', async () => {
      // TODO: Requires fake timers and interval mocking
    });

    it.skip('pauses polling when tab is hidden', async () => {
      // TODO: Requires document.visibilitychange event mocking
    });

    it.skip('updates last update timestamp', async () => {
      // TODO: Requires fake timers for timestamp updates
    });
  });

  describe('Bulk Submission', () => {
    it('shows bulk submit button when games are selected', async () => {
      const user = userEvent.setup();
      render(<EditorDashboardClient />);

      await waitFor(() => {
        expect(screen.getByTestId('game-row-1')).toBeInTheDocument();
      });

      // Select a Draft game
      const checkbox = screen.getByTestId('select-checkbox-1');
      await user.click(checkbox);

      // Bulk submit button should appear
      await waitFor(() => {
        expect(screen.getByTestId('bulk-submit-button')).toBeInTheDocument();
        expect(screen.getByText(/Submit 1 for Review/i)).toBeInTheDocument();
      });
    });

    it('bulk submits selected games', async () => {
      const user = userEvent.setup();
      vi.mocked(api.sharedGames.submitForApproval).mockResolvedValue(undefined);

      render(<EditorDashboardClient />);

      await waitFor(() => {
        expect(screen.getByTestId('game-row-1')).toBeInTheDocument();
      });

      // Select Draft game
      await user.click(screen.getByTestId('select-checkbox-1'));

      // Click bulk submit
      await user.click(screen.getByTestId('bulk-submit-button'));

      // Should call API
      await waitFor(() => {
        expect(api.sharedGames.submitForApproval).toHaveBeenCalledWith('1');
      });

      // Should show success toast
      await waitFor(() => {
        expect(screen.getByTestId('toast-success')).toBeInTheDocument();
        expect(screen.getByText(/1 games submitted for review/i)).toBeInTheDocument();
      });
    });

    it('select all checkbox selects only Draft games', async () => {
      const user = userEvent.setup();
      render(<EditorDashboardClient />);

      await waitFor(() => {
        expect(screen.getByTestId('select-all-checkbox')).toBeInTheDocument();
      });

      // Click select all
      await user.click(screen.getByTestId('select-all-checkbox'));

      // Only Draft game (game 1) should be selected
      await waitFor(() => {
        expect(screen.getByTestId('select-checkbox-1')).toBeChecked();
        // Other games don't have checkboxes (not Draft status)
      });

      // Bulk button should show 1 game
      expect(screen.getByText(/Submit 1 for Review/i)).toBeInTheDocument();
    });
  });

  describe('Responsive Layout', () => {
    it('renders stats grid with responsive classes', async () => {
      render(<EditorDashboardClient />);

      await waitFor(() => {
        expect(screen.getByTestId('stats-section')).toBeInTheDocument();
      });

      const statsSection = screen.getByTestId('stats-section');

      // Check responsive grid classes
      expect(statsSection).toHaveClass('grid', 'grid-cols-1', 'md:grid-cols-2', 'lg:grid-cols-4');
    });
  });

  describe('Empty States', () => {
    it('shows empty state when no games match filter', async () => {
      vi.mocked(api.sharedGames.getAll).mockResolvedValue({
        items: [],
        totalCount: 0,
        page: 1,
        pageSize: 20,
        totalPages: 0,
      });

      render(<EditorDashboardClient />);

      await waitFor(() => {
        expect(screen.getByTestId('empty-state')).toBeInTheDocument();
        expect(screen.getByText(/No games found in this filter/i)).toBeInTheDocument();
      });
    });
  });

  describe('Game Actions', () => {
    it('submits individual game for approval', async () => {
      const user = userEvent.setup();
      vi.mocked(api.sharedGames.submitForApproval).mockResolvedValue(undefined);

      render(<EditorDashboardClient />);

      await waitFor(() => {
        expect(screen.getByTestId('submit-button-1')).toBeInTheDocument();
      });

      // Click submit for game 1 (Draft)
      await user.click(screen.getByTestId('submit-button-1'));

      // Should call API
      await waitFor(() => {
        expect(api.sharedGames.submitForApproval).toHaveBeenCalledWith('1');
      });

      // Should show success toast
      await waitFor(() => {
        expect(screen.getByTestId('toast-success')).toBeInTheDocument();
      });
    });

    it('shows rejection feedback modal for rejected games', async () => {
      const user = userEvent.setup();
      render(<EditorDashboardClient />);

      await waitFor(() => {
        expect(screen.getByTestId('rejection-button-4')).toBeInTheDocument();
      });

      // Click feedback button for rejected game
      await user.click(screen.getByTestId('rejection-button-4'));

      // Modal should open
      await waitFor(() => {
        expect(screen.getByTestId('rejection-modal')).toBeInTheDocument();
        expect(screen.getByTestId('rejection-reason')).toHaveTextContent(
          /The game needs more detailed description/i
        );
      });
    });
  });

  describe('Animated Counter', () => {
    it('animates from 0 to target value', async () => {
      render(<EditorDashboardClient />);

      // Wait for stats to load
      await waitFor(() => {
        expect(screen.getByTestId('stats-card-pending')).toBeInTheDocument();
      });

      // Counter should eventually show final value
      const pendingCard = screen.getByTestId('stats-card-pending');
      await waitFor(() => {
        expect(within(pendingCard).getByText('1')).toBeInTheDocument();
      }, { timeout: 1500 });
    });
  });
});