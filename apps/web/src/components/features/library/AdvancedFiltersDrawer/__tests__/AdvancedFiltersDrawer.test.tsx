/**
 * AdvancedFiltersDrawer — cross-entity hub-level tests.
 *
 * SP4 mockup conformance (Issue #1585-followup, plan Task 3.3). Drawer is
 * no longer scope-conditional; one static 7-section descriptor drives every
 * tab. Tests assert open/close lifecycle, section rendering, kind-specific
 * interaction patterns, count subtitle / footer wiring, and a11y basics.
 *
 * Setup notes:
 * - IntlProvider wraps every render so useTranslation (react-intl) works.
 * - installMatchMedia(true) forces desktop (Radix Dialog) mode so
 *   getByRole('dialog') resolves synchronously; otherwise vaul renders
 *   a bottom sheet which exposes no dialog role in jsdom.
 */

import React from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { IntlProvider } from 'react-intl';

import { AdvancedFiltersDrawer } from '../AdvancedFiltersDrawer';
import { countActiveFilters, type LibraryFilters } from '../types';

// ── i18n messages used by the drawer (cross-entity) ─────────────────────────
const messages: Record<string, string> = {
  // header
  'pages.library.filters.title': 'Filtri avanzati',
  'pages.library.filters.description': 'Filtra la libreria per dimensioni cross-entity.',
  'pages.library.filters.closeAriaLabel': 'Chiudi pannello filtri',
  'pages.library.filters.header.subtitle':
    '{count, plural, =0 {Nessun filtro · scope: library} =1 {1 attivo · scope: library} other {# attivi · scope: library}}',
  // footer
  'pages.library.filters.apply': 'Applica',
  'pages.library.filters.applyWithCount': 'Applica ({count})',
  'pages.library.filters.reset': 'Reset',
  // section: status
  'pages.library.filters.section.status.title': 'Stato',
  'pages.library.filters.section.status.options.owned': 'Posseduto',
  'pages.library.filters.section.status.options.wishlist': 'Wishlist',
  'pages.library.filters.section.status.options.setup': 'In setup',
  'pages.library.filters.section.status.options.archived': 'Archiviato',
  // section: entity
  'pages.library.filters.section.entity.title': 'Tipo entità',
  'pages.library.filters.section.entity.options.game': 'Giochi',
  'pages.library.filters.section.entity.options.agent': 'Agenti',
  'pages.library.filters.section.entity.options.kb': 'Documenti KB',
  'pages.library.filters.section.entity.options.session': 'Sessioni',
  'pages.library.filters.section.entity.options.chat': 'Chat',
  // section: game (select-multi)
  'pages.library.filters.section.game.title': 'Gioco',
  'pages.library.filters.section.game.placeholder': 'Filtra per gioco specifico...',
  'pages.library.filters.section.game.empty': 'Nessun gioco disponibile.',
  // section: period
  'pages.library.filters.section.period.title': 'Periodo',
  'pages.library.filters.section.period.options.7d': 'Ultimi 7 giorni',
  'pages.library.filters.section.period.options.30d': 'Ultimi 30 giorni',
  'pages.library.filters.section.period.options.1y': 'Ultimo anno',
  'pages.library.filters.section.period.options.all': 'Sempre',
  'pages.library.filters.section.period.options.range': 'Range personalizzato',
  // section: tags
  'pages.library.filters.section.tags.title': 'Tag',
  'pages.library.filters.section.tags.options.family': 'Family',
  'pages.library.filters.section.tags.options.strategy': 'Strategy',
  'pages.library.filters.section.tags.options.coop': 'Coop',
  'pages.library.filters.section.tags.options.engine': 'Engine builder',
  'pages.library.filters.section.tags.options.auction': 'Auction',
  'pages.library.filters.section.tags.options.rollAndWrite': 'Roll & Write',
  'pages.library.filters.section.tags.options.cardDriven': 'Card driven',
  'pages.library.filters.section.tags.options.tableau': 'Tableau',
  // section: rating
  'pages.library.filters.section.rating.title': 'Rating',
  'pages.library.filters.section.rating.minAriaLabel': 'Rating minimo',
  'pages.library.filters.section.rating.maxAriaLabel': 'Rating massimo',
  // section: weight
  'pages.library.filters.section.weight.title': 'Complessità',
  'pages.library.filters.section.weight.options.light': 'Light',
  'pages.library.filters.section.weight.options.medium': 'Medium',
  'pages.library.filters.section.weight.options.heavy': 'Heavy',
  'pages.library.filters.section.weight.options.extra': 'Extra heavy',
};

function renderWithIntl(ui: React.ReactElement) {
  return render(
    <IntlProvider locale="it" messages={messages}>
      {ui}
    </IntlProvider>
  );
}

// ── matchMedia mock — force desktop (Radix Dialog) mode ─────────────────────
function installMatchMedia(matches: boolean) {
  const listeners = new Set<(e: MediaQueryListEvent) => void>();
  const mql = {
    matches,
    media: '(min-width: 768px)',
    addEventListener: (_e: string, cb: (e: MediaQueryListEvent) => void) => listeners.add(cb),
    removeEventListener: (_e: string, cb: (e: MediaQueryListEvent) => void) => listeners.delete(cb),
    onchange: null,
  };
  Object.defineProperty(window, 'matchMedia', {
    configurable: true,
    writable: true,
    value: vi.fn().mockReturnValue(mql),
  });
}

const noop = () => {};
const emptyFilters: LibraryFilters = {};

// ── tests ──────────────────────────────────────────────────────────────────
describe('AdvancedFiltersDrawer — open/close lifecycle', () => {
  beforeEach(() => installMatchMedia(true));
  afterEach(() => vi.restoreAllMocks());

  it('does not render dialog when open=false', () => {
    renderWithIntl(
      <AdvancedFiltersDrawer
        open={false}
        onOpenChange={noop}
        activeFilters={emptyFilters}
        onApply={noop}
        onClear={noop}
      />
    );
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('renders dialog with custom header when open=true', () => {
    renderWithIntl(
      <AdvancedFiltersDrawer
        open={true}
        onOpenChange={noop}
        activeFilters={emptyFilters}
        onApply={noop}
        onClear={noop}
      />
    );
    expect(screen.getByRole('dialog')).toBeInTheDocument();
    // The drawer renders the title twice: once in the visible header (font-display)
    // and once in the Radix sr-only DrawerTitle (a11y). Pin to the visible
    // occurrence by scoping to the header slot. Radix renders the dialog
    // content in a portal, so query `document.body` not the render container.
    const header = document.body.querySelector('[data-slot="advanced-filters-header"]');
    expect(header).not.toBeNull();
    expect(within(header as HTMLElement).getByText(/Filtri avanzati/)).toBeInTheDocument();
  });

  it('header subtitle shows zero-state message when no filters active', () => {
    renderWithIntl(
      <AdvancedFiltersDrawer
        open={true}
        onOpenChange={noop}
        activeFilters={emptyFilters}
        onApply={noop}
        onClear={noop}
      />
    );
    expect(screen.getByText(/Nessun filtro · scope: library/)).toBeInTheDocument();
  });

  it('header subtitle uses ICU plural for activeFilters count', () => {
    renderWithIntl(
      <AdvancedFiltersDrawer
        open={true}
        onOpenChange={noop}
        activeFilters={{ statuses: ['owned', 'wishlist'] }}
        onApply={noop}
        onClear={noop}
      />
    );
    expect(screen.getByText(/2 attivi · scope: library/)).toBeInTheDocument();
  });

  it('Close (✕) button calls onOpenChange(false)', async () => {
    const onOpenChange = vi.fn();
    const user = userEvent.setup();
    renderWithIntl(
      <AdvancedFiltersDrawer
        open={true}
        onOpenChange={onOpenChange}
        activeFilters={emptyFilters}
        onApply={noop}
        onClear={noop}
      />
    );
    await user.click(screen.getByRole('button', { name: 'Chiudi pannello filtri' }));
    expect(onOpenChange).toHaveBeenCalledWith(false);
  });
});

describe('AdvancedFiltersDrawer — 7-section rendering', () => {
  beforeEach(() => installMatchMedia(true));
  afterEach(() => vi.restoreAllMocks());

  it('renders all 7 sections in mockup order', () => {
    renderWithIntl(
      <AdvancedFiltersDrawer
        open={true}
        onOpenChange={noop}
        activeFilters={emptyFilters}
        onApply={noop}
        onClear={noop}
      />
    );
    const slots = screen.getAllByTestId(/^advanced-filters-section-/);
    // Issue #2186: 'entities' removed (handled by LibraryTabs entity scope).
    expect(slots.map(el => el.getAttribute('data-slot'))).toEqual([
      'advanced-filters-section-statuses',
      'advanced-filters-section-games',
      'advanced-filters-section-period',
      'advanced-filters-section-tags',
      'advanced-filters-section-rating',
      'advanced-filters-section-weights',
    ]);
  });

  it('does not render the legacy "entities" section (#2186)', () => {
    renderWithIntl(
      <AdvancedFiltersDrawer
        open={true}
        onOpenChange={noop}
        activeFilters={emptyFilters}
        onApply={noop}
        onClear={noop}
      />
    );
    expect(screen.queryByTestId('advanced-filters-section-entities')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Tipo entità/ })).not.toBeInTheDocument();
  });

  it('opens the first 3 sections by default (Stato / Gioco / Periodo after #2186)', () => {
    renderWithIntl(
      <AdvancedFiltersDrawer
        open={true}
        onOpenChange={noop}
        activeFilters={emptyFilters}
        onApply={noop}
        onClear={noop}
      />
    );
    expect(screen.getByRole('button', { name: /Stato/, expanded: true })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Gioco/, expanded: true })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Periodo/, expanded: true })).toBeInTheDocument();
  });

  it('keeps sections at index >= 3 collapsed by default (Tags / Rating / Complessità)', () => {
    renderWithIntl(
      <AdvancedFiltersDrawer
        open={true}
        onOpenChange={noop}
        activeFilters={emptyFilters}
        onApply={noop}
        onClear={noop}
      />
    );
    expect(screen.getByRole('button', { name: /Tag/, expanded: false })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Rating/, expanded: false })).toBeInTheDocument();
    expect(
      screen.getByRole('button', { name: /Complessità/, expanded: false })
    ).toBeInTheDocument();
  });

  it('expands a collapsed section when the toggle is clicked', async () => {
    const user = userEvent.setup();
    renderWithIntl(
      <AdvancedFiltersDrawer
        open={true}
        onOpenChange={noop}
        activeFilters={emptyFilters}
        onApply={noop}
        onClear={noop}
      />
    );
    // After #2186 'Periodo' is in the first 3 (default-open). Use a section
    // from the collapsed tail (Tag is index 3) to exercise the expand toggle.
    const tagToggle = screen.getByRole('button', { name: /Tag/, expanded: false });
    await user.click(tagToggle);
    expect(screen.getByRole('button', { name: /Tag/, expanded: true })).toBeInTheDocument();
  });
});

describe('AdvancedFiltersDrawer — chips-multi (status section)', () => {
  beforeEach(() => installMatchMedia(true));
  afterEach(() => vi.restoreAllMocks());

  it('renders 4 status chips (owned/wishlist/setup/archived)', () => {
    renderWithIntl(
      <AdvancedFiltersDrawer
        open={true}
        onOpenChange={noop}
        activeFilters={emptyFilters}
        onApply={noop}
        onClear={noop}
      />
    );
    const statusSection = screen.getByTestId('advanced-filters-section-statuses');
    expect(within(statusSection).getByRole('button', { name: /Posseduto/ })).toBeInTheDocument();
    expect(within(statusSection).getByRole('button', { name: /Wishlist/ })).toBeInTheDocument();
    expect(within(statusSection).getByRole('button', { name: /In setup/ })).toBeInTheDocument();
    expect(within(statusSection).getByRole('button', { name: /Archiviato/ })).toBeInTheDocument();
  });

  it('reflects activeFilters.statuses via aria-pressed', () => {
    renderWithIntl(
      <AdvancedFiltersDrawer
        open={true}
        onOpenChange={noop}
        activeFilters={{ statuses: ['owned'] }}
        onApply={noop}
        onClear={noop}
      />
    );
    const statusSection = screen.getByTestId('advanced-filters-section-statuses');
    const ownedChip = within(statusSection).getByRole('button', { name: /Posseduto/ });
    const wishlistChip = within(statusSection).getByRole('button', { name: /Wishlist/ });
    expect(ownedChip).toHaveAttribute('aria-pressed', 'true');
    expect(wishlistChip).toHaveAttribute('aria-pressed', 'false');
  });

  it('toggling a chip then clicking Applica emits the updated draft', async () => {
    const onApply = vi.fn();
    const user = userEvent.setup();
    renderWithIntl(
      <AdvancedFiltersDrawer
        open={true}
        onOpenChange={noop}
        activeFilters={emptyFilters}
        onApply={onApply}
        onClear={noop}
      />
    );
    const statusSection = screen.getByTestId('advanced-filters-section-statuses');
    await user.click(within(statusSection).getByRole('button', { name: /Posseduto/ }));
    await user.click(screen.getByRole('button', { name: /^Applica/ }));
    expect(onApply).toHaveBeenCalledWith({ statuses: ['owned'] });
  });
});

describe('AdvancedFiltersDrawer — select-multi (games section)', () => {
  beforeEach(() => installMatchMedia(true));
  afterEach(() => vi.restoreAllMocks());

  it('shows the placeholder and an empty-state when availableGames omitted', () => {
    renderWithIntl(
      <AdvancedFiltersDrawer
        open={true}
        onOpenChange={noop}
        activeFilters={emptyFilters}
        onApply={noop}
        onClear={noop}
      />
    );
    const gamesSection = screen.getByTestId('advanced-filters-section-games');
    expect(within(gamesSection).getByText('Filtra per gioco specifico...')).toBeInTheDocument();
    expect(within(gamesSection).getByText('Nessun gioco disponibile.')).toBeInTheDocument();
  });

  it('renders up to 5 available games as togglable pills', () => {
    renderWithIntl(
      <AdvancedFiltersDrawer
        open={true}
        onOpenChange={noop}
        activeFilters={emptyFilters}
        availableGames={[
          { id: 'g1', title: 'Catan' },
          { id: 'g2', title: 'Wingspan' },
        ]}
        onApply={noop}
        onClear={noop}
      />
    );
    const gamesSection = screen.getByTestId('advanced-filters-section-games');
    expect(within(gamesSection).getByRole('button', { name: /Catan/ })).toBeInTheDocument();
    expect(within(gamesSection).getByRole('button', { name: /Wingspan/ })).toBeInTheDocument();
  });
});

describe('AdvancedFiltersDrawer — period-quick (period section)', () => {
  beforeEach(() => installMatchMedia(true));
  afterEach(() => vi.restoreAllMocks());

  it('renders period section as a radiogroup with 5 options', async () => {
    const user = userEvent.setup();
    renderWithIntl(
      <AdvancedFiltersDrawer
        open={true}
        onOpenChange={noop}
        activeFilters={emptyFilters}
        onApply={noop}
        onClear={noop}
      />
    );
    // Period is now in the first 3 default-open sections (#2186 — entities
    // was removed so Period moved from index 3 → index 2).
    const radiogroup = screen.getByRole('radiogroup', { name: /Periodo/ });
    const options = within(radiogroup).getAllByRole('radio');
    expect(options).toHaveLength(5);
  });

  it('selects a period option (aria-checked) and emits on Applica', async () => {
    const onApply = vi.fn();
    const user = userEvent.setup();
    renderWithIntl(
      <AdvancedFiltersDrawer
        open={true}
        onOpenChange={noop}
        activeFilters={emptyFilters}
        onApply={onApply}
        onClear={noop}
      />
    );
    await user.click(screen.getByRole('radio', { name: /Ultimi 30 giorni/ }));
    await user.click(screen.getByRole('button', { name: /^Applica/ }));
    expect(onApply).toHaveBeenCalledWith({ period: '30d' });
  });
});

describe('AdvancedFiltersDrawer — range (rating section)', () => {
  beforeEach(() => installMatchMedia(true));
  afterEach(() => vi.restoreAllMocks());

  it('renders rating section with default readout 6.0 — 10.0', async () => {
    const user = userEvent.setup();
    renderWithIntl(
      <AdvancedFiltersDrawer
        open={true}
        onOpenChange={noop}
        activeFilters={emptyFilters}
        onApply={noop}
        onClear={noop}
      />
    );
    await user.click(screen.getByRole('button', { name: /Rating/ }));
    const readout = screen.getByTestId('advanced-filters-section-rating');
    expect(within(readout).getByText('6.0')).toBeInTheDocument();
    expect(within(readout).getByText('10.0')).toBeInTheDocument();
  });

  it('renders min and max range inputs with aria-labels', async () => {
    const user = userEvent.setup();
    renderWithIntl(
      <AdvancedFiltersDrawer
        open={true}
        onOpenChange={noop}
        activeFilters={emptyFilters}
        onApply={noop}
        onClear={noop}
      />
    );
    await user.click(screen.getByRole('button', { name: /Rating/ }));
    expect(screen.getByLabelText('Rating minimo')).toBeInTheDocument();
    expect(screen.getByLabelText('Rating massimo')).toBeInTheDocument();
  });
});

describe('AdvancedFiltersDrawer — footer actions', () => {
  beforeEach(() => installMatchMedia(true));
  afterEach(() => vi.restoreAllMocks());

  it('Reset button calls onClear and clears local draft', async () => {
    const onClear = vi.fn();
    const onApply = vi.fn();
    const user = userEvent.setup();
    renderWithIntl(
      <AdvancedFiltersDrawer
        open={true}
        onOpenChange={noop}
        activeFilters={{ statuses: ['owned', 'wishlist'] }}
        onApply={onApply}
        onClear={onClear}
      />
    );
    await user.click(screen.getByRole('button', { name: 'Reset' }));
    expect(onClear).toHaveBeenCalledTimes(1);
    // After Reset, click Applica should emit an empty draft.
    await user.click(screen.getByRole('button', { name: /^Applica/ }));
    expect(onApply).toHaveBeenCalledWith({});
  });

  it('Applica button shows count suffix when filters are present', () => {
    renderWithIntl(
      <AdvancedFiltersDrawer
        open={true}
        onOpenChange={noop}
        activeFilters={{ statuses: ['owned', 'wishlist'] }}
        onApply={noop}
        onClear={noop}
      />
    );
    expect(screen.getByRole('button', { name: 'Applica (2)' })).toBeInTheDocument();
  });

  it('Applica button shows no count when no filters are active', () => {
    renderWithIntl(
      <AdvancedFiltersDrawer
        open={true}
        onOpenChange={noop}
        activeFilters={emptyFilters}
        onApply={noop}
        onClear={noop}
      />
    );
    expect(screen.getByRole('button', { name: 'Applica' })).toBeInTheDocument();
  });

  it('Applica button calls onApply with draft and onOpenChange(false)', async () => {
    const onApply = vi.fn();
    const onOpenChange = vi.fn();
    const user = userEvent.setup();
    renderWithIntl(
      <AdvancedFiltersDrawer
        open={true}
        onOpenChange={onOpenChange}
        activeFilters={{ statuses: ['owned'] }}
        onApply={onApply}
        onClear={noop}
      />
    );
    await user.click(screen.getByRole('button', { name: /^Applica/ }));
    expect(onApply).toHaveBeenCalledWith({ statuses: ['owned'] });
    expect(onOpenChange).toHaveBeenCalledWith(false);
  });
});

describe('countActiveFilters', () => {
  it('returns 0 for empty filters', () => {
    expect(countActiveFilters({})).toBe(0);
  });

  it('counts array values by length (not by field count)', () => {
    expect(countActiveFilters({ statuses: ['owned', 'wishlist'] })).toBe(2);
  });

  it('counts scalar truthy values as 1', () => {
    expect(countActiveFilters({ period: '7d' })).toBe(1);
  });

  it('counts range fields ratingMin/ratingMax independently', () => {
    expect(countActiveFilters({ ratingMin: 6, ratingMax: 10 })).toBe(2);
  });

  it('ignores undefined fields', () => {
    expect(countActiveFilters({ statuses: undefined, period: undefined })).toBe(0);
  });
});
