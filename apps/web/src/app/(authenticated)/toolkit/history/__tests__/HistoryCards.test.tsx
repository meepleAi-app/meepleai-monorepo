/**
 * HistoryCards — card stack view for /toolkit/history (Issue #3006, Task A5).
 *
 * Mirrors `HistoryTable.test.tsx`'s mocking strategy: `useTranslation` is
 * mocked with an id-passthrough `t()` so label assertions check i18n message
 * ids directly, and `formatDate`/`formatTime`/`formatRelativeTime` are mocked
 * to stable strings so no assertion depends on wall-clock time (the
 * component still computes a real `getRelativeTimeParts` value internally,
 * but the mocked `formatRelativeTime` ignores its arguments).
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import type { HistoryRow } from '../_lib/history-filters';
import { HistoryCards } from '../_components/HistoryCards';

vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({
    t: (id: string) => id,
    formatDate: () => '10 lug 2026',
    formatTime: () => '14:30',
    formatRelativeTime: () => '2 giorni fa',
  }),
}));

const COMPLETE_ROW: HistoryRow = {
  id: 'row-1',
  gameId: 'game-1',
  gameName: 'Wingspan',
  startedAt: '2026-07-10T14:30:00Z',
  durationMinutes: 95,
  playerNames: ['Alice Smith', 'Bob Jones', 'Cara Lee', 'Dan Ito'],
  playerCount: 4,
  winnerName: 'Alice Smith',
  isCoop: false,
  winScore: 42,
  notes: 'Great game!',
};

const COOP_ROW: HistoryRow = {
  id: 'row-2',
  gameId: 'game-2',
  gameName: 'Pandemic',
  startedAt: '2026-07-08T10:00:00Z',
  durationMinutes: 60,
  playerNames: ['Dave', 'Eve'],
  playerCount: 2,
  winnerName: null,
  isCoop: true,
  winScore: null,
  notes: null,
};

const SOLO_NO_WINNER_ROW: HistoryRow = {
  id: 'row-3',
  gameId: 'game-3',
  gameName: 'Solo Quest',
  startedAt: '2026-07-05T09:00:00Z',
  durationMinutes: 30,
  playerNames: ['Solo Player'],
  playerCount: 1,
  winnerName: null,
  isCoop: false,
  winScore: null,
  notes: null,
};

function renderCards(
  overrides: Partial<{
    rows: HistoryRow[];
    onOpenDetail: (row: HistoryRow) => void;
  }> = {}
) {
  const onOpenDetail = overrides.onOpenDetail ?? vi.fn();
  const rows = overrides.rows ?? [COMPLETE_ROW];

  render(<HistoryCards rows={rows} onOpenDetail={onOpenDetail} />);

  return { onOpenDetail };
}

describe('HistoryCards', () => {
  it('renders the gameName and the winner label for a row', () => {
    renderCards({ rows: [COMPLETE_ROW] });

    expect(screen.getByText('Wingspan')).toBeInTheDocument();
    expect(screen.getByText('Alice Smith')).toBeInTheDocument();
  });

  it('calls onOpenDetail with the row when the card is clicked', () => {
    const { onOpenDetail } = renderCards({ rows: [COMPLETE_ROW] });

    fireEvent.click(screen.getByText('Wingspan'));

    expect(onOpenDetail).toHaveBeenCalledWith(COMPLETE_ROW);
  });

  it('calls onOpenDetail when Enter is pressed on a focused card', () => {
    const { onOpenDetail } = renderCards({ rows: [COMPLETE_ROW] });

    fireEvent.keyDown(screen.getByRole('button', { name: /Wingspan/ }), { key: 'Enter' });

    expect(onOpenDetail).toHaveBeenCalledWith(COMPLETE_ROW);
  });

  it('shows the win score pill with the trophy icon for a non-coop row', () => {
    renderCards({ rows: [COMPLETE_ROW] });

    expect(screen.getByText('42')).toBeInTheDocument();
  });

  it('shows the Cooperativa badge and the Co-op pill for a coop row', () => {
    renderCards({ rows: [COOP_ROW] });

    expect(screen.getByText('pages.toolkitHistory.table.coop')).toBeInTheDocument();
    expect(screen.getByText('pages.toolkitHistory.cards.coop')).toBeInTheDocument();
  });

  it('shows "—" with an aria-label for a non-coop row with no winner', () => {
    renderCards({ rows: [SOLO_NO_WINNER_ROW] });

    expect(screen.getByLabelText('pages.toolkitHistory.table.noWinner')).toHaveTextContent('—');
  });

  it('renders the relative date via formatRelativeTime', () => {
    renderCards({ rows: [COMPLETE_ROW] });

    expect(screen.getByText('2 giorni fa')).toBeInTheDocument();
  });

  it('renders the absolute date via formatDate in the card footer', () => {
    renderCards({ rows: [COMPLETE_ROW] });

    expect(screen.getByText('10 lug 2026')).toBeInTheDocument();
  });

  it('shows the note flag for a row with notes', () => {
    renderCards({ rows: [COMPLETE_ROW] });

    expect(screen.getByLabelText('pages.toolkitHistory.table.hasNote')).toBeInTheDocument();
  });

  it('does not show a note flag for a row without notes', () => {
    renderCards({ rows: [COOP_ROW] });

    expect(screen.queryByLabelText('pages.toolkitHistory.table.hasNote')).not.toBeInTheDocument();
  });

  it('renders the players aria-label with the player count and names', () => {
    renderCards({ rows: [COMPLETE_ROW] });

    expect(screen.getByLabelText('pages.toolkitHistory.table.playersAria')).toBeInTheDocument();
  });

  it('renders one card per row', () => {
    renderCards({ rows: [COMPLETE_ROW, COOP_ROW, SOLO_NO_WINNER_ROW] });

    expect(screen.getAllByRole('button')).toHaveLength(3);
  });
});
