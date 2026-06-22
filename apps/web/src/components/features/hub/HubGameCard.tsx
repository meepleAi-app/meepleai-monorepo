/**
 * HubGameCard — public catalog card variant for `/hub/games` (#1166).
 * Pure presentational. Cover gradient + entity chip + title + rating + publisher.
 *
 * Click → `/games/[id]` (canonical authenticated game detail). Previously
 * linked at `/hub/games/[id]` (#2043 Bug 3 workaround); Issue #2153 retires
 * the `/hub/*` namespace, so the card now points at the canonical detail
 * route directly and skips the redirect hop.
 */
'use client';

import Link from 'next/link';

import { useTranslation } from '@/hooks/useTranslation';
import type { SharedGame } from '@/lib/api/schemas/shared-games.schemas';
import { shouldUsePlaceholder } from '@/lib/games/cover-utils';
import { useGameTitle } from '@/lib/i18n/use-game-title';

export interface HubGameCardProps {
  readonly game: SharedGame;
  readonly onClick?: (id: string) => void;
}

function formatYear(y: number): string {
  return y > 0 ? String(y) : '—';
}

export function HubGameCard({ game, onClick }: HubGameCardProps) {
  const rating = game.averageRating ?? null;
  // Issue #2123: prefer R2-resolved coverUrl, fall back to thumbnail. The
  // shouldUsePlaceholder() guard catches any legacy BGG URL drift and
  // routes to the deterministic emoji fallback.
  const coverSrc = game.coverUrl ?? game.thumbnailUrl ?? '';
  const showImage = !shouldUsePlaceholder(coverSrc);

  // Issue #2339 — viewer-locale title resolution. `game.title` (canonical EN)
  // remains the source of truth for aria-label fallback per DEC-FE-9.
  const { value: title, source } = useGameTitle(game);
  const { t } = useTranslation();
  const titleAriaLabel =
    source === 'translation'
      ? t('common.localizedFromEnglish', { localizedTitle: title, originalTitle: game.title })
      : undefined;
  return (
    <Link
      href={`/games/${game.id}`}
      data-slot="hub-game-card"
      onClick={() => onClick?.(game.id)}
      className="group flex flex-col overflow-hidden rounded-xl border border-border bg-card transition-shadow hover:shadow-md focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-foreground/25"
    >
      <div
        aria-hidden="true"
        className="relative flex aspect-[5/3] items-center justify-center bg-gradient-to-br from-[hsl(var(--c-game)/0.18)] to-[hsl(var(--c-game)/0.04)] text-4xl"
      >
        {showImage ? (
          // eslint-disable-next-line @next/next/no-img-element
          <img src={coverSrc} alt="" className="h-full w-full object-cover" />
        ) : (
          <span>🎲</span>
        )}
        <span className="absolute right-2 top-2 inline-flex items-center gap-1 rounded-full bg-[hsl(var(--c-game)/0.95)] px-2 py-0.5 font-mono text-[9px] font-extrabold uppercase tracking-wider text-background">
          🎲 Game
        </span>
        {rating != null && (
          <span className="absolute bottom-2 right-2 inline-flex items-center gap-1 rounded-full bg-card/90 px-2 py-0.5 font-mono text-[10px] font-extrabold text-foreground backdrop-blur">
            ★ {rating.toFixed(1)}
          </span>
        )}
      </div>
      <div className="flex flex-col gap-1 p-3">
        <h3
          className="line-clamp-1 font-bold font-[Quicksand] text-sm text-foreground"
          aria-label={titleAriaLabel}
        >
          {title}
        </h3>
        <div className="font-mono text-[10px] text-muted-foreground">
          {formatYear(game.yearPublished)}
          {game.hasKnowledgeBase && (
            <span className="ml-2 inline-flex items-center rounded bg-[hsl(var(--c-kb)/0.15)] px-1.5 py-0.5 font-bold text-[9px] uppercase tracking-wider text-[hsl(var(--c-kb))]">
              KB
            </span>
          )}
        </div>
      </div>
    </Link>
  );
}
