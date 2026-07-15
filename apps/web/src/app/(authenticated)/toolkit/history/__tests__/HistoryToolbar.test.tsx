/**
 * HistoryToolbar — filter/sort toolbar for /toolkit/history (Issue #3006,
 * Task A7).
 *
 * Mirrors `HistoryPagination.test.tsx`'s mocking strategy: `useTranslation`
 * is mocked with an id-passthrough `t()` (interpolated calls encoded as
 * `id::JSON(values)`) so assertions check i18n message ids/values directly
 * without depending on the real ICU catalog.
 *
 * The search-debounce tests are isolated in their own `describe` block with
 * locally-scoped fake timers (`vi.useFakeTimers` / `vi.useRealTimers` in
 * `beforeEach`/`afterEach` of that block only) and drive the input via
 * `fireEvent.change` rather than `userEvent.type` — mixing `userEvent`'s
 * internal scheduling with fake timers is unreliable (see
 * `GamePicker.test.tsx` for the same pattern in this codebase). All other
 * tests use real timers with `userEvent` for realistic pointer/keyboard
 * interaction.
 */

import { act, fireEvent, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi, beforeEach, afterEach } from 'vitest';

import {
  HistoryToolbar,
  type HistoryToolbarGameOption,
  type HistoryToolbarProps,
  type HistoryToolbarWinnerOption,
} from '../_components/HistoryToolbar';
import { NO_WINNER_FILTER_VALUE, type HistoryFilterState } from '../_lib/history-filters';

vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({
    t: (id: string, values?: Record<string, unknown>) =>
      values ? `${id}::${JSON.stringify(values)}` : id,
  }),
}));

const GAME_OPTIONS: HistoryToolbarGameOption[] = [
  { id: 'game-1', label: 'Wingspan', count: 12 },
  { id: 'game-2', label: 'Catan', count: 8 },
  { id: 'game-3', label: 'Pandemic', count: 5 },
  { id: 'game-4', label: 'Azul', count: 4 },
  { id: 'game-5', label: 'Terraforming Mars', count: 3 },
];

const WINNER_OPTIONS: HistoryToolbarWinnerOption[] = [
  { value: 'Alice', label: 'Alice', count: 10 },
  { value: 'Bob', label: 'Bob', count: 6 },
  { value: NO_WINNER_FILTER_VALUE, label: 'raw-no-winner-label', count: 4 },
];

function baseState(overrides: Partial<HistoryFilterState> = {}): HistoryFilterState {
  return {
    search: '',
    gameIds: [],
    winners: [],
    datePreset: 'all',
    sort: 'recent',
    ...overrides,
  };
}

function renderToolbar(overrides: Partial<HistoryToolbarProps> = {}) {
  const onChange = overrides.onChange ?? vi.fn();
  const onViewChange = overrides.onViewChange ?? vi.fn();
  const onClearAll = overrides.onClearAll ?? vi.fn();
  const onExport = overrides.onExport ?? vi.fn();

  const props: HistoryToolbarProps = {
    state: overrides.state ?? baseState(),
    onChange,
    gameOptions: overrides.gameOptions ?? GAME_OPTIONS,
    winnerOptions: overrides.winnerOptions ?? WINNER_OPTIONS,
    view: overrides.view ?? 'table',
    onViewChange,
    totalCount: overrides.totalCount ?? 42,
    resultCount: overrides.resultCount ?? 42,
    onClearAll,
    onExport,
  };

  render(<HistoryToolbar {...props} />);

  return { onChange, onViewChange, onClearAll, onExport };
}

describe('HistoryToolbar', () => {
  describe('search debounce', () => {
    beforeEach(() => {
      vi.useFakeTimers({ shouldAdvanceTime: true });
    });

    afterEach(() => {
      vi.useRealTimers();
    });

    it('calls onChange with the updated search only after the 400ms debounce elapses', async () => {
      const { onChange } = renderToolbar();

      const searchInput = screen.getByRole('searchbox', {
        name: 'pages.toolkitHistory.filters.searchAriaLabel',
      });

      fireEvent.change(searchInput, { target: { value: 'wingspan' } });

      expect(onChange).not.toHaveBeenCalled();
      expect(screen.getByText('pages.toolkitHistory.filters.searching')).toBeInTheDocument();

      await act(async () => {
        vi.advanceTimersByTime(400);
      });

      expect(onChange).toHaveBeenCalledTimes(1);
      expect(onChange).toHaveBeenCalledWith(expect.objectContaining({ search: 'wingspan' }));
      expect(screen.queryByText('pages.toolkitHistory.filters.searching')).not.toBeInTheDocument();
    });

    it('only commits the last value when the search changes multiple times within the debounce window', async () => {
      const { onChange } = renderToolbar();

      const searchInput = screen.getByRole('searchbox', {
        name: 'pages.toolkitHistory.filters.searchAriaLabel',
      });

      fireEvent.change(searchInput, { target: { value: 'w' } });
      await act(async () => {
        vi.advanceTimersByTime(200);
      });
      fireEvent.change(searchInput, { target: { value: 'wi' } });
      await act(async () => {
        vi.advanceTimersByTime(200);
      });
      fireEvent.change(searchInput, { target: { value: 'wingspan' } });

      await act(async () => {
        vi.advanceTimersByTime(400);
      });

      expect(onChange).toHaveBeenCalledTimes(1);
      expect(onChange).toHaveBeenCalledWith(expect.objectContaining({ search: 'wingspan' }));
    });
  });

  it('clears the search immediately (bypassing the debounce) when the clear button is clicked', async () => {
    const user = userEvent.setup();
    const { onChange } = renderToolbar({ state: baseState({ search: 'catan' }) });

    const clearButton = screen.getByRole('button', {
      name: 'pages.toolkitHistory.filters.clearSearch',
    });
    await user.click(clearButton);

    expect(onChange).toHaveBeenCalledWith(expect.objectContaining({ search: '' }));
  });

  it('toggles a game checkbox on, updating gameIds via onChange', async () => {
    const user = userEvent.setup();
    const { onChange } = renderToolbar();

    await user.click(screen.getByRole('button', { name: 'pages.toolkitHistory.filters.games' }));
    await user.click(await screen.findByRole('checkbox', { name: 'Catan' }));

    expect(onChange).toHaveBeenCalledWith(expect.objectContaining({ gameIds: ['game-2'] }));
  });

  it('toggles a game checkbox off when it is already selected', async () => {
    const user = userEvent.setup();
    const { onChange } = renderToolbar({ state: baseState({ gameIds: ['game-1', 'game-2'] }) });

    await user.click(screen.getByRole('button', { name: 'pages.toolkitHistory.filters.games' }));
    await user.click(await screen.findByRole('checkbox', { name: 'Catan' }));

    expect(onChange).toHaveBeenCalledWith(expect.objectContaining({ gameIds: ['game-1'] }));
  });

  it('caps visible game options at 4 and reveals the rest via "Mostra altri"', async () => {
    const user = userEvent.setup();
    renderToolbar();

    await user.click(screen.getByRole('button', { name: 'pages.toolkitHistory.filters.games' }));

    expect(screen.queryByRole('checkbox', { name: 'Terraforming Mars' })).not.toBeInTheDocument();

    await user.click(
      screen.getByRole('button', {
        name: 'pages.toolkitHistory.filters.showMore::{"n":1}',
      })
    );

    expect(screen.getByRole('checkbox', { name: 'Terraforming Mars' })).toBeInTheDocument();
  });

  it('clears all selected games when "Tutti" is clicked', async () => {
    const user = userEvent.setup();
    const { onChange } = renderToolbar({ state: baseState({ gameIds: ['game-1', 'game-2'] }) });

    await user.click(screen.getByRole('button', { name: 'pages.toolkitHistory.filters.games' }));
    await user.click(screen.getByRole('option', { name: 'pages.toolkitHistory.filters.all' }));

    expect(onChange).toHaveBeenCalledWith(expect.objectContaining({ gameIds: [] }));
  });

  it('formats winner options as "{name} (won)" and the sentinel as "No winner", toggling winners on select', async () => {
    const user = userEvent.setup();
    const { onChange } = renderToolbar();

    await user.click(screen.getByRole('button', { name: 'pages.toolkitHistory.filters.winners' }));

    expect(
      screen.getByRole('checkbox', {
        name: 'pages.toolkitHistory.filters.winnerWon::{"name":"Alice"}',
      })
    ).toBeInTheDocument();
    expect(
      screen.getByRole('checkbox', { name: 'pages.toolkitHistory.filters.noWinner' })
    ).toBeInTheDocument();

    await user.click(
      screen.getByRole('checkbox', { name: 'pages.toolkitHistory.filters.noWinner' })
    );

    expect(onChange).toHaveBeenCalledWith(
      expect.objectContaining({ winners: [NO_WINNER_FILTER_VALUE] })
    );
  });

  it('applies a relative date preset immediately and clears any custom bounds', async () => {
    const user = userEvent.setup();
    const { onChange } = renderToolbar({
      state: baseState({ datePreset: 'custom', dateFrom: '2026-01-01', dateTo: '2026-02-01' }),
    });

    await user.click(screen.getByRole('button', { name: 'pages.toolkitHistory.filters.date' }));
    await user.click(screen.getByText('pages.toolkitHistory.filters.dateOptions.last30'));

    expect(onChange).toHaveBeenCalledWith(
      expect.objectContaining({ datePreset: 'last30', dateFrom: undefined, dateTo: undefined })
    );
  });

  it('applies a custom date range only after both dates are filled in and "Applica intervallo" is clicked', async () => {
    const user = userEvent.setup();
    const { onChange } = renderToolbar();

    await user.click(screen.getByRole('button', { name: 'pages.toolkitHistory.filters.date' }));
    await user.click(screen.getByText('pages.toolkitHistory.filters.dateOptions.custom'));

    const applyButton = screen.getByRole('button', {
      name: 'pages.toolkitHistory.filters.applyRange',
    });
    expect(applyButton).toBeDisabled();

    const fromInput = screen.getByLabelText('pages.toolkitHistory.filters.dateFromLabel');
    const toInput = screen.getByLabelText('pages.toolkitHistory.filters.dateToLabel');
    fireEvent.change(fromInput, { target: { value: '2026-06-01' } });
    fireEvent.change(toInput, { target: { value: '2026-06-30' } });

    expect(applyButton).not.toBeDisabled();
    await user.click(applyButton);

    expect(onChange).toHaveBeenCalledWith(
      expect.objectContaining({
        datePreset: 'custom',
        dateFrom: '2026-06-01',
        dateTo: '2026-06-30',
      })
    );
  });

  it('calls onChange with the new sort when a sort option is selected', async () => {
    const user = userEvent.setup();
    const { onChange } = renderToolbar();

    const sortTrigger = screen.getByRole('combobox', {
      name: 'pages.toolkitHistory.filters.sortBy',
    });
    await user.click(sortTrigger);
    await user.click(
      await screen.findByRole('option', { name: 'pages.toolkitHistory.filters.sortOptions.oldest' })
    );

    expect(onChange).toHaveBeenCalledWith(expect.objectContaining({ sort: 'oldest' }));
  });

  it('calls onViewChange when the cards view button is clicked', async () => {
    const user = userEvent.setup();
    const { onViewChange } = renderToolbar({ view: 'table' });

    await user.click(
      screen.getByRole('button', { name: 'pages.toolkitHistory.filters.viewCards' })
    );

    expect(onViewChange).toHaveBeenCalledWith('cards');
  });

  it('calls onExport when the export button is clicked', async () => {
    const user = userEvent.setup();
    const { onExport } = renderToolbar();

    await user.click(screen.getByRole('button', { name: 'pages.toolkitHistory.hero.exportCsv' }));

    expect(onExport).toHaveBeenCalledTimes(1);
  });

  describe('summary bar', () => {
    it('is hidden when no filters are active', () => {
      renderToolbar({ state: baseState() });

      expect(screen.queryByText(/summary\.activeFilters/)).not.toBeInTheDocument();
    });

    it('shows the active-filter count and result/total, and "Cancella tutto" calls onClearAll', async () => {
      const user = userEvent.setup();
      const { onClearAll } = renderToolbar({
        state: baseState({ gameIds: ['game-1'], search: 'wingspan' }),
        totalCount: 42,
        resultCount: 7,
      });

      expect(
        screen.getByText('pages.toolkitHistory.summary.activeFilters::{"n":2}')
      ).toBeInTheDocument();
      expect(
        screen.getByText('pages.toolkitHistory.summary.results::{"n":7,"total":42}')
      ).toBeInTheDocument();

      await user.click(
        screen.getByRole('button', { name: 'pages.toolkitHistory.summary.clearAll' })
      );

      expect(onClearAll).toHaveBeenCalledTimes(1);
    });
  });
});
