/**
 * HistoryTable — desktop 8-column sortable table for /toolkit/history (Issue #3006, Task A4).
 *
 * `useTranslation` is mocked with an id-passthrough `t()` so header/label assertions
 * check the i18n message ids directly (no locale-catalog coupling). `formatDate`/
 * `formatTime`/`formatRelativeTime` are mocked to stable strings so the Data column
 * never depends on wall-clock time.
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import type { HistoryRow, HistorySort } from '../_lib/history-filters';
import { HistoryTable } from '../_components/HistoryTable';

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

function renderTable(
  overrides: Partial<{
    rows: HistoryRow[];
    sort: HistorySort;
    onSortChange: (s: HistorySort) => void;
    onOpenDetail: (row: HistoryRow) => void;
    onOpenGameStats: (gameId: string) => void;
  }> = {}
) {
  const onSortChange = overrides.onSortChange ?? vi.fn();
  const onOpenDetail = overrides.onOpenDetail ?? vi.fn();
  const onOpenGameStats = overrides.onOpenGameStats ?? vi.fn();
  const rows = overrides.rows ?? [COMPLETE_ROW, COOP_ROW, SOLO_NO_WINNER_ROW];
  const sort = overrides.sort ?? 'recent';

  render(
    <table>
      <HistoryTable
        rows={rows}
        sort={sort}
        onSortChange={onSortChange}
        onOpenDetail={onOpenDetail}
        onOpenGameStats={onOpenGameStats}
      />
    </table>
  );

  return { onSortChange, onOpenDetail, onOpenGameStats };
}

describe('HistoryTable', () => {
  it('renders the 8 column headers via i18n ids', () => {
    renderTable();

    expect(screen.getByText('pages.toolkitHistory.table.date')).toBeInTheDocument();
    expect(screen.getByText('pages.toolkitHistory.table.game')).toBeInTheDocument();
    expect(screen.getByText('pages.toolkitHistory.table.duration')).toBeInTheDocument();
    expect(screen.getByText('pages.toolkitHistory.table.players')).toBeInTheDocument();
    expect(screen.getByText('pages.toolkitHistory.table.winner')).toBeInTheDocument();
    expect(screen.getByText('pages.toolkitHistory.table.score')).toBeInTheDocument();
    expect(screen.getByText('pages.toolkitHistory.table.notes')).toBeInTheDocument();
    expect(screen.getByText('pages.toolkitHistory.table.actions')).toBeInTheDocument();
  });

  it('clicking the Durata header always sorts by "longest"', () => {
    const { onSortChange } = renderTable({ sort: 'recent' });

    fireEvent.click(screen.getByText('pages.toolkitHistory.table.duration'));

    expect(onSortChange).toHaveBeenCalledWith('longest');
  });

  it('clicking the Score header always sorts by "score"', () => {
    const { onSortChange } = renderTable({ sort: 'longest' });

    fireEvent.click(screen.getByText('pages.toolkitHistory.table.score'));

    expect(onSortChange).toHaveBeenCalledWith('score');
  });

  it('clicking the Data header switches to "oldest" when currently "recent"', () => {
    const { onSortChange } = renderTable({ sort: 'recent' });

    fireEvent.click(screen.getByText('pages.toolkitHistory.table.date'));

    expect(onSortChange).toHaveBeenCalledWith('oldest');
  });

  it('clicking the Data header switches to "recent" when currently "oldest"', () => {
    const { onSortChange } = renderTable({ sort: 'oldest' });

    fireEvent.click(screen.getByText('pages.toolkitHistory.table.date'));

    expect(onSortChange).toHaveBeenCalledWith('recent');
  });

  it('sets aria-sort on the active sortable column header', () => {
    renderTable({ sort: 'longest' });

    const durationHeader = screen.getByText('pages.toolkitHistory.table.duration').closest('th');
    const dateHeader = screen.getByText('pages.toolkitHistory.table.date').closest('th');

    expect(durationHeader).toHaveAttribute('aria-sort', 'descending');
    expect(dateHeader).toHaveAttribute('aria-sort', 'none');
  });

  it('shows the co-op short label in the Score column for a co-op row', () => {
    renderTable({ rows: [COOP_ROW] });

    expect(screen.getByText('pages.toolkitHistory.table.coopShort')).toBeInTheDocument();
  });

  it('shows the co-op badge in the Vincitore column for a co-op row', () => {
    renderTable({ rows: [COOP_ROW] });

    expect(screen.getByText('pages.toolkitHistory.table.coop')).toBeInTheDocument();
  });

  it('shows "—" in the Vincitore column for a non-coop row with no winner', () => {
    renderTable({ rows: [SOLO_NO_WINNER_ROW] });

    expect(screen.getByLabelText('pages.toolkitHistory.table.noWinner')).toHaveTextContent('—');
  });

  it('shows the winner name for a row with a winner', () => {
    renderTable({ rows: [COMPLETE_ROW] });

    expect(screen.getByText('Alice Smith')).toBeInTheDocument();
  });

  it('shows the win score for a non-coop row', () => {
    renderTable({ rows: [COMPLETE_ROW] });

    expect(screen.getByText('42')).toBeInTheDocument();
  });

  it('calls onOpenDetail when a row is clicked', () => {
    const { onOpenDetail } = renderTable({ rows: [COMPLETE_ROW] });

    fireEvent.click(screen.getByText('Wingspan'));

    expect(onOpenDetail).toHaveBeenCalledWith(COMPLETE_ROW);
  });

  it('calls onOpenDetail when the view-details action is clicked', () => {
    const { onOpenDetail } = renderTable({ rows: [COMPLETE_ROW] });

    fireEvent.click(screen.getByLabelText('pages.toolkitHistory.table.viewDetails'));

    expect(onOpenDetail).toHaveBeenCalledWith(COMPLETE_ROW);
  });

  it('calls onOpenGameStats with the gameId when the game-stats action is clicked', () => {
    const { onOpenGameStats, onOpenDetail } = renderTable({ rows: [COMPLETE_ROW] });

    fireEvent.click(screen.getByLabelText('pages.toolkitHistory.table.gameStats'));

    expect(onOpenGameStats).toHaveBeenCalledWith('game-1');
    expect(onOpenDetail).not.toHaveBeenCalled();
  });

  it('disables the notes action for a row with no notes', () => {
    renderTable({ rows: [COOP_ROW] });

    expect(screen.getByLabelText('pages.toolkitHistory.table.noNote')).toBeDisabled();
  });

  it('opens the detail view when the notes action is clicked for a row with notes', () => {
    const { onOpenDetail } = renderTable({ rows: [COMPLETE_ROW] });

    fireEvent.click(screen.getByLabelText('pages.toolkitHistory.table.hasNote'));

    expect(onOpenDetail).toHaveBeenCalledWith(COMPLETE_ROW);
  });

  it('renders the players aria-label with the player count and names', () => {
    renderTable({ rows: [COMPLETE_ROW] });

    expect(screen.getByLabelText('pages.toolkitHistory.table.playersAria')).toBeInTheDocument();
  });
});
