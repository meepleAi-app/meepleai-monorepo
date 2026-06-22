/**
 * SharedGamesHero — page header for /shared-games index.
 *
 * Wave A.3b (Issue #596). Mirrors mockup `sp3-shared-games.jsx` lines 525-567.
 * Badge "Catalogo community" + h1 with gradient on the second token
 * (linear-gradient game→toolkit) + subtitle (max 540px).
 * `compact` prop reduces vertical padding for mobile.
 */

import type { JSX } from 'react';

import clsx from 'clsx';

export interface SharedGamesHeroProps {
  readonly badgeLabel: string;
  /** First half of the heading (rendered plain). */
  readonly headingLead: string;
  /** Token rendered with the brand gradient (game → toolkit). */
  readonly headingHighlight: string;
  /** Optional trailing text after the highlighted token. */
  readonly headingTail?: string;
  readonly subtitle: string;
  readonly compact?: boolean;
  readonly className?: string;
}

export function SharedGamesHero({
  badgeLabel,
  headingLead,
  headingHighlight,
  headingTail,
  subtitle,
  compact = false,
  className,
}: SharedGamesHeroProps): JSX.Element {
  return (
    <section
      data-slot="shared-games-hero"
      className={clsx(
        'flex flex-col items-start gap-3',
        compact ? 'px-4 pb-5 pt-6' : 'px-6 pb-7 pt-10',
        className
      )}
    >
      <span className="inline-flex items-center gap-1 rounded-full border border-[hsl(var(--c-game)/0.3)] bg-[hsl(var(--c-game)/0.08)] px-3 py-1 font-mono text-[10px] font-bold uppercase tracking-[0.08em] text-[hsl(var(--c-game))]">
        {badgeLabel}
      </span>
      <h1
        className={clsx(
          'm-0 font-display font-bold leading-[1.15] text-foreground',
          compact ? 'text-[26px]' : 'text-[36px]'
        )}
      >
        {headingLead}{' '}
        <span className="bg-gradient-to-r from-[hsl(var(--c-game))] to-[hsl(var(--c-toolkit))] bg-clip-text text-transparent">
          {headingHighlight}
        </span>
        {headingTail ? <> {headingTail}</> : null}
      </h1>
      <p className="m-0 max-w-[540px] text-[14px] leading-[1.55] text-[hsl(var(--text-sec))]">
        {subtitle}
      </p>
    </section>
  );
}
