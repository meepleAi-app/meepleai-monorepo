/**
 * HubEmptyFiltered — empty state when filters produce no results in `/toolkits` hub.
 *
 * Wave 4 (#1480). Maps the mockup HubEmptyFiltered (sp4-hub-toolkits.jsx:387-420).
 * Grid-spanning (`col-span-full`) dashed card with 🔎 icon + title + body + reset CTA.
 * Pure presentational, labels injected.
 */

'use client';

import type { JSX } from 'react';

import clsx from 'clsx';

export interface HubEmptyFilteredLabels {
  readonly title: string;
  readonly body: string;
  readonly reset: string;
  readonly resetAriaLabel: string;
}

export interface HubEmptyFilteredProps {
  readonly labels: HubEmptyFilteredLabels;
  readonly onReset: () => void;
  readonly className?: string;
}

export function HubEmptyFiltered({
  labels,
  onReset,
  className,
}: HubEmptyFilteredProps): JSX.Element {
  return (
    <div
      data-slot="toolkits-index-empty-filtered"
      className={clsx(
        'col-span-full flex flex-col items-center rounded-xl border border-dashed border-border-strong bg-card p-10 text-center sm:p-16',
        className
      )}
    >
      <div
        aria-hidden="true"
        className="mb-4 flex h-16 w-16 items-center justify-center rounded-full bg-entity-toolkit/15 text-3xl text-entity-toolkit sm:h-20 sm:w-20 sm:text-4xl"
      >
        🔎
      </div>
      <h3 className="m-0 mb-1.5 font-bold font-[Quicksand] text-base text-foreground sm:text-lg">
        {labels.title}
      </h3>
      <p className="m-0 mb-4 max-w-sm text-sm text-muted-foreground leading-relaxed">
        {labels.body}
      </p>
      <button
        type="button"
        onClick={onReset}
        aria-label={labels.resetAriaLabel}
        className="rounded-md bg-entity-toolkit px-4 py-2.5 font-bold font-[Quicksand] text-sm text-white shadow-lg"
      >
        {labels.reset}
      </button>
    </div>
  );
}
