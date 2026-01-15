/**
 * LedgerTimelineItem Unit Tests
 * Issue #2422: Ledger Mode History Timeline
 *
 * Coverage: Diff computation, expansion, restore button
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import type { GameStateSnapshot } from '@/types/game-state';

import { LedgerTimelineItem } from '../LedgerTimelineItem';

const mockSnapshot: GameStateSnapshot = {
  id: 'snap-1',
  timestamp: '2024-01-15T10:30:00Z',
  userId: 'user-1',
  action: 'Alice scored 5 points',
  state: {
    sessionId: 'session-1',
    gameId: 'game-1',
    templateId: 'template-1',
    version: '1.0',
    roundNumber: 3,
    phase: 'Main Game',
    players: [
      { playerName: 'Alice', playerOrder: 1, score: 25 },
      { playerName: 'Bob', playerOrder: 2, score: 18 },
    ],
  },
};

const mockPreviousSnapshot: GameStateSnapshot = {
  ...mockSnapshot,
  id: 'snap-0',
  timestamp: '2024-01-15T10:25:00Z',
  action: 'Round started',
  state: {
    ...mockSnapshot.state,
    roundNumber: 2,
    phase: 'Setup',
    players: [
      { playerName: 'Alice', playerOrder: 1, score: 20 },
      { playerName: 'Bob', playerOrder: 2, score: 18 },
    ],
  },
};

describe('LedgerTimelineItem', () => {
  it('should render snapshot info correctly', () => {
    render(<LedgerTimelineItem snapshot={mockSnapshot} isLatest={false} onRestore={vi.fn()} />);

    expect(screen.getByText('Alice scored 5 points')).toBeInTheDocument();
    expect(screen.getByText(/Round: 3/)).toBeInTheDocument();
    expect(screen.getByText(/Phase: Main Game/)).toBeInTheDocument();
    expect(screen.getByText(/Players: 2/)).toBeInTheDocument();
  });

  it('should show "Latest" badge when isLatest is true', () => {
    render(<LedgerTimelineItem snapshot={mockSnapshot} isLatest={true} onRestore={vi.fn()} />);

    expect(screen.getByText('Latest')).toBeInTheDocument();
  });

  it('should disable restore button when isLatest is true', () => {
    render(<LedgerTimelineItem snapshot={mockSnapshot} isLatest={true} onRestore={vi.fn()} />);

    const restoreButton = screen.getByTestId('restore-button');
    expect(restoreButton).toBeDisabled();
  });

  it('should call onRestore when restore button clicked', () => {
    const onRestore = vi.fn();
    render(<LedgerTimelineItem snapshot={mockSnapshot} isLatest={false} onRestore={onRestore} />);

    const restoreButton = screen.getByTestId('restore-button');
    fireEvent.click(restoreButton);

    expect(onRestore).toHaveBeenCalledWith(mockSnapshot);
  });

  it('should compute diff correctly when previousSnapshot provided', () => {
    render(
      <LedgerTimelineItem
        snapshot={mockSnapshot}
        previousSnapshot={mockPreviousSnapshot}
        isLatest={false}
        onRestore={vi.fn()}
      />
    );

    expect(screen.getByText(/3 changes/)).toBeInTheDocument();
  });

  it('should toggle diff details when clicked', () => {
    render(
      <LedgerTimelineItem
        snapshot={mockSnapshot}
        previousSnapshot={mockPreviousSnapshot}
        isLatest={false}
        onRestore={vi.fn()}
      />
    );

    const diffToggle = screen.getByTestId('diff-toggle');

    // Initially collapsed
    expect(screen.queryByTestId('diff-details')).not.toBeInTheDocument();

    // Expand
    fireEvent.click(diffToggle);
    expect(screen.getByTestId('diff-details')).toBeInTheDocument();

    // Verify diff content
    expect(screen.getByText(/phase:/)).toBeInTheDocument();
    expect(screen.getByText(/roundNumber:/)).toBeInTheDocument();
    expect(screen.getByText(/Alice score:/)).toBeInTheDocument();
  });

  it('should not show diff toggle when no previous snapshot', () => {
    render(<LedgerTimelineItem snapshot={mockSnapshot} isLatest={false} onRestore={vi.fn()} />);

    expect(screen.queryByTestId('diff-toggle')).not.toBeInTheDocument();
  });

  it('should highlight old and new values in diff', () => {
    render(
      <LedgerTimelineItem
        snapshot={mockSnapshot}
        previousSnapshot={mockPreviousSnapshot}
        isLatest={false}
        onRestore={vi.fn()}
      />
    );

    const diffToggle = screen.getByTestId('diff-toggle');
    fireEvent.click(diffToggle);

    const diffDetails = screen.getByTestId('diff-details');
    expect(diffDetails.innerHTML).toContain('text-red-600'); // Old value
    expect(diffDetails.innerHTML).toContain('text-green-600'); // New value
  });
});
