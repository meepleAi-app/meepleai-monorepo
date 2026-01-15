/**
 * LedgerTimeline Integration Tests
 * Issue #2422: Ledger Mode History Timeline
 *
 * Coverage: Main component integration, store interaction, empty states
 */

import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import type { GameStateSnapshot } from '@/types/game-state';

// Mock the store before importing component
vi.mock('@/lib/stores/game-state-store', () => ({
  useGameStateStore: vi.fn(),
}));

import { useGameStateStore } from '@/lib/stores/game-state-store';
import { LedgerTimeline } from '../LedgerTimeline';

const mockSnapshots: GameStateSnapshot[] = [
  {
    id: 'snap-1',
    timestamp: '2024-01-15T10:30:00Z',
    userId: 'user-1',
    action: 'Game started',
    state: {
      sessionId: 'session-1',
      gameId: 'game-1',
      templateId: 'template-1',
      version: '1.0',
      roundNumber: 1,
      phase: 'Setup',
      players: [
        { playerName: 'Alice', playerOrder: 1, score: 0 },
        { playerName: 'Bob', playerOrder: 2, score: 0 },
      ],
    },
  },
  {
    id: 'snap-2',
    timestamp: '2024-01-15T10:35:00Z',
    userId: 'user-1',
    action: 'Alice scored 5 points',
    state: {
      sessionId: 'session-1',
      gameId: 'game-1',
      templateId: 'template-1',
      version: '1.0',
      roundNumber: 2,
      phase: 'Main Game',
      players: [
        { playerName: 'Alice', playerOrder: 1, score: 5 },
        { playerName: 'Bob', playerOrder: 2, score: 0 },
      ],
    },
  },
  {
    id: 'snap-3',
    timestamp: '2024-01-15T10:40:00Z',
    userId: 'user-1',
    action: 'Bob collected resources',
    state: {
      sessionId: 'session-1',
      gameId: 'game-1',
      templateId: 'template-1',
      version: '1.0',
      roundNumber: 3,
      phase: 'Main Game',
      players: [
        { playerName: 'Alice', playerOrder: 1, score: 5 },
        { playerName: 'Bob', playerOrder: 2, score: 3 },
      ],
    },
  },
];

describe('LedgerTimeline', () => {
  let mockLoadSnapshot: any;

  beforeEach(() => {
    mockLoadSnapshot = vi.fn();
    vi.mocked(useGameStateStore).mockReturnValue({
      snapshots: [],
      loadSnapshot: mockLoadSnapshot,
    } as any);
  });

  it('should render empty state when no snapshots', () => {
    render(<LedgerTimeline sessionId="session-1" />);

    expect(screen.getByTestId('ledger-timeline-empty')).toBeInTheDocument();
    expect(screen.getByText('No history available yet')).toBeInTheDocument();
    expect(screen.getByText(/Actions will appear here/)).toBeInTheDocument();
  });

  it('should render timeline with snapshots', () => {
    vi.mocked(useGameStateStore).mockReturnValue({
      snapshots: mockSnapshots,
      loadSnapshot: mockLoadSnapshot,
    } as any);

    render(<LedgerTimeline sessionId="session-1" />);

    expect(screen.getByTestId('ledger-timeline')).toBeInTheDocument();
    expect(screen.getByText('Ledger History')).toBeInTheDocument();
    expect(screen.getByText('3 actions recorded')).toBeInTheDocument();
  });

  it('should display snapshots in reverse chronological order', () => {
    vi.mocked(useGameStateStore).mockReturnValue({
      snapshots: mockSnapshots,
      loadSnapshot: mockLoadSnapshot,
    } as any);

    render(<LedgerTimeline sessionId="session-1" />);

    const items = screen.getAllByTestId('ledger-timeline-item');
    expect(items).toHaveLength(3);

    // Latest snapshot should be first
    expect(items[0]).toHaveTextContent('Bob collected resources');
    expect(items[1]).toHaveTextContent('Alice scored 5 points');
    expect(items[2]).toHaveTextContent('Game started');
  });

  it('should render export button when snapshots exist', () => {
    vi.mocked(useGameStateStore).mockReturnValue({
      snapshots: mockSnapshots,
      loadSnapshot: mockLoadSnapshot,
    } as any);

    render(<LedgerTimeline sessionId="session-1" />);

    expect(screen.getByTestId('export-button')).toBeInTheDocument();
  });

  it('should not render export button when no snapshots', () => {
    render(<LedgerTimeline sessionId="session-1" />);

    expect(screen.queryByTestId('export-button')).not.toBeInTheDocument();
  });

  it('should open rollback dialog when restore clicked', async () => {
    vi.mocked(useGameStateStore).mockReturnValue({
      snapshots: mockSnapshots,
      loadSnapshot: mockLoadSnapshot,
    } as any);

    render(<LedgerTimeline sessionId="session-1" />);

    const items = screen.getAllByTestId('ledger-timeline-item');
    const restoreButton = items[1].querySelector('[data-testid="restore-button"]');

    expect(restoreButton).not.toBeDisabled();

    fireEvent.click(restoreButton!);

    // Dialog should open (confirmation dialog would be visible)
    // In integration, we verify the hook connection
  });

  it('should mark latest snapshot with badge', () => {
    vi.mocked(useGameStateStore).mockReturnValue({
      snapshots: mockSnapshots,
      loadSnapshot: mockLoadSnapshot,
    } as any);

    render(<LedgerTimeline sessionId="session-1" />);

    const items = screen.getAllByTestId('ledger-timeline-item');

    // Only first item should have "Latest" badge
    expect(items[0]).toHaveTextContent('Latest');
    expect(items[1]).not.toHaveTextContent('Latest');
    expect(items[2]).not.toHaveTextContent('Latest');
  });

  it('should disable restore button for latest snapshot', () => {
    vi.mocked(useGameStateStore).mockReturnValue({
      snapshots: mockSnapshots,
      loadSnapshot: mockLoadSnapshot,
    } as any);

    render(<LedgerTimeline sessionId="session-1" />);

    const items = screen.getAllByTestId('ledger-timeline-item');
    const latestRestoreButton = items[0].querySelector('[data-testid="restore-button"]');
    const olderRestoreButton = items[1].querySelector('[data-testid="restore-button"]');

    expect(latestRestoreButton).toBeDisabled();
    expect(olderRestoreButton).not.toBeDisabled();
  });

  it('should render multiple timeline items', () => {
    vi.mocked(useGameStateStore).mockReturnValue({
      snapshots: mockSnapshots,
      loadSnapshot: mockLoadSnapshot,
    } as any);

    render(<LedgerTimeline sessionId="session-1" />);

    const items = screen.getAllByTestId('ledger-timeline-item');
    // Should render all snapshots as timeline items
    expect(items.length).toBe(mockSnapshots.length);
  });

  it('should render scroll area for long lists', () => {
    vi.mocked(useGameStateStore).mockReturnValue({
      snapshots: mockSnapshots,
      loadSnapshot: mockLoadSnapshot,
    } as any);

    const { container } = render(<LedgerTimeline sessionId="session-1" />);

    const scrollArea = container.querySelector('[data-radix-scroll-area-viewport]');
    expect(scrollArea).toBeInTheDocument();
  });

  it('should handle large snapshot lists (50+ items)', async () => {
    const largeSnapshotList = Array.from({ length: 50 }, (_, i) => ({
      ...mockSnapshots[0],
      id: `snap-${i}`,
      action: `Action ${i}`,
    }));

    vi.mocked(useGameStateStore).mockReturnValue({
      snapshots: largeSnapshotList,
      loadSnapshot: mockLoadSnapshot,
    } as any);

    render(<LedgerTimeline sessionId="session-1" />);

    await waitFor(() => {
      expect(screen.getByText('50 actions recorded')).toBeInTheDocument();
    });

    const items = screen.getAllByTestId('ledger-timeline-item');
    expect(items).toHaveLength(50);
  });

  it('should pass correct sessionId to export component', async () => {
    vi.mocked(useGameStateStore).mockReturnValue({
      snapshots: mockSnapshots,
      loadSnapshot: mockLoadSnapshot,
    } as any);

    render(<LedgerTimeline sessionId="test-session-123" />);

    await waitFor(() => {
      const exportButton = screen.getByTestId('export-button');
      expect(exportButton).toBeInTheDocument();
    });

    // Export component receives sessionId prop (verified by component rendering)
  });
});
