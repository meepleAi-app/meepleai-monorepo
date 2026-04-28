/**
 * MeepleCardGame — V2 catalog card for the public `/shared-games` page.
 *
 * Wave A.3b — V2 Migration `/shared-games` (Issue #596).
 *
 * Wraps the canonical {@link MeepleCard} in a real `<a href>` so that:
 * - Search engines can crawl the catalog (SEO).
 * - Keyboard users can `Tab` through cards and `Enter` to navigate.
 * - Middle-click / Ctrl-click open the detail page in a new tab.
 *
 * The wrapped MeepleCard is rendered without its own click handler — pointer
 * events bubble up to the anchor instead. The aria-label includes the rating
 * to help screen readers convey the most useful summary up-front.
 */
import type { JSX } from 'react';

import clsx from 'clsx';
import Link from 'next/link';

import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import type { SharedGame } from '@/lib/api';

export interface MeepleCardGameProps {
  readonly game: SharedGame;
  /** Optional className applied to the anchor wrapper. */
  readonly className?: string;
  /**
   * Optional override for the navigation target. Defaults to
   * `/shared-games/<id>`.
   */
  readonly href?: string;
}

function formatPlayers(min: number, max: number): string | undefined {
  // 0 = unknown per backend convention (see SharedGameSchema).
  if (!min && !max) return undefined;
  if (min && max && min !== max) return `${min}–${max} giocatori`;
  const v = min || max;
  return v ? `${v} giocatori` : undefined;
}

function formatPlayingTime(minutes: number): string | undefined {
  if (!minutes || minutes <= 0) return undefined;
  return `${minutes} min`;
}

function buildAriaLabel(game: SharedGame): string {
  const parts: string[] = [game.title];
  if (typeof game.averageRating === 'number') {
    parts.push(`valutazione ${game.averageRating.toFixed(1)} su 10`);
  }
  return parts.join(', ');
}

export function MeepleCardGame({ game, className, href }: MeepleCardGameProps): JSX.Element {
  const target = href ?? `/shared-games/${game.id}`;

  const metadata: Array<{ label: string; value?: string }> = [];
  const players = formatPlayers(game.minPlayers, game.maxPlayers);
  if (players) metadata.push({ label: 'Giocatori', value: players });
  const playtime = formatPlayingTime(game.playingTimeMinutes);
  if (playtime) metadata.push({ label: 'Durata', value: playtime });
  if (game.yearPublished) {
    metadata.push({ label: 'Anno', value: String(game.yearPublished) });
  }

  // Cover labels surface "TOP" / "NEW" overlays on the artwork. Keep it terse.
  const coverLabels: { text: string }[] = [];
  if (game.isTopRated) coverLabels.push({ text: 'TOP' });
  if (game.isNew) coverLabels.push({ text: 'NEW' });

  return (
    <Link
      href={target}
      aria-label={buildAriaLabel(game)}
      data-testid="shared-games-card"
      data-game-id={game.id}
      className={clsx(
        'block rounded-2xl outline-none focus-visible:ring-2 focus-visible:ring-amber-400/70 focus-visible:ring-offset-2 focus-visible:ring-offset-background',
        className
      )}
    >
      <MeepleCard
        entity="game"
        variant="grid"
        title={game.title}
        imageUrl={game.imageUrl || undefined}
        rating={game.averageRating ?? undefined}
        ratingMax={10}
        metadata={metadata.length > 0 ? metadata : undefined}
        coverLabels={coverLabels.length > 0 ? coverLabels : undefined}
      />
    </Link>
  );
}
