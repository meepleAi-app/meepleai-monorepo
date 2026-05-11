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

// Entity colours replaced with Tailwind entity-token classes (P2 #807 Task 6+7+8)
// Cover gradients (multi-hue): kept inline with TODO — complex gradient stops not expressible via Tailwind entity tokens
// TODO #807-followup: GAME_HSL_BG_FROM hsl(28,80%,38%) + TO hsl(38,92%,60%) — warm game gradient, keep inline
// TODO #807-followup: KB_HSL_BG_FROM hsl(210,50%,28%) + TO hsl(195,80%,50%) — kb/tool gradient, keep inline

interface SourceTokens {
  readonly borderCls: string;
  readonly shadowCls: string;
  readonly coverGradient: string;
  readonly accentBgCls: string;
  readonly fallbackEmoji: string;
}

function tokensFor(source: 'catalog' | 'bgg'): SourceTokens {
  if (source === 'bgg') {
    return {
      borderCls: 'border-entity-document',
      shadowCls: 'ring-4 ring-entity-document/15',
      // eslint-disable-next-line meepleai/no-inline-hsl-v2 -- TODO #807-followup: kb+tool two-hue gradient; CSS vars cannot carry multi-stop gradients in style string
      coverGradient: 'linear-gradient(155deg, hsl(210, 50%, 28%), hsl(195, 80%, 50%))',
      accentBgCls: 'bg-entity-document',
      fallbackEmoji: '🌐',
    };
  }
  return {
    borderCls: 'border-entity-game',
    shadowCls: 'ring-4 ring-entity-game/15',
    // eslint-disable-next-line meepleai/no-inline-hsl-v2 -- TODO #807-followup: game+agent two-hue warm gradient; CSS vars cannot carry multi-stop gradients in style string
    coverGradient: 'linear-gradient(155deg, hsl(28, 80%, 38%), hsl(38, 92%, 60%))',
    accentBgCls: 'bg-entity-game',
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
    coverImageUrl != null ? `url(${coverImageUrl}) center/cover` : tokens.coverGradient;

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
        isSelected ? [tokens.borderCls, tokens.shadowCls] : 'border-slate-200',
        className
      )}
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
                className="inline-flex items-center gap-1 rounded-full bg-entity-toolkit/12 px-2 py-0.5 text-[10px] font-bold uppercase tracking-wide text-entity-toolkit"
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
          className={clsx(
            'absolute right-2 top-2 flex h-6 w-6 items-center justify-center rounded-full text-xs font-bold text-white shadow-sm',
            tokens.accentBgCls
          )}
        >
          ✓
        </span>
      )}
    </button>
  );
}
