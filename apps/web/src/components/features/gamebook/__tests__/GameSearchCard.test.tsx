/**
 * GameSearchCard unit tests — SP6 Phase C.1.B Task B (Issue #789).
 *
 * Coverage:
 *   - data-slot identity + data-source + data-selected
 *   - <button type="button"> root (selection action, not navigation)
 *   - title + publisher render
 *   - selected state shows ✓ check + aria-pressed=true
 *   - sharedByCount label rendered when present
 *   - alreadyIndexed badge rendered when game.isIndexed=true
 *   - emoji fallback (no cover image)
 *   - BGG vs catalog source styling differentiation via data-source
 *   - onClick fires with gameId
 *   - aria-label respects selected vs unselected
 */

import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import type { BggSearchResult, CatalogGameRef } from '@/lib/gamebook-upload';

import { GameSearchCard } from '../GameSearchCard';
import type { GameSearchCardProps } from '../GameSearchCard';

const CATALOG_GAME: CatalogGameRef = {
  id: '11111111-1111-1111-1111-111111111111',
  title: 'Tainted Grail',
  publisher: 'Awaken Realms',
  coverImageUrl: null,
  sharedByCount: 1247,
  isIndexed: true,
};

const BGG_GAME: BggSearchResult = {
  bggId: 318240,
  title: 'Andor Chronicles',
  publisher: 'Kosmos',
  yearPublished: 2021,
};

const LABELS: GameSearchCardProps['labels'] = {
  selectedAria: 'Selezionato',
  sharedByCount: '1.247 condivisi',
  alreadyIndexedBadge: 'Già indicizzato',
};

describe('GameSearchCard (catalog source)', () => {
  it('renders data-slot="game-search-card"', () => {
    render(
      <GameSearchCard
        game={CATALOG_GAME}
        source="catalog"
        isSelected={false}
        onClick={vi.fn()}
        labels={LABELS}
      />
    );
    expect(document.querySelector('[data-slot="game-search-card"]')).not.toBeNull();
  });

  it('renders data-source="catalog"', () => {
    render(
      <GameSearchCard
        game={CATALOG_GAME}
        source="catalog"
        isSelected={false}
        onClick={vi.fn()}
        labels={LABELS}
      />
    );
    const card = document.querySelector('[data-slot="game-search-card"]');
    expect(card?.getAttribute('data-source')).toBe('catalog');
  });

  it('renders semantic <button type="button">', () => {
    render(
      <GameSearchCard
        game={CATALOG_GAME}
        source="catalog"
        isSelected={false}
        onClick={vi.fn()}
        labels={LABELS}
      />
    );
    const card = document.querySelector('[data-slot="game-search-card"]');
    expect(card?.tagName).toBe('BUTTON');
    expect(card?.getAttribute('type')).toBe('button');
  });

  it('renders title from game.title', () => {
    render(
      <GameSearchCard
        game={CATALOG_GAME}
        source="catalog"
        isSelected={false}
        onClick={vi.fn()}
        labels={LABELS}
      />
    );
    expect(screen.getByText('Tainted Grail')).toBeTruthy();
  });

  it('renders publisher from game.publisher', () => {
    render(
      <GameSearchCard
        game={CATALOG_GAME}
        source="catalog"
        isSelected={false}
        onClick={vi.fn()}
        labels={LABELS}
      />
    );
    expect(screen.getByText('Awaken Realms')).toBeTruthy();
  });

  it('renders sharedByCount label from labels', () => {
    render(
      <GameSearchCard
        game={CATALOG_GAME}
        source="catalog"
        isSelected={false}
        onClick={vi.fn()}
        labels={LABELS}
      />
    );
    expect(screen.getByText('1.247 condivisi')).toBeTruthy();
  });

  it('renders alreadyIndexed badge when game.isIndexed=true', () => {
    render(
      <GameSearchCard
        game={CATALOG_GAME}
        source="catalog"
        isSelected={false}
        onClick={vi.fn()}
        labels={LABELS}
      />
    );
    expect(screen.getByText('Già indicizzato')).toBeTruthy();
  });

  // #1094 Real-C-E regression guard: indexed badge must use the darker
  // text-entity-toolkit-text variant (l=24%) for AA on bg-entity-toolkit/12.
  it('indexed badge uses text-entity-toolkit-text token (AA contrast) — #1094 Real-C-E', () => {
    render(
      <GameSearchCard
        game={CATALOG_GAME}
        source="catalog"
        isSelected={false}
        onClick={vi.fn()}
        labels={LABELS}
      />
    );
    const badge = document.querySelector(
      '[data-slot="game-search-card-indexed-badge"]'
    ) as HTMLElement;
    expect(badge).not.toBeNull();
    expect(badge.className).toContain('text-entity-toolkit-text');
  });

  it('omits alreadyIndexed badge when isIndexed=false', () => {
    render(
      <GameSearchCard
        game={{ ...CATALOG_GAME, isIndexed: false }}
        source="catalog"
        isSelected={false}
        onClick={vi.fn()}
        labels={LABELS}
      />
    );
    expect(screen.queryByText('Già indicizzato')).toBeNull();
  });

  it('renders fallback emoji when coverImageUrl is null', () => {
    render(
      <GameSearchCard
        game={CATALOG_GAME}
        source="catalog"
        isSelected={false}
        onClick={vi.fn()}
        labels={LABELS}
      />
    );
    const cover = document.querySelector('[data-slot="game-search-card-cover"]');
    expect(cover?.textContent?.trim().length).toBeGreaterThan(0);
  });
});

describe('GameSearchCard selection state', () => {
  it('omits selection check when isSelected=false', () => {
    render(
      <GameSearchCard
        game={CATALOG_GAME}
        source="catalog"
        isSelected={false}
        onClick={vi.fn()}
        labels={LABELS}
      />
    );
    expect(document.querySelector('[data-slot="game-search-card-check"]')).toBeNull();
  });

  it('renders selection check when isSelected=true', () => {
    render(
      <GameSearchCard
        game={CATALOG_GAME}
        source="catalog"
        isSelected={true}
        onClick={vi.fn()}
        labels={LABELS}
      />
    );
    expect(document.querySelector('[data-slot="game-search-card-check"]')).not.toBeNull();
  });

  it('exposes data-selected="true" when isSelected=true', () => {
    render(
      <GameSearchCard
        game={CATALOG_GAME}
        source="catalog"
        isSelected={true}
        onClick={vi.fn()}
        labels={LABELS}
      />
    );
    const card = document.querySelector('[data-slot="game-search-card"]');
    expect(card?.getAttribute('data-selected')).toBe('true');
  });

  it('exposes aria-pressed="true" when isSelected=true', () => {
    render(
      <GameSearchCard
        game={CATALOG_GAME}
        source="catalog"
        isSelected={true}
        onClick={vi.fn()}
        labels={LABELS}
      />
    );
    const card = document.querySelector('[data-slot="game-search-card"]');
    expect(card?.getAttribute('aria-pressed')).toBe('true');
  });

  it('exposes aria-pressed="false" when isSelected=false', () => {
    render(
      <GameSearchCard
        game={CATALOG_GAME}
        source="catalog"
        isSelected={false}
        onClick={vi.fn()}
        labels={LABELS}
      />
    );
    const card = document.querySelector('[data-slot="game-search-card"]');
    expect(card?.getAttribute('aria-pressed')).toBe('false');
  });
});

describe('GameSearchCard click handler', () => {
  it('fires onClick with catalog game id', () => {
    const onClick = vi.fn();
    render(
      <GameSearchCard
        game={CATALOG_GAME}
        source="catalog"
        isSelected={false}
        onClick={onClick}
        labels={LABELS}
      />
    );
    fireEvent.click(document.querySelector('[data-slot="game-search-card"]')!);
    expect(onClick).toHaveBeenCalledWith(CATALOG_GAME.id);
  });

  it('fires onClick with bgg game id formatted as bgg:{bggId}', () => {
    const onClick = vi.fn();
    render(
      <GameSearchCard
        game={BGG_GAME}
        source="bgg"
        isSelected={false}
        onClick={onClick}
        labels={{ ...LABELS, sharedByCount: '' }}
      />
    );
    fireEvent.click(document.querySelector('[data-slot="game-search-card"]')!);
    expect(onClick).toHaveBeenCalledWith('bgg:318240');
  });
});

describe('GameSearchCard (bgg source)', () => {
  it('renders data-source="bgg"', () => {
    render(
      <GameSearchCard
        game={BGG_GAME}
        source="bgg"
        isSelected={false}
        onClick={vi.fn()}
        labels={{ ...LABELS, sharedByCount: '' }}
      />
    );
    const card = document.querySelector('[data-slot="game-search-card"]');
    expect(card?.getAttribute('data-source')).toBe('bgg');
  });

  it('renders BGG title', () => {
    render(
      <GameSearchCard
        game={BGG_GAME}
        source="bgg"
        isSelected={false}
        onClick={vi.fn()}
        labels={{ ...LABELS, sharedByCount: '' }}
      />
    );
    expect(screen.getByText('Andor Chronicles')).toBeTruthy();
  });

  it('renders BGG publisher and year inline', () => {
    render(
      <GameSearchCard
        game={BGG_GAME}
        source="bgg"
        isSelected={false}
        onClick={vi.fn()}
        labels={{ ...LABELS, sharedByCount: '' }}
      />
    );
    expect(screen.getByText('Kosmos')).toBeTruthy();
    expect(screen.getByText(/2021/)).toBeTruthy();
  });

  it('omits sharedByCount label for BGG source', () => {
    render(
      <GameSearchCard
        game={BGG_GAME}
        source="bgg"
        isSelected={false}
        onClick={vi.fn()}
        labels={{ ...LABELS, sharedByCount: 'should not appear' }}
      />
    );
    expect(screen.queryByText('should not appear')).toBeNull();
  });

  it('omits alreadyIndexed badge for BGG source', () => {
    render(
      <GameSearchCard
        game={BGG_GAME}
        source="bgg"
        isSelected={false}
        onClick={vi.fn()}
        labels={LABELS}
      />
    );
    expect(screen.queryByText('Già indicizzato')).toBeNull();
  });
});

describe('GameSearchCard a11y', () => {
  it('exposes aria-label with title on root button', () => {
    render(
      <GameSearchCard
        game={CATALOG_GAME}
        source="catalog"
        isSelected={false}
        onClick={vi.fn()}
        labels={LABELS}
      />
    );
    const card = document.querySelector('[data-slot="game-search-card"]');
    expect(card?.getAttribute('aria-label')).toContain('Tainted Grail');
  });

  it('cover decorative emoji is aria-hidden', () => {
    render(
      <GameSearchCard
        game={CATALOG_GAME}
        source="catalog"
        isSelected={false}
        onClick={vi.fn()}
        labels={LABELS}
      />
    );
    const cover = document.querySelector('[data-slot="game-search-card-cover"]');
    expect(cover?.getAttribute('aria-hidden')).toBe('true');
  });

  it('applies custom className to root', () => {
    render(
      <GameSearchCard
        game={CATALOG_GAME}
        source="catalog"
        isSelected={false}
        onClick={vi.fn()}
        labels={LABELS}
        className="extra-class"
      />
    );
    const card = document.querySelector('[data-slot="game-search-card"]');
    expect(card?.classList.contains('extra-class')).toBe(true);
  });
});
