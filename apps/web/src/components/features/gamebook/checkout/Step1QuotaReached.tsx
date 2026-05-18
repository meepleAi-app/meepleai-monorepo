'use client';

import type { ReactElement } from 'react';

import clsx from 'clsx';

export interface Step1Labels {
  readonly heading: string;
  readonly subheading: string;
  readonly quotaLabel: string;
  readonly resetIn: string;
  readonly primaryCta: string;
  readonly secondaryCta: string;
  readonly explainLink: string;
}

export interface Step1QuotaReachedProps {
  readonly used: number;
  readonly total: number;
  readonly labels: Step1Labels;
  readonly onPrimaryClick: () => void;
  readonly onSecondaryClick: () => void;
}

export function Step1QuotaReached({
  used,
  total,
  labels,
  onPrimaryClick,
  onSecondaryClick,
}: Step1QuotaReachedProps): ReactElement {
  const fillPercent = total > 0 ? Math.min(100, Math.round((used / total) * 100)) : 0;
  return (
    <div data-slot="checkout-step-1" className="flex flex-col gap-4 p-5">
      <div
        aria-hidden="true"
        className="mx-auto flex h-24 w-24 items-center justify-center rounded-3xl bg-entity-event/15 ring-1 ring-entity-event/30"
      >
        <span className="text-5xl">💎</span>
      </div>
      <div className="flex flex-col gap-1">
        <h2 className="text-xl font-bold text-foreground">{labels.heading}</h2>
        <p className="text-sm text-muted-foreground">{labels.subheading}</p>
      </div>
      <section
        data-slot="quota-card"
        className="flex flex-col gap-2 rounded-lg border border-entity-event/25 bg-entity-event/10 p-3"
      >
        <div className="flex items-end justify-between">
          <span className="font-mono text-[11px] uppercase tracking-wider text-muted-foreground">
            {labels.quotaLabel}
          </span>
          <span
            data-testid="quota-counter"
            className="font-mono text-2xl font-extrabold tabular-nums text-entity-event"
          >
            {used}
            <span className="text-[60%] text-muted-foreground"> / {total}</span>
          </span>
        </div>
        <div
          aria-label={`Quota ${used} su ${total} paragrafi`}
          className="h-2 overflow-hidden rounded-full bg-muted"
        >
          <div
            data-slot="quota-card-fill"
            style={{ width: `${fillPercent}%` }}
            className="h-full bg-entity-event/70 transition-[width] duration-300 motion-reduce:transition-none"
          />
        </div>
        <p className="font-mono text-[11px] text-muted-foreground">{labels.resetIn}</p>
      </section>
      <div className="flex flex-col gap-2">
        <button
          type="button"
          onClick={onPrimaryClick}
          className={clsx(
            'w-full rounded-lg bg-entity-event px-5 py-3 text-sm font-bold text-white',
            'transition-[filter] duration-150 hover:brightness-110 active:scale-[0.98]',
            'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2'
          )}
        >
          {labels.primaryCta}
        </button>
        <button
          type="button"
          onClick={onSecondaryClick}
          className={clsx(
            'w-full rounded-lg border border-border-strong bg-card px-5 py-3 text-sm font-semibold text-foreground',
            'transition-colors hover:bg-muted',
            'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2'
          )}
        >
          {labels.secondaryCta}
        </button>
        <button
          type="button"
          className="self-center text-xs text-muted-foreground hover:text-foreground"
        >
          {labels.explainLink}
        </button>
      </div>
    </div>
  );
}
