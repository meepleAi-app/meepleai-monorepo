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
  ctaAdd: '+ Aggiungi gioco',
  ctaImportBgg: '↓ Importa BGG',
  ctaExportAriaLabel: 'Esporta',
  eyebrow: 'Library · power-user view',
};

const baseStats: readonly LibraryHeroStat[] = [
  { key: 'totalGames', label: 'Giochi', value: 12, entity: 'game' },
  { key: 'kbReady', label: 'KB pronti', value: 5, entity: 'kb' },
  { key: 'wishlist', label: 'Wishlist', value: 2, entity: 'game' },
  { key: 'loaned', label: 'In prestito', value: 2, entity: 'game' },
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
      { key: 'totalGames', label: 'Giochi', value: 0, entity: 'game' },
      { key: 'kbReady', label: 'KB pronti', value: 0, entity: 'kb' },
      { key: 'wishlist', label: 'Wishlist', value: 0, entity: 'game' },
      { key: 'loaned', label: 'In prestito', value: 0, entity: 'game' },
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
    const cta = screen.getByRole('button', { name: '+ Aggiungi gioco' });
    expect(cta).toBeInTheDocument();
    fireEvent.click(cta);
    expect(onAddGame).toHaveBeenCalledTimes(1);
  });

  it('renders the eyebrow pill (📚 Library · power-user view) above the title', () => {
    render(
      <LibraryHeroDesktop labels={baseLabels} stats={baseStats} onImportBgg={() => undefined} />
    );
    expect(screen.getByText(/Library · power-user view/i)).toBeInTheDocument();
  });

  it('renders the "Importa BGG" secondary CTA and calls onImportBgg when clicked', () => {
    const onImportBgg = vi.fn();
    render(<LibraryHeroDesktop labels={baseLabels} stats={baseStats} onImportBgg={onImportBgg} />);
    const cta = screen.getByRole('button', { name: '↓ Importa BGG' });
    expect(cta).toBeInTheDocument();
    fireEvent.click(cta);
    expect(onImportBgg).toHaveBeenCalledTimes(1);
  });

  it('renders the Export icon button with accessible label and triggers onExport', () => {
    const onExport = vi.fn();
    render(
      <LibraryHeroDesktop
        labels={baseLabels}
        stats={baseStats}
        onImportBgg={() => undefined}
        onExport={onExport}
      />
    );
    const exportBtn = screen.getByRole('button', { name: 'Esporta' });
    expect(exportBtn).toBeInTheDocument();
    fireEvent.click(exportBtn);
    expect(onExport).toHaveBeenCalledTimes(1);
  });

  it('renders the Export icon button as disabled when onExport is not provided', () => {
    render(
      <LibraryHeroDesktop labels={baseLabels} stats={baseStats} onImportBgg={() => undefined} />
    );
    const exportBtn = screen.getByRole('button', { name: 'Esporta' });
    expect(exportBtn).toBeDisabled();
  });

  it('renders stat pills with entity-colored borders (one pill per stat)', () => {
    const { container } = render(
      <LibraryHeroDesktop labels={baseLabels} stats={baseStats} onImportBgg={() => undefined} />
    );
    const pills = container.querySelectorAll('[data-slot="library-hero-stat"]');
    expect(pills).toHaveLength(4);
    // Each pill carries its entity discriminator so the entity-colored border
    // (border-entity-{game|kb|agent|chat}) can be asserted via class lookup.
    const entities = Array.from(pills).map(node => node.getAttribute('data-entity'));
    expect(entities).toEqual(['game', 'kb', 'game', 'game']);
  });

  it('hides the action bar when compact is true (mobile collapse)', () => {
    render(<LibraryHeroDesktop labels={baseLabels} stats={baseStats} compact />);
    expect(screen.queryByRole('button', { name: '+ Aggiungi gioco' })).toBeNull();
    expect(screen.queryByRole('button', { name: '↓ Importa BGG' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Esporta' })).toBeNull();
  });

  it('hides the "Importa BGG" button when onImportBgg is undefined (F2 #1975 admin-only gate)', () => {
    // F2 #1975: BGG ToS restricts the import flow to admins. When the parent
    // (LibraryHub) detects a non-admin user it omits the callback; the hero
    // must collapse the CTA entirely (no disabled button — full removal).
    render(
      <LibraryHeroDesktop labels={baseLabels} stats={baseStats} onAddGame={() => undefined} />
    );
    expect(screen.queryByRole('button', { name: '↓ Importa BGG' })).toBeNull();
    // Primary CTA is still rendered — only the BGG secondary CTA is gated.
    expect(screen.getByRole('button', { name: '+ Aggiungi gioco' })).toBeInTheDocument();
  });

  it('keeps subtitle visible when compact is true (mockup: subtitle is reduced, not removed)', () => {
    // Mockup contract (sp4-library-desktop.jsx:72-75): the subtitle <p> renders
    // unconditionally; only its font-size shrinks (14.5 → 13) in compact mode.
    // The earlier Wave B.3 behaviour (hide subtitle in compact) was superseded
    // by the SP4 re-skin (PR1 Task 1.2).
    render(<LibraryHeroDesktop labels={baseLabels} stats={baseStats} compact />);
    expect(
      screen.getByText('Esplora, filtra e gestisci i tuoi giochi e le knowledge base.')
    ).toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 1, name: 'La tua libreria' })).toBeInTheDocument();
  });

  it('exposes data-slot="library-hero-desktop" on the root for spec scoping', () => {
    const { container } = render(<LibraryHeroDesktop labels={baseLabels} stats={baseStats} />);
    expect(container.querySelector('[data-slot="library-hero-desktop"]')).not.toBeNull();
  });
});
