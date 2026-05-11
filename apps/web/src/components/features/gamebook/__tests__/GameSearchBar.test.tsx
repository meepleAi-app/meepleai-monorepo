/**
 * GameSearchBar unit tests — SP6 Phase C.1.B Task B (Issue #789).
 *
 * Coverage:
 *   - data-slot identity
 *   - search input renders + onQueryChange fires
 *   - 2 segmented tabs Catalog/BGG with role="tablist"
 *   - tabs fire onTabChange
 *   - keyboard nav (ArrowLeft/Right/Home/End) via useTablistKeyboardNav
 *   - aria-pressed + data-active on selected tab
 *   - isPending state surfaces via aria-busy / data-pending
 *   - aria-label from labels
 */

import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { GameSearchBar } from '../GameSearchBar';
import type { GameSearchBarProps } from '../GameSearchBar';

const LABELS: GameSearchBarProps['labels'] = {
  placeholder: 'Cerca un gioco…',
  tabsCatalog: 'Catalogo',
  tabsBgg: 'BGG',
  searchAria: 'Cerca un gioco da indicizzare',
};

const DEFAULT_PROPS: GameSearchBarProps = {
  query: '',
  onQueryChange: vi.fn(),
  activeTab: 'catalog',
  onTabChange: vi.fn(),
  labels: LABELS,
};

describe('GameSearchBar', () => {
  it('renders data-slot="game-search-bar"', () => {
    render(<GameSearchBar {...DEFAULT_PROPS} />);
    expect(document.querySelector('[data-slot="game-search-bar"]')).not.toBeNull();
  });

  it('renders search input with placeholder', () => {
    render(<GameSearchBar {...DEFAULT_PROPS} />);
    const input = screen.getByPlaceholderText('Cerca un gioco…');
    expect(input).toBeTruthy();
  });

  it('renders search input with aria-label from labels', () => {
    render(<GameSearchBar {...DEFAULT_PROPS} />);
    const input = screen.getByLabelText('Cerca un gioco da indicizzare');
    expect(input).toBeTruthy();
  });

  it('renders search input with type="search"', () => {
    render(<GameSearchBar {...DEFAULT_PROPS} />);
    const input = document.querySelector('input[type="search"]');
    expect(input).not.toBeNull();
  });

  it('reflects query prop in input value', () => {
    render(<GameSearchBar {...DEFAULT_PROPS} query="andor" />);
    const input = document.querySelector('input[type="search"]') as HTMLInputElement;
    expect(input.value).toBe('andor');
  });

  it('fires onQueryChange when input value changes', () => {
    const onQueryChange = vi.fn();
    render(<GameSearchBar {...DEFAULT_PROPS} onQueryChange={onQueryChange} />);
    const input = document.querySelector('input[type="search"]') as HTMLInputElement;
    fireEvent.change(input, { target: { value: 'gloomhaven' } });
    expect(onQueryChange).toHaveBeenCalledWith('gloomhaven');
  });

  it('renders 2 tabs with role="tab"', () => {
    render(<GameSearchBar {...DEFAULT_PROPS} />);
    const tabs = screen.getAllByRole('tab');
    expect(tabs).toHaveLength(2);
  });

  it('renders tablist with role="tablist"', () => {
    render(<GameSearchBar {...DEFAULT_PROPS} />);
    expect(screen.getByRole('tablist')).toBeTruthy();
  });

  it('marks active tab "catalog" with aria-selected="true"', () => {
    render(<GameSearchBar {...DEFAULT_PROPS} activeTab="catalog" />);
    const catalog = screen.getAllByRole('tab').find(t => t.textContent?.includes('Catalogo'));
    expect(catalog?.getAttribute('aria-selected')).toBe('true');
  });

  it('marks active tab "bgg" with aria-selected="true"', () => {
    render(<GameSearchBar {...DEFAULT_PROPS} activeTab="bgg" />);
    const bgg = screen.getAllByRole('tab').find(t => t.textContent?.includes('BGG'));
    expect(bgg?.getAttribute('aria-selected')).toBe('true');
  });

  it('inactive tab has aria-selected="false"', () => {
    render(<GameSearchBar {...DEFAULT_PROPS} activeTab="catalog" />);
    const bgg = screen.getAllByRole('tab').find(t => t.textContent?.includes('BGG'));
    expect(bgg?.getAttribute('aria-selected')).toBe('false');
  });

  it('exposes data-active="true" on active tab', () => {
    render(<GameSearchBar {...DEFAULT_PROPS} activeTab="bgg" />);
    const tabs = document.querySelectorAll('[data-slot="game-search-tab"]');
    const active = Array.from(tabs).find(t => t.getAttribute('data-active') === 'true');
    expect(active).not.toBeUndefined();
    expect(active?.textContent).toContain('BGG');
  });

  it('all tabs have data-slot="game-search-tab"', () => {
    render(<GameSearchBar {...DEFAULT_PROPS} />);
    const tabs = document.querySelectorAll('[data-slot="game-search-tab"]');
    expect(tabs).toHaveLength(2);
  });

  it('fires onTabChange("bgg") when BGG tab clicked from catalog', () => {
    const onTabChange = vi.fn();
    render(<GameSearchBar {...DEFAULT_PROPS} activeTab="catalog" onTabChange={onTabChange} />);
    const bgg = screen.getAllByRole('tab').find(t => t.textContent?.includes('BGG'));
    fireEvent.click(bgg!);
    expect(onTabChange).toHaveBeenCalledWith('bgg');
  });

  it('fires onTabChange("catalog") when Catalog tab clicked from bgg', () => {
    const onTabChange = vi.fn();
    render(<GameSearchBar {...DEFAULT_PROPS} activeTab="bgg" onTabChange={onTabChange} />);
    const catalog = screen.getAllByRole('tab').find(t => t.textContent?.includes('Catalogo'));
    fireEvent.click(catalog!);
    expect(onTabChange).toHaveBeenCalledWith('catalog');
  });

  it('does not render pending state by default', () => {
    render(<GameSearchBar {...DEFAULT_PROPS} />);
    const root = document.querySelector('[data-slot="game-search-bar"]');
    expect(root?.getAttribute('aria-busy')).toBeNull();
  });

  it('exposes aria-busy="true" when isPending=true', () => {
    render(<GameSearchBar {...DEFAULT_PROPS} isPending={true} />);
    const root = document.querySelector('[data-slot="game-search-bar"]');
    expect(root?.getAttribute('aria-busy')).toBe('true');
  });

  it('keyboard nav: ArrowRight from catalog moves to bgg', () => {
    const onTabChange = vi.fn();
    render(<GameSearchBar {...DEFAULT_PROPS} activeTab="catalog" onTabChange={onTabChange} />);
    const catalog = screen.getAllByRole('tab').find(t => t.textContent?.includes('Catalogo'));
    fireEvent.keyDown(catalog!, { key: 'ArrowRight' });
    expect(onTabChange).toHaveBeenCalledWith('bgg');
  });

  it('keyboard nav: ArrowLeft from bgg moves to catalog', () => {
    const onTabChange = vi.fn();
    render(<GameSearchBar {...DEFAULT_PROPS} activeTab="bgg" onTabChange={onTabChange} />);
    const bgg = screen.getAllByRole('tab').find(t => t.textContent?.includes('BGG'));
    fireEvent.keyDown(bgg!, { key: 'ArrowLeft' });
    expect(onTabChange).toHaveBeenCalledWith('catalog');
  });

  it('keyboard nav: Home from bgg moves to catalog (first)', () => {
    const onTabChange = vi.fn();
    render(<GameSearchBar {...DEFAULT_PROPS} activeTab="bgg" onTabChange={onTabChange} />);
    const bgg = screen.getAllByRole('tab').find(t => t.textContent?.includes('BGG'));
    fireEvent.keyDown(bgg!, { key: 'Home' });
    expect(onTabChange).toHaveBeenCalledWith('catalog');
  });

  it('keyboard nav: End from catalog moves to bgg (last)', () => {
    const onTabChange = vi.fn();
    render(<GameSearchBar {...DEFAULT_PROPS} activeTab="catalog" onTabChange={onTabChange} />);
    const catalog = screen.getAllByRole('tab').find(t => t.textContent?.includes('Catalogo'));
    fireEvent.keyDown(catalog!, { key: 'End' });
    expect(onTabChange).toHaveBeenCalledWith('bgg');
  });

  it('applies custom className to root', () => {
    render(<GameSearchBar {...DEFAULT_PROPS} className="extra-class" />);
    const root = document.querySelector('[data-slot="game-search-bar"]');
    expect(root?.classList.contains('extra-class')).toBe(true);
  });
});
