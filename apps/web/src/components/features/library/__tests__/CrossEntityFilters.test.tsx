import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { IntlProvider } from 'react-intl';

import { CrossEntityFilters, type GameStateFilter } from '../CrossEntityFilters';

const messages: Record<string, string> = {
  'pages.library.filters.stato.label': 'Stato',
  'pages.library.filters.stato.owned': 'Posseduti',
  'pages.library.filters.stato.wishlist': 'Wishlist',
  'pages.library.filters.stato.loaned': 'In prestito',
  'pages.library.filters.stato.withKb': 'Con Knowledge Base',
  'pages.library.filters.title': 'Più filtri',
};
function renderWithIntl(ui: React.ReactElement) {
  return render(
    <IntlProvider locale="it" messages={messages}>
      {ui}
    </IntlProvider>
  );
}
const noop = () => {};
const emptyFilter: GameStateFilter = { states: [], withKb: false };

describe('CrossEntityFilters', () => {
  it('renders the STATO chip group only for the games tab', () => {
    const { rerender } = renderWithIntl(
      <CrossEntityFilters
        tab="games"
        gameStateFilter={emptyFilter}
        onGameStateFilterChange={noop}
      />
    );
    expect(screen.getByTestId('cross-entity-filters-stato')).toBeInTheDocument();
    rerender(
      <IntlProvider locale="it" messages={messages}>
        <CrossEntityFilters
          tab="sessions"
          gameStateFilter={emptyFilter}
          onGameStateFilterChange={noop}
        />
      </IntlProvider>
    );
    expect(screen.queryByTestId('cross-entity-filters-stato')).not.toBeInTheDocument();
  });

  it('renders nothing for the all tab (search+sort are global in toolbar)', () => {
    const { container } = renderWithIntl(
      <CrossEntityFilters tab="all" gameStateFilter={emptyFilter} onGameStateFilterChange={noop} />
    );
    expect(container.querySelector('[data-testid="cross-entity-filters-stato"]')).toBeNull();
  });

  it('toggling a state chip emits the updated filter', async () => {
    const onChange = vi.fn();
    const user = userEvent.setup();
    renderWithIntl(
      <CrossEntityFilters
        tab="games"
        gameStateFilter={emptyFilter}
        onGameStateFilterChange={onChange}
      />
    );
    await user.click(screen.getByRole('button', { name: /posseduti/i }));
    expect(onChange).toHaveBeenCalledWith({ states: ['Owned'], withKb: false });
  });

  it('active state chip reflects the filter (aria-pressed)', () => {
    renderWithIntl(
      <CrossEntityFilters
        tab="games"
        gameStateFilter={{ states: ['Wishlist'], withKb: false }}
        onGameStateFilterChange={noop}
      />
    );
    expect(screen.getByRole('button', { name: /wishlist/i })).toHaveAttribute(
      'aria-pressed',
      'true'
    );
    expect(screen.getByRole('button', { name: /posseduti/i })).toHaveAttribute(
      'aria-pressed',
      'false'
    );
  });

  it('with-KB toggle emits withKb=true', async () => {
    const onChange = vi.fn();
    const user = userEvent.setup();
    renderWithIntl(
      <CrossEntityFilters
        tab="games"
        gameStateFilter={emptyFilter}
        onGameStateFilterChange={onChange}
      />
    );
    await user.click(screen.getByRole('button', { name: /con knowledge base/i }));
    expect(onChange).toHaveBeenCalledWith({ states: [], withKb: true });
  });

  // Phase 3b (#1593) — "Più filtri" chip tests

  it('renders Più filtri chip on games + agents tabs, hidden on all (R4)', () => {
    const onMoreFilters = vi.fn();
    const { rerender } = renderWithIntl(
      <CrossEntityFilters
        tab="games"
        gameStateFilter={emptyFilter}
        onGameStateFilterChange={noop}
        onMoreFilters={onMoreFilters}
      />
    );
    expect(screen.getByTestId('cross-entity-filters-more')).toBeInTheDocument();

    rerender(
      <IntlProvider locale="it" messages={messages}>
        <CrossEntityFilters
          tab="agents"
          gameStateFilter={emptyFilter}
          onGameStateFilterChange={noop}
          onMoreFilters={onMoreFilters}
        />
      </IntlProvider>
    );
    expect(screen.getByTestId('cross-entity-filters-more')).toBeInTheDocument();

    rerender(
      <IntlProvider locale="it" messages={messages}>
        <CrossEntityFilters
          tab="all"
          gameStateFilter={emptyFilter}
          onGameStateFilterChange={noop}
          onMoreFilters={onMoreFilters}
        />
      </IntlProvider>
    );
    expect(screen.queryByTestId('cross-entity-filters-more')).toBeNull();
  });

  it('invokes onMoreFilters on chip click', () => {
    const onMoreFilters = vi.fn();
    renderWithIntl(
      <CrossEntityFilters
        tab="games"
        gameStateFilter={emptyFilter}
        onGameStateFilterChange={noop}
        onMoreFilters={onMoreFilters}
      />
    );
    fireEvent.click(screen.getByTestId('cross-entity-filters-more'));
    expect(onMoreFilters).toHaveBeenCalledTimes(1);
  });

  it('shows activeFiltersCount badge when > 0', () => {
    renderWithIntl(
      <CrossEntityFilters
        tab="games"
        gameStateFilter={emptyFilter}
        onGameStateFilterChange={noop}
        onMoreFilters={vi.fn()}
        activeFiltersCount={3}
      />
    );
    expect(screen.getByTestId('cross-entity-filters-more')).toHaveTextContent(/\(3\)/);
  });
});
