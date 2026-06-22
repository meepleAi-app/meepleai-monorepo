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
  'pages.library.filters.search.placeholder': 'Cerca in libreria... (premi /)',
  'pages.library.filters.search.ariaLabel': 'Cerca in libreria',
  'pages.library.filters.search.keyboardHintAriaLabel': 'Scorciatoia tastiera',
  'pages.library.filters.advanced.label': 'Filtri avanzati',
  'pages.library.filters.chips.stato': 'STATO',
  'pages.library.filters.chips.gioco': 'GIOCO',
  'pages.library.filters.chips.data': 'DATA',
  'pages.library.filters.chips.sort': 'SORT',
  'pages.library.filters.chips.value.all': 'Tutti',
  'pages.library.filters.chips.value.always': 'Sempre',
  'pages.library.view.ariaLabel': 'Cambia visualizzazione',
  'pages.library.view.grid': 'Griglia',
  'pages.library.view.list': 'Lista',
  'pages.library.view.compact': 'Compatta',
  'pages.library.sort.recent': 'Aggiunti di recente',
  'pages.library.sort.title': 'Titolo (A–Z)',
  'pages.library.sort.rating': 'Valutazione',
  'pages.library.sort.state': 'Stato',
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

// Defaults for the new search/view/sort surface props introduced by Task 1.4.
const surfaceDefaults = {
  search: '',
  onSearchChange: noop,
  view: 'grid' as const,
  onViewChange: noop,
  sortKey: 'recent' as const,
};

describe('CrossEntityFilters', () => {
  it('renders the functional STATO chip group only for the games tab', () => {
    const { rerender } = renderWithIntl(
      <CrossEntityFilters
        {...surfaceDefaults}
        tab="games"
        gameStateFilter={emptyFilter}
        onGameStateFilterChange={noop}
      />
    );
    expect(screen.getByRole('button', { name: /posseduti/i })).toBeInTheDocument();
    rerender(
      <IntlProvider locale="it" messages={messages}>
        <CrossEntityFilters
          {...surfaceDefaults}
          tab="sessions"
          gameStateFilter={emptyFilter}
          onGameStateFilterChange={noop}
        />
      </IntlProvider>
    );
    expect(screen.queryByRole('button', { name: /posseduti/i })).not.toBeInTheDocument();
  });

  it('renders the toolbar root on the all tab without the functional STATO group', () => {
    const { container } = renderWithIntl(
      <CrossEntityFilters
        {...surfaceDefaults}
        tab="all"
        gameStateFilter={emptyFilter}
        onGameStateFilterChange={noop}
      />
    );
    // Task 1.4: root toolbar is always rendered (search + chips + view live here now).
    expect(container.querySelector('[data-testid="cross-entity-filters-stato"]')).not.toBeNull();
    // But the functional STATO buttons (Owned/Wishlist/InPrestito) are only in games tab.
    expect(screen.queryByRole('button', { name: /posseduti/i })).not.toBeInTheDocument();
  });

  it('toggling a state chip emits the updated filter', async () => {
    const onChange = vi.fn();
    const user = userEvent.setup();
    renderWithIntl(
      <CrossEntityFilters
        {...surfaceDefaults}
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
        {...surfaceDefaults}
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
        {...surfaceDefaults}
        tab="games"
        gameStateFilter={emptyFilter}
        onGameStateFilterChange={onChange}
      />
    );
    await user.click(screen.getByRole('button', { name: /con knowledge base/i }));
    expect(onChange).toHaveBeenCalledWith({ states: [], withKb: true });
  });

  // Phase 3b (#1593) — "Filtri avanzati" chip tests

  it('renders Filtri avanzati chip on games + agents tabs, hidden on all (R4)', () => {
    const onMoreFilters = vi.fn();
    const { rerender } = renderWithIntl(
      <CrossEntityFilters
        {...surfaceDefaults}
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
          {...surfaceDefaults}
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
          {...surfaceDefaults}
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
        {...surfaceDefaults}
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
        {...surfaceDefaults}
        tab="games"
        gameStateFilter={emptyFilter}
        onGameStateFilterChange={noop}
        onMoreFilters={vi.fn()}
        activeFiltersCount={3}
      />
    );
    expect(screen.getByTestId('cross-entity-filters-more')).toHaveTextContent(/\(3\)/);
  });

  // Task 1.4 — SP4 mockup conformance (jsx:218–310): search row + filter chips + view toggle.

  it('renders the 3 cross-entity filter chips after the #2186 GIOCO removal', () => {
    renderWithIntl(
      <CrossEntityFilters
        {...surfaceDefaults}
        tab="all"
        gameStateFilter={emptyFilter}
        onGameStateFilterChange={noop}
        onMoreFilters={vi.fn()}
      />
    );
    // STATO + DATA + SORT remain; GIOCO removed (#2186 — duplicated the
    // LibraryTabs entity scope and contributed to triple-filter overlap).
    expect(screen.getByRole('button', { name: /STATO/ })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /DATA/ })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /SORT/ })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /GIOCO/ })).not.toBeInTheDocument();
  });

  it('renders search input with / keyboard hint badge', () => {
    renderWithIntl(
      <CrossEntityFilters
        {...surfaceDefaults}
        tab="all"
        gameStateFilter={emptyFilter}
        onGameStateFilterChange={noop}
        onMoreFilters={vi.fn()}
      />
    );
    expect(screen.getByPlaceholderText(/premi \//)).toBeInTheDocument();
    const kbd = screen.getByText('/', { selector: 'kbd' });
    expect(kbd).toBeInTheDocument();
  });

  it('focuses search input when / key pressed globally', () => {
    renderWithIntl(
      <CrossEntityFilters
        {...surfaceDefaults}
        tab="all"
        gameStateFilter={emptyFilter}
        onGameStateFilterChange={noop}
        onMoreFilters={vi.fn()}
      />
    );
    const search = screen.getByPlaceholderText(/premi \//);
    fireEvent.keyDown(document.body, { key: '/' });
    expect(search).toHaveFocus();
  });

  it('does not steal focus from another input when / pressed while typing', () => {
    renderWithIntl(
      <>
        <CrossEntityFilters
          {...surfaceDefaults}
          tab="all"
          gameStateFilter={emptyFilter}
          onGameStateFilterChange={noop}
          onMoreFilters={vi.fn()}
        />
        <input data-testid="other-input" />
      </>
    );
    const other = screen.getByTestId('other-input');
    other.focus();
    fireEvent.keyDown(other, { key: '/' });
    expect(other).toHaveFocus();
    expect(screen.getByPlaceholderText(/premi \//)).not.toHaveFocus();
  });

  it('renders view toggle as radiogroup with 3 options (grid checked)', () => {
    renderWithIntl(
      <CrossEntityFilters
        {...surfaceDefaults}
        tab="all"
        gameStateFilter={emptyFilter}
        onGameStateFilterChange={noop}
        onMoreFilters={vi.fn()}
        view="grid"
      />
    );
    expect(screen.getByRole('radiogroup')).toBeInTheDocument();
    expect(screen.getByRole('radio', { name: /griglia/i })).toHaveAttribute('aria-checked', 'true');
    expect(screen.getByRole('radio', { name: /lista/i })).toHaveAttribute('aria-checked', 'false');
    expect(screen.getByRole('radio', { name: /compatta/i })).toHaveAttribute(
      'aria-checked',
      'false'
    );
  });

  it('calls onViewChange when user clicks a view radio', () => {
    const onViewChange = vi.fn();
    renderWithIntl(
      <CrossEntityFilters
        {...surfaceDefaults}
        tab="all"
        gameStateFilter={emptyFilter}
        onGameStateFilterChange={noop}
        onMoreFilters={vi.fn()}
        view="grid"
        onViewChange={onViewChange}
      />
    );
    fireEvent.click(screen.getByRole('radio', { name: /lista/i }));
    expect(onViewChange).toHaveBeenCalledWith('list');
  });

  it('search input value is bound to props.search and emits onSearchChange', () => {
    const onSearchChange = vi.fn();
    renderWithIntl(
      <CrossEntityFilters
        {...surfaceDefaults}
        tab="all"
        gameStateFilter={emptyFilter}
        onGameStateFilterChange={noop}
        onMoreFilters={vi.fn()}
        search="catan"
        onSearchChange={onSearchChange}
      />
    );
    const input = screen.getByPlaceholderText(/premi \//) as HTMLInputElement;
    expect(input.value).toBe('catan');
    fireEvent.change(input, { target: { value: 'wingspan' } });
    expect(onSearchChange).toHaveBeenCalledWith('wingspan');
  });
});
