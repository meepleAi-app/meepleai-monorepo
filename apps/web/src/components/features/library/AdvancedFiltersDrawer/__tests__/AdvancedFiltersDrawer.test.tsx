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

// ── i18n messages needed by the skeleton ─────────────────────────────────────
const messages: Record<string, string> = {
  'pages.library.filters.title': 'Più filtri',
  'pages.library.filters.description': "Filtra la libreria per dimensioni specifiche dell'entità.",
  'common.cancel': 'Annulla',
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
