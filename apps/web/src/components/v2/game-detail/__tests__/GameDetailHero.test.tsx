/**
 * Wave C.1 (Issue #581) — GameDetailHero unit tests.
 */

import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { GameDetailHero, type GameDetailHeroLabels } from '../GameDetailHero';

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

const baseMeta = {
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
  });

  it('renders Play and Edit CTAs for variant="own"', () => {
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
  });

  it('renders Add CTA for variant="community"', () => {
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
  });

  it('renders favorite indicator when isFavorite is true', () => {
    render(
      <GameDetailHero
        title="Wingspan"
        imageUrl={null}
        variant="own"
        meta={baseMeta}
        isFavorite
        labels={labels}
      />
    );
    expect(screen.getByLabelText('Preferito')).toBeInTheDocument();
  });

  it('omits meta line when all meta fields are null', () => {
    const { container } = render(
      <GameDetailHero
        title="Wingspan"
        imageUrl={null}
        variant="own"
        meta={{
          designer: null,
          year: null,
          players: null,
          duration: null,
          complexity: null,
          rating: null,
        }}
        isFavorite={false}
        labels={labels}
      />
    );
    expect(container.querySelector('[data-slot="game-detail-hero-meta"]')).not.toBeInTheDocument();
  });

  it('exposes data-variant on the root section', () => {
    const { container } = render(
      <GameDetailHero
        title="Wingspan"
        imageUrl={null}
        variant="community"
        meta={baseMeta}
        isFavorite={false}
        labels={labels}
      />
    );
    const root = container.querySelector('[data-slot="game-detail-hero"]');
    expect(root).toHaveAttribute('data-variant', 'community');
  });

  it('renders a back button only when onBack is provided', () => {
    const onBack = vi.fn();
    const { rerender } = render(
      <GameDetailHero
        title="Wingspan"
        imageUrl={null}
        variant="own"
        meta={baseMeta}
        isFavorite={false}
        labels={labels}
      />
    );
    expect(screen.queryByLabelText(labels.backAriaLabel)).not.toBeInTheDocument();

    rerender(
      <GameDetailHero
        title="Wingspan"
        imageUrl={null}
        variant="own"
        meta={baseMeta}
        isFavorite={false}
        labels={labels}
        onBack={onBack}
      />
    );
    fireEvent.click(screen.getByLabelText(labels.backAriaLabel));
    expect(onBack).toHaveBeenCalledTimes(1);
  });

  it('renders an img tag with empty alt when imageUrl is provided', () => {
    const { container } = render(
      <GameDetailHero
        title="Wingspan"
        imageUrl="https://cdn.example/wingspan.jpg"
        variant="own"
        meta={baseMeta}
        isFavorite={false}
        labels={labels}
      />
    );
    const img = container.querySelector('img');
    expect(img).toBeInTheDocument();
    expect(img).toHaveAttribute('alt', '');
    expect(img).toHaveAttribute('aria-hidden', 'true');
  });
});
