/**
 * JoinSuccessCard — post-submission confirmation panel for `/join`.
 *
 * Wave A.2 (Issue #589). Mirrors mockup `sp3-join.jsx` lines 437-489.
 * Spec §3.2 `JoinSuccessCardProps`.
 *
 * Pure presentational: parent owns the FSM and toggles between this and the
 * form by branching on `state === 'success'`. We expose `onResetClick` so the
 * parent can reset its FSM (mutation.reset()) AND clear form data.
 *
 * i18n strings are injected as `labels` to keep this surface reusable in
 * non-`/join` contexts (e.g. preference editing post-alpha) and to make tests
 * deterministic without an intl provider.
 */

'use client';

import type { JSX } from 'react';

import clsx from 'clsx';

import { entityHsl } from '@/lib/color-utils';

export interface JoinSuccessCardLabels {
  readonly heading: string;
  readonly subText: string;
  readonly subTextSecond: string;
  readonly positionLabel: string;
  /** Pre-formatted estimate string, e.g. "stima 2 settimane" (caller resolves ICU plural). */
  readonly positionEstimate: string;
  readonly resetCta: string;
}

export interface JoinSuccessCardProps {
  readonly position: number;
  readonly estimatedWeeks: number;
  readonly onResetClick: () => void;
  readonly labels: JoinSuccessCardLabels;
  readonly className?: string;
}

export function JoinSuccessCard({
  position,
  onResetClick,
  labels,
  className,
}: JoinSuccessCardProps): JSX.Element {
  return (
    <div
      data-slot="join-success-card"
      className={clsx('flex flex-col items-center px-1 pt-2 pb-1 text-center', className)}
    >
      <div
        aria-hidden="true"
        style={{ backgroundColor: entityHsl('toolkit', 0.14) }}
        className="mb-3.5 flex h-16 w-16 items-center justify-center rounded-full text-[30px]"
      >
        🎲
      </div>

      <h2 className="m-0 mb-1.5 font-display text-[19px] font-bold text-foreground">
        {labels.heading}
      </h2>

      <p className="m-0 mb-4 text-[12px] leading-[1.6] text-[hsl(var(--text-muted))]">
        {labels.subText}
        <br />
        {labels.subTextSecond}
      </p>

      <div className="mb-3.5 w-full rounded-md bg-[hsl(var(--bg-muted))] px-3 py-2.5 text-left">
        <div className="mb-1 font-mono text-[9px] font-bold uppercase tracking-[0.08em] text-[hsl(var(--text-muted))]">
          {labels.positionLabel}
        </div>
        <div className="font-display text-[22px] font-extrabold leading-[1.1] text-[hsl(var(--c-game))]">
          #{position}{' '}
          <span className="font-mono text-[11px] font-semibold tracking-[0.04em] text-[hsl(var(--text-muted))]">
            · {labels.positionEstimate}
          </span>
        </div>
      </div>

      <button
        type="button"
        onClick={onResetClick}
        className={clsx(
          'inline-flex w-full items-center justify-center',
          'px-3.5 py-2.5 rounded-md',
          'border border-border bg-transparent',
          'font-display text-[13px] font-bold text-[hsl(var(--text-sec))]',
          'transition-colors duration-150 ease-out',
          'hover:bg-[hsl(var(--bg-hover))]',
          'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[hsl(var(--c-game))] focus-visible:ring-offset-2'
        )}
      >
        {labels.resetCta}
      </button>
    </div>
  );
}
