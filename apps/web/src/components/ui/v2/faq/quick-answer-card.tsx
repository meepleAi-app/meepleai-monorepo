/**
 * QuickAnswerCard — featured popular FAQ tile.
 *
 * Wave A.1 Phase 4.5 (Issue #583). Mirrors mockup lines 268-322.
 * Border-left 3px game color, hover -2px lift, popularRank badge top-right.
 */

'use client';

import type { JSX, ReactNode } from 'react';

import clsx from 'clsx';

import { entityHsl } from '@/lib/color-utils';

export interface QuickAnswerCardProps {
  readonly question: ReactNode;
  readonly short: ReactNode;
  readonly categoryLabel: string;
  readonly categoryIcon: string;
  readonly popularRank: number;
  readonly onClick: () => void;
  readonly readMoreLabel?: string;
  readonly className?: string;
}

export function QuickAnswerCard({
  question,
  short,
  categoryLabel,
  categoryIcon,
  popularRank,
  onClick,
  readMoreLabel = 'Read more →',
  className,
}: QuickAnswerCardProps): JSX.Element {
  return (
    <button
      type="button"
      onClick={onClick}
      data-slot="quick-answer-card"
      style={{ borderLeftColor: entityHsl('game') }}
      className={clsx(
        'flex h-full w-full flex-col gap-2 p-4 text-left',
        'bg-card rounded-lg cursor-pointer',
        'border border-border border-l-[3px]',
        'transition-[transform,border-color,box-shadow] duration-150 ease-out',
        'hover:-translate-y-0.5 hover:shadow-md hover:border-[hsl(var(--c-game)/0.35)]',
        'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[hsl(var(--c-game))] focus-visible:ring-offset-2',
        className
      )}
    >
      <div className="flex items-center gap-2 font-mono text-[9px] font-bold uppercase tracking-[0.08em] text-[hsl(var(--text-muted))]">
        <span aria-hidden="true">{categoryIcon}</span>
        <span>{categoryLabel}</span>
        <span className="ml-auto tabular-nums text-[hsl(var(--c-game))]">#{popularRank}</span>
      </div>
      <h3 className="m-0 font-display text-sm font-bold leading-[1.35] text-foreground">
        {question}
      </h3>
      <p className="m-0 line-clamp-2 text-[12.5px] leading-[1.55] text-[hsl(var(--text-sec))]">
        {short}
      </p>
      <div className="mt-auto font-mono text-[11px] font-bold text-[hsl(var(--c-game))]">
        {readMoreLabel}
      </div>
    </button>
  );
}
