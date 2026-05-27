/**
 * AdvancedFiltersDrawer — skeleton tests (Phase 3a #1606).
 *
 * Tests: open/close behaviour + Cancel button.
 * No filter section assertions here — those are Tasks 4-6.
 *
 * Setup notes:
 * - IntlProvider wraps every render so useTranslation (react-intl) works.
 * - installMatchMedia(true) forces desktop (Radix Dialog) mode so
 *   getByRole('dialog') resolves synchronously; otherwise vaul renders
 *   a bottom sheet which exposes no dialog role in jsdom.
 * - The Cancel button is a plain <button> inside DrawerFooter (NOT
 *   DrawerClose) so it can call handleCancel (reset draft + close) in
 *   Task 7 without relying on Radix close semantics.
 */

import React from 'react';
import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { IntlProvider } from 'react-intl';

import { AdvancedFiltersDrawer } from '../AdvancedFiltersDrawer';
import type { LibraryFilters } from '../types';

// ── i18n messages needed by the skeleton + game scope tests ──────────────────
const messages: Record<string, string> = {
  'pages.library.filters.title': 'Più filtri',
  'pages.library.filters.description': "Filtra la libreria per dimensioni specifiche dell'entità.",
  'common.cancel': 'Annulla',
  // section labels (game scope)
  'pages.library.filters.section.state': 'Stato',
  'pages.library.filters.section.withKb': 'Solo con Knowledge Base',
  'pages.library.filters.section.rating': 'Rating minimo',
  'pages.library.filters.section.players': 'Numero di giocatori',
  'pages.library.filters.section.year': 'Anno di pubblicazione',
  // section labels (agent scope)
  'pages.library.filters.section.agentType': 'Tipo di agente',
  'pages.library.filters.section.activeOnly': 'Solo attivi',
  // section labels (session scope)
  'pages.library.filters.section.sessionStatus': 'Stato sessione',
  'pages.library.filters.section.sessionType': 'Tipo sessione',
  'pages.library.filters.section.playerCount': 'Giocatori (min)',
  // section labels (kb scope)
  'pages.library.filters.section.processingStates': 'Stato di elaborazione',
  'pages.library.filters.kbState.ready': 'Pronto',
  'pages.library.filters.kbState.pending': 'In elaborazione',
  'pages.library.filters.kbState.failed': 'Errore',
  // section labels (chat scope)
  'pages.library.filters.section.messageCountMin': 'Messaggi (min)',
  // checkbox option labels
  'pages.library.filters.state.owned': 'Posseduto',
  'pages.library.filters.state.wishlist': 'Wishlist',
  'pages.library.filters.state.loaned': 'In prestito',
};

function renderWithIntl(ui: React.ReactElement) {
  return render(
    <IntlProvider locale="it" messages={messages}>
      {ui}
    </IntlProvider>
  );
}

// ── matchMedia mock — force desktop (Radix Dialog) mode ──────────────────────
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

// ── fixture ───────────────────────────────────────────────────────────────────
const noop = () => {};
const emptyGameFilters: LibraryFilters = { scope: 'game' };

// ── tests ─────────────────────────────────────────────────────────────────────
describe('AdvancedFiltersDrawer — skeleton (open/close + Cancel)', () => {
  // Force Radix Dialog (desktop) mode so role="dialog" is available synchronously.
  beforeEach(() => {
    installMatchMedia(true);
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('does not render content when open=false', () => {
    renderWithIntl(
      <AdvancedFiltersDrawer
        open={false}
        onOpenChange={noop}
        entityScope="game"
        activeFilters={emptyGameFilters}
        onApply={noop}
        onClear={noop}
      />
    );
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('renders dialog with title when open=true', () => {
    renderWithIntl(
      <AdvancedFiltersDrawer
        open={true}
        onOpenChange={noop}
        entityScope="game"
        activeFilters={emptyGameFilters}
        onApply={noop}
        onClear={noop}
      />
    );
    expect(screen.getByRole('dialog')).toBeInTheDocument();
    expect(screen.getByText(/più filtri/i)).toBeInTheDocument();
  });

  it('Cancel button calls onOpenChange(false)', async () => {
    const onOpenChange = vi.fn();
    const user = userEvent.setup();
    renderWithIntl(
      <AdvancedFiltersDrawer
        open={true}
        onOpenChange={onOpenChange}
        entityScope="game"
        activeFilters={emptyGameFilters}
        onApply={noop}
        onClear={noop}
      />
    );
    await user.click(screen.getByRole('button', { name: /annulla/i }));
    expect(onOpenChange).toHaveBeenCalledWith(false);
  });
});

describe('AdvancedFiltersDrawer — game scope rendering', () => {
  beforeEach(() => {
    installMatchMedia(true);
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('renders 5 sections for game scope (state, withKb, rating, players, year)', () => {
    renderWithIntl(
      <AdvancedFiltersDrawer
        open={true}
        onOpenChange={noop}
        entityScope="game"
        activeFilters={{ scope: 'game' }}
        onApply={noop}
        onClear={noop}
      />
    );
    const slots = screen.getAllByTestId(/^advanced-filters-section-/);
    expect(slots).toHaveLength(5);
    expect(slots.map(el => el.getAttribute('data-slot'))).toEqual([
      'advanced-filters-section-states',
      'advanced-filters-section-withKb',
      'advanced-filters-section-rating',
      'advanced-filters-section-players',
      'advanced-filters-section-year',
    ]);
  });

  it('game.states checkbox group renders 3 options with checked state from activeFilters', () => {
    renderWithIntl(
      <AdvancedFiltersDrawer
        open={true}
        onOpenChange={noop}
        entityScope="game"
        activeFilters={{ scope: 'game', states: ['Owned', 'Wishlist'] }}
        onApply={noop}
        onClear={noop}
      />
    );
    const owned = screen.getByRole('checkbox', { name: /posseduto/i });
    const wishlist = screen.getByRole('checkbox', { name: /wishlist/i });
    const loaned = screen.getByRole('checkbox', { name: /in prestito/i });
    expect(owned).toBeChecked();
    expect(wishlist).toBeChecked();
    expect(loaned).not.toBeChecked();
  });

  it('toggling a state checkbox updates internal draft (not yet applied — Apply test in Task 7)', async () => {
    const user = userEvent.setup();
    renderWithIntl(
      <AdvancedFiltersDrawer
        open={true}
        onOpenChange={noop}
        entityScope="game"
        activeFilters={{ scope: 'game' }}
        onApply={noop}
        onClear={noop}
      />
    );
    const owned = screen.getByRole('checkbox', { name: /posseduto/i });
    await user.click(owned);
    expect(owned).toBeChecked();
  });

  it('game.withKb renders a toggle with checked state from activeFilters', () => {
    renderWithIntl(
      <AdvancedFiltersDrawer
        open={true}
        onOpenChange={noop}
        entityScope="game"
        activeFilters={{ scope: 'game', withKb: true }}
        onApply={noop}
        onClear={noop}
      />
    );
    const toggle = screen.getByRole('switch', { name: /knowledge base/i });
    expect(toggle).toBeChecked();
  });

  it('game.rating slider has correct min/max and reflects activeFilters.ratingMin', () => {
    renderWithIntl(
      <AdvancedFiltersDrawer
        open={true}
        onOpenChange={noop}
        entityScope="game"
        activeFilters={{ scope: 'game', ratingMin: 7 }}
        onApply={noop}
        onClear={noop}
      />
    );
    const slider = screen.getByRole('slider', { name: /rating/i });
    expect(slider).toHaveAttribute('aria-valuenow', '7');
    expect(slider).toHaveAttribute('aria-valuemin', '0');
    expect(slider).toHaveAttribute('aria-valuemax', '10');
  });
});

describe('AdvancedFiltersDrawer — agent scope rendering', () => {
  beforeEach(() => {
    installMatchMedia(true);
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('renders 2 sections for agent scope (types, activeOnly)', () => {
    renderWithIntl(
      <AdvancedFiltersDrawer
        open={true}
        onOpenChange={noop}
        entityScope="agent"
        activeFilters={{ scope: 'agent' }}
        onApply={noop}
        onClear={noop}
      />
    );
    const slots = screen.getAllByTestId(/^advanced-filters-section-/);
    expect(slots).toHaveLength(2);
    expect(slots.map(el => el.getAttribute('data-slot'))).toEqual([
      'advanced-filters-section-types',
      'advanced-filters-section-activeOnly',
    ]);
  });

  it('agent.activeOnly toggle reflects activeFilters and updates draft on click', async () => {
    const user = userEvent.setup();
    renderWithIntl(
      <AdvancedFiltersDrawer
        open={true}
        onOpenChange={noop}
        entityScope="agent"
        activeFilters={{ scope: 'agent', activeOnly: false }}
        onApply={noop}
        onClear={noop}
      />
    );
    const toggle = screen.getByRole('switch', { name: /solo attivi/i });
    expect(toggle).toHaveAttribute('aria-checked', 'false');
    await user.click(toggle);
    expect(toggle).toHaveAttribute('aria-checked', 'true');
  });
});

describe('AdvancedFiltersDrawer — session scope rendering', () => {
  beforeEach(() => {
    installMatchMedia(true);
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('renders 3 sections for session scope (statuses, sessionTypes, playerCount)', () => {
    renderWithIntl(
      <AdvancedFiltersDrawer
        open={true}
        onOpenChange={noop}
        entityScope="session"
        activeFilters={{ scope: 'session' }}
        onApply={noop}
        onClear={noop}
      />
    );
    const slots = screen.getAllByTestId(/^advanced-filters-section-/);
    expect(slots).toHaveLength(3);
    expect(slots.map(el => el.getAttribute('data-slot'))).toEqual([
      'advanced-filters-section-statuses',
      'advanced-filters-section-sessionTypes',
      'advanced-filters-section-playerCount',
    ]);
  });

  it('session.playerCount slider has min=1 max=12 and reflects activeFilters.playerCountMin', () => {
    renderWithIntl(
      <AdvancedFiltersDrawer
        open={true}
        onOpenChange={noop}
        entityScope="session"
        activeFilters={{ scope: 'session', playerCountMin: 4 }}
        onApply={noop}
        onClear={noop}
      />
    );
    const slider = screen.getByRole('slider', { name: /giocatori/i });
    expect(slider).toHaveAttribute('aria-valuemin', '1');
    expect(slider).toHaveAttribute('aria-valuemax', '12');
    expect(slider).toHaveAttribute('aria-valuenow', '4');
  });
});

describe('AdvancedFiltersDrawer — kb scope rendering', () => {
  beforeEach(() => {
    installMatchMedia(true);
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('renders 1 section (processingStates) with 3 checkboxes (Ready/Pending/Failed)', () => {
    renderWithIntl(
      <AdvancedFiltersDrawer
        open={true}
        onOpenChange={noop}
        entityScope="kb"
        activeFilters={{ scope: 'kb', processingStates: ['Ready'] }}
        onApply={noop}
        onClear={noop}
      />
    );
    const slots = screen.getAllByTestId(/^advanced-filters-section-/);
    expect(slots).toHaveLength(1);
    expect(slots[0]?.getAttribute('data-slot')).toBe('advanced-filters-section-processingStates');

    expect(screen.getByRole('checkbox', { name: /pronto/i })).toBeChecked();
    expect(screen.getByRole('checkbox', { name: /in elaborazione/i })).not.toBeChecked();
    expect(screen.getByRole('checkbox', { name: /errore/i })).not.toBeChecked();
  });
});

describe('AdvancedFiltersDrawer — chat scope rendering', () => {
  beforeEach(() => {
    installMatchMedia(true);
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('renders 1 section (messageCountMin slider) with min=0 max=100', () => {
    renderWithIntl(
      <AdvancedFiltersDrawer
        open={true}
        onOpenChange={noop}
        entityScope="chat"
        activeFilters={{ scope: 'chat', messageCountMin: 10 }}
        onApply={noop}
        onClear={noop}
      />
    );
    const slots = screen.getAllByTestId(/^advanced-filters-section-/);
    expect(slots).toHaveLength(1);
    const slider = screen.getByRole('slider', { name: /messaggi/i });
    expect(slider).toHaveAttribute('aria-valuemin', '0');
    expect(slider).toHaveAttribute('aria-valuemax', '100');
    expect(slider).toHaveAttribute('aria-valuenow', '10');
  });
});
