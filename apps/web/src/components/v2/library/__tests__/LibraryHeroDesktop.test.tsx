/**
 * Wave B.3 (Issue #574) — LibraryHeroDesktop v2 component tests.
 *
 * Pure component (mirror Wave B.2 AgentsHero):
 *   labels passed via prop, no `useTranslation` internal — keeps the component
 *   provider-free and testable without IntlProvider wrap.
 *
 * Contract under test (spec §3.2):
 *   - title (h1) + subtitle from `labels`
 *   - 4 stat tiles: totalGames / kbReady / wishlist / loaned
 *   - primary CTA "Aggiungi gioco" wired to `onAddGame`
 *   - `compact` prop collapses subtitle (title remains)
 *   - root carries `data-slot="library-hero-desktop"` for spec scoping
 */

import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import {
  LibraryHeroDesktop,
  type LibraryHeroDesktopLabels,
  type LibraryHeroStat,
} from '../LibraryHeroDesktop';

const baseLabels: LibraryHeroDesktopLabels = {
  title: 'La tua libreria',
  subtitle: 'Esplora, filtra e gestisci i tuoi giochi e le knowledge base.',
  ctaAdd: 'Aggiungi gioco',
};

const baseStats: readonly LibraryHeroStat[] = [
  { key: 'totalGames', label: 'Giochi', value: 12 },
  { key: 'kbReady', label: 'KB pronti', value: 5 },
  { key: 'wishlist', label: 'Wishlist', value: 2 },
  { key: 'loaned', label: 'In prestito', value: 2 },
];

describe('LibraryHeroDesktop (Wave B.3)', () => {
  it('renders title as h1 from labels.title', () => {
    render(<LibraryHeroDesktop labels={baseLabels} stats={baseStats} />);
    expect(screen.getByRole('heading', { level: 1, name: 'La tua libreria' })).toBeInTheDocument();
  });

  it('renders subtitle from labels.subtitle by default', () => {
    render(<LibraryHeroDesktop labels={baseLabels} stats={baseStats} />);
    expect(
      screen.getByText('Esplora, filtra e gestisci i tuoi giochi e le knowledge base.')
    ).toBeInTheDocument();
  });

  it('renders all 4 stat tiles with their labels and values', () => {
    render(<LibraryHeroDesktop labels={baseLabels} stats={baseStats} />);
    expect(screen.getByText('Giochi')).toBeInTheDocument();
    expect(screen.getByText('12')).toBeInTheDocument();
    expect(screen.getByText('KB pronti')).toBeInTheDocument();
    expect(screen.getByText('5')).toBeInTheDocument();
    expect(screen.getByText('Wishlist')).toBeInTheDocument();
    expect(screen.getAllByText('2')).toHaveLength(2); // wishlist + loaned both 2
    expect(screen.getByText('In prestito')).toBeInTheDocument();
  });

  it('renders zero values explicitly (no fallback dash)', () => {
    const zeroStats: readonly LibraryHeroStat[] = [
      { key: 'totalGames', label: 'Giochi', value: 0 },
      { key: 'kbReady', label: 'KB pronti', value: 0 },
      { key: 'wishlist', label: 'Wishlist', value: 0 },
      { key: 'loaned', label: 'In prestito', value: 0 },
    ];
    const { container } = render(<LibraryHeroDesktop labels={baseLabels} stats={zeroStats} />);
    const tiles = container.querySelectorAll('[data-slot="library-hero-stat"]');
    expect(tiles).toHaveLength(4);
    const valueNodes = container.querySelectorAll('[data-slot="library-hero-stat-value"]');
    expect(valueNodes).toHaveLength(4);
    valueNodes.forEach(node => {
      expect(node.textContent?.trim()).toBe('0');
    });
  });

  it('renders the "Aggiungi gioco" CTA and calls onAddGame on click', () => {
    const onAddGame = vi.fn();
    render(<LibraryHeroDesktop labels={baseLabels} stats={baseStats} onAddGame={onAddGame} />);
    const cta = screen.getByRole('button', { name: 'Aggiungi gioco' });
    expect(cta).toBeInTheDocument();
    fireEvent.click(cta);
    expect(onAddGame).toHaveBeenCalledTimes(1);
  });

  it('omits subtitle when compact is true (mobile collapse) but keeps title', () => {
    render(<LibraryHeroDesktop labels={baseLabels} stats={baseStats} compact />);
    expect(
      screen.queryByText('Esplora, filtra e gestisci i tuoi giochi e le knowledge base.')
    ).toBeNull();
    expect(screen.getByRole('heading', { level: 1, name: 'La tua libreria' })).toBeInTheDocument();
  });

  it('exposes data-slot="library-hero-desktop" on the root for spec scoping', () => {
    const { container } = render(<LibraryHeroDesktop labels={baseLabels} stats={baseStats} />);
    expect(container.querySelector('[data-slot="library-hero-desktop"]')).not.toBeNull();
  });
});
