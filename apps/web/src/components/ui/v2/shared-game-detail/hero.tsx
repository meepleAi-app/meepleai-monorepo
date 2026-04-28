/**
 * Hero — game cover + metadata + ConnectionBar for /shared-games/[id] V2.
 *
 * Wave A.4 (Issue #603). Mirrors mockup `sp3-shared-game-detail.jsx` GameHero
 * (lines 239-325) and the ConnectionBar 1:1 prod pattern (PR #549/#552).
 *
 * Layout:
 *  - Cover region: gradient bg-[hsl(var(--c-game)/0.12)] with 110px emoji or image,
 *    bottom-aligned overlay gradient.
 *  - Metadata row: c-game/.14 entity pill ("Game"), Stars + numeric rating (mono),
 *    h1 title (font-display 800 weight), mono meta line (author • year • players •
 *    duration • complexity).
 *  - ConnectionBar: horizontal flex with 3 entity pips (toolkits/agents/kbs),
 *    each tinted via entityHsl(.10 / .14 active). Empty pips render dashed border
 *    + Plus icon. Aria-label `${label}: ${count}` (or just `${label}` if empty).
 *
 * `compact` prop reduces image height (220→160) and h1 font (30→22) for narrow
 * viewports / preview cards in future waves.
 */

import type { JSX } from 'react';

import clsx from 'clsx';

import { entityHsl } from '@/components/ui/data-display/meeple-card/tokens';

export interface HeroLabels {
  /** Entity pill label for the game itself (e.g. "Game"). */
  readonly entityLabel: string;
  /** Aria label prefix for the rating, e.g. "Voto" → "Voto: 4 di 5". */
  readonly ratingAriaLabel: string;
  /** "of" connector for the rating aria-label, e.g. "Voto: 4 di 5". */
  readonly ratingOf: string;
  /** ConnectionBar pip labels. */
  readonly toolkitsLabel: string;
  readonly agentsLabel: string;
  readonly kbsLabel: string;
  /** "Players" / "min" / "complexity" / "by" meta connectors. */
  readonly metaPlayers: string;
  readonly metaMinutes: string;
  readonly metaComplexity: string;
  readonly metaAuthor: string;
}

export interface HeroProps {
  readonly title: string;
  readonly emoji?: string;
  readonly coverUrl?: string | null;
  readonly authorName?: string | null;
  readonly year?: number | null;
  readonly minPlayers?: number | null;
  readonly maxPlayers?: number | null;
  readonly playingTimeMinutes?: number | null;
  readonly complexityRating?: number | null;
  /** 0..5 star rating (already converted from backend 0..10 if needed). */
  readonly rating?: number | null;
  /** Aggregate counts driving ConnectionBar pips. */
  readonly toolkitsCount: number;
  readonly agentsCount: number;
  readonly kbsCount: number;
  readonly labels: HeroLabels;
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
      className="inline-flex shrink-0 items-center gap-0.5 font-mono text-[12px] tabular-nums text-[hsl(var(--c-warning))]"
    >
      {Array.from({ length: 5 }, (_, i) => (
        <span aria-hidden="true" key={i}>
          {i < full ? '★' : '☆'}
        </span>
      ))}
    </span>
  );
}

function ConnectionPip({
  entity,
  label,
  count,
}: {
  readonly entity: 'toolkit' | 'agent' | 'kb';
  readonly label: string;
  readonly count: number;
}): JSX.Element {
  const isEmpty = count === 0;
  const ariaLabel = isEmpty ? label : `${label}: ${count}`;
  return (
    <span
      role="status"
      aria-label={ariaLabel}
      data-slot="connection-pip"
      data-entity={entity}
      data-empty={isEmpty ? 'true' : 'false'}
      className={clsx(
        'inline-flex items-center gap-1.5 rounded-full px-3 py-1.5 font-mono text-[11px] uppercase tracking-[0.06em] transition-[transform,box-shadow] duration-150',
        'hover:scale-[1.03] hover:shadow-md',
        isEmpty ? 'border border-dashed opacity-60' : 'border-0'
      )}
      style={
        isEmpty
          ? {
              borderColor: entityHsl(entity, 0.4),
              color: entityHsl(entity),
              backgroundColor: 'transparent',
            }
          : {
              backgroundColor: entityHsl(entity, 0.12),
              color: entityHsl(entity),
            }
      }
    >
      {isEmpty ? (
        <span aria-hidden="true" className="text-[12px] leading-none">
          +
        </span>
      ) : (
        <span aria-hidden="true" className="font-bold tabular-nums">
          {count}
        </span>
      )}
      <span>{label}</span>
    </span>
  );
}

export function Hero({
  title,
  emoji = '🎲',
  coverUrl,
  authorName,
  year,
  minPlayers,
  maxPlayers,
  playingTimeMinutes,
  complexityRating,
  rating,
  toolkitsCount,
  agentsCount,
  kbsCount,
  labels,
  compact = false,
  className,
}: HeroProps): JSX.Element {
  const coverH = compact ? 'h-[160px]' : 'h-[220px]';
  const titleSize = compact ? 'text-[22px]' : 'text-[30px]';
  const emojiSize = compact ? 'text-[80px]' : 'text-[110px]';

  const ratingFive = rating != null ? Math.round(Math.max(0, Math.min(5, rating))) : null;
  const ratingDisplay = rating != null ? rating.toFixed(1) : null;

  const playersText =
    minPlayers != null && maxPlayers != null
      ? minPlayers === maxPlayers
        ? `${minPlayers}`
        : `${minPlayers}–${maxPlayers}`
      : null;

  const metaParts: Array<string> = [];
  if (authorName) metaParts.push(`${labels.metaAuthor} ${authorName}`);
  if (year != null) metaParts.push(String(year));
  if (playersText) metaParts.push(`${playersText} ${labels.metaPlayers}`);
  if (playingTimeMinutes != null) metaParts.push(`${playingTimeMinutes} ${labels.metaMinutes}`);
  if (complexityRating != null)
    metaParts.push(`${labels.metaComplexity} ${complexityRating.toFixed(1)}`);

  return (
    <header
      data-slot="shared-game-detail-hero"
      className={clsx(
        'relative overflow-hidden rounded-lg border border-border bg-card',
        className
      )}
    >
      {/* Cover */}
      <div
        className={clsx('relative flex items-center justify-center overflow-hidden', coverH)}
        style={{
          backgroundColor: entityHsl('game', 0.12),
        }}
      >
        {coverUrl ? (
          // eslint-disable-next-line @next/next/no-img-element
          <img src={coverUrl} alt="" className="absolute inset-0 h-full w-full object-cover" />
        ) : (
          <span
            aria-hidden="true"
            className={clsx(emojiSize, 'drop-shadow-md')}
            style={{ filter: 'drop-shadow(0 6px 12px rgba(0,0,0,0.18))' }}
          >
            {emoji}
          </span>
        )}
        <div
          aria-hidden="true"
          className="absolute inset-x-0 bottom-0 h-1/2 bg-gradient-to-t from-black/35 to-transparent"
        />
      </div>

      {/* Body */}
      <div className="flex flex-col gap-3 p-4 md:p-5">
        {/* Title row: entity pill + rating */}
        <div className="flex flex-wrap items-center gap-2">
          <span
            data-slot="entity-pill"
            data-entity="game"
            className="inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 font-mono text-[10px] font-semibold uppercase tracking-[0.08em]"
            style={{
              backgroundColor: entityHsl('game', 0.14),
              color: entityHsl('game'),
            }}
          >
            <span aria-hidden="true">🎲</span>
            <span>{labels.entityLabel}</span>
          </span>

          {ratingFive != null && ratingDisplay != null ? (
            <span className="inline-flex items-center gap-1.5">
              <Stars
                rating={ratingFive}
                ariaLabel={`${labels.ratingAriaLabel}: ${ratingFive} ${labels.ratingOf} 5`}
              />
              <span className="font-mono text-[12px] font-semibold tabular-nums text-foreground">
                {ratingDisplay}
              </span>
            </span>
          ) : null}
        </div>

        {/* Title */}
        <h1
          className={clsx(
            'm-0 font-display font-extrabold leading-tight text-foreground',
            titleSize
          )}
        >
          {title}
        </h1>

        {/* Meta line */}
        {metaParts.length > 0 ? (
          <p className="m-0 font-mono text-[11px] uppercase tracking-[0.06em] text-[hsl(var(--text-muted))]">
            {metaParts.join(' · ')}
          </p>
        ) : null}

        {/* ConnectionBar */}
        <div
          role="group"
          aria-label="Connections"
          data-slot="connection-bar"
          className="flex flex-wrap gap-2 overflow-x-auto pt-1"
        >
          <ConnectionPip entity="toolkit" label={labels.toolkitsLabel} count={toolkitsCount} />
          <ConnectionPip entity="agent" label={labels.agentsLabel} count={agentsCount} />
          <ConnectionPip entity="kb" label={labels.kbsLabel} count={kbsCount} />
        </div>
      </div>
    </header>
  );
}
