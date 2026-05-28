/**
 * Issue #1483 — DiscoverHero unit tests.
 *
 * DiscoverHero is a pure composition component: title + optional subtitle +
 * optional searchSlot + optional filterSlot rendered inside a gradient header.
 * Labels are passed as props — no useTranslation used internally.
 *
 * Contract:
 *   - Always renders data-slot="discover-hero" on root header element
 *   - Always renders the title in an h1
 *   - subtitle is optional — rendered as a <p> when provided, absent otherwise
 *   - searchSlot wrapped in data-slot="discover-hero-search" when provided
 *   - filterSlot wrapped in data-slot="discover-hero-filters" when provided
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { DiscoverHero } from '../DiscoverHero';

describe('DiscoverHero', () => {
  it('renders data-slot="discover-hero" on the root header', () => {
    const { container } = render(<DiscoverHero title="Scopri" />);
    const root = container.querySelector('[data-slot="discover-hero"]');
    expect(root).not.toBeNull();
    expect(root?.tagName.toLowerCase()).toBe('header');
  });

  it('renders the title in an h1', () => {
    render(<DiscoverHero title="Esplora il catalogo" />);
    expect(
      screen.getByRole('heading', { level: 1, name: 'Esplora il catalogo' })
    ).toBeInTheDocument();
  });

  it('renders subtitle when provided', () => {
    render(<DiscoverHero title="Scopri" subtitle="Trova giochi, agenti e altro ancora." />);
    expect(screen.getByText('Trova giochi, agenti e altro ancora.')).toBeInTheDocument();
  });

  it('does not render subtitle when omitted', () => {
    render(<DiscoverHero title="Scopri" />);
    expect(screen.queryByRole('paragraph')).toBeNull();
  });

  it('renders searchSlot inside data-slot="discover-hero-search" when provided', () => {
    const { container } = render(
      <DiscoverHero title="Scopri" searchSlot={<input data-testid="search-box" />} />
    );
    const searchWrapper = container.querySelector('[data-slot="discover-hero-search"]');
    expect(searchWrapper).not.toBeNull();
    expect(searchWrapper?.querySelector('[data-testid="search-box"]')).not.toBeNull();
  });

  it('does not render discover-hero-search slot when searchSlot is omitted', () => {
    const { container } = render(<DiscoverHero title="Scopri" />);
    expect(container.querySelector('[data-slot="discover-hero-search"]')).toBeNull();
  });

  it('renders filterSlot inside data-slot="discover-hero-filters" when provided', () => {
    const { container } = render(
      <DiscoverHero title="Scopri" filterSlot={<div data-testid="filter-bar" />} />
    );
    const filterWrapper = container.querySelector('[data-slot="discover-hero-filters"]');
    expect(filterWrapper).not.toBeNull();
    expect(filterWrapper?.querySelector('[data-testid="filter-bar"]')).not.toBeNull();
  });

  it('does not render discover-hero-filters slot when filterSlot is omitted', () => {
    const { container } = render(<DiscoverHero title="Scopri" />);
    expect(container.querySelector('[data-slot="discover-hero-filters"]')).toBeNull();
  });

  it('renders both slots when both are provided', () => {
    const { container } = render(
      <DiscoverHero
        title="Scopri"
        subtitle="Sottotitolo"
        searchSlot={<span>search</span>}
        filterSlot={<span>filters</span>}
      />
    );
    expect(container.querySelector('[data-slot="discover-hero-search"]')).not.toBeNull();
    expect(container.querySelector('[data-slot="discover-hero-filters"]')).not.toBeNull();
    expect(screen.getByText('Sottotitolo')).toBeInTheDocument();
  });
});
