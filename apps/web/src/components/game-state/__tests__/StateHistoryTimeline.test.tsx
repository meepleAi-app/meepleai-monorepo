/**
 * StateHistoryTimeline Tests
 * Issue #2766: Sprint 6 - Test Coverage Push
 *
 * Tests for timeline displaying state history snapshots.
 */

import { fireEvent, render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import type { GameState, GameStateSnapshot } from '@/types/game-state';

import { StateHistoryTimeline } from '../StateHistoryTimeline';

// Mock the game state store
const mockLoadSnapshot = vi.fn();
vi.mock('@/lib/stores/game-state-store', () => ({
  useGameStateStore: vi.fn(() => ({
    snapshots: [],
    loadSnapshot: mockLoadSnapshot,
  })),
}));

// Import the mock to control it in tests
import { useGameStateStore } from '@/lib/stores/game-state-store';
const mockUseGameStateStore = useGameStateStore as unknown as ReturnType<typeof vi.fn>;

const createMockGameState = (overrides?: Partial<GameState>): GameState => ({
  sessionId: 'session-123',
  gameId: 'game-456',
  templateId: 'template-789',
  version: '1.0.0',
  phase: 'Action',
  currentPlayerIndex: 0,
  roundNumber: 1,
  players: [
    { playerName: 'Alice', playerOrder: 1 },
    { playerName: 'Bob', playerOrder: 2 },
  ],
  ...overrides,
});

const createMockSnapshot = (overrides?: Partial<GameStateSnapshot>): GameStateSnapshot => ({
  id: 'snapshot-1',
  state: createMockGameState(),
  timestamp: '2024-01-15T10:30:00Z',
  userId: 'user-123',
  action: 'Player moved piece',
  ...overrides,
});

describe('StateHistoryTimeline - Issue #2766', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockUseGameStateStore.mockReturnValue({
      snapshots: [],
      loadSnapshot: mockLoadSnapshot,
    });
  });

  describe('Empty State', () => {
    it('should render empty state when no snapshots', () => {
      render(<StateHistoryTimeline sessionId="session-123" />);

      expect(screen.getByTestId('state-history-timeline')).toBeInTheDocument();
      expect(screen.getByText('State History')).toBeInTheDocument();
      expect(screen.getByText('No snapshots available yet')).toBeInTheDocument();
      expect(
        screen.getByText('Snapshots will appear here as the game progresses')
      ).toBeInTheDocument();
    });
  });

  describe('With Snapshots', () => {
    it('should render timeline with snapshots', () => {
      const snapshots = [
        createMockSnapshot({ id: 'snap-1', action: 'Game started' }),
        createMockSnapshot({ id: 'snap-2', action: 'Turn completed' }),
      ];

      mockUseGameStateStore.mockReturnValue({
        snapshots,
        loadSnapshot: mockLoadSnapshot,
      });

      render(<StateHistoryTimeline sessionId="session-123" />);

      expect(screen.getByText('State History')).toBeInTheDocument();
      expect(screen.getByText('2 snapshots available')).toBeInTheDocument();
    });

    it('should render snapshot actions', () => {
      const snapshots = [
        createMockSnapshot({ id: 'snap-1', action: 'Game started' }),
        createMockSnapshot({ id: 'snap-2', action: 'Player drew card' }),
      ];

      mockUseGameStateStore.mockReturnValue({
        snapshots,
        loadSnapshot: mockLoadSnapshot,
      });

      render(<StateHistoryTimeline sessionId="session-123" />);

      expect(screen.getByText('Game started')).toBeInTheDocument();
      expect(screen.getByText('Player drew card')).toBeInTheDocument();
    });

    it('should show Latest badge on most recent snapshot', () => {
      const snapshots = [
        createMockSnapshot({ id: 'snap-1', timestamp: '2024-01-15T10:00:00Z' }),
        createMockSnapshot({ id: 'snap-2', timestamp: '2024-01-15T11:00:00Z' }),
      ];

      mockUseGameStateStore.mockReturnValue({
        snapshots,
        loadSnapshot: mockLoadSnapshot,
      });

      render(<StateHistoryTimeline sessionId="session-123" />);

      // The snapshots are reversed, so snap-2 (latest) should show "Latest" badge
      expect(screen.getByText('Latest')).toBeInTheDocument();
    });

    it('should render formatted timestamps', () => {
      const snapshots = [
        createMockSnapshot({
          id: 'snap-1',
          timestamp: '2024-01-15T14:30:45Z',
        }),
      ];

      mockUseGameStateStore.mockReturnValue({
        snapshots,
        loadSnapshot: mockLoadSnapshot,
      });

      render(<StateHistoryTimeline sessionId="session-123" />);

      // Check for formatted date parts (month, day, time)
      expect(screen.getByText(/Jan/)).toBeInTheDocument();
      expect(screen.getByText(/15/)).toBeInTheDocument();
    });
  });

  describe('Snapshot Preview Stats', () => {
    it('should show round number when present', () => {
      const snapshots = [
        createMockSnapshot({
          id: 'snap-1',
          state: createMockGameState({ roundNumber: 5 }),
        }),
      ];

      mockUseGameStateStore.mockReturnValue({
        snapshots,
        loadSnapshot: mockLoadSnapshot,
      });

      render(<StateHistoryTimeline sessionId="session-123" />);

      expect(screen.getByText('Round: 5')).toBeInTheDocument();
    });

    it('should show phase when present', () => {
      const snapshots = [
        createMockSnapshot({
          id: 'snap-1',
          state: createMockGameState({ phase: 'Setup' }),
        }),
      ];

      mockUseGameStateStore.mockReturnValue({
        snapshots,
        loadSnapshot: mockLoadSnapshot,
      });

      render(<StateHistoryTimeline sessionId="session-123" />);

      expect(screen.getByText('Phase: Setup')).toBeInTheDocument();
    });

    it('should show player count', () => {
      const snapshots = [
        createMockSnapshot({
          id: 'snap-1',
          state: createMockGameState({
            players: [
              { playerName: 'A', playerOrder: 1 },
              { playerName: 'B', playerOrder: 2 },
              { playerName: 'C', playerOrder: 3 },
            ],
          }),
        }),
      ];

      mockUseGameStateStore.mockReturnValue({
        snapshots,
        loadSnapshot: mockLoadSnapshot,
      });

      render(<StateHistoryTimeline sessionId="session-123" />);

      expect(screen.getByText('Players: 3')).toBeInTheDocument();
    });
  });

  describe('Restore Functionality', () => {
    it('should show Restore button for each snapshot', () => {
      const snapshots = [
        createMockSnapshot({ id: 'snap-1' }),
        createMockSnapshot({ id: 'snap-2' }),
      ];

      mockUseGameStateStore.mockReturnValue({
        snapshots,
        loadSnapshot: mockLoadSnapshot,
      });

      render(<StateHistoryTimeline sessionId="session-123" />);

      const restoreButtons = screen.getAllByText('Restore');
      expect(restoreButtons.length).toBe(2);
    });

    it('should disable Restore button for latest snapshot', () => {
      const snapshots = [
        createMockSnapshot({ id: 'snap-1', timestamp: '2024-01-15T10:00:00Z' }),
        createMockSnapshot({ id: 'snap-2', timestamp: '2024-01-15T11:00:00Z' }),
      ];

      mockUseGameStateStore.mockReturnValue({
        snapshots,
        loadSnapshot: mockLoadSnapshot,
      });

      render(<StateHistoryTimeline sessionId="session-123" />);

      const restoreButtons = screen.getAllByRole('button', { name: /restore/i });
      // First button (latest - snap-2 after reverse) should be disabled
      expect(restoreButtons[0]).toBeDisabled();
      // Second button (snap-1) should be enabled
      expect(restoreButtons[1]).toBeEnabled();
    });

    it('should call loadSnapshot when Restore is clicked and confirmed', () => {
      // Mock window.confirm
      vi.spyOn(window, 'confirm').mockReturnValue(true);

      const snapshots = [
        createMockSnapshot({ id: 'snap-old', timestamp: '2024-01-15T09:00:00Z' }),
        createMockSnapshot({ id: 'snap-new', timestamp: '2024-01-15T10:00:00Z' }),
      ];

      mockUseGameStateStore.mockReturnValue({
        snapshots,
        loadSnapshot: mockLoadSnapshot,
      });

      render(<StateHistoryTimeline sessionId="session-123" />);

      // Find the non-disabled restore button (second one, which is snap-old after reverse)
      const restoreButtons = screen.getAllByRole('button', { name: /restore/i });
      fireEvent.click(restoreButtons[1]);

      expect(mockLoadSnapshot).toHaveBeenCalledWith('snap-old');
    });

    it('should not call loadSnapshot when Restore is cancelled', () => {
      vi.spyOn(window, 'confirm').mockReturnValue(false);

      const snapshots = [
        createMockSnapshot({ id: 'snap-old', timestamp: '2024-01-15T09:00:00Z' }),
        createMockSnapshot({ id: 'snap-new', timestamp: '2024-01-15T10:00:00Z' }),
      ];

      mockUseGameStateStore.mockReturnValue({
        snapshots,
        loadSnapshot: mockLoadSnapshot,
      });

      render(<StateHistoryTimeline sessionId="session-123" />);

      const restoreButtons = screen.getAllByRole('button', { name: /restore/i });
      fireEvent.click(restoreButtons[1]);

      expect(mockLoadSnapshot).not.toHaveBeenCalled();
    });
  });

  describe('Timeline Visual Elements', () => {
    it('should render timeline dots', () => {
      const snapshots = [
        createMockSnapshot({ id: 'snap-1' }),
        createMockSnapshot({ id: 'snap-2' }),
      ];

      mockUseGameStateStore.mockReturnValue({
        snapshots,
        loadSnapshot: mockLoadSnapshot,
      });

      const { container } = render(<StateHistoryTimeline sessionId="session-123" />);

      // Check for timeline dots (rounded-full elements)
      const dots = container.querySelectorAll('.rounded-full');
      expect(dots.length).toBeGreaterThan(0);
    });

    it('should render clock icons', () => {
      const snapshots = [createMockSnapshot({ id: 'snap-1' })];

      mockUseGameStateStore.mockReturnValue({
        snapshots,
        loadSnapshot: mockLoadSnapshot,
      });

      const { container } = render(<StateHistoryTimeline sessionId="session-123" />);

      // Check for Clock icon (lucide-react renders as SVG)
      const svgElements = container.querySelectorAll('svg');
      expect(svgElements.length).toBeGreaterThan(0);
    });
  });

  describe('Multiple Snapshots Ordering', () => {
    it('should render snapshots in reverse chronological order', () => {
      const snapshots = [
        createMockSnapshot({
          id: 'snap-1',
          timestamp: '2024-01-15T09:00:00Z',
          action: 'First move played',
        }),
        createMockSnapshot({
          id: 'snap-2',
          timestamp: '2024-01-15T10:00:00Z',
          action: 'Second move played',
        }),
        createMockSnapshot({
          id: 'snap-3',
          timestamp: '2024-01-15T11:00:00Z',
          action: 'Third move played',
        }),
      ];

      mockUseGameStateStore.mockReturnValue({
        snapshots,
        loadSnapshot: mockLoadSnapshot,
      });

      render(<StateHistoryTimeline sessionId="session-123" />);

      // Get all action texts in order - use specific pattern to avoid matching "Phase: Action"
      const moveActions = screen.getAllByText(/move played/i);
      const actionTexts = moveActions.map((el) => el.textContent);

      // Should be in reverse order (newest first)
      expect(actionTexts[0]).toBe('Third move played');
      expect(actionTexts[1]).toBe('Second move played');
      expect(actionTexts[2]).toBe('First move played');
    });
  });

  describe('Snapshot Count Display', () => {
    it('should show correct count for single snapshot', () => {
      mockUseGameStateStore.mockReturnValue({
        snapshots: [createMockSnapshot()],
        loadSnapshot: mockLoadSnapshot,
      });

      render(<StateHistoryTimeline sessionId="session-123" />);

      expect(screen.getByText('1 snapshots available')).toBeInTheDocument();
    });

    it('should show correct count for many snapshots', () => {
      const manySnapshots = Array.from({ length: 10 }, (_, i) =>
        createMockSnapshot({ id: `snap-${i}` })
      );

      mockUseGameStateStore.mockReturnValue({
        snapshots: manySnapshots,
        loadSnapshot: mockLoadSnapshot,
      });

      render(<StateHistoryTimeline sessionId="session-123" />);

      expect(screen.getByText('10 snapshots available')).toBeInTheDocument();
    });
  });
});
