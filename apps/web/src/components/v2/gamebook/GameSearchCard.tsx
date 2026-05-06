/**
 * GameSearchCard — SP6 Phase C.1.B v2 component (Issue #789).
 *
 * Game catalog/BGG search-result card for /gamebook/upload Step 1. Pure
 * component (Wave D.3 pattern): orchestrator pre-resolves all i18n labels
 * (incl. ICU plural sharedByCount + locale-formatted numbers) and injects
 * via `labels` prop.
 *
 * Mapped from `admin-mockups/design_files/sp6-libro-game-photo-upload.jsx`
 * (GameCard inside Step1_Default + Step1_Searching).
 *
 * Gate C — MeepleCard fit decision (DIVERGE):
 *   The shared `MeepleCard` API (avatar/title/subtitle/rating/cover) cannot
 *   express the search-context requirements:
 *     1. Cover with linear-gradient background + centered emoji fallback
 *        (no real image asset support in v1) — MeepleCard expects an image
 *        URL or single-tone fallback.
 *     2. `sharedByCount` chip with 👥 icon inline — MeepleCard has no
 *        community-stats slot.
 *     3. `alreadyIndexed` badge as a discrete metadata pill — MeepleCard's
 *        `StatusBadge` slot is single-discrete-state only.
 *     4. Selection state with 4px ring + corner ✓ check — MeepleCard has no
 *        `isSelected` API; selection styling lives outside its primitive.
 *   Building bespoke. Mirror Wave D.1 (PR #736) cards-diverge-from-MeepleCard
 *   pattern. Documented per contract §13 Gate C.
 *
 * BGG vs Catalog source styling:
 *   - `source="catalog"` uses game-orange (entityHsl game) accent
 *   - `source="bgg"` uses kb-blue (entityHsl kb) accent
 *
 * a11y:
 *   - `<button type="button">` (selection action, not navigation)
 *   - `aria-pressed={isSelected}` — toggle-style activation per WAI-ARIA
 *   - Cover container `aria-hidden="true"` (decorative emoji + gradient)
 *   - `aria-label` includes title for SR clarity ("Tainted Grail")
 *
 * data-slot="game-search-card" + `data-source` + `data-selected` for E2E.
 */

'use client';

import type { ReactElement } from 'react';

import clsx from 'clsx';

import type { BggSearchResult, CatalogGameRef } from '@/lib/gamebook-upload';

export interface GameSearchCardLabels {
  /** Aria-label suffix for selection state (e.g. "Selezionato"). */
  readonly selectedAria: string;
  /** Pre-resolved ICU plural "1.247 condivisi" — empty string for BGG. */
  readonly sharedByCount: string;
  /** Already-indexed pill label (e.g. "Già indicizzato"). */
  readonly alreadyIndexedBadge: string;
}

export interface GameSearchCardProps {
  /** Catalog game OR BGG result. Discriminated by `source` prop. */
  readonly game: CatalogGameRef | BggSearchResult;
  /** Source of the result — drives accent color + which fields show. */
  readonly source: 'catalog' | 'bgg';
  /** Whether this card is the active selection in the grid. */
  readonly isSelected: boolean;
  /**
   * Click handler. Receives a stable id:
   *   - Catalog: `game.id` (UUID)
   *   - BGG: `bgg:{bggId}` synthetic prefix (no UUID until install)
   */
  readonly onClick: (gameId: string) => void;
  readonly labels: GameSearchCardLabels;
  readonly className?: string;
}

const GAME_HSL_SOLID = 'hsl(25, 95%, 39%)';
const GAME_HSL_RING = 'hsla(25, 95%, 45%, 0.15)';
const GAME_HSL_BG_FROM = 'hsl(28, 80%, 38%)';
const GAME_HSL_BG_TO = 'hsl(38, 92%, 60%)';
const KB_HSL_SOLID = 'hsl(210, 40%, 48%)';
const KB_HSL_RING = 'hsla(210, 40%, 48%, 0.15)';
const KB_HSL_BG_FROM = 'hsl(210, 50%, 28%)';
const KB_HSL_BG_TO = 'hsl(195, 80%, 50%)';
const TOOLKIT_HSL_BG = 'hsla(142, 70%, 31%, 0.12)';
const TOOLKIT_HSL_FG = 'hsl(142, 70%, 31%)';

interface SourceTokens {
  readonly accent: string;
  readonly ring: string;
  readonly coverFrom: string;
  readonly coverTo: string;
  readonly fallbackEmoji: string;
}

function tokensFor(source: 'catalog' | 'bgg'): SourceTokens {
  if (source === 'bgg') {
    return {
      accent: KB_HSL_SOLID,
      ring: KB_HSL_RING,
      coverFrom: KB_HSL_BG_FROM,
      coverTo: KB_HSL_BG_TO,
      fallbackEmoji: '🌐',
    };
  }
  return {
    accent: GAME_HSL_SOLID,
    ring: GAME_HSL_RING,
    coverFrom: GAME_HSL_BG_FROM,
    coverTo: GAME_HSL_BG_TO,
    fallbackEmoji: '📖',
  };
}

function isCatalogGame(
  game: CatalogGameRef | BggSearchResult,
  source: 'catalog' | 'bgg'
): game is CatalogGameRef {
  return source === 'catalog';
}

function gameStableId(game: CatalogGameRef | BggSearchResult, source: 'catalog' | 'bgg'): string {
  if (isCatalogGame(game, source)) return game.id;
  return `bgg:${game.bggId}`;
}

export function GameSearchCard({
  game,
  source,
  isSelected,
  onClick,
  labels,
  className,
}: GameSearchCardProps): ReactElement {
  const tokens = tokensFor(source);
  const stableId = gameStableId(game, source);
  const title = game.title;
  const publisher = game.publisher;
  const showSharedByCount =
    isCatalogGame(game, source) && labels.sharedByCount !== '' && game.sharedByCount > 0;
  const showAlreadyIndexed = isCatalogGame(game, source) && game.isIndexed;
  const yearPublished = !isCatalogGame(game, source) ? game.yearPublished : null;
  const coverImageUrl = isCatalogGame(game, source) ? game.coverImageUrl : null;

  const ariaLabel = isSelected ? `${title} — ${labels.selectedAria}` : title;

  const coverBackground =
    coverImageUrl != null
      ? `url(${coverImageUrl}) center/cover`
      : `linear-gradient(155deg, ${tokens.coverFrom}, ${tokens.coverTo})`;

  return (
    <button
      type="button"
      onClick={() => onClick(stableId)}
      aria-label={ariaLabel}
      aria-pressed={isSelected}
      data-slot="game-search-card"
      data-source={source}
      data-selected={isSelected ? 'true' : 'false'}
      data-game-id={stableId}
      className={clsx(
        'group relative flex flex-col overflow-hidden rounded-xl border-2 bg-card text-left',
        'transition-shadow motion-reduce:transition-none',
        'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
        'hover:shadow-md',
        className
      )}
      style={{
        borderColor: isSelected ? tokens.accent : 'hsl(215, 16%, 88%)',
        boxShadow: isSelected ? `0 0 0 4px ${tokens.ring}` : undefined,
      }}
    >
      {/* Cover */}
      <div
        data-slot="game-search-card-cover"
        aria-hidden="true"
        className="relative flex aspect-[4/3] w-full items-center justify-center overflow-hidden"
        style={{ background: coverBackground }}
      >
        {coverImageUrl == null && (
          <span className="text-3xl drop-shadow-md">{tokens.fallbackEmoji}</span>
        )}
      </div>

      {/* Body */}
      <div className="flex flex-col gap-1 p-3">
        <div
          className="line-clamp-2 text-sm font-bold leading-tight text-foreground"
          data-slot="game-search-card-title"
        >
          {title}
        </div>
        <div
          className="truncate font-mono text-[11px] text-slate-700"
          data-slot="game-search-card-meta"
        >
          {publisher ?? '—'}
          {yearPublished != null && (
            <>
              <span className="mx-1 opacity-50">·</span>
              <span className="tabular-nums">{yearPublished}</span>
            </>
          )}
        </div>

        {/* Catalog: sharedBy + alreadyIndexed */}
        {(showSharedByCount || showAlreadyIndexed) && (
          <div
            data-slot="game-search-card-stats"
            className="mt-1 flex flex-wrap items-center gap-2"
          >
            {showSharedByCount && (
              <span
                data-slot="game-search-card-shared"
                className="inline-flex items-center gap-1 text-[11px] text-slate-700"
              >
                <span aria-hidden="true">👥</span>
                <span className="tabular-nums">{labels.sharedByCount}</span>
              </span>
            )}
            {showAlreadyIndexed && (
              <span
                data-slot="game-search-card-indexed-badge"
                className="inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-[10px] font-bold uppercase tracking-wide"
                style={{ backgroundColor: TOOLKIT_HSL_BG, color: TOOLKIT_HSL_FG }}
              >
                {labels.alreadyIndexedBadge}
              </span>
            )}
          </div>
        )}
      </div>

      {/* Selected check (top-right corner) */}
      {isSelected && (
        <span
          data-slot="game-search-card-check"
          aria-hidden="true"
          className="absolute right-2 top-2 flex h-6 w-6 items-center justify-center rounded-full text-xs font-bold text-white shadow-sm"
          style={{ backgroundColor: tokens.accent }}
        >
          ✓
        </span>
      )}
    </button>
  );
}
