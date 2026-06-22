/**
 * ErrorState — error banner for `/toolkits` hub catalog fetch failure.
 *
 * Wave 4 (#1480). Maps the mockup ErrorState (sp4-hub-toolkits.jsx:422-452).
 * Grid-spanning (`col-span-full`) card with destructive accent + retry CTA.
 * Pure presentational, labels injected.
 */

'use client';

import type { JSX } from 'react';

import clsx from 'clsx';

export interface ErrorStateLabels {
  readonly title: string;
  readonly body: string;
  readonly retry: string;
  readonly retryAriaLabel: string;
}

export interface ErrorStateProps {
  readonly labels: ErrorStateLabels;
  readonly onRetry: () => void;
  readonly className?: string;
}

export function ErrorState({ labels, onRetry, className }: ErrorStateProps): JSX.Element {
  return (
    <div
      data-slot="toolkits-index-error-state"
      role="alert"
      className={clsx(
        'col-span-full flex flex-col items-center rounded-xl border border-destructive/30 bg-destructive/10 p-8 text-center sm:p-12',
        className
      )}
    >
      <div
        aria-hidden="true"
        className="mb-3 flex h-14 w-14 items-center justify-center rounded-full bg-destructive/15 text-2xl text-destructive"
      >
        ⚠
      </div>
      <h3 className="m-0 mb-1 font-bold font-[Quicksand] text-base text-foreground">
        {labels.title}
      </h3>
      <p className="m-0 mb-4 max-w-sm text-[12.5px] text-muted-foreground leading-relaxed">
        {labels.body}
      </p>
      <button
        type="button"
        onClick={onRetry}
        aria-label={labels.retryAriaLabel}
        className="rounded-md bg-destructive px-4 py-2 font-bold font-[Quicksand] text-xs text-white"
      >
        {labels.retry}
      </button>
    </div>
  );
}
