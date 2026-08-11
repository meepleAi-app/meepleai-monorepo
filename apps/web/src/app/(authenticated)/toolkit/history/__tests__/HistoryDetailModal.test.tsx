/**
 * HistoryDetailModal — session detail drawer for /toolkit/history (Issue #3006, Task A8).
 *
 * Mirrors HistoryCards.test.tsx's mocking strategy for `useTranslation` (id-passthrough
 * `t()` with `formatDate`/`formatTime` stubbed to stable strings) plus
 * HistoryToolbar.test.tsx's interpolation-aware `t()` (`id::JSON(values)`) so
 * title/sub assertions can check exact interpolated output without depending on the
 * real ICU catalog.
 *
 * The Drawer renders via Radix Dialog in jsdom — `matchMedia` is mocked to
 * `matches: true` (desktop) for deterministic synchronous rendering, mirroring
 * `GameNightEditDrawer.test.tsx`'s jsdom shim (no vaul pointer-capture shims needed
 * since we never exercise the mobile/vaul branch here).
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi, beforeEach } from 'vitest';

import type { HistoryRow } from '../_lib/history-filters';
import { HistoryDetailModal } from '../_components/HistoryDetailModal';

vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({
    t: (id: string, values?: Record<string, unknown>) =>
      values ? `${id}::${JSON.stringify(values)}` : id,
    formatDate: () => '10 lug 2026',
    formatTime: () => '14:30',
  }),
}));

function installMatchMedia(matches: boolean): void {
  Object.defineProperty(window, 'matchMedia', {
    configurable: true,
    writable: true,
    value: vi.fn().mockReturnValue({
      matches,
      media: '(min-width: 768px)',
      addEventListener: () => {},
      removeEventListener: () => {},
      onchange: null,
    }),
  });
}

const COMPLETE_ROW: HistoryRow = {
  id: 'row-1',
  gameId: 'game-1',
  gameName: 'Wingspan',
  startedAt: '2026-07-10T14:30:00Z',
  durationMinutes: 95,
  playerNames: ['Alice Smith', 'Bob Jones'],
  playerCount: 2,
  winnerName: 'Alice Smith',
  isCoop: false,
  winScore: 42,
  notes: 'Great game!',
};

const NO_NOTES_ROW: HistoryRow = {
  ...COMPLETE_ROW,
  id: 'row-2',
  notes: null,
};

beforeEach(() => {
  installMatchMedia(true); // desktop → Radix Dialog, deterministic in jsdom
});

describe('HistoryDetailModal', () => {
  it('renders nothing when row is null', () => {
    const { container } = render(<HistoryDetailModal row={null} onClose={vi.fn()} />);

    expect(container).toBeEmptyDOMElement();
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('shows the notes text when the row has notes', () => {
    render(<HistoryDetailModal row={COMPLETE_ROW} onClose={vi.fn()} />);

    expect(screen.getByText('Great game!')).toBeInTheDocument();
    expect(screen.queryByText('pages.toolkitHistory.modal.notesEmpty')).not.toBeInTheDocument();
  });

  it('shows notesEmpty when the row has no notes', () => {
    render(<HistoryDetailModal row={NO_NOTES_ROW} onClose={vi.fn()} />);

    expect(screen.getByText('pages.toolkitHistory.modal.notesEmpty')).toBeInTheDocument();
    expect(screen.queryByText('Great game!')).not.toBeInTheDocument();
  });

  it('shows "—" (noData) for the turns stat', () => {
    render(<HistoryDetailModal row={COMPLETE_ROW} onClose={vi.fn()} />);

    expect(screen.getByTestId('history-detail-modal-stat-turns')).toHaveTextContent(
      'pages.toolkitHistory.modal.noData'
    );
  });

  it('shows noData for variance/highlights and the real winScore for winnerScore', () => {
    render(<HistoryDetailModal row={COMPLETE_ROW} onClose={vi.fn()} />);

    expect(screen.getByTestId('history-detail-modal-stat-variance')).toHaveTextContent(
      'pages.toolkitHistory.modal.noData'
    );
    expect(screen.getByTestId('history-detail-modal-stat-highlights')).toHaveTextContent(
      'pages.toolkitHistory.modal.noData'
    );
    expect(screen.getByTestId('history-detail-modal-stat-winner-score')).toHaveTextContent('42');
  });

  it('falls back to noData for winnerScore when winScore is null', () => {
    render(<HistoryDetailModal row={{ ...COMPLETE_ROW, winScore: null }} onClose={vi.fn()} />);

    expect(screen.getByTestId('history-detail-modal-stat-winner-score')).toHaveTextContent(
      'pages.toolkitHistory.modal.noData'
    );
  });

  it('marks the winner in the leaderboard and shows noData for every score slot', () => {
    render(<HistoryDetailModal row={COMPLETE_ROW} onClose={vi.fn()} />);

    const winnerRow = screen.getByText('Alice Smith').closest('li');
    expect(winnerRow).not.toBeNull();
    expect(winnerRow).toHaveTextContent('🏆');
    expect(winnerRow).toHaveTextContent('pages.toolkitHistory.modal.noData');

    const nonWinnerRow = screen.getByText('Bob Jones').closest('li');
    expect(nonWinnerRow).not.toBeNull();
    expect(nonWinnerRow).not.toHaveTextContent('🏆');
    expect(nonWinnerRow).toHaveTextContent('pages.toolkitHistory.modal.noData');
  });

  it('does not render a timeline section', () => {
    render(<HistoryDetailModal row={COMPLETE_ROW} onClose={vi.fn()} />);

    expect(screen.queryByText('pages.toolkitHistory.modal.timeline')).not.toBeInTheDocument();
  });

  it('calls onClose when the close button is clicked', async () => {
    const onClose = vi.fn();
    const user = userEvent.setup();
    render(<HistoryDetailModal row={COMPLETE_ROW} onClose={onClose} />);

    await user.click(screen.getByRole('button', { name: 'pages.toolkitHistory.modal.close' }));

    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('does not render the dead footer actions (no backend command exists for them)', () => {
    render(<HistoryDetailModal row={COMPLETE_ROW} onClose={vi.fn()} />);

    expect(
      screen.queryByRole('button', { name: 'pages.toolkitHistory.modal.gameStats' })
    ).not.toBeInTheDocument();
    expect(
      screen.queryByRole('button', { name: 'pages.toolkitHistory.modal.playAgain' })
    ).not.toBeInTheDocument();
    expect(
      screen.queryByRole('button', { name: 'pages.toolkitHistory.modal.editNotes' })
    ).not.toBeInTheDocument();
    expect(
      screen.queryByRole('button', { name: 'pages.toolkitHistory.modal.delete' })
    ).not.toBeInTheDocument();
  });

  it('renders the interpolated title and sub with the row data', () => {
    render(<HistoryDetailModal row={COMPLETE_ROW} onClose={vi.fn()} />);

    expect(
      screen.getByText('pages.toolkitHistory.modal.title::{"game":"Wingspan","date":"10 lug 2026"}')
    ).toBeInTheDocument();
    expect(
      screen.getByText(
        'pages.toolkitHistory.modal.sub::{"duration":"1h 35m","count":2,"time":"14:30"}'
      )
    ).toBeInTheDocument();
  });
});
