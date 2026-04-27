/**
 * JoinHero — left-column hero for `/join`.
 *
 * Wave A.2 (Issue #589). Mirrors mockup `sp3-join.jsx` lines 382-411 (HeroText)
 * combined with the desktop features grid (lines 723-729). On mobile (`compact`
 * = false in mockup terminology means *centered* layout with smaller max-width;
 * here we invert: `compact = true` ⇒ left-aligned desktop variant), the layout
 * stacks AlphaPill + headline + sub-tagline + features vertically.
 *
 * Spec §3.2 `JoinHeroProps`.
 */

'use client';

import type { JSX } from 'react';

import clsx from 'clsx';

import type { EntityType } from '@/components/ui/v2/entity-tokens';

import { AlphaPill } from './alpha-pill';
import { FeatureMiniCard } from './feature-mini-card';

export interface JoinHeroFeature {
  readonly entity: EntityType;
  readonly emoji: string;
  readonly title: string;
  readonly description: string;
}

export interface JoinHeroProps {
  /** When `true`, left-aligned desktop variant; otherwise centered (mobile). */
  readonly compact?: boolean;
  readonly alphaPillLabel: string;
  readonly heading: string;
  /** Inline highlight rendered as gradient text (e.g. "alpha privata"). */
  readonly headingHighlight: string;
  readonly subTagline: string;
  readonly features: readonly JoinHeroFeature[];
  readonly className?: string;
}

export function JoinHero({
  compact = false,
  alphaPillLabel,
  heading,
  headingHighlight,
  subTagline,
  features,
  className,
}: JoinHeroProps): JSX.Element {
  // Mockup splits the heading on the first occurrence of `headingHighlight`,
  // applying a gradient (`game → event`) to the highlighted span. We trust the
  // caller to supply a heading that contains the highlight.
  const highlightIdx = heading.indexOf(headingHighlight);
  const before = highlightIdx >= 0 ? heading.slice(0, highlightIdx) : heading;
  const after = highlightIdx >= 0 ? heading.slice(highlightIdx + headingHighlight.length) : '';

  return (
    <div
      data-slot="join-hero"
      className={clsx(
        'flex flex-col',
        compact ? 'items-start text-left' : 'items-center text-center',
        className
      )}
    >
      <AlphaPill label={alphaPillLabel} className="mb-3.5" />

      <h1
        className={clsx(
          'm-0 font-display font-extrabold tracking-[-0.02em] leading-[1.15] text-foreground',
          compact ? 'text-[22px]' : 'text-[26px]'
        )}
      >
        {before}
        {highlightIdx >= 0 && (
          <span className="bg-gradient-to-r from-[hsl(var(--c-game))] to-[hsl(var(--c-event))] bg-clip-text text-transparent">
            {headingHighlight}
          </span>
        )}
        {after}
      </h1>

      <p
        className={clsx(
          'mt-2 text-[13px] leading-[1.55] text-[hsl(var(--text-sec))]',
          compact ? 'max-w-[360px]' : 'max-w-[320px]'
        )}
      >
        {subTagline}
      </p>

      <div className={clsx('mt-5 grid w-full gap-3', compact ? 'grid-cols-3' : 'grid-cols-1')}>
        {features.map(f => (
          <FeatureMiniCard
            key={f.title}
            entity={f.entity}
            emoji={f.emoji}
            title={f.title}
            description={f.description}
          />
        ))}
      </div>
    </div>
  );
}
