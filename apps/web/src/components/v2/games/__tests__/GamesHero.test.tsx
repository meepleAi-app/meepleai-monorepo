/**
 * Wave B.1 (Issue #633) — GamesHero v2 component tests.
 *
 * Pure component pattern (mirror Wave A.4 `shared-game-detail/hero.tsx`):
 *   labels passed via prop, no `useTranslation` internal — keeps the component
 *   provider-free and testable without IntlProvider wrap.
 *
 * Contract under test (spec §3.2 + plan §4.1):
 *   - h1 title + subtitle from `labels`
 *   - exactly 4 stat tiles ({owned, wishlist, totalEntries, kbDocs} per JSON
 *     schema — naming aligned to committed i18n source of truth, not spec §3.5
 *     which still references the older `{total, totalPlays, ...}` draft)
 *   - primary CTA "Aggiungi gioco" wired to `onAddGame`
 *   - `compact` prop collapses subtitle (mobile breakpoint visual delta)
 *   - root carries `data-slot="games-library-hero"` for spec scoping
 */

import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { GamesHero, type GamesHeroLabels, type GamesHeroStat } from '../GamesHero';

const baseLabels: GamesHeroLabels = {
  title: 'La tua libreria',
  subtitle: "Tutti i tuoi giochi, organizzati e pronti per giocare con l'AI.",
  ctaAdd: 'Aggiungi gioco',
};

const baseStats: readonly GamesHeroStat[] = [
  { label: 'Posseduti', value: 7 },
  { label: 'Wishlist', value: 3 },
  { label: 'Totali', value: 12 },
  { label: 'Documenti KB', value: 5 },
];

describe('GamesHero (Wave B.1)', () => {
  it('renders title as h1 from labels.title', () => {
    render(<GamesHero labels={baseLabels} stats={baseStats} />);
    expect(screen.getByRole('heading', { level: 1, name: 'La tua libreria' })).toBeInTheDocument();
  });

  it('renders subtitle from labels.subtitle by default', () => {
    render(<GamesHero labels={baseLabels} stats={baseStats} />);
    expect(
      screen.getByText("Tutti i tuoi giochi, organizzati e pronti per giocare con l'AI.")
    ).toBeInTheDocument();
  });

  it('renders all 4 stat tiles with their labels and values', () => {
    render(<GamesHero labels={baseLabels} stats={baseStats} />);
    expect(screen.getByText('Posseduti')).toBeInTheDocument();
    expect(screen.getByText('7')).toBeInTheDocument();
    expect(screen.getByText('Wishlist')).toBeInTheDocument();
    expect(screen.getByText('3')).toBeInTheDocument();
    expect(screen.getByText('Totali')).toBeInTheDocument();
    expect(screen.getByText('12')).toBeInTheDocument();
    expect(screen.getByText('Documenti KB')).toBeInTheDocument();
    expect(screen.getByText('5')).toBeInTheDocument();
  });

  it('renders zero values explicitly when stats are empty (no fallback dash)', () => {
    const zeroStats: readonly GamesHeroStat[] = [
      { label: 'Posseduti', value: 0 },
      { label: 'Wishlist', value: 0 },
      { label: 'Totali', value: 0 },
      { label: 'Documenti KB', value: 0 },
    ];
    const { container } = render(<GamesHero labels={baseLabels} stats={zeroStats} />);
    const tiles = container.querySelectorAll('[data-slot="games-hero-stat"]');
    expect(tiles).toHaveLength(4);
    // Each tile renders the literal "0" — no em-dash / placeholder.
    const zeroNodes = container.querySelectorAll('[data-slot="games-hero-stat-value"]');
    expect(zeroNodes).toHaveLength(4);
    zeroNodes.forEach(node => {
      expect(node.textContent?.trim()).toBe('0');
    });
  });

  it('renders the "Aggiungi gioco" CTA and calls onAddGame on click', () => {
    const onAddGame = vi.fn();
    render(<GamesHero labels={baseLabels} stats={baseStats} onAddGame={onAddGame} />);
    const cta = screen.getByRole('button', { name: 'Aggiungi gioco' });
    expect(cta).toBeInTheDocument();
    fireEvent.click(cta);
    expect(onAddGame).toHaveBeenCalledTimes(1);
  });

  it('omits subtitle when compact is true (mobile collapse)', () => {
    render(<GamesHero labels={baseLabels} stats={baseStats} compact />);
    expect(
      screen.queryByText("Tutti i tuoi giochi, organizzati e pronti per giocare con l'AI.")
    ).toBeNull();
    // Title must still render in compact mode.
    expect(screen.getByRole('heading', { level: 1, name: 'La tua libreria' })).toBeInTheDocument();
  });

  it('exposes data-slot="games-library-hero" on the root for spec scoping', () => {
    const { container } = render(<GamesHero labels={baseLabels} stats={baseStats} />);
    expect(container.querySelector('[data-slot="games-library-hero"]')).not.toBeNull();
  });
});
