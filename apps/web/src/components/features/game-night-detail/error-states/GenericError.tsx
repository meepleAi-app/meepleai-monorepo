/**
 * GenericError — fallback surface for 5xx / network failures on
 * /join/event/[code] (issue #1169). Unlike the terminal 404/410 surfaces
 * this one offers a retry CTA because the underlying error is transient.
 */

'use client';

import { Button } from '@/components/ui/primitives/button';

export interface GenericErrorLabels {
  readonly heading: string;
  readonly body: string;
  readonly retryCta: string;
}

export interface GenericErrorProps {
  readonly labels: GenericErrorLabels;
  readonly onRetry: () => void;
  readonly isRetrying?: boolean;
}

export function GenericError({
  labels,
  onRetry,
  isRetrying = false,
}: GenericErrorProps): React.JSX.Element {
  return (
    <section
      data-slot="public-join-event-generic-error"
      role="alert"
      aria-live="polite"
      className="flex flex-col items-center gap-4 px-5 py-8 text-center"
    >
      <div
        aria-hidden="true"
        className="flex h-16 w-16 items-center justify-center rounded-full border border-border bg-muted text-2xl"
      >
        ⚠️
      </div>
      <div className="flex flex-col gap-1.5">
        <h1 className="font-display text-lg font-extrabold text-foreground">{labels.heading}</h1>
        <p className="text-sm text-muted-foreground">{labels.body}</p>
      </div>
      <Button
        type="button"
        variant="outline"
        data-slot="public-join-event-generic-error-retry"
        disabled={isRetrying}
        onClick={onRetry}
      >
        {labels.retryCta}
      </Button>
    </section>
  );
}
