/**
 * InviteHero — centered hero with game-night pill, h1 title, and dateShort/time
 * mono line for `/invites/[token]` page.
 *
 * Wave A.5b (Issue #611). Mirrors mockup `sp3-accept-invite.jsx` lines 348-379
 * (Hero). Composition: pill (`--c-event` 14% bg) → h1 (display, hostName
 * highlighted in `--c-game`, gameName highlighted in `--c-toolkit`) → mono
 * subtitle (dateShort · time).
 *
 * Compact variant (mobile) reduces top padding and font-size from 20px → 17px.
 */

import type { JSX, ReactNode } from 'react';

import clsx from 'clsx';

export interface InviteHeroProps {
  /** Localized "Game night" eyebrow text. */
  readonly eyebrowText: string;
  /** Decorative icon for the pill (default emoji 🎯). */
  readonly eyebrowIcon?: ReactNode;
  /** Host display name (rendered in `--c-game` highlight). */
  readonly hostName: string;
  /** Localized game-name (rendered in `--c-toolkit` highlight). */
  readonly gameName: string;
  /**
   * Localized invite verb template, e.g. "ti invita a giocare a".
   * Plain string spliced between the two highlights.
   */
  readonly inviteVerb: string;
  /** Short formatted date (e.g. "ven 15 giu"). */
  readonly dateShort: string;
  /** Time string (e.g. "19:30"). */
  readonly time: string;
  readonly compact?: boolean;
  readonly className?: string;
}

export function InviteHero({
  eyebrowText,
  eyebrowIcon = '🎯',
  hostName,
  gameName,
  inviteVerb,
  dateShort,
  time,
  compact = false,
  className,
}: InviteHeroProps): JSX.Element {
  return (
    <div
      data-slot="invite-hero"
      className={clsx(
        'text-center',
        compact ? 'px-4 pb-1.5 pt-3.5' : 'px-6 pb-2 pt-4.5',
        className
      )}
    >
      <div className="mb-2.5 inline-flex items-center gap-1.5 rounded-full bg-[hsl(var(--c-event)/0.14)] px-2.5 py-1 font-mono text-[10px] font-bold uppercase tracking-[0.08em] text-[hsl(var(--c-event))]">
        <span aria-hidden="true">{eyebrowIcon}</span>
        {eyebrowText}
      </div>
      <h1
        className={clsx(
          'm-0 mb-1 font-display font-extrabold leading-[1.2] tracking-[-0.01em] text-foreground',
          compact ? 'text-[17px]' : 'text-[20px]'
        )}
      >
        <strong className="font-extrabold text-[hsl(var(--c-game))]">{hostName}</strong>{' '}
        {inviteVerb}{' '}
        <strong className="font-extrabold text-[hsl(var(--c-toolkit))]">{gameName}</strong>
      </h1>
      <p className="m-0 font-mono text-[11px] tracking-[0.04em] text-[hsl(var(--text-muted))]">
        {dateShort} · {time}
      </p>
    </div>
  );
}
