/**
 * @vitest-environment jsdom
 */

/**
 * WishlistToolbar — filter/sort toolbar for /library/wishlist (Issue #3007,
 * Task B5).
 *
 * Mirrors `MeepleWishlistCard.test.tsx`'s i18n strategy (real `IntlProvider`
 * wired to the flattened `it.json` catalog) combined with
 * `HistoryToolbar.test.tsx`'s search-debounce isolation pattern (locally
 * scoped fake timers, driven via `fireEvent.change` rather than
 * `userEvent.type` — mixing `userEvent`'s internal scheduling with fake
 * timers is unreliable).
 */

import { act, fireEvent, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { IntlProvider } from 'react-intl';
import { describe, expect, it, vi, beforeEach, afterEach } from 'vitest';

import { flattenMessages } from '@/locales';
import itMessages from '@/locales/it.json';

import { WishlistToolbar } from '../_components/WishlistToolbar';

import type { Priority, WishlistFilterState } from '../_lib/wishlist-filters';
import type { ReactElement } from 'react';

const MESSAGES = flattenMessages(itMessages as unknown as Record<string, unknown>);
const FILTERS_I18N = itMessages.pages.library.wishlist.filters;
const SUMMARY_I18N = itMessages.pages.library.wishlist.summary;
const PRIORITY_I18N = itMessages.pages.library.wishlist.priority;

function renderWithIntl(ui: ReactElement) {
  return render(
    <IntlProvider locale="it" messages={MESSAGES}>
      {ui}
    </IntlProvider>
  );
}

function baseState(overrides: Partial<WishlistFilterState> = {}): WishlistFilterState {
  return {
    search: '',
    priorities: [],
    sort: 'priority',
    ...overrides,
  };
}

const PRIORITY_COUNTS: Record<Priority, number> = { high: 5, medium: 4, low: 3 };

function renderToolbar(
  overrides: Partial<{
    state: WishlistFilterState;
    onChange: (next: WishlistFilterState) => void;
    priorityCounts: Record<Priority, number>;
    totalCount: number;
    resultCount: number;
    onClearAll: () => void;
  }> = {}
) {
  const onChange = overrides.onChange ?? vi.fn();
  const onClearAll = overrides.onClearAll ?? vi.fn();

  renderWithIntl(
    <WishlistToolbar
      state={overrides.state ?? baseState()}
      onChange={onChange}
      priorityCounts={overrides.priorityCounts ?? PRIORITY_COUNTS}
      totalCount={overrides.totalCount ?? 12}
      resultCount={overrides.resultCount ?? 12}
      onClearAll={onClearAll}
    />
  );

  return { onChange, onClearAll };
}

describe('WishlistToolbar', () => {
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
        name: FILTERS_I18N.searchAriaLabel,
      });

      fireEvent.change(searchInput, { target: { value: 'brass' } });

      expect(onChange).not.toHaveBeenCalled();
      expect(screen.getByText(FILTERS_I18N.searching)).toBeInTheDocument();

      await act(async () => {
        vi.advanceTimersByTime(400);
      });

      expect(onChange).toHaveBeenCalledTimes(1);
      expect(onChange).toHaveBeenCalledWith(expect.objectContaining({ search: 'brass' }));
      expect(screen.queryByText(FILTERS_I18N.searching)).not.toBeInTheDocument();
    });

    it('cancels the pending debounced commit when the search is cleared before it fires', async () => {
      const { onChange } = renderToolbar();

      const searchInput = screen.getByRole('searchbox', {
        name: FILTERS_I18N.searchAriaLabel,
      });

      fireEvent.change(searchInput, { target: { value: 'catan' } });
      await act(async () => {
        vi.advanceTimersByTime(200);
      });
      expect(onChange).not.toHaveBeenCalled();

      fireEvent.click(screen.getByRole('button', { name: FILTERS_I18N.clearSearch }));

      await act(async () => {
        vi.advanceTimersByTime(400);
      });

      expect(onChange).toHaveBeenCalledTimes(1);
      expect(onChange).toHaveBeenCalledWith(expect.objectContaining({ search: '' }));
    });
  });

  it('clears the search immediately (bypassing the debounce) when the clear button is clicked', async () => {
    const user = userEvent.setup();
    const { onChange } = renderToolbar({ state: baseState({ search: 'catan' }) });

    await user.click(screen.getByRole('button', { name: FILTERS_I18N.clearSearch }));

    expect(onChange).toHaveBeenCalledWith(expect.objectContaining({ search: '' }));
  });

  it('toggles a priority chip on, updating priorities via onChange', async () => {
    const user = userEvent.setup();
    const { onChange } = renderToolbar();

    await user.click(screen.getByRole('checkbox', { name: PRIORITY_I18N.high }));

    expect(onChange).toHaveBeenCalledWith(expect.objectContaining({ priorities: ['high'] }));
  });

  it('toggles a priority chip off when it is already selected', async () => {
    const user = userEvent.setup();
    const { onChange } = renderToolbar({ state: baseState({ priorities: ['high', 'medium'] }) });

    await user.click(screen.getByRole('checkbox', { name: PRIORITY_I18N.high }));

    expect(onChange).toHaveBeenCalledWith(expect.objectContaining({ priorities: ['medium'] }));
  });

  it('marks the "Tutti" chip as checked when no priority is selected', () => {
    renderToolbar({ state: baseState({ priorities: [] }) });

    expect(screen.getByRole('checkbox', { name: FILTERS_I18N.all })).toHaveAttribute(
      'aria-checked',
      'true'
    );
  });

  it('clears all selected priorities when "Tutti" is clicked', async () => {
    const user = userEvent.setup();
    const { onChange } = renderToolbar({ state: baseState({ priorities: ['high', 'low'] }) });

    await user.click(screen.getByRole('checkbox', { name: FILTERS_I18N.all }));

    expect(onChange).toHaveBeenCalledWith(expect.objectContaining({ priorities: [] }));
  });

  it('shows the priority chip counts', () => {
    renderToolbar({ priorityCounts: { high: 5, medium: 4, low: 3 }, totalCount: 12 });

    expect(screen.getByRole('checkbox', { name: FILTERS_I18N.all })).toHaveTextContent('12');
    expect(screen.getByRole('checkbox', { name: PRIORITY_I18N.high })).toHaveTextContent('5');
    expect(screen.getByRole('checkbox', { name: PRIORITY_I18N.medium })).toHaveTextContent('4');
    expect(screen.getByRole('checkbox', { name: PRIORITY_I18N.low })).toHaveTextContent('3');
  });

  it('shows the priority icon (mockup PRIO map) in each priority chip', () => {
    renderToolbar();

    expect(screen.getByRole('checkbox', { name: PRIORITY_I18N.high })).toHaveTextContent('🔥');
    expect(screen.getByRole('checkbox', { name: PRIORITY_I18N.medium })).toHaveTextContent('⭐');
    expect(screen.getByRole('checkbox', { name: PRIORITY_I18N.low })).toHaveTextContent('⬇️');
  });

  it('calls onChange with the new sort when a sort option is selected', async () => {
    const user = userEvent.setup();
    const { onChange } = renderToolbar({ state: baseState({ sort: 'priority' }) });

    await user.click(screen.getByRole('button', { name: FILTERS_I18N.sortBy }));
    await user.click(await screen.findByRole('option', { name: FILTERS_I18N.sortOptions.oldest }));

    expect(onChange).toHaveBeenCalledWith(expect.objectContaining({ sort: 'oldest' }));
  });

  it('marks the current sort option as selected in the popover', async () => {
    const user = userEvent.setup();
    renderToolbar({ state: baseState({ sort: 'alpha' }) });

    await user.click(screen.getByRole('button', { name: FILTERS_I18N.sortBy }));

    expect(
      await screen.findByRole('option', { name: FILTERS_I18N.sortOptions.alpha })
    ).toHaveAttribute('aria-selected', 'true');
  });

  describe('summary bar', () => {
    it('is hidden when no filters are active', () => {
      renderToolbar({ state: baseState() });

      expect(screen.queryByText(/filtro/)).not.toBeInTheDocument();
    });

    it('shows the active-filter count and result/total, and "Cancella filtri" calls onClearAll', async () => {
      const user = userEvent.setup();
      const { onClearAll } = renderToolbar({
        state: baseState({ priorities: ['high'], search: 'brass' }),
        totalCount: 12,
        resultCount: 3,
      });

      expect(screen.getByText('2 filtri attivi')).toBeInTheDocument();
      expect(screen.getByText('3 giochi su 12')).toBeInTheDocument();

      await user.click(screen.getByRole('button', { name: SUMMARY_I18N.clearAll }));

      expect(onClearAll).toHaveBeenCalledTimes(1);
    });
  });
});
