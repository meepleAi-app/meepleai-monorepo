/**
 * Wave C.1 (Issue #581) — GameDetailHero unit tests.
 *
 * Covers:
 *  1. Title rendered in h1
 *  2. Owned variant badge display
 *  3. Community variant badge display
 *  4. variant='own' → Play + Edit CTAs (no AddToLibrary)
 *  5. variant='community' → AddToLibrary CTA (no Edit)
 *  6. Optional CTAs not rendered when absent
 *  7. Meta fields rendered when present, omitted when null
 */

import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import {
  GameDetailHero,
  type GameDetailHeroLabels,
  type GameDetailHeroMeta,
} from '../GameDetailHero';

const labels: GameDetailHeroLabels = {
  back: 'Torna ai giochi',
  backAriaLabel: 'Torna al catalogo dei giochi',
  ownedBadge: '✓ In libreria',
  communityBadge: '🌐 Community',
  ctaPlay: 'Gioca ora',
  ctaEdit: 'Modifica',
  ctaShare: 'Condividi',
  ctaShareAriaLabel: 'Condividi questo gioco',
  ctaAddToLibrary: '+ Aggiungi a libreria',
  ctaSimilar: 'Vedi simili',
  favoriteAriaLabel: 'Preferito',
};

const baseMeta: GameDetailHeroMeta = {
  designer: 'Elizabeth Hargrave',
  year: 2019,
  players: '1-5 giocatori',
  duration: '70 min',
  complexity: 'Complessità 2.4/5',
  rating: 'BGG 8.1',
};

describe('GameDetailHero (Wave C.1)', () => {
  it('renders the title in an h1', () => {
    render(
      <GameDetailHero
        title="Wingspan"
        imageUrl={null}
        variant="own"
        meta={baseMeta}
        isFavorite={false}
        labels={labels}
      />
    );
    expect(screen.getByRole('heading', { level: 1, name: 'Wingspan' })).toBeInTheDocument();
  });

  it('shows owned badge for variant="own"', () => {
    render(
      <GameDetailHero
        title="Wingspan"
        imageUrl={null}
        variant="own"
        meta={baseMeta}
        isFavorite={false}
        labels={labels}
      />
    );
    expect(screen.getByText('✓ In libreria')).toBeInTheDocument();
    expect(screen.queryByText('🌐 Community')).not.toBeInTheDocument();
  });

  it('shows community badge for variant="community"', () => {
    render(
      <GameDetailHero
        title="Wingspan"
        imageUrl={null}
        variant="community"
        meta={baseMeta}
        isFavorite={false}
        labels={labels}
      />
    );
    expect(screen.getByText('🌐 Community')).toBeInTheDocument();
    expect(screen.queryByText('✓ In libreria')).not.toBeInTheDocument();
  });

  it('renders Play and Edit CTAs for variant="own", hides AddToLibrary', () => {
    const onPlay = vi.fn();
    const onEdit = vi.fn();
    render(
      <GameDetailHero
        title="Wingspan"
        imageUrl={null}
        variant="own"
        meta={baseMeta}
        isFavorite={false}
        labels={labels}
        onPlay={onPlay}
        onEdit={onEdit}
      />
    );
    fireEvent.click(screen.getByRole('button', { name: /Gioca ora/ }));
    fireEvent.click(screen.getByRole('button', { name: /Modifica/ }));
    expect(onPlay).toHaveBeenCalledTimes(1);
    expect(onEdit).toHaveBeenCalledTimes(1);
    expect(screen.queryByRole('button', { name: /Aggiungi a libreria/ })).not.toBeInTheDocument();
  });

  it('renders AddToLibrary CTA for variant="community", hides Edit', () => {
    const onAdd = vi.fn();
    render(
      <GameDetailHero
        title="Wingspan"
        imageUrl={null}
        variant="community"
        meta={baseMeta}
        isFavorite={false}
        labels={labels}
        onAddToLibrary={onAdd}
      />
    );
    fireEvent.click(screen.getByRole('button', { name: /Aggiungi a libreria/ }));
    expect(onAdd).toHaveBeenCalledTimes(1);
    expect(screen.queryByRole('button', { name: /Modifica/ })).not.toBeInTheDocument();
  });

  it('does not render optional CTA buttons when callbacks are absent', () => {
    render(
      <GameDetailHero
        title="Wingspan"
        imageUrl={null}
        variant="own"
        meta={baseMeta}
        isFavorite={false}
        labels={labels}
        // no onPlay, onEdit, onShare, onBack
      />
    );
    expect(screen.queryByRole('button', { name: /Gioca ora/ })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Modifica/ })).not.toBeInTheDocument();
  });

  it('renders meta fields when present and omits null fields', () => {
    const partialMeta: GameDetailHeroMeta = {
      designer: 'Elizabeth Hargrave',
      year: null,
      players: null,
      duration: null,
      complexity: null,
      rating: null,
    };
    render(
      <GameDetailHero
        title="Wingspan"
        imageUrl={null}
        variant="own"
        meta={partialMeta}
        isFavorite={false}
        labels={labels}
      />
    );
    expect(screen.getByText('Elizabeth Hargrave')).toBeInTheDocument();
    expect(screen.queryByText('2019')).not.toBeInTheDocument();
  });

  // #5066: the cover comes from the library detail, which falls back to a private game, and a private game
  // imported from BGG stored the BGG image URLs. Hard ban on browser requests to BGG hosts (#2123, ADR-059 §5).
  it('does not request a BGG image: it renders the placeholder instead', () => {
    const { container } = render(
      <GameDetailHero
        title="Wingspan"
        imageUrl="https://cf.geekdo-images.com/abc/pic123.jpg"
        variant="own"
        meta={baseMeta}
        isFavorite={false}
        labels={labels}
      />
    );
    const cover = container.querySelector('[data-slot="game-detail-hero-cover"]');
    expect(cover?.querySelector('img')).toBeNull();
    expect(container.innerHTML).not.toMatch(/geekdo/);
  });

  it('renders an allowed cover image', () => {
    const { container } = render(
      <GameDetailHero
        title="Wingspan"
        imageUrl="/api/v1/covers/wingspan.webp"
        variant="own"
        meta={baseMeta}
        isFavorite={false}
        labels={labels}
      />
    );
    const img = container.querySelector('[data-slot="game-detail-hero-cover"] img');
    expect(img?.getAttribute('src')).toBe('/api/v1/covers/wingspan.webp');
  });

  it('exposes data-slot="game-detail-hero" for E2E selector', () => {
    const { container } = render(
      <GameDetailHero
        title="Wingspan"
        imageUrl={null}
        variant="own"
        meta={baseMeta}
        isFavorite={false}
        labels={labels}
      />
    );
    expect(container.querySelector('[data-slot="game-detail-hero"]')).toBeInTheDocument();
  });
});
