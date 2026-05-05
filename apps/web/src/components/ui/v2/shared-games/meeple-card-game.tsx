/**
 * MeepleCardGame — community shared-game tile for /shared-games index.
 *
 * Wave A.3b (Issue #596). Mirrors mockup `sp3-shared-games.jsx` lines 208-274.
 *
 * Spec deviation (§3.2): root MUST be `<Link>` from `next/link` (not `<article tabIndex={0}>`)
 * to leverage Next.js prefetch + browser-native focus/keyboard. Link styles are reset.
 *
 * Footer renders 3 entity counters (toolkits / agents / kbs). The `kb` chip is rendered
 * count-only (label="" passed to the existing `EntityChip` primitive) per mockup line 270.
 * Chips are rendered only when count > 0.
 *
 * `newWeek >= 2` produces a top-right "+{count}" badge tinted `hsl(var(--c-event))`.
 */

import type { JSX } from 'react';

import clsx from 'clsx';
import Link from 'next/link';

import { EntityChip } from '@/components/ui/v2/entity-chip/entity-chip';

export interface MeepleCardGameLabels {
  /** Aria label prefix for the rating, e.g. "Voto" → "Voto 4 di 5". */
  readonly ratingAriaLabel: string;
  /** Footer chip labels (kb intentionally omitted: count-only per mockup). */
  readonly toolkitLabel: string;
  readonly agentLabel: string;
  /** Aria label fragment for newWeek badge, e.g. `${count} nuovi questa settimana`. */
  readonly newWeekAriaLabel: (count: number) => string;
}

export interface MeepleCardGameProps {
  readonly id: string;
  readonly title: string;
  /** Optional cover image; falls back to a tinted emoji placeholder when absent. */
  readonly coverUrl?: string | null;
  /** Year published (rendered in meta line). */
  readonly year?: number | null;
  /** Average rating in 0..5 scale (already converted from backend 0..10). */
  readonly rating: number;
  /** Number of toolkits attached to this shared game. */
  readonly toolkitsCount: number;
  /** Number of agent definitions attached. */
  readonly agentsCount: number;
  /** Number of knowledge bases (vector documents) attached. */
  readonly kbsCount: number;
  /** Count of children created this week (>=2 triggers visible badge). */
  readonly newThisWeekCount: number;
  readonly labels: MeepleCardGameLabels;
  readonly compact?: boolean;
  readonly className?: string;
}

function Stars({
  rating,
  ariaLabel,
}: {
  readonly rating: number;
  readonly ariaLabel: string;
}): JSX.Element {
  const full = Math.round(Math.max(0, Math.min(5, rating)));
  return (
    <span
      role="img"
      aria-label={ariaLabel}
      className="inline-flex shrink-0 items-center gap-0.5 font-mono text-[11px] tabular-nums text-[hsl(var(--c-warning))]"
    >
      {Array.from({ length: 5 }, (_, i) => (
        <span aria-hidden="true" key={i}>
          {i < full ? '★' : '☆'}
        </span>
      ))}
    </span>
  );
}

export function MeepleCardGame({
  id,
  title,
  coverUrl,
  year,
  rating,
  toolkitsCount,
  agentsCount,
  kbsCount,
  newThisWeekCount,
  labels,
  compact = false,
  className,
}: MeepleCardGameProps): JSX.Element {
  const showNewBadge = newThisWeekCount >= 2;
  const coverH = compact ? 'h-24' : 'h-[116px]';
  const ratingFive = Math.round(Math.max(0, Math.min(5, rating)));

  return (
    <Link
      href={`/shared-games/${id}`}
      prefetch
      data-slot="shared-games-card"
      data-game-id={id}
      className={clsx(
        'group relative flex flex-col overflow-hidden rounded-lg border border-border bg-card no-underline',
        'text-foreground transition-[transform,box-shadow,border-color] duration-150',
        'hover:-translate-y-0.5 hover:shadow-md hover:border-[hsl(var(--c-game)/0.4)]',
        'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[hsl(var(--c-game))] focus-visible:ring-offset-2',
        className
      )}
    >
      {/* Cover */}
      <div
        className={clsx(
          'relative flex items-center justify-center bg-[hsl(var(--c-game)/0.12)]',
          coverH
        )}
      >
        {coverUrl ? (
          // eslint-disable-next-line @next/next/no-img-element
          <img
            src={coverUrl}
            alt=""
            className="absolute inset-0 h-full w-full object-cover"
            loading="lazy"
          />
        ) : (
          <span aria-hidden="true" className={compact ? 'text-[36px]' : 'text-[44px]'}>
            🎲
          </span>
        )}
        {showNewBadge ? (
          <span
            aria-label={labels.newWeekAriaLabel(newThisWeekCount)}
            className="absolute right-2 top-2 rounded-full bg-[hsl(var(--c-event))] px-2 py-0.5 font-mono text-[10px] font-bold text-white shadow-sm"
          >
            +{newThisWeekCount}
          </span>
        ) : null}
      </div>

      {/* Body */}
      <div className={clsx('flex flex-col gap-1.5', compact ? 'p-2.5' : 'p-3.5')}>
        <div className="flex items-start justify-between gap-2">
          <h3
            className={clsx(
              'm-0 line-clamp-2 font-display font-bold leading-tight text-foreground',
              compact ? 'text-[13px]' : 'text-[14px]'
            )}
          >
            {title}
          </h3>
          <Stars rating={rating} ariaLabel={`${labels.ratingAriaLabel} ${ratingFive} ${'di'} 5`} />
        </div>

        {year != null ? (
          <p className="m-0 font-mono text-[10px] uppercase tracking-[0.06em] text-[hsl(var(--text-muted))]">
            {year}
          </p>
        ) : null}

        {(toolkitsCount > 0 || agentsCount > 0 || kbsCount > 0) && (
          <div className="mt-1 flex flex-wrap gap-1.5">
            {toolkitsCount > 0 ? (
              <EntityChip entity="toolkit" label={`${toolkitsCount} ${labels.toolkitLabel}`} />
            ) : null}
            {agentsCount > 0 ? (
              <EntityChip entity="agent" label={`${agentsCount} ${labels.agentLabel}`} />
            ) : null}
            {kbsCount > 0 ? <EntityChip entity="kb" label={String(kbsCount)} /> : null}
          </div>
        )}
      </div>
    </Link>
  );
}
