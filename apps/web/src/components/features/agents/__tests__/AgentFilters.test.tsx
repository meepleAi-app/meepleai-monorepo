/**
 * Wave B.2 (Issue #634) — AgentFilters v2 component tests.
 *
 * Pure component (mirror Wave B.1): labels via prop, no `useTranslation`.
 *
 * Contract under test (spec §3.2 + plan §4.2):
 *   - Search: type=search, sr-only label, 300ms debounce trailing-edge
 *   - Status segmented: WAI-ARIA tablist (role="tablist" + role="tab" +
 *     aria-selected). Roving tabindex via `useTablistKeyboardNav`. Arrow
 *     Left/Right wrap, Home/End jump.
 *   - Sort: native <select> with <label>
 *   - Result count rendered
 *   - NO view toggle (B.2 spec drops the grid/list switcher per spec-panel
 *     resolution B-2)
 */

import { act, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { AgentFilters, type AgentFiltersLabels, type AgentFiltersProps } from '../AgentFilters';

const labels: AgentFiltersLabels = {
  search: {
    placeholder: 'Cerca per nome, tipo o strategia…',
    ariaLabel: 'Cerca tra gli agenti',
    clearAriaLabel: 'Cancella la ricerca',
  },
  status: {
    label: 'Stato',
    options: {
      all: 'Tutti',
      attivo: 'Attivi',
      'in-setup': 'In setup',
      archiviato: 'Archiviati',
    },
  },
  sort: {
    label: 'Ordina per',
    options: {
      recent: 'Più recenti',
      alpha: 'A-Z',
      used: 'Più usati',
    },
  },
  resultCount: (count: number) => `${count} agenti`,
};

function renderFilters(overrides: Partial<AgentFiltersProps> = {}) {
  const props: AgentFiltersProps = {
    labels,
    query: '',
    onQueryChange: vi.fn(),
    status: 'all',
    onStatusChange: vi.fn(),
    sort: 'recent',
    onSortChange: vi.fn(),
    resultCount: 6,
    compact: false,
    ...overrides,
  };
  return { ...render(<AgentFilters {...props} />), props };
}

describe('AgentFilters (Wave B.2)', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });
  afterEach(() => {
    vi.useRealTimers();
  });

  it('renders search input as type=search with placeholder and sr-only label', () => {
    renderFilters();
    const input = screen.getByPlaceholderText(
      'Cerca per nome, tipo o strategia…'
    ) as HTMLInputElement;
    expect(input).toBeInTheDocument();
    expect(input.type).toBe('search');
    expect(screen.getByLabelText('Cerca tra gli agenti')).toBeInTheDocument();
  });

  it('debounces onQueryChange by 300ms (single trailing-edge call)', () => {
    const onQueryChange = vi.fn();
    renderFilters({ onQueryChange });
    const input = screen.getByLabelText('Cerca tra gli agenti') as HTMLInputElement;

    fireEvent.change(input, { target: { value: 'c' } });
    fireEvent.change(input, { target: { value: 'ca' } });
    fireEvent.change(input, { target: { value: 'cat' } });

    act(() => vi.advanceTimersByTime(299));
    expect(onQueryChange).not.toHaveBeenCalled();

    act(() => vi.advanceTimersByTime(1));
    expect(onQueryChange).toHaveBeenCalledTimes(1);
    expect(onQueryChange).toHaveBeenCalledWith('cat');
  });

  it('renders the status tablist with 4 tabs and aria-selected on the current key', () => {
    renderFilters({ status: 'attivo' });
    const tablist = screen.getByRole('tablist', { name: 'Stato' });
    expect(tablist).toBeInTheDocument();
    const tabs = screen.getAllByRole('tab');
    expect(tabs).toHaveLength(4);
    const attivo = screen.getByRole('tab', { name: 'Attivi' });
    expect(attivo).toHaveAttribute('aria-selected', 'true');
    const inSetup = screen.getByRole('tab', { name: 'In setup' });
    expect(inSetup).toHaveAttribute('aria-selected', 'false');
  });

  it('fires onStatusChange when a status tab is clicked', () => {
    const onStatusChange = vi.fn();
    renderFilters({ onStatusChange });
    fireEvent.click(screen.getByRole('tab', { name: 'In setup' }));
    expect(onStatusChange).toHaveBeenCalledTimes(1);
    expect(onStatusChange).toHaveBeenCalledWith('in-setup');
  });

  it('navigates status tabs with ArrowRight (wraps last → first)', () => {
    const onStatusChange = vi.fn();
    renderFilters({ status: 'archiviato', onStatusChange });
    const archTab = screen.getByRole('tab', { name: 'Archiviati' });
    archTab.focus();
    fireEvent.keyDown(archTab, { key: 'ArrowRight' });
    expect(onStatusChange).toHaveBeenCalledWith('all');
  });

  it('navigates status tabs with ArrowLeft (wraps first → last)', () => {
    const onStatusChange = vi.fn();
    renderFilters({ status: 'all', onStatusChange });
    const allTab = screen.getByRole('tab', { name: 'Tutti' });
    allTab.focus();
    fireEvent.keyDown(allTab, { key: 'ArrowLeft' });
    expect(onStatusChange).toHaveBeenCalledWith('archiviato');
  });

  it('jumps to first/last with Home/End on the status tablist', () => {
    const onStatusChange = vi.fn();
    renderFilters({ status: 'in-setup', onStatusChange });
    const inSetupTab = screen.getByRole('tab', { name: 'In setup' });
    inSetupTab.focus();
    fireEvent.keyDown(inSetupTab, { key: 'Home' });
    expect(onStatusChange).toHaveBeenLastCalledWith('all');
    fireEvent.keyDown(inSetupTab, { key: 'End' });
    expect(onStatusChange).toHaveBeenLastCalledWith('archiviato');
  });

  it('renders the sort <select> with 3 options and fires onSortChange', () => {
    const onSortChange = vi.fn();
    renderFilters({ onSortChange });
    const select = screen.getByLabelText('Ordina per') as HTMLSelectElement;
    expect(select.tagName).toBe('SELECT');
    expect(select.options).toHaveLength(3);
    fireEvent.change(select, { target: { value: 'used' } });
    expect(onSortChange).toHaveBeenCalledWith('used');
  });

  it('does NOT render a view toggle group (B.2 drops grid/list switcher)', () => {
    renderFilters();
    expect(screen.queryByRole('group', { name: /visual/i })).toBeNull();
  });

  it('renders the result count via labels.resultCount(n)', () => {
    renderFilters({ resultCount: 7 });
    expect(screen.getByText('7 agenti')).toBeInTheDocument();
  });

  it('exposes a clear-search button when query is non-empty (calls onQueryChange("") immediately)', () => {
    const onQueryChange = vi.fn();
    renderFilters({ query: 'catan', onQueryChange });
    const clearBtn = screen.getByRole('button', { name: 'Cancella la ricerca' });
    fireEvent.click(clearBtn);
    expect(onQueryChange).toHaveBeenCalledWith('');
  });

  it('does not render the clear-search button when query is empty', () => {
    renderFilters({ query: '' });
    expect(screen.queryByRole('button', { name: 'Cancella la ricerca' })).toBeNull();
  });

  it('exposes data-slot="agents-filters" on the root for spec scoping', () => {
    const { container } = renderFilters();
    expect(container.querySelector('[data-slot="agents-filters"]')).not.toBeNull();
  });
});
